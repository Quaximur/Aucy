using Aucy.Application.Helpers;
using Aucy.Contracts.OnionClient;
using Aucy.Data;
using Aucy.Domain.Entities;
using Cysharp.Runtime.Multicast;
using Medallion.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Data;

namespace Aucy.Application;

public struct WorkerPostEvent()
{
    public bool IsLot;
    public Guid UserId;
    public Guid OrderId;
    public int Quantity;
    public long ExtraMoney;
}

public sealed partial class OrderBookWorker(
    IDbContextFactory<AucyDbContext> aucyDbFactory,
    IServiceScopeFactory scopeFactory,
    IDistributedReaderWriterLockProvider rwLockProvider,
    ILogger<OrderBookWorker> logger,
    WorkerGroupService groupService
    )
{
    private readonly IDbContextFactory<AucyDbContext> _aucyDbFactory = aucyDbFactory;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IDistributedReaderWriterLockProvider _rwLockProvider = rwLockProvider;
    private readonly ILogger<OrderBookWorker> _logger = logger;
    private const int INTERVAL_MS = 1000;

    private readonly WorkerGroupService _groupService = groupService;

    public async void RunAsync(Guid itemConfigId)
    {
        while (true)
        {
            await ProcessMatchingTickAsync(itemConfigId);
            await Task.Delay(INTERVAL_MS);
        }
    }

    // TODO: use checked() for money
    private async Task ProcessMatchingTickAsync(Guid itemConfigId)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var inventoryProperties = scope.ServiceProvider.GetRequiredService<InventoryPropertiesService>();

        // Orders deletion/creation lock
        // + await existing deletion/creation operations lock from user requests
        // bid/lot processed values collecting is unlocked
        using var matchingLock = await _rwLockProvider.AcquireWriteLockAsync(Keys.GetItemMatchingLock(itemConfigId));

        using var aucyDb = scope.ServiceProvider.GetRequiredService<AucyDbContext>();

        using var transaction = await aucyDb.Database.BeginTransactionAsync();

        List<WorkerPostEvent> workerEvents = [];

        try
        {
            var minLotPrice = await aucyDb.Lots
                        .Where(l => l.ItemConfigId == itemConfigId && l.RemainingQuantity > 0)
                        .MinAsync(l => (long?)l.UnitPrice) ?? long.MinValue;

            var maxBidPrice = await aucyDb.Bids
                .Where(b => b.ItemConfigId == itemConfigId && b.RemainingQuantity > 0)
                .MaxAsync(b => (long?)b.BidPrice) ?? long.MaxValue;

            // Mark all as matched before processing
            await aucyDb.Lots
                .Where(x => x.ItemConfigId == itemConfigId && !x.IsMatched)
                .ExecuteUpdateAsync(x => x.SetProperty(x => x.IsMatched, true));

            await aucyDb.Bids
                .Where(x => x.ItemConfigId == itemConfigId && !x.IsMatched)
                .ExecuteUpdateAsync(x => x.SetProperty(x => x.IsMatched, true));

            if (minLotPrice > maxBidPrice)
            {
                await transaction.CommitAsync();
                return;
            }

            // Load all active lots and bids
            var activeLots = await aucyDb.Lots
                .AsTracking()
                .Where(l => l.ItemConfigId == itemConfigId && l.RemainingQuantity > 0 && l.UnitPrice <= maxBidPrice)
                .OrderBy(l => l.UnitPrice)
                .ThenBy(l => l.CreatedAt)
                .ToListAsync();

            var activeBids = await aucyDb.Bids
                .AsTracking()
                .Where(b => b.ItemConfigId == itemConfigId && b.RemainingQuantity > 0 && b.BidPrice >= minLotPrice)
                .OrderByDescending(b => b.BidPrice)
                .ThenBy(b => b.CreatedAt)
                .ToListAsync();

            // Execute matching
            // TODO: IN FUTURE: leave locked only (minLotPrice, maxBidPrice) orders. Unlock others
            // if lot is created after bid with higher price in one tick, then add difference to Lot.ExtraSoldoutMoney of the seller
            // if bid is created after lot with lower price in one tick, then add difference to Inventory.Money of the bidder 
            await MatchOrdersAsync(aucyDb, inventoryProperties, activeLots, activeBids, itemConfigId, workerEvents);

            await transaction.CommitAsync();

            try
            {
                foreach (var workerEvent in workerEvents)
                {
                    if (workerEvent.IsLot)
                    {
                        _groupService.NotifySold(userId: workerEvent.UserId,
                            lotId: workerEvent.OrderId,
                            soldQuantity: workerEvent.Quantity,
                            extraMoney: workerEvent.ExtraMoney);
                    }
                    else
                    {
                        _groupService.NotifyBought(userId: workerEvent.UserId,
                            bidId: workerEvent.OrderId,
                            boughtQuantity: workerEvent.Quantity,
                            extraMoney: workerEvent.ExtraMoney);
                    }
                }
            }
            catch
            {
                _logger.LogError("Worker failed to notify clients");
            }
        }
        catch
        {
            await transaction.RollbackAsync();
        }
    }

    private async Task MatchOrdersAsync(AucyDbContext aucyDb,
        InventoryPropertiesService inventoryProperties,
        List<Lot> lots,
        List<Bid> bids,
        Guid itemConfigId,
        List<WorkerPostEvent> workerEvents)
    {
        int lotIndex = 0;
        int bidIndex = 0;

        var lotEvent = new WorkerPostEvent()
        {
            IsLot = true,
        };

        var bidEvent = new WorkerPostEvent()
        {
            IsLot = false,
        };

        while (lotIndex < lots.Count && bidIndex < bids.Count)
        {
            var currentLot = lots[lotIndex];
            var currentBid = bids[bidIndex];

            lotEvent.UserId = currentLot.OwnerUserId;
            lotEvent.OrderId = currentLot.Id;

            bidEvent.UserId = currentBid.BidderUserId;
            bidEvent.OrderId = currentBid.Id;


            // Stop if no crossing prices
            if (currentLot.UnitPrice > currentBid.BidPrice)
                break;

            int matchQuantity = Math.Min(currentLot.RemainingQuantity, currentBid.RemainingQuantity);

            long clearingPrice;
            if (currentLot.UnitPrice != currentBid.BidPrice)
            {
                // Determine clearing price based on who created their order first

                bool lotCreatedFirst = currentLot.CreatedAt <= currentBid.CreatedAt;

                if (lotCreatedFirst)
                    clearingPrice = currentLot.UnitPrice; // Clearing at lot's lower price, bidder keeps the surplus
                else
                    clearingPrice = currentBid.BidPrice; // Clearing at bid's higher price, seller gets extra profit

                // Handle surplus/extra profit
                if (lotCreatedFirst)
                {
                    // Bidder pays clearing price (lot's unit price)
                    // Surplus = bidPrice - clearingPrice stays with bidder
                    // No ExtraSoldoutMoney for seller in this case

                    long extraProfit = (currentBid.BidPrice - clearingPrice) * matchQuantity;
                    await inventoryProperties.DepositMoney(currentBid.BidderUserId, extraProfit); // TODO: gather extraMoney for sequence (no multiple deposits)
                    bidEvent.ExtraMoney += extraProfit;
                }
                else
                {
                    // Seller receives clearing price (bid's bid price)
                    // Extra profit = clearingPrice - unitPrice goes to seller
                    long extraProfit = (clearingPrice - currentLot.UnitPrice) * matchQuantity;
                    currentLot.ExtraSoldoutMoney += extraProfit;
                    lotEvent.ExtraMoney += extraProfit;
                }
            }
            else
            {
                clearingPrice = currentLot.UnitPrice;
            }

            // Update remaining quantities
            currentLot.RemainingQuantity -= matchQuantity;
            lotEvent.Quantity += matchQuantity;

            currentBid.RemainingQuantity -= matchQuantity;
            bidEvent.Quantity += matchQuantity;

            LogSaleInformation(logger: _logger,
                matchQuantity: matchQuantity,
                itemConfigId: itemConfigId,
                ownerUserId: currentLot.OwnerUserId,
                bidderUserId: currentBid.BidderUserId,
                clearingPrice: clearingPrice);

            // Move to next order if fully filled
            if (currentLot.RemainingQuantity == 0)
            {
                workerEvents.Add(lotEvent);
                lotIndex++;
                lotEvent = new WorkerPostEvent() { IsLot = true };
            }

            if (currentBid.RemainingQuantity == 0)
            {
                workerEvents.Add(bidEvent);
                bidIndex++;
                bidEvent = new WorkerPostEvent() { IsLot = false };
            }

        }

        if (lotIndex >= lots.Count && bidIndex < bids.Count)
            workerEvents.Add(bidEvent);
        else if (lotIndex < lots.Count && bidIndex >= bids.Count)
            workerEvents.Add(lotEvent);

        // Save changes to matched orders
        await aucyDb.SaveChangesAsync();
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Sold quantity '{MatchQuantity}' of ItemConfig '{ItemConfigId}' from Owner '{OwnerUserId}' to Bidder '{BidderUserId}' for price {ClearingPrice}")]
    static partial void LogSaleInformation(
        ILogger logger,
        int matchQuantity,
        Guid itemConfigId,
        Guid ownerUserId,
        Guid bidderUserId,
        long clearingPrice
    );
}
