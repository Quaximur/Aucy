using Aucy.Application.Exceptions;
using Aucy.Application.Helpers;
using Aucy.Contracts.OnionClient;
using Aucy.Data;
using Aucy.Domain.Entities;
using AutoMapper;
using Grpc.Core;
using MagicOnion;
using MagicOnion.Server;
using Medallion.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;
using System.Data;

namespace Aucy.Application;

public class OrderBookHelper(
    InventoryPropertiesHelper inventoryPropertiesService,
    ItemsHelper itemsService,
    IDbContextFactory<AucyDbContext> aucyDbFactory,
    IMapper mapper,
    IDistributedReaderWriterLockProvider rwLockProvider,
    IDistributedLockProvider lockProvider) : IOrderBookHelper
{
    private readonly IDbContextFactory<AucyDbContext> _aucyDbFactory = aucyDbFactory;
    private readonly InventoryPropertiesHelper _inventoryProperties = inventoryPropertiesService;
    private readonly ItemsHelper _itemsService = itemsService;
    private readonly IMapper _mapper = mapper;
    private readonly IDistributedLockProvider _lockProvider = lockProvider;
    private readonly IDistributedReaderWriterLockProvider _rwLockProvider = rwLockProvider;

    public async UnaryResult<LotCreatedDto> CreateLot(Guid userId, CreateLotDto request)
    {
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

        return await GetRetryPolicy().ExecuteAsync(async () =>
        {
            if (!itemConfig.IsUnary)
            {
                var lotCreatedDto = await SellNonUnary(userId, request, aucyDb, itemConfig);
                return lotCreatedDto;
            }
            else
            {
                var lotCreatedDto = await SellUnary(userId, request, aucyDb, itemConfig);
                return lotCreatedDto;
            }
        });
    }

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

    public async UnaryResult<OwnLotsDto> GetOwnLots(Guid userId)
    {
        using var aucyDb = await _aucyDbFactory.CreateDbContextAsync();

        var lotsWithItemConfigs = await aucyDb.Lots
            .Where(l => l.OwnerUserId == userId)
            .Include(l => l.ItemConfig)
            .Select(l => _mapper.Map<OwnLotDto>(l))
            .ToListAsync();

        return new OwnLotsDto(lotsWithItemConfigs);
    }

    /// <summary>
    /// Remove lot from selling, but leave sold quantity untouched
    /// </summary>
    public async UnaryResult<LotRemovedDto> RemoveLot(Guid userId, RemoveLotDto request)
    {
        return await GetRetryPolicy().ExecuteAsync(async () =>
        {
            using var aucyDb = await _aucyDbFactory.CreateDbContextAsync();

            var itemConfigDto = await aucyDb.Lots
                .Where(x => x.Id == request.LotId && x.ItemConfig != null)
                .Select(x => _mapper.Map<ItemConfigDto>(x.ItemConfig))
                .FirstOrDefaultAsync()
                    ?? throw new NoRetryRpcException(new Status(StatusCode.NotFound, "Lot or Item config doesn`t exist"));

            IDistributedSynchronizationHandle? userLock = null;
            IDistributedSynchronizationHandle? rwItemLock = null;
            IDistributedSynchronizationHandle? unaryLock = null;

            if (!itemConfigDto.IsUnary)
            {
                userLock = await _lockProvider.TryAcquireLockAsync(Keys.GetNonUnaryUserLock(itemConfigDto.Id, userId))
                    ?? throw new NoRetryRpcException(new Status(StatusCode.FailedPrecondition,
                        "Some of your operations with this item is already processing"));

                // wait for matching worker if not unary
                rwItemLock = await _rwLockProvider.AcquireReadLockAsync(Keys.GetItemMatchingLock(itemConfigDto.Id));
            }
            else
            {
                var isOwnLot = await aucyDb.Lots.AnyAsync(x => x.Id == request.LotId && x.OwnerUserId == userId);

                if (!isOwnLot)
                    throw new NoRetryRpcException(new Status(StatusCode.PermissionDenied, "You have no permission to modify this lot"));

                userLock = await _lockProvider.TryAcquireLockAsync(Keys.GetUnaryUserLock(itemConfigDto.Id, userId))
                    ?? throw new NoRetryRpcException(new Status(StatusCode.FailedPrecondition,
                        "Some of your operations with this item is already processing"));

                unaryLock = await _lockProvider.AcquireLockAsync(Keys.GetUnaryLock(itemConfigDto.Id));
            }

            var transaction = await aucyDb.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

            try
            {
                var lotFields = await aucyDb.Lots
                    .Where(l => l.Id == request.LotId && l.OwnerUserId == userId && l.RemainingQuantity > 0)
                    .Select(b => new
                    {
                        b.Id,
                        b.ItemConfigId,
                        ItemConfigWeight = b.ItemConfig.Weight,
                        b.TotalQuantity,
                        b.RemainingQuantity,
                    })
                    .FirstOrDefaultAsync() ?? throw new NoRetryRpcException(new Status(StatusCode.NotFound,
                        "Nothing to remove or Lot doesn`t exist"));

                int lotsUpdated = 0;
                var quantityToRemove = lotFields.RemainingQuantity;

                if (quantityToRemove < lotFields.TotalQuantity) // remove only unsold quantity
                {
                    lotsUpdated = await aucyDb.Lots
                        .Where(l => l.Id == request.LotId && l.OwnerUserId == userId && l.RemainingQuantity >= quantityToRemove)
                        .ExecuteUpdateAsync(x =>
                        {
                            x.SetProperty(x => x.RemainingQuantity, x => x.RemainingQuantity - quantityToRemove)
                                .SetProperty(x => x.TotalQuantity, x => x.TotalQuantity - quantityToRemove);
                        });
                }
                else
                {
                    lotsUpdated = await aucyDb.Lots
                        .Where(l => l.Id == request.LotId && l.OwnerUserId == userId &&
                            l.RemainingQuantity == quantityToRemove && l.TotalQuantity == quantityToRemove)
                        .ExecuteDeleteAsync();
                }

                if (lotsUpdated != 1)
                    throw new RpcException(new Status(StatusCode.NotFound,
                        $"Lot is not determined or remaining quantity has been changed during operation. Entries found: {lotsUpdated}"));

                var usedCapacityDiff = await _itemsService.UpdateOrInsertItemStack(aucyDb, lotFields.ItemConfigId,
                       quantityToRemove, userId);

                float usedWeightDiff = 0;

                if (lotFields.ItemConfigWeight > 0)
                    usedWeightDiff = checked(lotFields.ItemConfigWeight * quantityToRemove);

                await _inventoryProperties.ExecuteInventoryUpdate(aucyDb, userId, usedWeightDiff, usedCapacityDiff);

                await transaction.CommitAsync();

                return new LotRemovedDto(lotId: request.LotId, itemConfig: itemConfigDto, removedQuantity: quantityToRemove);
            }
            catch (OverflowException)
            {
                await transaction.RollbackAsync();
                throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, "Amount is too large"));
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
                    $"An error occurred while removing the lot: {ex.Message}"));
            }
            finally
            {
                if (rwItemLock != null)
                    await rwItemLock.DisposeAsync();
                if (unaryLock != null)
                    await unaryLock.DisposeAsync();
                if (userLock != null)
                    await userLock.DisposeAsync();
            }
        });
    }

    public async UnaryResult<LotCollectedDto> CollectLot(Guid userId, CollectLotDto request)
    {
        if (request.Quantity <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Quantity should be greater than 0"));

        return await GetRetryPolicy().ExecuteAsync(async () =>
        {
            using var aucyDb = await _aucyDbFactory.CreateDbContextAsync();

            var itemConfigFields = await aucyDb.Lots
                .Where(x => x.Id == request.LotId && x.ItemConfig != null)
                .Select(x => new { x.ItemConfig.IsUnary, x.ItemConfigId })
                .FirstOrDefaultAsync()
                        ?? throw new NoRetryRpcException(new Status(StatusCode.NotFound, "Lot or Item config doesn`t exist"));

            // No worker lock needed
            // No Non-User Unary lock needed 
            // (Concurrent Purchace -> RemainingQuantity decrease -> still correct calculation with quantity diff)
            var userLockKey = itemConfigFields.IsUnary ?
                    Keys.GetUnaryUserLock(itemConfigFields.ItemConfigId, userId) :
                    Keys.GetNonUnaryUserLock(itemConfigFields.ItemConfigId, userId);

            using var userLock = await _lockProvider.TryAcquireLockAsync(userLockKey)
                ?? throw new NoRetryRpcException(new Status(StatusCode.FailedPrecondition,
                    "Some of your operations with this item is already processing"));

            using var transaction = await aucyDb.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

            try
            {
                var lotFields = await aucyDb.Lots
                    .Where(l => l.Id == request.LotId && l.OwnerUserId == userId &&
                        l.TotalQuantity - l.RemainingQuantity >= request.Quantity)
                    .Select(b => new
                    {
                        b.Id,
                        b.ItemConfigId,
                        ItemConfigWeight = b.ItemConfig.Weight,
                        b.TotalQuantity,
                        b.RemainingQuantity,
                        b.UnitPrice,
                        b.ExtraSoldoutMoney
                    })
                    .FirstOrDefaultAsync() ?? throw new NoRetryRpcException(new Status(StatusCode.NotFound, "Lot doesn`t exist or remaining quantity is less than needed"));

                if (lotFields.RemainingQuantity == lotFields.TotalQuantity)
                    throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, "There are no sold items in the lot"));

                var soldQuantity = lotFields.TotalQuantity - lotFields.RemainingQuantity;
                var takeQuantity = request.Quantity;

                if (takeQuantity > soldQuantity)
                    throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, "Requested quantity exceeds the sold quantity in the lot"));

                int updatedLots;

                if (takeQuantity < lotFields.TotalQuantity)
                {
                    updatedLots = await aucyDb.Lots
                        .Where(b => b.Id == request.LotId && b.OwnerUserId == userId)
                        .ExecuteUpdateAsync(x =>
                        {
                            x.SetProperty(x => x.RemainingQuantity, x => x.RemainingQuantity - takeQuantity)
                                .SetProperty(x => x.TotalQuantity, x => x.TotalQuantity - takeQuantity);
                        });
                }
                else // lot is sold
                {
                    updatedLots = await aucyDb.Lots
                        .Where(l => l.Id == request.LotId && l.OwnerUserId == userId &&
                            l.RemainingQuantity == 0 && l.TotalQuantity == takeQuantity)
                        .ExecuteDeleteAsync();
                }

                if (updatedLots != 1)
                    throw new NoRetryRpcException(new Status(StatusCode.NotFound,
                        $"Lot is not determined (or lack of quantity). Entries found: {updatedLots}"));

                var returningMoney = checked(lotFields.UnitPrice * takeQuantity + lotFields.ExtraSoldoutMoney);
                // TODO: apply commission

                await _inventoryProperties.DepositMoney(aucyDb, userId, returningMoney);

                await transaction.CommitAsync();

                return new LotCollectedDto(lotId: lotFields.Id,
                    returningMoney: returningMoney,
                    collectedQuantity: takeQuantity);
            }
            catch (OverflowException)
            {
                await transaction.RollbackAsync();
                throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, "Amount is too large"));
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
                    $"An error occurred while collecting the lot: {ex.Message}"));
            }
            finally
            {
                if (userLock != null)
                    await userLock.DisposeAsync();
            }
        });
    }

    public async UnaryResult<BidCreatedDto> CreateBid(Guid userId, CreateBidDto request)
    {
        if (request.BidPrice <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Bid price should be greater than 0"));

        if (request.TotalQuantity <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Total quantity should be greater than 0"));

        using var aucyDb = await _aucyDbFactory.CreateDbContextAsync();

        var itemConfigDto = await aucyDb.ItemConfigs
                .Where(x => x.Id == request.ItemConfigId)
                .Select(x => _mapper.Map<ItemConfigDto>(x))
                .FirstOrDefaultAsync()
                     ?? throw new RpcException(new Status(StatusCode.NotFound, "Item config doesn`t exist"));

        return await GetRetryPolicy().ExecuteAsync(async () =>
        {
            if (!itemConfigDto.IsUnary)
                return await BuyNonUnary(userId, request, aucyDb, itemConfigDto);
            else
                return await BuyUnary(userId, request, aucyDb, itemConfigDto);
        });
    }

    private async UnaryResult<BidCreatedDto> BuyNonUnary(Guid userId, CreateBidDto request, AucyDbContext aucyDb,
        ItemConfigDto itemConfigDto)
    {
        var itemConfigId = request.ItemConfigId;

        using var userLock = await _lockProvider.TryAcquireLockAsync(Keys.GetNonUnaryUserLock(itemConfigId, userId))
            ?? throw new NoRetryRpcException(new Status(StatusCode.FailedPrecondition,
                "Some of your operations with this item is already processing"));

        using var rwLock = await _rwLockProvider.AcquireReadLockAsync(Keys.GetItemMatchingLock(itemConfigId));

        using var transaction = await aucyDb.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        try
        {
            var ownLotExists = await aucyDb.Lots.AnyAsync(l =>
                l.OwnerUserId == userId &&
                l.ItemConfigId == itemConfigId);

            if (ownLotExists)
                throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, "You can`t create a bid for the item you are selling"));

            var bidExists = await aucyDb.Bids.AnyAsync(b =>
                b.BidderUserId == userId &&
                b.ItemConfigId == itemConfigId);

            if (bidExists)
                throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, "You already have a bid with this item"));

            var reservedMoney = checked(request.BidPrice * request.TotalQuantity);
            await _inventoryProperties.WithdrawMoney(aucyDb, userId, reservedMoney);

            var bid = Bid.Create(
                bidderUserId: userId,
                itemConfigId: itemConfigId,
                bidPrice: request.BidPrice,
                totalQuantity: request.TotalQuantity);

            //await MatchBidWithLots(bid, aucyDb);

            aucyDb.Bids.Add(bid);

            await aucyDb.SaveChangesAsync();
            await transaction.CommitAsync();

            return new BidCreatedDto(bidId: bid.Id,
                itemConfig: itemConfigDto,
                bidPrice: bid.BidPrice,
                totalQuantity: bid.TotalQuantity,
                createdAt: bid.CreatedAt);
        }
        catch (OverflowException)
        {
            await transaction.RollbackAsync();
            throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, "Bid total price is too large"));
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

    private async UnaryResult<BidCreatedDto> BuyUnary(Guid userId, CreateBidDto request, AucyDbContext aucyDb,
        ItemConfigDto itemConfigDto)
    {
        if (request.TotalQuantity != 1)
            throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, "Total quantity should be 1 for unary item"));

        var itemConfigId = request.ItemConfigId;

        using var userLock = await _lockProvider.TryAcquireLockAsync(Keys.GetUnaryUserLock(itemConfigId, userId))
            ?? throw new NoRetryRpcException(new Status(StatusCode.FailedPrecondition, "Some of your operations with this item is already processing"));

        using var unaryLock = await _lockProvider.AcquireLockAsync(Keys.GetUnaryLock(itemConfigId));

        using var transaction = await aucyDb.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        try
        {
            var ownLotExists = await aucyDb.Lots.AnyAsync(l =>
                    l.OwnerUserId == userId &&
                    l.ItemConfigId == request.ItemConfigId);

            if (ownLotExists)
                throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, "You can`t create a bid for the item you are selling"));

            var lotFields = await aucyDb.Lots
                .Where(l => l.ItemConfigId == request.ItemConfigId && l.RemainingQuantity > 0)
                .Select(l => new
                {
                    l.Id,
                    l.UnitPrice
                })
                .FirstOrDefaultAsync() ?? throw new NoRetryRpcException(new Status(StatusCode.NotFound,
                    "No lots available for this item")); // No lot matching for unary items

            if (lotFields.UnitPrice != request.BidPrice)
                throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument,
                    "Bid price should be equal to lot price for unary item"));

            await _inventoryProperties.WithdrawMoney(aucyDb, userId, lotFields.UnitPrice);

            var updatedLots = await aucyDb.Lots.Where(x => x.Id == lotFields.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(x => x.RemainingQuantity, x => x.RemainingQuantity - 1));

            if (updatedLots != 1)
                throw new RpcException(new Status(StatusCode.NotFound,
                    $"Lot is not determined. Entries found: {updatedLots}"));

            var bid = Bid.Create(
                bidderUserId: userId,
                itemConfigId: request.ItemConfigId,
                bidPrice: request.BidPrice,
                totalQuantity: 1);

            bid.RemainingQuantity = 0;

            // TODO: can be placed in inventory instantly instead of bid creation + check capacity and weight then

            aucyDb.Bids.Add(bid);

            await aucyDb.SaveChangesAsync();
            await transaction.CommitAsync();

            return new BidCreatedDto(bidId: bid.Id,
                itemConfig: itemConfigDto,
                bidPrice: bid.BidPrice,
                totalQuantity: bid.TotalQuantity,
                createdAt: bid.CreatedAt);
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

    public async UnaryResult<OwnBidsDto> GetOwnBids(Guid userId)
    {
        using var aucyDb = await _aucyDbFactory.CreateDbContextAsync();

        var ownBids = await aucyDb.Bids
            .Where(b => b.BidderUserId == userId)
            .Include(b => b.ItemConfig)
            .Select(b => _mapper.Map<OwnBidDto>(b))
            .ToListAsync();

        return new OwnBidsDto(ownBids);
    }

    public async UnaryResult<BidRemovedDto> RemoveBid(Guid userId, RemoveBidDto request)
    {
        return await GetRetryPolicy().ExecuteAsync(async () =>
        {
            using var aucyDb = await _aucyDbFactory.CreateDbContextAsync();

            var itemConfigFields = await aucyDb.Bids
                .Where(x => x.Id == request.BidId && x.ItemConfig != null)
                .Select(x => new { x.ItemConfigId, x.ItemConfig.IsUnary })
                .FirstOrDefaultAsync()
                    ?? throw new NoRetryRpcException(new Status(StatusCode.NotFound, "Item config doesn`t exist"));

            IDistributedSynchronizationHandle userLock;
            IDistributedSynchronizationHandle? rwItemLock = null;
            IDistributedSynchronizationHandle? unaryLock = null;

            if (!itemConfigFields.IsUnary)
            {
                userLock = await _lockProvider.TryAcquireLockAsync(Keys.GetNonUnaryUserLock(itemConfigFields.ItemConfigId, userId))
                    ?? throw new NoRetryRpcException(new Status(StatusCode.FailedPrecondition,
                        "Some of your operations with this item is already processing"));

                // wait for matching worker if not unary
                rwItemLock = await _rwLockProvider.AcquireReadLockAsync(Keys.GetItemMatchingLock(itemConfigFields.ItemConfigId));
            }
            else
            {
                userLock = await _lockProvider.TryAcquireLockAsync(Keys.GetUnaryUserLock(itemConfigFields.ItemConfigId, userId))
                    ?? throw new NoRetryRpcException(new Status(StatusCode.FailedPrecondition,
                        "Some of your operations with this item is already processing"));

                unaryLock = await _lockProvider.AcquireLockAsync(Keys.GetUnaryLock(itemConfigFields.ItemConfigId));
            }

            var transaction = await aucyDb.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

            try
            {
                var bidFields = await aucyDb.Bids
                    .Where(b => b.Id == request.BidId && b.BidderUserId == userId && b.RemainingQuantity > 0)
                    .Select(b => new
                    {
                        b.Id,
                        b.ItemConfigId,
                        ItemConfigWeight = b.ItemConfig.Weight,
                        b.TotalQuantity,
                        b.RemainingQuantity,
                        b.BidPrice
                    })
                    .FirstOrDefaultAsync() ?? throw new NoRetryRpcException(new Status(StatusCode.NotFound,
                        "Nothing to remove or Bid doesn`t exist"));

                int bidsUpdated = 0;
                var quantityToRemove = bidFields.RemainingQuantity;

                if (quantityToRemove < bidFields.TotalQuantity) // If some items were bought, remove only unbought quantity
                {
                    bidsUpdated = await aucyDb.Bids
                        .Where(b => b.Id == request.BidId && b.BidderUserId == userId && b.RemainingQuantity >= quantityToRemove)
                        .ExecuteUpdateAsync(x =>
                        {
                            x.SetProperty(x => x.RemainingQuantity, x => x.RemainingQuantity - quantityToRemove)
                                .SetProperty(x => x.TotalQuantity, x => x.TotalQuantity - quantityToRemove);
                        });
                }
                else // All items are unsold. Just delete the bid
                {
                    bidsUpdated = await aucyDb.Bids
                        .Where(b => b.Id == request.BidId && b.BidderUserId == userId &&
                            b.RemainingQuantity == quantityToRemove && b.TotalQuantity == quantityToRemove)
                        .ExecuteDeleteAsync();
                }

                if (bidsUpdated != 1)
                    throw new RpcException(new Status(StatusCode.NotFound,
                        $"Bid is not determined or remaining quantity has been changed during operation. Entries found: {bidsUpdated}"));

                var returningMoney = checked(bidFields.BidPrice * quantityToRemove);
                if (returningMoney > 0)
                    await _inventoryProperties.DepositMoney(aucyDb, userId, returningMoney);

                // Bought items remain in bids. They will be collected by user manually.

                await transaction.CommitAsync();

                return new BidRemovedDto(bidId: bidFields.Id,
                    returningMoney: returningMoney,
                    removedQuantity: quantityToRemove);
            }
            catch (OverflowException)
            {
                await transaction.RollbackAsync();
                throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, "Returning amount is too large"));
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
                    $"An error occurred while removing the bid: {ex.Message}"));
            }
            finally
            {
                if (rwItemLock != null)
                    await rwItemLock.DisposeAsync();

                if (unaryLock != null)
                    await unaryLock.DisposeAsync();

                await userLock.DisposeAsync();
            }
        });
    }

    public async UnaryResult<BidCollectedDto> CollectBid(Guid userId, CollectBidDto request)
    {
        if (request.Quantity <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Quantity should be greater than 0"));

        return await GetRetryPolicy().ExecuteAsync(async () =>
        {
            using var aucyDb = await _aucyDbFactory.CreateDbContextAsync();

            var itemConfigDto = await aucyDb.Bids
                .Where(x => x.Id == request.BidId && x.ItemConfig != null)
                .Select(x => _mapper.Map<ItemConfigDto>(x.ItemConfig))
                .FirstOrDefaultAsync()
                    ?? throw new NoRetryRpcException(new Status(StatusCode.NotFound, "Bid or Item config doesn't exist"));

            // No worker lock needed
            // No Non-User Unary lock needed 
            // (Concurrent Sale -> RemainingQuantity decrease -> still correct calculation with quantity diff)
            var userLockKey = itemConfigDto.IsUnary ?
                    Keys.GetUnaryUserLock(itemConfigDto.Id, userId) :
                    Keys.GetNonUnaryUserLock(itemConfigDto.Id, userId);

            using var userLock = await _lockProvider.TryAcquireLockAsync(userLockKey)
                ?? throw new NoRetryRpcException(new Status(StatusCode.FailedPrecondition,
                    "Some of your operations with this item is already processing"));

            using var transaction = await aucyDb.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

            try
            {
                var bidFields = await aucyDb.Bids
                    .Where(b => b.Id == request.BidId && b.BidderUserId == userId &&
                        b.TotalQuantity - b.RemainingQuantity >= request.Quantity)
                    .Select(b => new
                    {
                        b.Id,
                        b.ItemConfigId,
                        ItemConfigWeight = b.ItemConfig.Weight,
                        b.TotalQuantity,
                        b.RemainingQuantity,
                        b.BidPrice
                    })
                    .FirstOrDefaultAsync() ?? throw new NoRetryRpcException(new Status(StatusCode.NotFound,
                        "Bid doesn't exist or remaining quantity is less than needed"));

                // Check if any items were bought
                if (bidFields.RemainingQuantity == bidFields.TotalQuantity)
                    throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, "There are no purchased items in the bid"));

                var boughtQuantity = bidFields.TotalQuantity - bidFields.RemainingQuantity;

                var takeQuantity = request.Quantity;

                if (takeQuantity > boughtQuantity)
                    throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, "Requested quantity exceeds the purchased quantity in the bid"));

                int bidsUpdated;

                if (takeQuantity < bidFields.TotalQuantity)
                {
                    bidsUpdated = await aucyDb.Bids
                        .Where(b => b.Id == request.BidId && b.BidderUserId == userId)
                        .ExecuteUpdateAsync(x =>
                        {
                            x.SetProperty(x => x.RemainingQuantity, x => x.RemainingQuantity - takeQuantity)
                                .SetProperty(x => x.TotalQuantity, x => x.TotalQuantity - takeQuantity);
                        });
                }
                else
                {
                    // Collect all purchased items - delete the bid entirely
                    bidsUpdated = await aucyDb.Bids
                        .Where(b => b.Id == request.BidId && b.BidderUserId == userId &&
                            b.RemainingQuantity == 0 && b.TotalQuantity == takeQuantity)
                        .ExecuteDeleteAsync();
                }

                if (bidsUpdated != 1)
                    throw new RpcException(new Status(StatusCode.NotFound,
                        $"Bid is not determined. Entries found: {bidsUpdated}"));

                // Add purchased items to user's inventory
                var usedCapacityDiff = await _itemsService.UpdateOrInsertItemStack(aucyDb, bidFields.ItemConfigId,
                    takeQuantity, userId);

                // Alter inventory UsedWeight
                float usedWeightDiff = 0;

                if (bidFields.ItemConfigWeight > 0)
                    usedWeightDiff = checked(bidFields.ItemConfigWeight * takeQuantity);

                await _inventoryProperties.ExecuteInventoryUpdate(aucyDb, userId, usedWeightDiff, usedCapacityDiff);

                await transaction.CommitAsync();

                return new BidCollectedDto(bidId: bidFields.Id,
                    itemConfig: itemConfigDto,
                    collectedQuantity: takeQuantity);
            }
            catch (OverflowException)
            {
                await transaction.RollbackAsync();
                throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, "Amount is too large"));
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
                    $"An error occurred while collecting the bid: {ex.Message}"));
            }
        });
    }

    private AsyncRetryPolicy GetRetryPolicy()
    {
        return Policy
            .Handle<Exception>(ex => ex is not NoRetryRpcException)
            .RetryAsync(3);
    }
}