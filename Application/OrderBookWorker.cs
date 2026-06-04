using Aucy.Application.Helpers;
using Aucy.Data;
using AutoMapper;
using Medallion.Threading;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

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

    public async Task ProcessMatchingTickAsync(Guid itemConfigId)
    {
        using var aucyDb = await _aucyDbFactory.CreateDbContextAsync();

        // Orders deletion/creation/update lock
        // + await existing deletion/creation/update operations lock form user requests
        using var matchingLock = await _rwLockProvider.AcquireReadLockAsync(Keys.GetItemMatchingLock(itemConfigId));

        using var transaction = await aucyDb.Database.BeginTransactionAsync();

        var minLotPrice = await aucyDb.Lots
            .Where(l => l.ItemConfigId == itemConfigId && l.RemainingQuantity > 0)
            .MinAsync(l => l.UnitPrice);

        var maxBidPrice = await aucyDb.Bids
            .Where(b => b.ItemConfigId == itemConfigId && b.RemainingQuantity > 0)
            .MaxAsync(b => b.BidPrice);

        await aucyDb.Lots
            .Where(x => x.ItemConfigId == itemConfigId && !x.IsMatched)
            .ExecuteUpdateAsync(x => x.SetProperty(x => x.IsMatched, true));

        await aucyDb.Bids
           .Where(x => x.ItemConfigId == itemConfigId && !x.IsMatched)
           .ExecuteUpdateAsync(x => x.SetProperty(x => x.IsMatched, true));

        // Use ReadCommitted for reading Bids and Lots to avoid dirty reads (IsMatched)
        // Btw, it is default for PostgreSQL and MS SQL

        if (minLotPrice > maxBidPrice)
        {
            await transaction.CommitAsync();
            return;
        }

        // leave locked only (minLotPrice, maxBidPrice) orders. Unlock others

        // TODO: OrderBook matching for (minLotPrice, maxBidPrice) orders logic here
    }
}
