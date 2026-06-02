using Aucy.Application.Exceptions;
using Aucy.Application.Helpers;
using Aucy.Contracts.OnionContracts;
using Aucy.Data;
using Aucy.Domain.Entities;
using AutoMapper;
using Grpc.Core;
using MagicOnion;
using MagicOnion.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;
using System.Collections.Concurrent;
using System.Data;

namespace Aucy.Application;

[Authorize]
public class OrderBookService(
    InventoryPropertiesService inventoryPropertiesService,
    ItemsService itemsService,
    IDbContextFactory<AucyDbContext> aucyDbFactory,
    IMapper mapper) : ServiceBase<IOrderBookService>,
#pragma warning disable CS8644 // Type does not implement interface member. Nullability of reference types in interface implemented by the base type doesn't match.
    IOrderBookService
#pragma warning restore CS8644 // Type does not implement interface member. Nullability of reference types in interface implemented by the base type doesn't match.
{
    private const int MatchingBatchSize = 256;

    /// <summary>
    /// Operations locking for NonUnary items mathing (by ItemConfig.Id).
    /// </summary>
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> MatchingLocks = new(); // TODO: Price predicate lock

    private readonly IDbContextFactory<AucyDbContext> _aucyDbFactory = aucyDbFactory;
    private readonly InventoryPropertiesService _inventoryProperties = inventoryPropertiesService;
    private readonly ItemsService _itemsService = itemsService;
    private readonly IMapper _mapper = mapper;

    public async UnaryResult<LotDto> CreateLot(CreateLotDto request)
    {
        var userId = Context.GetUserId();

        Console.WriteLine($"Creating lot request for user: {userId}");

        if (request.UnitPrice <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Unit price should be greater than 0"));

        if (request.TotalQuantity <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Total quantity should be greater than 0"));

        if (request.ItemConfig == null)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Item config should be provided"));

        using var aucyDb = await _aucyDbFactory.CreateDbContextAsync();

        var ownBidExists = await aucyDb.Bids.AnyAsync(l =>
            l.BidderUserId == userId &&
            l.ItemConfigId == request.ItemConfig.Id);

        if (ownBidExists)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "You can`t create a lot for the item you are buying"));

        var itemConfigFields = await aucyDb.ItemConfigs
            .Where(x => x.Id == request.ItemConfig.Id)
            .Select(x => new { x.IsUnary })
            .FirstOrDefaultAsync()
                    ?? throw new RpcException(new Status(StatusCode.NotFound, "Item config doesn`t exist"));

        if (itemConfigFields.IsUnary)
            return await SellUnary(userId, request);
        else
            return await SellNonUnary(userId, request);
    }

    public async UnaryResult<LotDto> SellNonUnary(Guid userId, CreateLotDto request)
    {
        return await GetRetryPolicy().ExecuteAsync(async () =>
        {
            var itemConfig = request.ItemConfig;
            var itemConfigId = itemConfig.Id;
            var matchingLock = GetMatchingLock(itemConfigId);

            await matchingLock.WaitAsync();

            using var aucyDb = await _aucyDbFactory.CreateDbContextAsync();

            using var transaction = await aucyDb.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);

            try
            {
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
                var usedCapacityDiff = await _itemsService.UpdateOrDeleteItemStack(itemConfigId, request.TotalQuantity, userId);

                // Alter inventory UsedWeight
                float usedWeightDiff = 0;

                // Can be applied by parts in cycle if numbers are too big.
                if (itemConfig.Weight > 0)
                    usedWeightDiff = checked(-itemConfig.Weight * request.TotalQuantity);

                await _inventoryProperties.ExecuteInventoryUpdate(userId, usedWeightDiff, usedCapacityDiff);

                // TIP: If you don`t want to create lot after instant soldout,
                // then you have to validate inventory properties
                var lot = Lot.Create(ownerUserId: userId,
                    itemConfigId: itemConfigId,
                    unitPrice: request.UnitPrice,
                    totalQuantity: request.TotalQuantity);

                await MatchLotWithBids(lot, aucyDb);

                aucyDb.Lots.Add(lot);

                await aucyDb.SaveChangesAsync();
                await transaction.CommitAsync();

                Console.WriteLine($"Created lot: {lot.Id} for user: {userId} with itemConfig: {itemConfigId}");

                return _mapper.Map<LotDto>(lot);
            }
            catch (OverflowException)
            {
                await transaction.RollbackAsync();
                throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, "Lot total width is too large"));
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
            finally
            {
                matchingLock.Release();
            }
        });
    }

    public async UnaryResult<LotDto> SellUnary(Guid userId, CreateLotDto request)
    {
        if (request.TotalQuantity != 1)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Total quantity should be 1 for unary item"));

        return await GetRetryPolicy().ExecuteAsync(async () =>
        {
            using var aucyDb = await _aucyDbFactory.CreateDbContextAsync();

            using var transaction = await aucyDb.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);

            try
            {
                var usedCapacityDiff = await _itemsService.UpdateOrDeleteItemStack(request.ItemConfig.Id, 1, userId);

                // Alter inventory UsedWeight
                float usedWeightDiff = 0;

                // Can be applied by parts in cycle if numbers are too large.
                if (request.ItemConfig.Weight > 0)
                    usedWeightDiff = checked(-request.ItemConfig.Weight * request.TotalQuantity);

                await _inventoryProperties.ExecuteInventoryUpdate(userId, weightDiff: usedWeightDiff,
                    capacityDiff: usedCapacityDiff);

                // No mathing with bids for Unary items
                var lot = Lot.Create(ownerUserId: userId,
                    itemConfigId: request.ItemConfig.Id,
                    unitPrice: request.UnitPrice,
                    totalQuantity: request.TotalQuantity);

                aucyDb.Add(lot);

                await aucyDb.SaveChangesAsync();
                await transaction.CommitAsync();

                return _mapper.Map<LotDto>(lot);
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
        });
    }

    public async UnaryResult<OwnLotsDto> GetOwnLots()
    {
        var userId = Context.GetUserId();

        using var aucyDb = await _aucyDbFactory.CreateDbContextAsync();

        var lotsWithItemConfigs = await aucyDb.Lots
            .Where(l => l.OwnerUserId == userId)
            .Include(l => l.ItemConfig)
            .Select(l => _mapper.Map<OwnLotDto>(l))
            .ToListAsync();

        return new OwnLotsDto(lotsWithItemConfigs);
    }

    public async UnaryResult<Guid> RemoveLot(RemoveLotDto request)
    {
        var userId = Context.GetUserId();

        return await GetRetryPolicy().ExecuteAsync(async () =>
        {
            using var aucyDb = _aucyDbFactory.CreateDbContext();

            var itemConfigFields = await aucyDb.Lots
                .Where(x => x.Id == request.LotId && x.ItemConfig != null)
                .Select(x => new { x.ItemConfigId, x.ItemConfig.IsUnary })
                .FirstOrDefaultAsync()
                    ?? throw new NoRetryRpcException(new Status(StatusCode.NotFound, "Item config doesn`t exist"));

            SemaphoreSlim? matchingLock = null;

            if (!itemConfigFields.IsUnary)
            {
                matchingLock = GetMatchingLock(itemConfigFields.ItemConfigId);
                await matchingLock.WaitAsync();
            }

            var transaction = await aucyDb.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);

            try
            {
                var lotFields = await aucyDb.Lots
                    .Where(b => b.Id == request.LotId && b.OwnerUserId == userId)
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
                    .FirstOrDefaultAsync() ?? throw new NoRetryRpcException(new Status(StatusCode.NotFound, "Lot doesn`t exist"));

                int lotsUpdated = 0;

                if (lotFields.RemainingQuantity < lotFields.TotalQuantity)
                {
                    var remainingQuantity = lotFields.RemainingQuantity;
                    lotsUpdated = await aucyDb.Lots
                        .Where(b => b.Id == request.LotId && b.OwnerUserId == userId)
                        .ExecuteUpdateAsync(x =>
                        {
                            x.SetProperty(x => x.RemainingQuantity, x => x.RemainingQuantity - remainingQuantity)
                                .SetProperty(x => x.TotalQuantity, x => x.TotalQuantity - remainingQuantity);
                        });
                }
                else
                {
                    lotsUpdated = await aucyDb.Lots
                        .Where(b => b.Id == request.LotId && b.OwnerUserId == userId)
                        .ExecuteDeleteAsync();
                }

                if (lotsUpdated != 1)
                    throw new RpcException(new Status(StatusCode.NotFound,
                        $"Lot is not determined. Entries found: {lotsUpdated}"));

                if (lotFields.RemainingQuantity > 0)
                {
                    var usedCapacityDiff = await _itemsService.UpdateOrInsertItemStack(lotFields.ItemConfigId,
                        lotFields.RemainingQuantity, userId);

                    float usedWeightDiff = 0;

                    if (lotFields.ItemConfigWeight > 0)
                        usedWeightDiff = checked(lotFields.ItemConfigWeight * lotFields.RemainingQuantity);

                    await _inventoryProperties.ExecuteInventoryUpdate(userId, usedWeightDiff, usedCapacityDiff);
                }

                await transaction.CommitAsync();

                return request.LotId;
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
                matchingLock?.Release();
            }
        });
    }

    public async UnaryResult<Guid> CollectLot(CollectLotDto request)
    {
        //var userId = Context.GetUserId();

        //var transaction = await aucyDb.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);

        //var soldQuantity = lotFields.TotalQuantity - lotFields.RemainingQuantity;

        //var returningMoney = checked(lotFields.UnitPrice * soldQuantity + lotFields.ExtraSoldoutMoney);
        //// TODO: apply commission

        //if (returningMoney > 0)
        //    await _inventoryProperties.DepositMoney(userId, returningMoney);

        throw new NotImplementedException("CollectLot method is not implemented yet");
    }

    public async UnaryResult<BidDto> CreateBid(CreateBidDto request)
    {
        var userId = Context.GetUserId();

        if (request.BidPrice <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Bid price should be greater than 0"));

        if (request.TotalQuantity <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Total quantity should be greater than 0"));

        using var aucyDb = await _aucyDbFactory.CreateDbContextAsync();

        var itemConfigFields = await aucyDb.ItemConfigs
                .Where(x => x.Id == request.ItemConfigId)
                .Select(x => new { x.IsUnary })
                .FirstOrDefaultAsync()
                     ?? throw new RpcException(new Status(StatusCode.NotFound, "Item config doesn`t exist"));

        var ownLotExists = await aucyDb.Lots.AnyAsync(l =>
            l.OwnerUserId == userId &&
            l.ItemConfigId == request.ItemConfigId);

        if (ownLotExists)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "You can`t create a bid for the item you are selling"));

        if (itemConfigFields.IsUnary)
            return await BuyUnary(userId, request);
        else
            return await BuyNonUnary(userId, request);
    }

    private async UnaryResult<BidDto> BuyNonUnary(Guid userId, CreateBidDto request)
    {
        return await GetRetryPolicy().ExecuteAsync(async () =>
        {
            var matchingLock = GetMatchingLock(request.ItemConfigId);

            await matchingLock.WaitAsync();

            using var aucyDb = await _aucyDbFactory.CreateDbContextAsync();

            using var transaction = await aucyDb.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);

            try
            {
                var bidExists = await aucyDb.Bids.AnyAsync(b =>
                    b.BidderUserId == userId &&
                    b.ItemConfigId == request.ItemConfigId);

                if (bidExists)
                    throw new NoRetryRpcException(new Status(StatusCode.InvalidArgument, "You already have a bid with this item"));

                var reservedMoney = checked(request.BidPrice * request.TotalQuantity);
                await _inventoryProperties.WithdrawMoney(userId, reservedMoney);

                var bid = Bid.Create(
                    bidderUserId: userId,
                    itemConfigId: request.ItemConfigId,
                    bidPrice: request.BidPrice,
                    totalQuantity: request.TotalQuantity);

                await MatchBidWithLots(bid, aucyDb);

                aucyDb.Bids.Add(bid);

                await aucyDb.SaveChangesAsync(); // + Trigger
                await transaction.CommitAsync();

                return _mapper.Map<BidDto>(bid);
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
            finally
            {
                matchingLock.Release();
            }
        });
    }

    private async UnaryResult<BidDto> BuyUnary(Guid userId, CreateBidDto request)
    {
        if (request.TotalQuantity != 1)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Total quantity should be 1 for unary item"));

        return await GetRetryPolicy().ExecuteAsync(async () =>
        {
            using var aucyDb = await _aucyDbFactory.CreateDbContextAsync();

            using var transaction = await aucyDb.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);

            try
            {
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

                await _inventoryProperties.WithdrawMoney(userId, lotFields.UnitPrice);

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

                aucyDb.Bids.Add(bid);

                await aucyDb.SaveChangesAsync();
                await transaction.CommitAsync();

                return _mapper.Map<BidDto>(bid);
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
        });
    }

    public async UnaryResult<OwnBidsDto> GetOwnBids()
    {
        var userId = Context.GetUserId();

        using var aucyDb = await _aucyDbFactory.CreateDbContextAsync();

        var ownBids = await aucyDb.Bids
            .Where(b => b.BidderUserId == userId)
            .Include(b => b.ItemConfig)
            .Select(b => _mapper.Map<OwnBidDto>(b))
            .ToListAsync();

        return new OwnBidsDto(ownBids);
    }

    public async UnaryResult<Guid> RemoveBid(RemoveBidDto request)
    {
        var userId = Context.GetUserId();

        return await GetRetryPolicy().ExecuteAsync(async () =>
        {
            using var aucyDb = await _aucyDbFactory.CreateDbContextAsync();

            var itemConfigFields = await aucyDb.Bids
                .Where(x => x.Id == request.BidId && x.ItemConfig != null)
                .Select(x => new { x.ItemConfigId, x.ItemConfig.IsUnary })
                .FirstOrDefaultAsync()
                    ?? throw new NoRetryRpcException(new Status(StatusCode.NotFound, "Item config doesn`t exist"));

            SemaphoreSlim? matchingLock = null;

            if (!itemConfigFields.IsUnary)
            {
                matchingLock = GetMatchingLock(itemConfigFields.ItemConfigId);

                await matchingLock.WaitAsync();
            }

            var transaction = await aucyDb.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);

            try
            {
                var bidFields = await aucyDb.Bids
                    .Where(b => b.Id == request.BidId && b.BidderUserId == userId)
                    .Select(b => new
                    {
                        b.Id,
                        b.ItemConfigId,
                        ItemConfigWeight = b.ItemConfig.Weight,
                        b.TotalQuantity,
                        b.RemainingQuantity,
                        b.BidPrice
                    })
                    .FirstOrDefaultAsync() ?? throw new NoRetryRpcException(new Status(StatusCode.NotFound, "Bid doesn`t exist"));

                int bidsUpdated = 0;

                if (bidFields.RemainingQuantity < bidFields.TotalQuantity) // If some items were bought, remove only unbought quantity
                {
                    var remainingQuantity = bidFields.RemainingQuantity;
                    bidsUpdated = await aucyDb.Bids
                        .Where(b => b.Id == request.BidId && b.BidderUserId == userId)
                        .ExecuteUpdateAsync(x =>
                        {
                            x.SetProperty(x => x.RemainingQuantity, x => x.RemainingQuantity - remainingQuantity)
                                .SetProperty(x => x.TotalQuantity, x => x.TotalQuantity - remainingQuantity);
                        });
                }
                else // all items are unsold, we can just delete the bid)
                {
                    bidsUpdated = await aucyDb.Bids
                        .Where(b => b.Id == request.BidId && b.BidderUserId == userId)
                        .ExecuteDeleteAsync();
                }

                if (bidsUpdated != 1)
                    throw new RpcException(new Status(StatusCode.NotFound,
                        $"Bid is not determined. Entries found: {bidsUpdated}"));

                var returningMoney = checked(bidFields.BidPrice * bidFields.RemainingQuantity);
                if (returningMoney > 0)
                    await _inventoryProperties.DepositMoney(userId, returningMoney);

                // Bought items remain in bids. They will be collected by user manually.

                await transaction.CommitAsync();

                return request.BidId;
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
                matchingLock?.Release();
            }
        });
    }

    public UnaryResult<BidDto> CollectBid(CollectBidDto request)
    {
        // If some items were bought
        //if (bidFields.RemainingQuantity < bidFields.TotalQuantity)
        //{
        //    var boughtQuantity = bidFields.TotalQuantity - bidFields.RemainingQuantity;

        //    // Try to update an existing ItemStack with the same ConfigId or create new one if not found
        //    var usedCapacityDiff = await _itemsService.UpdateOrInsertItemStack(bidFields.ItemConfigId,
        //        boughtQuantity, userId);

        //    // Alter inventory UsedWeight
        //    float usedWeightDiff = 0;

        //    // Can be applied by parts in cycle if numbers are too big.
        //    if (bidFields.ItemConfigWeight > 0)
        //        usedWeightDiff = checked(bidFields.ItemConfigWeight * boughtQuantity);

        //    await _inventoryProperties.ExecuteInventoryUpdate(userId, usedWeightDiff, usedCapacityDiff);
        //}

        throw new NotImplementedException();
    }

    private static SemaphoreSlim GetMatchingLock(Guid itemConfigId)
    {
        return MatchingLocks.GetOrAdd(itemConfigId, _ => new SemaphoreSlim(1, 1));
    }

    private async Task MatchLotWithBids(Lot lot, AucyDbContext aucyDb)
    {
        while (lot.RemainingQuantity > 0)
        {
            // TODO: repeat if any order was deleted
            var bids = await aucyDb.Bids
                .AsTracking()
                .Where(x =>
                    x.ItemConfigId == lot.ItemConfigId &&
                    x.RemainingQuantity > 0 &&
                    x.BidPrice >= lot.UnitPrice)
                .OrderByDescending(x => x.BidPrice)
                .ThenBy(x => x.CreatedAt)
                //.ThenBy(x => x.Id)
                .Take(MatchingBatchSize)
                .ToListAsync();

            if (bids.Count == 0)
                return;

            foreach (var bid in bids)
            {
                if (lot.RemainingQuantity == 0)
                    break;

                var tradeQuantity = Math.Min(lot.RemainingQuantity, bid.RemainingQuantity);

                bid.RemainingQuantity -= tradeQuantity;
                lot.RemainingQuantity -= tradeQuantity;

                // Extra soldout money stores in lot.
                // In case of changing commission user should deside when to take his money.
                if (lot.UnitPrice < bid.BidPrice)
                    lot.ExtraSoldoutMoney += checked((bid.BidPrice - lot.UnitPrice) * tradeQuantity);
            }

            await aucyDb.SaveChangesAsync();

            if (bids.Count < MatchingBatchSize)
                return;
        }
    }

    private async Task MatchBidWithLots(Bid bid, AucyDbContext aucyDb)
    {
        /// Item can be bought with lower price than specified in <see cref="Bid.BidPrice"/> at the moment of bid creation. 
        /// In this case, user will pay less money for the item. That difference will be stored here.
        long refund = 0;

        while (bid.RemainingQuantity > 0)
        {
            // TODO: repeat if any order was deleted
            var lots = await aucyDb.Lots
                .AsTracking()
                .Where(x =>
                    x.ItemConfigId == bid.ItemConfigId &&
                    x.RemainingQuantity > 0 &&
                    x.UnitPrice <= bid.BidPrice)
                .OrderBy(x => x.UnitPrice)
                .ThenBy(x => x.CreatedAt)
                //.ThenBy(x => x.Id)
                .Take(MatchingBatchSize)
                .ToListAsync();

            if (lots.Count == 0)
                break;

            foreach (var lot in lots)
            {
                if (bid.RemainingQuantity == 0)
                    break;

                var tradeQuantity = Math.Min(bid.RemainingQuantity, lot.RemainingQuantity);

                lot.RemainingQuantity -= tradeQuantity;
                bid.RemainingQuantity -= tradeQuantity;

                if (bid.BidPrice > lot.UnitPrice)
                    refund += checked((bid.BidPrice - lot.UnitPrice) * tradeQuantity);
            }

            await aucyDb.SaveChangesAsync();

            if (lots.Count < MatchingBatchSize)
                break;
        }

        // No commission => instant money recovery
        if (refund > 0)
            await _inventoryProperties.DepositMoney(bid.BidderUserId, refund);
    }

    private AsyncRetryPolicy GetRetryPolicy()
    {
        return Policy
            .Handle<Exception>(ex => ex is not NoRetryRpcException)
            .RetryAsync(3);
    }
}