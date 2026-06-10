using Aucy.Application.Helpers;
using Aucy.Data;
using Aucy.Domain.Entities;
using AutoMapper;
using Medallion.Threading;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Aucy.Application;

public class OrderBookWorker(
    InventoryPropertiesService inventoryPropertiesService,
    ItemsService itemsService,
    IDbContextFactory<AucyDbContext> aucyDbFactory,
    IMapper mapper,
    IDistributedReaderWriterLockProvider rwLockProvider,
    IDistributedLockProvider lockProvider)
{

    private readonly IDbContextFactory<AucyDbContext> _aucyDbFactory = aucyDbFactory;
    private readonly InventoryPropertiesService _inventoryProperties = inventoryPropertiesService;
    private readonly ItemsService _itemsService = itemsService;
    private readonly IMapper _mapper = mapper;
    private readonly IDistributedLockProvider _lockProvider = lockProvider;
    private readonly IDistributedReaderWriterLockProvider _rwLockProvider = rwLockProvider;

    private const int INTERVAL_MS = 1000;

    public async Task Run(Guid itemConfigId)
    {
        while (true)
        {
            await ProcessMatchingTickAsync(itemConfigId);
            await Task.Delay(INTERVAL_MS);
        }
    }

    // TODO: use checked() for money
    public async Task ProcessMatchingTickAsync(Guid itemConfigId)
    {
        // Orders deletion/creation lock
        // + await existing deletion/creation operations lock from user requests
        // bid/lot processed values collecting is unlocked
        using var matchingLock = await _rwLockProvider.AcquireWriteLockAsync(Keys.GetItemMatchingLock(itemConfigId));

        using var aucyDb = await _aucyDbFactory.CreateDbContextAsync();

        using var transaction = await aucyDb.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        var minLotPrice = await aucyDb.Lots
            .Where(l => l.ItemConfigId == itemConfigId && l.RemainingQuantity > 0)
            .MinAsync(l => l.UnitPrice);

        var maxBidPrice = await aucyDb.Bids
            .Where(b => b.ItemConfigId == itemConfigId && b.RemainingQuantity > 0)
            .MaxAsync(b => b.BidPrice);

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
            .Where(l => l.ItemConfigId == itemConfigId && l.RemainingQuantity > 0 && l.UnitPrice <= maxBidPrice)
            .OrderBy(l => l.UnitPrice)
            .ThenBy(l => l.CreatedAt)
            .ToListAsync();

        var activeBids = await aucyDb.Bids
            .Where(b => b.ItemConfigId == itemConfigId && b.RemainingQuantity > 0 && b.BidPrice >= minLotPrice)
            .OrderByDescending(b => b.BidPrice)
            .ThenBy(b => b.CreatedAt)
            .ToListAsync();

        // Execute matching
        // TODO: IN FUTURE: leave locked only (minLotPrice, maxBidPrice) orders. Unlock others
        // if lot is created after bid with higher price in one tick, then add difference to Lot.ExtraSoldoutMoney of the seller
        // if bid is created after lot with lower price in one tick, then add difference to Inventory.Money of the bidder 
        await MatchOrdersAsync(aucyDb, activeLots, activeBids, itemConfigId);

        await transaction.CommitAsync();
    }

    private async Task MatchOrdersAsync(AucyDbContext aucyDb, List<Lot> lots, List<Bid> bids, Guid itemConfigId)
    {
        int lotIndex = 0;
        int bidIndex = 0;

        while (lotIndex < lots.Count && bidIndex < bids.Count)
        {
            var currentLot = lots[lotIndex];
            var currentBid = bids[bidIndex];

            // Stop if no crossing prices
            if (currentLot.UnitPrice > currentBid.BidPrice)
                break;

            int matchQuantity = Math.Min(currentLot.RemainingQuantity, currentBid.RemainingQuantity);

            if (currentLot.UnitPrice != currentBid.BidPrice)
            {
                // Determine clearing price based on who created their order first
                long clearingPrice;
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
                    await _inventoryProperties.DepositMoney(currentBid.BidderUserId, extraProfit);
                }
                else
                {
                    // Seller receives clearing price (bid's bid price)
                    // Extra profit = clearingPrice - unitPrice goes to seller
                    long extraProfit = (clearingPrice - currentLot.UnitPrice) * matchQuantity;
                    currentLot.ExtraSoldoutMoney += extraProfit;
                }
            }

            // Update remaining quantities
            currentLot.RemainingQuantity -= matchQuantity;
            currentBid.RemainingQuantity -= matchQuantity;

            // Move to next order if fully filled
            if (currentLot.RemainingQuantity == 0)
                lotIndex++;
            if (currentBid.RemainingQuantity == 0)
                bidIndex++;
        }

        // Save changes to matched orders
        await aucyDb.SaveChangesAsync();
    }
}
