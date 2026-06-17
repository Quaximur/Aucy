using Aucy.Application.Exceptions;
using Aucy.Application.Helpers;
using Aucy.Contracts.OnionClient;
using Aucy.Data;
using Aucy.Domain.Entities;
using AutoMapper;
using Grpc.Core;
using MagicOnion;
using MagicOnion.Server.Hubs;
using Medallion.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;
using System.Data;

namespace Aucy.Application;

// OrderHub for User-Server duplex streaming
// WorkerHub for Server-Sent Events
// Order: Subscribe for Worker Group events -> Store Worker Events -> OrderHub.Connent -> Apply OnJoin ->
// -> Apply unapplied Worker Events -> continue listening for new Worker Events

// 2 different Hubs: WorkerHub disconnection leads to events loss. Reconnection needs OrderHub.Join.

[Authorize]
public class OrderHub(WorkerGroupService workerGroupService,
    InventoryPropertiesHelper inventoryPropertiesService,
    ItemsHelper itemsService,
    IDbContextFactory<AucyDbContext> aucyDbFactory,
    IMapper mapper,
    IDistributedReaderWriterLockProvider rwLockProvider,
    IDistributedLockProvider lockProvider) : StreamingHubBase<IOrderHub, IOrderHubReceiver>, IOrderHub
{
    private readonly WorkerGroupService _workerGroupService = workerGroupService; // for SSE (global group)
    private readonly IDbContextFactory<AucyDbContext> _aucyDbFactory = aucyDbFactory;
    private readonly InventoryPropertiesHelper _inventoryProperties = inventoryPropertiesService;
    private readonly ItemsHelper _itemsService = itemsService;
    private readonly IMapper _mapper = mapper;
    private readonly IDistributedLockProvider _lockProvider = lockProvider;
    private readonly IDistributedReaderWriterLockProvider _rwLockProvider = rwLockProvider;

    //private IGroup<IOrderHubReceiver>? _room;
    private Guid _userId;

    protected override async ValueTask OnConnected()
    {
        _userId = Context.GetUserId();

        await _workerGroupService.AddMember(_userId, Client);

        //_room = await Group.AddAsync(_userId.ToString());

        using var aucyDb = await _aucyDbFactory.CreateDbContextAsync();

        var inventory = await aucyDb.Inventories
                .Where(x => x.Id == _userId)
                .Select(x => new InventoryDto
                {
                    Capacity = x.Capacity,
                    UsedCapacity = x.UsedCapacity,
                    WeightLimit = x.WeightLimit,
                    UsedWeight = x.UsedWeight,
                    ItemStacks = x.ItemStacks
                        .OrderBy(x => x.CreatedAt)
                        .Select(i => new ItemStackDto
                        {
                            ItemConfig = _mapper.Map<ItemConfigDto>(i.Config),
                            Quantity = i.Quantity,
                        })
                        .ToArray()
                })
                .FirstOrDefaultAsync() ?? throw new RpcException(new Status(StatusCode.NotFound,
                    "Inventory doesn't exist"));

        var lotsWithItemConfigs = await aucyDb.Lots
           .Where(l => l.OwnerUserId == _userId)
           .Include(l => l.ItemConfig)
           .Select(l => _mapper.Map<OwnLotDto>(l))
           .ToListAsync();

        var lots = new OwnLotsDto(lotsWithItemConfigs);

        var ownBids = await aucyDb.Bids
            .Where(b => b.BidderUserId == _userId)
            .Include(b => b.ItemConfig)
            .Select(b => _mapper.Map<OwnBidDto>(b))
            .ToListAsync();

        var bids = new OwnBidsDto(ownBids);

        Client.OnJoin(lots, bids, inventory);
    }

    protected override async ValueTask OnDisconnected()
    {
        // _room is Disposed by the Hub

        await _workerGroupService.RemoveMember(_userId);
    }

    public ValueTask CollectBid(CollectBidDto request)
    {
        throw new NotImplementedException();
    }

    public ValueTask CollectLot(CollectLotDto request)
    {
        throw new NotImplementedException();
    }

    public ValueTask CreateBid(CreateBidDto request)
    {
        throw new NotImplementedException();
    }

    public async ValueTask CreateLot(CreateLotDto request)
    {
        var userId = Context.GetUserId();

        Console.WriteLine($"Creating lot request for user: {userId}");

        if (request.UnitPrice <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Unit price should be greater than 0"));

        if (request.TotalQuantity <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Total quantity should be greater than 0"));

        using var aucyDb = await _aucyDbFactory.CreateDbContextAsync();

        var itemConfig = await aucyDb.ItemConfigs
            .Where(x => x.Id == request.ItemConfigId)
            .Select(x => _mapper.Map<ItemConfigDto>(x))
            .FirstOrDefaultAsync()
                    ?? throw new RpcException(new Status(StatusCode.NotFound, "Item config doesn`t exist"));

        await GetRetryPolicy().ExecuteAsync(async () =>
        {
            if (!itemConfig.IsUnary)
            {
                var lotDto = await SellNonUnary(userId, request, aucyDb, itemConfig);
                Client.OnLotCreated(lotDto);
            }
            else
            {
                var lotDto = await SellUnary(userId, request, aucyDb, itemConfig);
                Client.OnLotCreated(lotDto);
            }
        });
    }

    [Ignore]
    private async UnaryResult<LotCreatedDto> SellNonUnary(Guid userId, CreateLotDto request, AucyDbContext aucyDb,
        ItemConfigDto itemConfigDto)
    {
        var itemConfigId = request.ItemConfigId;

        using var userLock = await _lockProvider.TryAcquireLockAsync(Keys.GetNonUnaryUserLock(itemConfigId, userId))
            ?? throw new NoRetryRpcException(new Status(StatusCode.FailedPrecondition, "Some of your operations with this item is already processing"));

        using var rwLock = await _rwLockProvider.AcquireReadLockAsync(Keys.GetItemMatchingLock(itemConfigId));

        using var transaction = await aucyDb.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        try
        {
            var ownBidExists = await aucyDb.Bids.AnyAsync(l =>
                    l.BidderUserId == userId &&
                    l.ItemConfigId == itemConfigId);

            if (ownBidExists)
                throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, "You can`t create a lot for the item you are buying"));

            var lotExists = await aucyDb.Lots.AnyAsync(l =>
                l.OwnerUserId == userId &&
                l.ItemConfigId == itemConfigId);

            if (lotExists)
                throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, "You already have a lot with this item"));

            // Check items existence and quantity in inventory
            var itemStack = await aucyDb.ItemStacks
                .Where(s => s.InventoryId == userId &&
                    s.ConfigId == itemConfigId &&
                    s.Quantity >= request.TotalQuantity)
                .Select(s => new { s.Id, s.InventoryId, s.ConfigId, s.Quantity })
                .FirstOrDefaultAsync() ?? throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument,
                    "There are not enough items in your inventory"));

            // Reduce the quantity or remove the ItemStack
            var usedCapacityDiff = await _itemsService.UpdateOrDeleteItemStack(aucyDb, itemConfigId, request.TotalQuantity, userId);

            // Alter inventory UsedWeight
            float usedWeightDiff = 0;

            // Can be applied by parts in cycle if numbers are too big.
            if (itemConfigDto.Weight > 0)
                usedWeightDiff = checked(-itemConfigDto.Weight * request.TotalQuantity);

            await _inventoryProperties.ExecuteInventoryUpdate(aucyDb, userId, usedWeightDiff, usedCapacityDiff);

            // TIP: If you don`t want to create lot after instant soldout,
            // then you have to validate inventory properties
            var lot = Lot.Create(ownerUserId: userId,
                itemConfigId: itemConfigId,
                unitPrice: request.UnitPrice,
                totalQuantity: request.TotalQuantity);

            //await MatchLotWithBids(lot, aucyDb);

            aucyDb.Lots.Add(lot);

            await aucyDb.SaveChangesAsync();
            await transaction.CommitAsync();

            Console.WriteLine($"Created lot: {lot.Id} for user: {userId} with itemConfig: {itemConfigId}");

            return new LotCreatedDto(lotId: lot.Id,
                itemConfig: itemConfigDto,
                unitPrice: lot.UnitPrice,
                totalQuantity: lot.TotalQuantity,
                createdAt: lot.CreatedAt);
        }
        catch (OverflowException)
        {
            await transaction.RollbackAsync();
            throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, "Lot total price is too large"));
        }
        catch (RpcException)
        {
            await transaction.RollbackAsync();
            throw;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw new RpcException(new Status(StatusCode.Internal,
                $"An error occurred while creating the lot: {ex.Message}"));
        }
    }

    [Ignore]
    private async UnaryResult<LotCreatedDto> SellUnary(Guid userId, CreateLotDto request, AucyDbContext aucyDb, ItemConfigDto itemConfigDto)
    {
        if (request.TotalQuantity != 1)
            throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, "Total quantity should be 1 for unary item"));

        var itemConfigId = request.ItemConfigId;

        var hasItem = await aucyDb.ItemStacks.AnyAsync(x => x.ConfigId == itemConfigId && x.InventoryId == userId);

        if (!hasItem)
            throw new NoRetryRpcException(new Status(StatusCode.PermissionDenied, "You have no specified item in inventory"));

        using var userLock = await _lockProvider.TryAcquireLockAsync(Keys.GetUnaryUserLock(itemConfigId, userId))
            ?? throw new NoRetryRpcException(new Status(StatusCode.FailedPrecondition, "Some of your operations with this item is already processing"));

        // throw because only one user can sell it`s Unary item
        using var unaryLock = await _lockProvider.TryAcquireLockAsync(Keys.GetUnaryLock(itemConfigId))
            ?? throw new NoRetryRpcException(new Status(StatusCode.FailedPrecondition, "Failed to acquire user lock"));

        using var transaction = await aucyDb.Database.BeginTransactionAsync();

        try
        {
            var ownBidExists = await aucyDb.Bids.AnyAsync(l =>
                l.BidderUserId == userId &&
                l.ItemConfigId == itemConfigId);

            if (ownBidExists)
                throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, "You can`t create a lot for the item you are buying"));

            var usedCapacityDiff = await _itemsService.UpdateOrDeleteItemStack(aucyDb, itemConfigId, 1, userId);

            // Alter inventory UsedWeight
            float usedWeightDiff = 0;

            // Can be applied by parts in cycle if numbers are too large.
            if (itemConfigDto.Weight > 0)
                usedWeightDiff = checked(-itemConfigDto.Weight * request.TotalQuantity);

            await _inventoryProperties.ExecuteInventoryUpdate(aucyDb, userId, weightDiff: usedWeightDiff,
                capacityDiff: usedCapacityDiff);

            // No mathing with bids for Unary items
            var lot = Lot.Create(ownerUserId: userId,
                itemConfigId: itemConfigId,
                unitPrice: request.UnitPrice,
                totalQuantity: request.TotalQuantity);

            aucyDb.Add(lot);

            await aucyDb.SaveChangesAsync();
            await transaction.CommitAsync();

            return new LotCreatedDto(lotId: lot.Id,
                itemConfig: itemConfigDto,
                unitPrice: lot.UnitPrice,
                totalQuantity: lot.TotalQuantity,
                createdAt: lot.CreatedAt);
        }
        catch (OverflowException)
        {
            await transaction.RollbackAsync();
            throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, "Lot total price is too large"));
        }
        catch (RpcException)
        {
            await transaction.RollbackAsync();
            throw;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw new RpcException(new Status(StatusCode.Internal,
                $"An error occurred while creating the bid: {ex.Message}"));
        }
    }

    public ValueTask RemoveBid(RemoveBidDto request)
    {
        throw new NotImplementedException();
    }

    public ValueTask RemoveLot(RemoveLotDto request)
    {
        throw new NotImplementedException();
    }

    // ignore
    private AsyncRetryPolicy GetRetryPolicy()
    {
        return Policy
            .Handle<Exception>(ex => ex is not NoRetryRpcException)
            .RetryAsync(3);
    }
}
