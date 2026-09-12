using Aucy.Application;
using Aucy.Application.Helpers;
using Aucy.Data;
using Aucy.Domain.Entities;
using Medallion.Threading;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data;

namespace Aucy.Workers.OrderBook;

public sealed partial class OrderBookWorker(
    IDbContextFactory<AucyDbContext> aucyDbFactory,
    IServiceScopeFactory scopeFactory,
    IDistributedReaderWriterLockProvider rwLockProvider,
    ILogger<OrderBookWorker> logger,
    WorkerGroupNotifier groupNotifier
    )
{
    private sealed record LotRow(
    Guid Id,
    Guid OwnerUserId,
    int RemainingQuantity,
    long UnitPrice,
    DateTime CreatedAt);

    private sealed record BidRow(
        Guid Id,
        Guid BidderUserId,
        int RemainingQuantity,
        long BidPrice,
        DateTime CreatedAt);

    private sealed record PriceBounds(long? MinLotPrice, long? MaxBidPrice);

    private sealed class LotDelta
    {
        public int QtySold;
        public long ExtraSoldoutMoney;
    }

    private sealed class BidDelta
    {
        public int QtyBought;
        public long ExtraMoney;
    }

    private struct WorkerPostEvent()
    {
        public bool IsLot;
        public Guid UserId;
        public Guid OrderId;
        public int Quantity;
        public long ExtraMoney;
    }

    private readonly IDbContextFactory<AucyDbContext> _aucyDbFactory = aucyDbFactory;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IDistributedReaderWriterLockProvider _rwLockProvider = rwLockProvider;
    private readonly ILogger<OrderBookWorker> _logger = logger;
    private readonly WorkerGroupNotifier _groupNotifier = groupNotifier;

    private const int INTERVAL_MS = 1000;

    public async void RunAsync(Guid itemConfigId)
    {
        while (true)
        {
            await ProcessMatchingTickAsync(itemConfigId); // TODO: V2
            await Task.Delay(INTERVAL_MS);
        }
    }

    private async Task ProcessMatchingTickAsyncV2(Guid itemConfigId)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var inventoryProperties = scope.ServiceProvider.GetRequiredService<InventoryPropertiesHelper>();

        using var matchingLock = await _rwLockProvider.AcquireWriteLockAsync(
            Keys.GetItemMatchingLock(itemConfigId));

        await using var aucyDb = scope.ServiceProvider.GetRequiredService<AucyDbContext>();

        await using var transaction = await aucyDb.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        List<WorkerPostEvent> workerEvents = [];

        try
        {
            // 1. Границы стакана (одним запросом)
            var bounds = await aucyDb.Database
                .SqlQuery<PriceBounds>(@$"
                SELECT
                    (SELECT MIN(""UnitPrice"") FROM ""Lots""
                     WHERE ""ItemConfigId"" = {itemConfigId} AND ""RemainingQuantity"" > 0) AS MinLotPrice,
                    (SELECT MAX(""BidPrice"") FROM ""Bids""
                     WHERE ""ItemConfigId"" = {itemConfigId} AND ""RemainingQuantity"" > 0) AS MaxBidPrice
            ")
                .FirstAsync();

            var minLotPrice = bounds.MinLotPrice ?? long.MinValue;
            var maxBidPrice = bounds.MaxBidPrice ?? long.MaxValue;

            if (minLotPrice > maxBidPrice)
            {
                await transaction.CommitAsync();
                return;
            }

            // 2. Суммарный спрос/предложение по пересекающимся заявкам
            var totalBidDemand = await aucyDb.Bids
                .Where(b => b.ItemConfigId == itemConfigId
                         && b.RemainingQuantity > 0
                         && b.BidPrice >= minLotPrice)
                .SumAsync(b => (long?)b.RemainingQuantity) ?? 0L;

            var totalLotSupply = await aucyDb.Lots
                .Where(l => l.ItemConfigId == itemConfigId
                         && l.RemainingQuantity > 0
                         && l.UnitPrice <= maxBidPrice)
                .SumAsync(l => (long?)l.RemainingQuantity) ?? 0L;

            if (totalBidDemand == 0 || totalLotSupply == 0)
            {
                await transaction.CommitAsync();
                return;
            }

            // 3. Загружаем ТОЛЬКО клин: лоты до покрытия totalBidDemand, биды до покрытия totalLotSupply
            var lots = await aucyDb.Database
                .SqlQuery<LotRow>($@"
                SELECT ""Id"", ""OwnerUserId"", ""RemainingQuantity"", ""UnitPrice"", ""CreatedAt""
                FROM (
                    SELECT *,
                           SUM(""RemainingQuantity"") OVER (
                               ORDER BY ""UnitPrice"", ""CreatedAt"", ""Id""
                           ) AS ""RunningQty""
                    FROM ""Lots""
                    WHERE ""ItemConfigId"" = {itemConfigId}
                      AND ""RemainingQuantity"" > 0
                      AND ""UnitPrice"" <= {maxBidPrice}
                ) t
                WHERE ""RunningQty"" - ""RemainingQuantity"" < {totalBidDemand}
                ORDER BY ""UnitPrice"", ""CreatedAt"", ""Id""")
                .ToListAsync();

            var bids = await aucyDb.Database
                .SqlQuery<BidRow>($@"
                SELECT ""Id"", ""BidderUserId"", ""RemainingQuantity"", ""BidPrice"", ""CreatedAt""
                FROM (
                    SELECT *,
                           SUM(""RemainingQuantity"") OVER (
                               ORDER BY ""BidPrice"" DESC, ""CreatedAt"", ""Id""
                           ) AS ""RunningQty""
                    FROM ""Bids""
                    WHERE ""ItemConfigId"" = {itemConfigId}
                      AND ""RemainingQuantity"" > 0
                      AND ""BidPrice"" >= {minLotPrice}
                ) t
                WHERE ""RunningQty"" - ""RemainingQuantity"" < {totalLotSupply}
                ORDER BY ""BidPrice"" DESC, ""CreatedAt"", ""Id""")
                .ToListAsync();

            // 4. Матчинг + накопление дельт для bulk update
            var lotDeltas = new Dictionary<Guid, LotDelta>();
            var bidDeltas = new Dictionary<Guid, BidDelta>();

            MatchOrdersInMemory(lots, bids, itemConfigId, workerEvents, lotDeltas, bidDeltas);

            // 5. Bulk-запись через SQL (без трекинга)
            await ApplyDeltasAsync(aucyDb, inventoryProperties, itemConfigId, lotDeltas, bidDeltas);

            // 6. Помечаем всё немэтченное как IsMatched (как было)
            await aucyDb.Lots
                .Where(x => x.ItemConfigId == itemConfigId && !x.IsMatched)
                .ExecuteUpdateAsync(x => x.SetProperty(x => x.IsMatched, true));

            await aucyDb.Bids
                .Where(x => x.ItemConfigId == itemConfigId && !x.IsMatched)
                .ExecuteUpdateAsync(x => x.SetProperty(x => x.IsMatched, true));

            await transaction.CommitAsync();

            // 7. Нотификации после коммита
            try
            {
                foreach (var workerEvent in workerEvents)
                {
                    if (workerEvent.IsLot)
                    {
                        _groupNotifier.NotifySold(
                            userId: workerEvent.UserId,
                            lotId: workerEvent.OrderId,
                            soldQuantity: workerEvent.Quantity,
                            extraMoney: workerEvent.ExtraMoney);
                    }
                    else
                    {
                        _groupNotifier.NotifyBought(
                            userId: workerEvent.UserId,
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
            throw;
        }
    }

    private void MatchOrdersInMemory(
    List<LotRow> lots,
    List<BidRow> bids,
    Guid itemConfigId,
    List<WorkerPostEvent> workerEvents,
    Dictionary<Guid, LotDelta> lotDeltas,
    Dictionary<Guid, BidDelta> bidDeltas)
    {
        int lotIndex = 0;
        int bidIndex = 0;

        var lotEvent = new WorkerPostEvent { IsLot = true };
        var bidEvent = new WorkerPostEvent { IsLot = false };

        while (lotIndex < lots.Count && bidIndex < bids.Count)
        {
            var currentLot = lots[lotIndex];
            var currentBid = bids[bidIndex];

            if (currentLot.UnitPrice > currentBid.BidPrice)
                break;

            lotEvent.UserId = currentLot.OwnerUserId;
            lotEvent.OrderId = currentLot.Id;

            bidEvent.UserId = currentBid.BidderUserId;
            bidEvent.OrderId = currentBid.Id;

            int matchQuantity = Math.Min(currentLot.RemainingQuantity, currentBid.RemainingQuantity);

            long clearingPrice;
            if (currentLot.UnitPrice != currentBid.BidPrice)
            {
                bool lotCreatedFirst = currentLot.CreatedAt <= currentBid.CreatedAt;
                if (lotCreatedFirst)
                    clearingPrice = currentLot.UnitPrice;
                else
                    clearingPrice = currentBid.BidPrice;

                if (lotCreatedFirst)
                {
                    long extraProfit = (currentBid.BidPrice - clearingPrice) * matchQuantity;
                    // Депонируем биддеру — суммируем в bidDelta
                    var bd = GetBidDelta(bidDeltas, currentBid.Id);
                    bd.ExtraMoney += extraProfit;
                    bd.QtyBought += 0; // уже накопится ниже
                    bidEvent.ExtraMoney += extraProfit;
                }
                else
                {
                    long extraProfit = (clearingPrice - currentLot.UnitPrice) * matchQuantity;
                    var ld = GetLotDelta(lotDeltas, currentLot.Id);
                    ld.ExtraSoldoutMoney += extraProfit;
                    lotEvent.ExtraMoney += extraProfit;
                }
            }
            else
            {
                clearingPrice = currentLot.UnitPrice;
            }

            // Обновляем локальные копии
            lots[lotIndex] = currentLot with { RemainingQuantity = currentLot.RemainingQuantity - matchQuantity };
            bids[bidIndex] = currentBid with { RemainingQuantity = currentBid.RemainingQuantity - matchQuantity };

            GetLotDelta(lotDeltas, currentLot.Id).QtySold += matchQuantity;
            GetBidDelta(bidDeltas, currentBid.Id).QtyBought += matchQuantity;

            lotEvent.Quantity += matchQuantity;
            bidEvent.Quantity += matchQuantity;

            LogSaleInformation(
                logger: _logger,
                matchQuantity: matchQuantity,
                itemConfigId: itemConfigId,
                ownerUserId: currentLot.OwnerUserId,
                bidderUserId: currentBid.BidderUserId,
                clearingPrice: clearingPrice);

            if (lots[lotIndex].RemainingQuantity == 0)
            {
                workerEvents.Add(lotEvent);
                lotIndex++;
                lotEvent = new WorkerPostEvent { IsLot = true };
            }

            if (bids[bidIndex].RemainingQuantity == 0)
            {
                workerEvents.Add(bidEvent);
                bidIndex++;
                bidEvent = new WorkerPostEvent { IsLot = false };
            }
        }

        if (lotIndex >= lots.Count && bidIndex < bids.Count)
            workerEvents.Add(bidEvent);
        else if (lotIndex < lots.Count && bidIndex >= bids.Count)
            workerEvents.Add(lotEvent);
    }

    private static LotDelta GetLotDelta(Dictionary<Guid, LotDelta> d, Guid id)
    {
        if (!d.TryGetValue(id, out var v))
        {
            v = new LotDelta();
            d[id] = v;
        }
        return v;
    }

    private static BidDelta GetBidDelta(Dictionary<Guid, BidDelta> d, Guid id)
    {
        if (!d.TryGetValue(id, out var v))
        {
            v = new BidDelta();
            d[id] = v;
        }
        return v;
    }

    private async Task ApplyDeltasAsync(
        AucyDbContext aucyDb,
        InventoryPropertiesHelper inventoryProperties,
        Guid itemConfigId,
        Dictionary<Guid, LotDelta> lotDeltas,
        Dictionary<Guid, BidDelta> bidDeltas)
    {
        if (lotDeltas.Count == 0 && bidDeltas.Count == 0)
            return;

        // ---- Лоты: RemainingQuantity -= QtySold, ExtraSoldoutMoney += Extra ----
        // Делаем пачками, чтобы не упереться в лимит параметров
        foreach (var chunk in lotDeltas.Chunk(200))
        {
            var ids = chunk.Select(kv => kv.Key).ToArray();
            var caseQty = string.Join(" ", chunk.Select(kv =>
                $"WHEN \"Id\" = '{kv.Key}' THEN {kv.Value.QtySold}"));
            var caseExtra = string.Join(" ", chunk.Select(kv =>
                $"WHEN \"Id\" = '{kv.Key}' THEN {kv.Value.ExtraSoldoutMoney}"));

            await aucyDb.Database.ExecuteSqlRawAsync($@"
            UPDATE ""Lots""
            SET ""RemainingQuantity"" = ""RemainingQuantity"" -
                    (CASE {caseQty} ELSE 0 END),
                ""ExtraSoldoutMoney"" = ""ExtraSoldoutMoney"" +
                    (CASE {caseExtra} ELSE 0 END)
            WHERE ""Id"" = ANY(@ids)",
                new NpgsqlParameter("ids", ids));
        }

        // ---- Биды: RemainingQuantity -= QtyBought ----
        foreach (var chunk in bidDeltas.Chunk(200))
        {
            var ids = chunk.Select(kv => kv.Key).ToArray();
            var caseQty = string.Join(" ", chunk.Select(kv =>
                $"WHEN \"Id\" = '{kv.Key}' THEN {kv.Value.QtyBought}"));

            await aucyDb.Database.ExecuteSqlRawAsync($@"
            UPDATE ""Bids""
            SET ""RemainingQuantity"" = ""RemainingQuantity"" -
                    (CASE {caseQty} ELSE 0 END)
            WHERE ""Id"" = ANY(@ids)",
                new NpgsqlParameter("ids", ids));
        }

        // ---- Возвраты биддерам (extraProfit) ----
        // Если BidDelta хранит ExtraMoney — соберём по userId и сделаем один депозит на юзера.
        var refundsByBidId = bidDeltas
            .Where(kv => kv.Value.ExtraMoney > 0)
            .ToDictionary(kv => kv.Key, kv => kv.Value.ExtraMoney);

        if (refundsByBidId.Count > 0)
        {
            var bidIds = refundsByBidId.Keys.ToArray();

            var owners = await aucyDb.Bids
                .AsNoTracking()
                .Where(b => bidIds.Contains(b.Id))
                .Select(b => new { b.Id, b.BidderUserId })
                .ToListAsync();

            var refundsByUser = owners
                .GroupBy(o => o.BidderUserId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(o => refundsByBidId[o.Id]));

            foreach (var (userId, amount) in refundsByUser)
            {
                if (amount == 0) continue;
                await inventoryProperties.DepositMoney(aucyDb, userId, amount);
            }
        }
    }

    // TODO: use checked() for money
    private async Task ProcessMatchingTickAsync(Guid itemConfigId)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var inventoryProperties = scope.ServiceProvider.GetRequiredService<InventoryPropertiesHelper>();

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
                        _groupNotifier.NotifySold(userId: workerEvent.UserId,
                            lotId: workerEvent.OrderId,
                            soldQuantity: workerEvent.Quantity,
                            extraMoney: workerEvent.ExtraMoney);
                    }
                    else
                    {
                        _groupNotifier.NotifyBought(userId: workerEvent.UserId,
                            bidId: workerEvent.OrderId,
                            boughtQuantity: workerEvent.Quantity,
                            extraMoney: workerEvent.ExtraMoney);
                    }
                }

                // THINK: wait for all events to complete before releasing rw lock?
                // Mb it is not necessary, because the State on the server-side will be OK anyway.
                // The problem can apear only if another request with the same Lot/Bid will be processed faster
                // than notification gets to the client.
                // (user create lot x15 -> Matching Item x15 -> worker sends notification x15 sold -> user remove lot x15 ->
                // user gets error (nothing to remove) -> user gets notification x15 sold)
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
        InventoryPropertiesHelper inventoryProperties,
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
                    await inventoryProperties.DepositMoney(aucyDb, currentBid.BidderUserId, extraProfit); // TODO: gather extraMoney for sequence (no multiple deposits)
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
