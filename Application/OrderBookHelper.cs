using Aucy.Application.Exceptions;
using Aucy.Application.Constants;
using Aucy.Application.Helpers;
using Aucy.Contracts.OnionClient;
using Aucy.Data;
using Aucy.Domain.Entities;
using AutoMapper;
using Grpc.Core;
using MagicOnion;
using Medallion.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    IDistributedLockProvider lockProvider,
    IPriceTierQuantizer quantizer,
    ILogger<OrderBookHelper> logger) : IOrderBookHelper
{
    private readonly IDbContextFactory<AucyDbContext> _aucyDbFactory = aucyDbFactory;
    private readonly InventoryPropertiesHelper _inventoryProperties = inventoryPropertiesService;
    private readonly ItemsHelper _itemsService = itemsService;
    private readonly IMapper _mapper = mapper;
    private readonly IDistributedLockProvider _lockProvider = lockProvider;
    private readonly IDistributedReaderWriterLockProvider _rwLockProvider = rwLockProvider;
    private readonly IPriceTierQuantizer _quantizer = quantizer;
    private readonly ILogger<OrderBookHelper> _logger = logger;

    public async UnaryResult<LotCreatedDto> CreateLot(Guid userId, CreateLotDto request)
    {
        if (request.UnitPrice <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, AuctionErrorCodes.INVALID_UNIT_PRICE));

        if (request.TotalQuantity <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, AuctionErrorCodes.INVALID_TOTAL_QUANTITY));

        using var aucyDb = await _aucyDbFactory.CreateDbContextAsync();

        var itemConfigDto = await aucyDb.ItemConfigs
            .Where(x => x.Id == request.ItemConfigId)
            .Select(x => _mapper.Map<ItemConfigDto>(x))
            .FirstOrDefaultAsync()
                    ?? throw new RpcException(new Status(StatusCode.NotFound, AuctionErrorCodes.ITEM_CONFIG_NOT_FOUND));

        if (request.UnitPrice < itemConfigDto.MinPrice || request.UnitPrice > itemConfigDto.MaxPrice)
            throw new RpcException(new Status(StatusCode.InvalidArgument, AuctionErrorCodes.INVALID_PRICE_RANGE));

        // Validate that provided price matches one of configured price tiers
        if (!_quantizer.IsPriceValid(itemConfigDto.MinPrice, itemConfigDto.MaxPrice, 
            itemConfigDto.PriceTierCount, request.UnitPrice))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument,
                AuctionErrorCodes.INVALID_PRICE_TIER));
        }

        if (request.TotalQuantity > itemConfigDto.MaxSellQuantity)
            throw new RpcException(new Status(StatusCode.InvalidArgument, AuctionErrorCodes.LOT_QUANTITY_EXCEEDS_MAX));

        return await GetRetryPolicy().ExecuteAsync(async () =>
        {
            if (!itemConfigDto.IsUnary)
            {
                var lotCreatedDto = await SellNonUnary(userId, request, aucyDb, itemConfigDto);
                return lotCreatedDto;
            }
            else
            {
                var lotCreatedDto = await SellUnary(userId, request, aucyDb, itemConfigDto);
                return lotCreatedDto;
            }
        });
    }

    private async UnaryResult<LotCreatedDto> SellNonUnary(Guid userId, CreateLotDto request, AucyDbContext aucyDb,
        ItemConfigDto itemConfigDto)
    {
        var itemConfigId = request.ItemConfigId;

        using var userLock = await _lockProvider.TryAcquireLockAsync(LockKeys.GetNonUnaryUserLock(itemConfigId, userId))
            ?? throw new NoRetryRpcException(new Status(StatusCode.FailedPrecondition, 
                AuctionErrorCodes.ITEM_ALREADY_IN_PROCESSING));

        using var rwLock = await _rwLockProvider.AcquireReadLockAsync(LockKeys.GetItemMatchingLock(itemConfigId));

        using var transaction = await aucyDb.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        try
        {
            var ownBidExists = await aucyDb.Bids.AnyAsync(l =>
                    l.BidderUserId == userId &&
                    l.ItemConfigId == itemConfigId);

            if (ownBidExists)
                throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, 
                    AuctionErrorCodes.CANNOT_CREATE_LOT_WHILE_BUYING));

            var lotExists = await aucyDb.Lots.AnyAsync(l =>
                l.OwnerUserId == userId &&
                l.ItemConfigId == itemConfigId);

            if (lotExists)
                throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, AuctionErrorCodes.LOT_ALREADY_EXISTS));

            // Check items existence and quantity in inventory
            var itemStack = await aucyDb.ItemStacks
                .Where(s => s.InventoryId == userId &&
                    s.ConfigId == itemConfigId &&
                    s.Quantity >= request.TotalQuantity)
                .Select(s => new { s.Id, s.InventoryId, s.ConfigId, s.Quantity })
                .FirstOrDefaultAsync() ?? throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument,
                    AuctionErrorCodes.INSUFFICIENT_INVENTORY));

            // Reduce the quantity or remove the ItemStack
            var usedCapacityDiff = await _itemsService.UpdateOrDeleteItemStack(aucyDb, itemConfigId, 
                request.TotalQuantity, userId);

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

            _logger.LogInformation("Created lot {LotId} for user {UserId} with itemConfig {ItemConfigId}", 
                lot.Id, userId, itemConfigId);

            return new LotCreatedDto(lotId: lot.Id,
                itemConfigId: itemConfigId,
                itemConfig: null, // NonUnary item config is stored on the client
                unitPrice: lot.UnitPrice,
                totalQuantity: lot.TotalQuantity,
                weightDiff: usedWeightDiff,
                createdAt: lot.CreatedAt);
        }
        catch (OverflowException)
        {
            await transaction.RollbackAsync();
            throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, AuctionErrorCodes.LOT_PRICE_TOO_LARGE));
        }
        catch (RpcException)
        {
            await transaction.RollbackAsync();
            throw;
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw new RpcException(new Status(StatusCode.Internal, AuctionErrorCodes.FAILED_TO_CREATE_LOT));
        }
    }

    private async UnaryResult<LotCreatedDto> SellUnary(Guid userId, CreateLotDto request, AucyDbContext aucyDb, 
        ItemConfigDto itemConfigDto)
    {
        if (request.TotalQuantity != 1)
            throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, 
                AuctionErrorCodes.INVALID_QUANTITY_FOR_UNARY_ITEM));

        var itemConfigId = request.ItemConfigId;

        var hasItem = await aucyDb.ItemStacks.AnyAsync(x => x.ConfigId == itemConfigId && x.InventoryId == userId);

        if (!hasItem)
            throw new NoRetryRpcException(new Status(StatusCode.PermissionDenied, AuctionErrorCodes.ITEM_STACK_NOT_FOUND));

        using var userLock = await _lockProvider.TryAcquireLockAsync(LockKeys.GetUnaryUserLock(itemConfigId, userId))
            ?? throw new NoRetryRpcException(new Status(StatusCode.FailedPrecondition, 
            AuctionErrorCodes.ITEM_ALREADY_IN_PROCESSING));

        // throw because only one user can sell it`s Unary item
        using var unaryLock = await _lockProvider.TryAcquireLockAsync(LockKeys.GetUnaryLock(itemConfigId))
            ?? throw new NoRetryRpcException(new Status(StatusCode.FailedPrecondition, 
                AuctionErrorCodes.ITEM_ALREADY_IN_PROCESSING));

        using var transaction = await aucyDb.Database.BeginTransactionAsync();

        try
        {
            var ownBidExists = await aucyDb.Bids.AnyAsync(l =>
                l.BidderUserId == userId &&
                l.ItemConfigId == itemConfigId);

            if (ownBidExists)
                throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, 
                    AuctionErrorCodes.CANNOT_CREATE_LOT_WHILE_BUYING));

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

            _logger.LogInformation("Created lot {LotId} for user {UserId} with itemConfig {ItemConfigId}", 
                lot.Id, userId, itemConfigId);

            return new LotCreatedDto(lotId: lot.Id,
                itemConfigId: itemConfigId,
                itemConfig: itemConfigDto,
                unitPrice: lot.UnitPrice,
                totalQuantity: lot.TotalQuantity,
                weightDiff: usedWeightDiff,
                createdAt: lot.CreatedAt);
        }
        catch (OverflowException)
        {
            await transaction.RollbackAsync();
            throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, AuctionErrorCodes.LOT_PRICE_TOO_LARGE));
        }
        catch (RpcException)
        {
            await transaction.RollbackAsync();
            throw;
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw new RpcException(new Status(StatusCode.Internal, AuctionErrorCodes.FAILED_TO_CREATE_LOT));
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
                    ?? throw new NoRetryRpcException(new Status(StatusCode.NotFound, AuctionErrorCodes.LOT_NOT_FOUND));

            IDistributedSynchronizationHandle? userLock = null;
            IDistributedSynchronizationHandle? rwItemLock = null;
            IDistributedSynchronizationHandle? unaryLock = null;

            if (!itemConfigDto.IsUnary)
            {
                userLock = await _lockProvider.TryAcquireLockAsync(LockKeys.GetNonUnaryUserLock(itemConfigDto.Id, userId))
                    ?? throw new NoRetryRpcException(new Status(StatusCode.FailedPrecondition,
                        AuctionErrorCodes.ITEM_ALREADY_IN_PROCESSING));

                // wait for matching worker if not unary
                rwItemLock = await _rwLockProvider.AcquireReadLockAsync(LockKeys.GetItemMatchingLock(itemConfigDto.Id));
            }
            else
            {
                var isOwnLot = await aucyDb.Lots.AnyAsync(x => x.Id == request.LotId && x.OwnerUserId == userId);

                if (!isOwnLot)
                    throw new NoRetryRpcException(new Status(StatusCode.PermissionDenied, 
                        AuctionErrorCodes.NO_LOT_MODIFICATION_PERMISSION));

                userLock = await _lockProvider.TryAcquireLockAsync(LockKeys.GetUnaryUserLock(itemConfigDto.Id, userId))
                    ?? throw new NoRetryRpcException(new Status(StatusCode.FailedPrecondition,
                        AuctionErrorCodes.ITEM_ALREADY_IN_PROCESSING));

                unaryLock = await _lockProvider.AcquireLockAsync(LockKeys.GetUnaryLock(itemConfigDto.Id));
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
                        AuctionErrorCodes.NOTHING_TO_REMOVE_LOT));

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
                    throw new RpcException(new Status(StatusCode.FailedPrecondition, AuctionErrorCodes.LOT_REMOVING_CONFLICT));

                var usedCapacityDiff = await _itemsService.UpdateOrInsertItemStack(aucyDb, lotFields.ItemConfigId,
                       quantityToRemove, userId);

                float usedWeightDiff = 0;

                if (lotFields.ItemConfigWeight > 0)
                    usedWeightDiff = checked(lotFields.ItemConfigWeight * quantityToRemove);

                await _inventoryProperties.ExecuteInventoryUpdate(aucyDb, userId, usedWeightDiff, usedCapacityDiff);

                await transaction.CommitAsync();

                _logger.LogInformation("Removed {RemovedLotQuantity} of lot {LotId} for user {UserId} with itemConfig {ItemConfigId}",
                    quantityToRemove, lotFields.Id, userId, lotFields.ItemConfigId);

                return new LotRemovedDto(lotId: request.LotId,
                    itemConfigId: lotFields.ItemConfigId,
                    itemConfig: itemConfigDto.IsUnary ? itemConfigDto : null,  // Send Unary. NonUnary item config is stored on the client 
                    removedQuantity: quantityToRemove,
                    weightDiff: usedWeightDiff);
            }
            catch (OverflowException)
            {
                await transaction.RollbackAsync();
                throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, AuctionErrorCodes.AMOUNT_TOO_LARGE));
            }
            catch (RpcException)
            {
                await transaction.RollbackAsync();
                throw;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw new RpcException(new Status(StatusCode.Internal, AuctionErrorCodes.FAILED_TO_REMOVE_LOT));
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
            throw new RpcException(new Status(StatusCode.InvalidArgument, AuctionErrorCodes.INVALID_QUANTITY));

        return await GetRetryPolicy().ExecuteAsync(async () =>
        {
            using var aucyDb = await _aucyDbFactory.CreateDbContextAsync();

            var itemConfigFields = await aucyDb.Lots
                .Where(x => x.Id == request.LotId && x.ItemConfig != null)
                .Select(x => new { x.ItemConfig.IsUnary, x.ItemConfigId })
                .FirstOrDefaultAsync()
                        ?? throw new NoRetryRpcException(new Status(StatusCode.NotFound, AuctionErrorCodes.LOT_NOT_FOUND));

            // No worker lock needed
            // No Non-User Unary lock needed 
            // (Concurrent Purchace -> RemainingQuantity decrease -> still correct calculation with quantity diff)
            var userLockKey = itemConfigFields.IsUnary ?
                    LockKeys.GetUnaryUserLock(itemConfigFields.ItemConfigId, userId) :
                    LockKeys.GetNonUnaryUserLock(itemConfigFields.ItemConfigId, userId);

            using var userLock = await _lockProvider.TryAcquireLockAsync(userLockKey)
                ?? throw new NoRetryRpcException(new Status(StatusCode.FailedPrecondition,
                    AuctionErrorCodes.ITEM_ALREADY_IN_PROCESSING));

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
                    .FirstOrDefaultAsync() ?? throw new NoRetryRpcException(new Status(StatusCode.NotFound, 
                        AuctionErrorCodes.NOT_ENOUGH_TO_COLLECT_LOT));

                if (lotFields.RemainingQuantity == lotFields.TotalQuantity)
                    throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, 
                        AuctionErrorCodes.NOT_ENOUGH_TO_COLLECT_LOT));

                var soldQuantity = lotFields.TotalQuantity - lotFields.RemainingQuantity;
                var takeQuantity = request.Quantity;

                if (takeQuantity > soldQuantity)
                    throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, 
                        AuctionErrorCodes.NOT_ENOUGH_TO_COLLECT_LOT));

                int updatedLots;

                if (takeQuantity < lotFields.TotalQuantity)
                {
                    updatedLots = await aucyDb.Lots
                        .Where(b => b.Id == request.LotId && b.OwnerUserId == userId)
                        .ExecuteUpdateAsync(x =>
                        {
                            x.SetProperty(x => x.TotalQuantity, x => x.TotalQuantity - takeQuantity);
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
                    throw new NoRetryRpcException(new Status(StatusCode.NotFound, AuctionErrorCodes.NOT_ENOUGH_TO_COLLECT_LOT));

                var returningMoney = checked(lotFields.UnitPrice * takeQuantity + lotFields.ExtraSoldoutMoney);
                // TODO: apply commission

                await _inventoryProperties.DepositMoney(aucyDb, userId, returningMoney);

                await transaction.CommitAsync();

                _logger.LogInformation("Collected {CollectedMoney} money from {Quantity} items of lot {LotId} for user {UserId} with itemConfig {ItemConfigId}",
                    returningMoney, takeQuantity, lotFields.Id, userId, lotFields.ItemConfigId);

                return new LotCollectedDto(lotId: lotFields.Id,
                    returningMoney: returningMoney,
                    collectedQuantity: takeQuantity);
            }
            catch (OverflowException)
            {
                await transaction.RollbackAsync();
                throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, AuctionErrorCodes.AMOUNT_TOO_LARGE));
            }
            catch (RpcException)
            {
                await transaction.RollbackAsync();
                throw;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw new RpcException(new Status(StatusCode.Internal,
                    AuctionErrorCodes.FAILED_TO_COLLECT_LOT));
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
            throw new RpcException(new Status(StatusCode.InvalidArgument, AuctionErrorCodes.INVALID_UNIT_PRICE));

        if (request.TotalQuantity <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, AuctionErrorCodes.INVALID_QUANTITY));

        using var aucyDb = await _aucyDbFactory.CreateDbContextAsync();

        var itemConfigDto = await aucyDb.ItemConfigs
                .Where(x => x.Id == request.ItemConfigId)
                .Select(x => _mapper.Map<ItemConfigDto>(x))
                .FirstOrDefaultAsync()
                     ?? throw new RpcException(new Status(StatusCode.NotFound, AuctionErrorCodes.ITEM_CONFIG_NOT_FOUND));

        if (request.BidPrice < itemConfigDto.MinPrice || request.BidPrice > itemConfigDto.MaxPrice)
            throw new RpcException(new Status(StatusCode.InvalidArgument, AuctionErrorCodes.INVALID_PRICE_RANGE));

        // Validate that provided price matches one of configured price tiers
        if (!_quantizer.IsPriceValid(itemConfigDto.MinPrice, itemConfigDto.MaxPrice, itemConfigDto.PriceTierCount, request.BidPrice))
            throw new RpcException(new Status(StatusCode.InvalidArgument, AuctionErrorCodes.INVALID_PRICE_TIER));

        if (request.TotalQuantity > itemConfigDto.MaxBuyQuantity)
            throw new RpcException(new Status(StatusCode.InvalidArgument, AuctionErrorCodes.BID_QUANTITY_EXCEEDS_MAX));

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

        using var userLock = await _lockProvider.TryAcquireLockAsync(LockKeys.GetNonUnaryUserLock(itemConfigId, userId))
            ?? throw new NoRetryRpcException(new Status(StatusCode.FailedPrecondition,
                AuctionErrorCodes.ITEM_ALREADY_IN_PROCESSING));

        using var rwLock = await _rwLockProvider.AcquireReadLockAsync(LockKeys.GetItemMatchingLock(itemConfigId));

        using var transaction = await aucyDb.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        try
        {
            var ownLotExists = await aucyDb.Lots.AnyAsync(l =>
                l.OwnerUserId == userId &&
                l.ItemConfigId == itemConfigId);

            if (ownLotExists)
                throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, 
                    AuctionErrorCodes.CANNOT_CREATE_BID_WHILE_SELLING));

            var bidExists = await aucyDb.Bids.AnyAsync(b =>
                b.BidderUserId == userId &&
                b.ItemConfigId == itemConfigId);

            if (bidExists)
                throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, AuctionErrorCodes.BID_ALREADY_EXISTS));

            var reservedMoney = checked(request.BidPrice * request.TotalQuantity);
            await _inventoryProperties.WithdrawMoney(aucyDb, userId, reservedMoney);

            var bid = Bid.Create(
                bidderUserId: userId,
                itemConfigId: itemConfigId,
                bidPrice: request.BidPrice,
                totalQuantity: request.TotalQuantity);

            aucyDb.Bids.Add(bid);

            await aucyDb.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Created bid {BidId} for user {UserId} with itemConfig {ItemConfigId}",
                   bid.Id, userId, itemConfigId);

            return new BidCreatedDto(bidId: bid.Id,
                itemConfigId: itemConfigId,
                itemConfig: null,  // NonUnary item config is stored on the client
                bidPrice: bid.BidPrice,
                totalQuantity: bid.TotalQuantity,
                createdAt: bid.CreatedAt);
        }
        catch (OverflowException)
        {
            await transaction.RollbackAsync();
            throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, AuctionErrorCodes.BID_PRICE_TOO_LARGE));
        }
        catch (RpcException)
        {
            await transaction.RollbackAsync();
            throw;
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw new RpcException(new Status(StatusCode.Internal, AuctionErrorCodes.FAILED_TO_CREATE_BID));
        }
    }

    private async UnaryResult<BidCreatedDto> BuyUnary(Guid userId, CreateBidDto request, AucyDbContext aucyDb,
        ItemConfigDto itemConfigDto)
    {
        if (request.TotalQuantity != 1)
            throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, 
                AuctionErrorCodes.INVALID_QUANTITY_FOR_UNARY_ITEM));

        var itemConfigId = request.ItemConfigId;

        using var userLock = await _lockProvider.TryAcquireLockAsync(LockKeys.GetUnaryUserLock(itemConfigId, userId))
            ?? throw new NoRetryRpcException(new Status(StatusCode.FailedPrecondition, 
                AuctionErrorCodes.ITEM_ALREADY_IN_PROCESSING));

        using var unaryLock = await _lockProvider.AcquireLockAsync(LockKeys.GetUnaryLock(itemConfigId));

        using var transaction = await aucyDb.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        try
        {
            var ownLotExists = await aucyDb.Lots.AnyAsync(l =>
                    l.OwnerUserId == userId &&
                    l.ItemConfigId == request.ItemConfigId);

            if (ownLotExists)
                throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, 
                    AuctionErrorCodes.CANNOT_CREATE_BID_WHILE_SELLING));

            var lotFields = await aucyDb.Lots
                .Where(l => l.ItemConfigId == request.ItemConfigId && l.RemainingQuantity > 0)
                .Select(l => new
                {
                    l.Id,
                    l.UnitPrice
                })
                .FirstOrDefaultAsync() 
                    ?? throw new NoRetryRpcException(new Status(StatusCode.NotFound, AuctionErrorCodes.LOT_NOT_FOUND));

            if (lotFields.UnitPrice != request.BidPrice)
                throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument,
                    AuctionErrorCodes.INVALID_UNARY_BID_PRICE));

            await _inventoryProperties.WithdrawMoney(aucyDb, userId, lotFields.UnitPrice);

            var updatedLots = await aucyDb.Lots.Where(x => x.Id == lotFields.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(x => x.RemainingQuantity, x => x.RemainingQuantity - 1));

            if (updatedLots != 1)
                throw new RpcException(new Status(StatusCode.NotFound, AuctionErrorCodes.LOT_NOT_FOUND));

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

            _logger.LogInformation("Created bid {BidId} for user {UserId} with itemConfig {ItemConfigId}",
                   bid.Id, userId, itemConfigId);

            return new BidCreatedDto(bidId: bid.Id,
                itemConfigId: itemConfigId,
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
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw new RpcException(new Status(StatusCode.Internal, AuctionErrorCodes.FAILED_TO_CREATE_BID));
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
                    ?? throw new NoRetryRpcException(new Status(StatusCode.NotFound, AuctionErrorCodes.BID_NOT_FOUND));

            IDistributedSynchronizationHandle userLock;
            IDistributedSynchronizationHandle? rwItemLock = null;
            IDistributedSynchronizationHandle? unaryLock = null;

            if (!itemConfigFields.IsUnary)
            {
                userLock = await _lockProvider.TryAcquireLockAsync(LockKeys.GetNonUnaryUserLock(itemConfigFields.ItemConfigId, userId))
                    ?? throw new NoRetryRpcException(new Status(StatusCode.FailedPrecondition,
                        AuctionErrorCodes.ITEM_ALREADY_IN_PROCESSING));

                // wait for matching worker if not unary
                rwItemLock = await _rwLockProvider.AcquireReadLockAsync(LockKeys.GetItemMatchingLock(itemConfigFields.ItemConfigId));
            }
            else
            {
                userLock = await _lockProvider.TryAcquireLockAsync(LockKeys.GetUnaryUserLock(itemConfigFields.ItemConfigId, userId))
                    ?? throw new NoRetryRpcException(new Status(StatusCode.FailedPrecondition,
                        AuctionErrorCodes.ITEM_ALREADY_IN_PROCESSING));

                unaryLock = await _lockProvider.AcquireLockAsync(LockKeys.GetUnaryLock(itemConfigFields.ItemConfigId));
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
                    .FirstOrDefaultAsync() 
                        ?? throw new NoRetryRpcException(new Status(StatusCode.NotFound, AuctionErrorCodes.NOTHING_TO_REMOVE_BID));

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
                    throw new RpcException(new Status(StatusCode.FailedPrecondition,
                        AuctionErrorCodes.BID_REMOVING_CONFLICT));

                var returningMoney = checked(bidFields.BidPrice * quantityToRemove);
                if (returningMoney > 0)
                    await _inventoryProperties.DepositMoney(aucyDb, userId, returningMoney);

                // Bought items remain in bids. They will be collected by user manually.

                await transaction.CommitAsync();

                _logger.LogInformation("Removed {RemovedBidQuantity} of bid {BidId} for user {UserId} with itemConfig {ItemConfigId} with {BidReturningMoney} returning money",
                   quantityToRemove, bidFields.Id, userId, bidFields.ItemConfigId, returningMoney);

                return new BidRemovedDto(bidId: bidFields.Id,
                    returningMoney: returningMoney,
                    removedQuantity: quantityToRemove);
            }
            catch (OverflowException)
            {
                await transaction.RollbackAsync();
                throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, AuctionErrorCodes.AMOUNT_TOO_LARGE));
            }
            catch (RpcException)
            {
                await transaction.RollbackAsync();
                throw;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw new RpcException(new Status(StatusCode.Internal, AuctionErrorCodes.FAILED_TO_REMOVE_BID));
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
            throw new RpcException(new Status(StatusCode.InvalidArgument, AuctionErrorCodes.INVALID_QUANTITY));

        return await GetRetryPolicy().ExecuteAsync(async () =>
        {
            using var aucyDb = await _aucyDbFactory.CreateDbContextAsync();

            var itemConfigDto = await aucyDb.Bids
                .Where(x => x.Id == request.BidId && x.ItemConfig != null)
                .Select(x => _mapper.Map<ItemConfigDto>(x.ItemConfig))
                .FirstOrDefaultAsync()
                    ?? throw new NoRetryRpcException(new Status(StatusCode.NotFound, AuctionErrorCodes.BID_NOT_FOUND));

            // No worker lock needed
            // No Non-User Unary lock needed 
            // (Concurrent Sale -> RemainingQuantity decrease -> still correct calculation with quantity diff)
            var userLockKey = itemConfigDto.IsUnary ?
                    LockKeys.GetUnaryUserLock(itemConfigDto.Id, userId) :
                    LockKeys.GetNonUnaryUserLock(itemConfigDto.Id, userId);

            using var userLock = await _lockProvider.TryAcquireLockAsync(userLockKey)
                ?? throw new NoRetryRpcException(new Status(StatusCode.FailedPrecondition,
                    AuctionErrorCodes.ITEM_ALREADY_IN_PROCESSING));

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
                    .FirstOrDefaultAsync() 
                        ?? throw new NoRetryRpcException(new Status(StatusCode.NotFound,
                            AuctionErrorCodes.NOT_ENOUGH_TO_COLLECT_BID));

                // Check if any items were bought
                if (bidFields.RemainingQuantity == bidFields.TotalQuantity)
                    throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, 
                        AuctionErrorCodes.NOT_ENOUGH_TO_COLLECT_BID));

                var boughtQuantity = bidFields.TotalQuantity - bidFields.RemainingQuantity;

                var takeQuantity = request.Quantity;

                if (takeQuantity > boughtQuantity)
                    throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, 
                        AuctionErrorCodes.NOT_ENOUGH_TO_COLLECT_BID));

                int bidsUpdated;

                if (takeQuantity < bidFields.TotalQuantity)
                {
                    bidsUpdated = await aucyDb.Bids
                        .Where(b => b.Id == request.BidId && b.BidderUserId == userId)
                        .ExecuteUpdateAsync(x =>
                        {
                            x.SetProperty(x => x.TotalQuantity, x => x.TotalQuantity - takeQuantity);
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
                    throw new RpcException(new Status(StatusCode.NotFound, AuctionErrorCodes.NOT_ENOUGH_TO_COLLECT_BID));

                // Add purchased items to user's inventory
                var usedCapacityDiff = await _itemsService.UpdateOrInsertItemStack(aucyDb, bidFields.ItemConfigId,
                    takeQuantity, userId);

                // Alter inventory UsedWeight
                float usedWeightDiff = 0;

                if (bidFields.ItemConfigWeight > 0)
                    usedWeightDiff = checked(bidFields.ItemConfigWeight * takeQuantity);

                await _inventoryProperties.ExecuteInventoryUpdate(aucyDb, userId, usedWeightDiff, usedCapacityDiff);

                await transaction.CommitAsync();

                _logger.LogInformation("Collected {Quantity} bought items from bid {BidId} by user {UserId} with itemConfig {ItemConfigId}",
                   takeQuantity, bidFields.Id, userId, bidFields.ItemConfigId);

                return new BidCollectedDto(bidId: bidFields.Id,
                    itemConfigId: bidFields.ItemConfigId,
                    itemConfig: itemConfigDto.IsUnary ? itemConfigDto : null,  // Send Unary. NonUnary item config is stored on the client 
                    collectedQuantity: takeQuantity,
                    weightDiff: usedWeightDiff);
            }
            catch (OverflowException)
            {
                await transaction.RollbackAsync();
                throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, AuctionErrorCodes.AMOUNT_TOO_LARGE));
            }
            catch (RpcException)
            {
                await transaction.RollbackAsync();
                throw;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw new RpcException(new Status(StatusCode.Internal, AuctionErrorCodes.FAILED_TO_COLLECT_BID));
            }
        });
    }

    private static AsyncRetryPolicy GetRetryPolicy()
    {
        return Policy
            .Handle<Exception>(ex => ex is not NoRetryRpcException)
            .RetryAsync(3);
    }
}