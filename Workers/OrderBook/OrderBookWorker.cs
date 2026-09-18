using Aucy.Application;
using Aucy.Data;
using Medallion.Threading;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data;

namespace Aucy.Workers.OrderBook;

public sealed partial class OrderBookWorker(
    IDbContextFactory<AucyDbContext> aucyDbFactory,
    IDistributedReaderWriterLockProvider rwLockProvider,
    ILogger<OrderBookWorker> logger,
    WorkerGroupNotifier groupNotifier
    )
{
    private class LotRow(Guid id, Guid ownerUserId, int remainingQuantity, long unitPrice, DateTime createdAt, long extraSoldoutMoney)
    {
        public Guid Id { get; set; } = id;
        public Guid OwnerUserId { get; set; } = ownerUserId;
        public int RemainingQuantity { get; set; } = remainingQuantity;
        public long UnitPrice { get; set; } = unitPrice;
        public DateTime CreatedAt { get; set; } = createdAt;
        public long ExtraSoldoutMoney { get; set; } = extraSoldoutMoney;
    }

    private class BidRow(Guid id, Guid ownerUserId, int remainingQuantity, long unitPrice, DateTime createdAt, long extraBuyoutMoney)
    {
        public Guid Id { get; set; } = id;
        public Guid OwnerUserId { get; set; } = ownerUserId;
        public int RemainingQuantity { get; set; } = remainingQuantity;
        public long UnitPrice { get; set; } = unitPrice;
        public DateTime CreatedAt { get; set; } = createdAt;
        public long ExtraBuyoutMoney { get; set; } = extraBuyoutMoney;
    }

    // Combined shape for the single wedge query: 'L' rows map to LotRow, 'B' rows map to BidRow.
    private sealed record MatchRow(
        string Kind,
        Guid Id,
        Guid UserId,
        int RemainingQuantity,
        long Price,
        DateTime CreatedAt);

    private sealed record PriceBounds(long? MinLotPrice, long? MaxBidPrice);

    private struct WorkerPostEvent()
    {
        public bool IsLot;
        public Guid UserId;
        public Guid OrderId;
        public int Quantity;
        public long ExtraMoney;
    }

    private readonly IDbContextFactory<AucyDbContext> _aucyDbFactory = aucyDbFactory;
    private readonly IDistributedReaderWriterLockProvider _rwLockProvider = rwLockProvider;
    private readonly ILogger<OrderBookWorker> _logger = logger;
    private readonly WorkerGroupNotifier _groupNotifier = groupNotifier;

    private const int INTERVAL_MS = 1000;
    private const int BATCH_UPDATE_SIZE = 1000;

    public async void RunAsync(Guid itemConfigId)
    {
        while (true)
        {
            await ProcessMatchingTickAsyncV3(itemConfigId);
            await Task.Delay(INTERVAL_MS);
        }
    }

    public async Task ProcessMatchingTickAsyncV3(Guid itemConfigId)
    {
        // Orders deletion/creation lock
        // + await existing deletion/creation operations lock from user requests
        // bid/lot processed values collecting is unlocked
        using var matchingLock = await _rwLockProvider.AcquireWriteLockAsync(LockKeys.GetItemMatchingLock(itemConfigId));

        using var aucyDb = await _aucyDbFactory.CreateDbContextAsync();

        using var transaction = await aucyDb.Database.BeginTransactionAsync();

        List<WorkerPostEvent> workerEvents = [];

        try
        {
            await aucyDb.Lots
                .Where(x => x.ItemConfigId == itemConfigId && !x.IsMatched)
                .ExecuteUpdateAsync(x => x.SetProperty(x => x.IsMatched, true));

            await aucyDb.Bids
                .Where(x => x.ItemConfigId == itemConfigId && !x.IsMatched)
                .ExecuteUpdateAsync(x => x.SetProperty(x => x.IsMatched, true));

            var bounds = await aucyDb.Database
                    .SqlQuery<PriceBounds>(@$"
                SELECT
                    (SELECT MIN(""UnitPrice"") FROM ""Lots""
                     WHERE ""ItemConfigId"" = {itemConfigId} AND ""RemainingQuantity"" > 0) AS ""MinLotPrice"",
                    (SELECT MAX(""BidPrice"") FROM ""Bids""
                     WHERE ""ItemConfigId"" = {itemConfigId} AND ""RemainingQuantity"" > 0) AS ""MaxBidPrice""
            ")
                    .FirstOrDefaultAsync();

            if (bounds == null)
            {
                await transaction.CommitAsync();
                return;
            }

            long minLotPrice;
            if (bounds.MinLotPrice.HasValue)
            {
                minLotPrice = bounds.MinLotPrice.Value;
            }
            else
            {
                await transaction.CommitAsync();
                return;
            }

            long maxBidPrice;
            if (bounds.MaxBidPrice.HasValue)
            {
                maxBidPrice = bounds.MaxBidPrice.Value;
            }
            else
            {
                await transaction.CommitAsync();
                return;
            }

            if (minLotPrice > maxBidPrice)
            {
                await transaction.CommitAsync();
                return;
            }

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

            //    Загружаем ТОЛЬКО клин: лоты до покрытия totalBidDemand, биды до покрытия totalLotSupply.
            //    Один комбинированный запрос вместо двух отдельных: дискриминатор "Kind" ('L'/'B')
            //    позволяет разложить результат по двум спискам на стороне C#, сохраняя нужный
            //    порядок внутри каждой группы (лоты — по возрастанию цены, биды — по убыванию).
            //    ROWS BETWEEN ... явно фиксирует поведение running-sum окна независимо от совпадений
            //    по (Price, CreatedAt) — уникальность "Id" в ORDER BY гарантирует отсутствие "peers",
            //    но явный ROWS делает это независимым от дефолтного RANGE-поведения Postgres.
            var wedgeRows = await aucyDb.Database
                .SqlQuery<MatchRow>($@"
                WITH ranked_lots AS (
                    SELECT
                        ""Id"", ""OwnerUserId"", ""RemainingQuantity"", ""UnitPrice"", ""CreatedAt"",
                        SUM(""RemainingQuantity"") OVER (
                            ORDER BY ""UnitPrice"", ""CreatedAt"", ""Id""
                            ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
                        ) AS ""RunningQty""
                    FROM ""Lots""
                    WHERE ""ItemConfigId"" = {itemConfigId}
                      AND ""RemainingQuantity"" > 0
                      AND ""UnitPrice"" <= {maxBidPrice}
                ),
                ranked_bids AS (
                    SELECT
                        ""Id"", ""BidderUserId"", ""RemainingQuantity"", ""BidPrice"", ""CreatedAt"",
                        SUM(""RemainingQuantity"") OVER (
                            ORDER BY ""BidPrice"" DESC, ""CreatedAt"", ""Id""
                            ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
                        ) AS ""RunningQty""
                    FROM ""Bids""
                    WHERE ""ItemConfigId"" = {itemConfigId}
                      AND ""RemainingQuantity"" > 0
                      AND ""BidPrice"" >= {minLotPrice}
                )
                SELECT
                    'L'             AS ""Kind"",
                    ""Id"",
                    ""OwnerUserId""  AS ""UserId"",
                    ""RemainingQuantity"",
                    ""UnitPrice""    AS ""Price"",
                    ""CreatedAt"",
                    ""UnitPrice""    AS ""SortKey""
                FROM ranked_lots
                WHERE ""RunningQty"" - ""RemainingQuantity"" < {totalBidDemand}

                UNION ALL

                SELECT
                    'B'              AS ""Kind"",
                    ""Id"",
                    ""BidderUserId""  AS ""UserId"",
                    ""RemainingQuantity"",
                    ""BidPrice""      AS ""Price"",
                    ""CreatedAt"",
                    -""BidPrice""     AS ""SortKey""
                FROM ranked_bids
                WHERE ""RunningQty"" - ""RemainingQuantity"" < {totalLotSupply}

                ORDER BY ""Kind"", ""SortKey"", ""CreatedAt"", ""Id""")
                .ToListAsync();

            var activeLots = new List<LotRow>();
            var activeBids = new List<BidRow>();

            foreach (var row in wedgeRows)
            {
                if (row.Kind == "L")
                    activeLots.Add(new LotRow(row.Id, row.UserId, row.RemainingQuantity, row.Price, row.CreatedAt, 0));
                else
                    activeBids.Add(new BidRow(row.Id, row.UserId, row.RemainingQuantity, row.Price, row.CreatedAt, 0));
            }

            // Execute matching
            // TODO: IN FUTURE: leave locked only (minLotPrice, maxBidPrice) orders. Unlock others
            // if lot is created after bid with higher price in one tick, then add difference to Lot.ExtraSoldoutMoney of the seller
            // if bid is created after lot with lower price in one tick, then add difference to Inventory.Money of the bidder 
            await MatchOrdersAsyncV3(aucyDb, activeLots, activeBids, itemConfigId, workerEvents);

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

    private async Task MatchOrdersAsyncV3(AucyDbContext aucyDb,
        List<LotRow> lots,
        List<BidRow> bids,
        Guid itemConfigId,
        List<WorkerPostEvent> workerEvents)
    {
        int lotIndex = 0;
        int bidIndex = 0;
        int totalBidEvents = 0;
        int totalLotEvents = 0;

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

            bidEvent.UserId = currentBid.OwnerUserId;
            bidEvent.OrderId = currentBid.Id;


            // Stop if no crossing prices
            if (currentLot.UnitPrice > currentBid.UnitPrice)
                break;

            int matchQuantity = Math.Min(currentLot.RemainingQuantity, currentBid.RemainingQuantity);

            long clearingPrice;
            if (currentLot.UnitPrice != currentBid.UnitPrice)
            {
                // Determine clearing price based on who created their order first

                bool lotCreatedFirst = currentLot.CreatedAt <= currentBid.CreatedAt;

                if (lotCreatedFirst)
                    clearingPrice = currentLot.UnitPrice; // Clearing at lot's lower price, bidder keeps the surplus
                else
                    clearingPrice = currentBid.UnitPrice; // Clearing at bid's higher price, seller gets extra profit

                // Handle surplus/extra profit
                if (lotCreatedFirst)
                {
                    // Bidder pays clearing price (lot's unit price)
                    // Surplus = bidPrice - clearingPrice stays with bidder

                    long extraProfit = (currentBid.UnitPrice - clearingPrice) * matchQuantity;
                    currentBid.ExtraBuyoutMoney += extraProfit;
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

            //LogSaleInformation(logger: _logger,
            //    matchQuantity: matchQuantity,
            //    itemConfigId: itemConfigId,
            //    ownerUserId: currentLot.OwnerUserId,
            //    bidderUserId: currentBid.OwnerUserId,
            //    clearingPrice: clearingPrice);

            // Move to next order if fully filled
            if (currentLot.RemainingQuantity == 0)
            {
                workerEvents.Add(lotEvent);
                totalLotEvents++;
                lotIndex++;
                lotEvent = new WorkerPostEvent() { IsLot = true };
            }

            if (currentBid.RemainingQuantity == 0)
            {
                workerEvents.Add(bidEvent);
                totalBidEvents++;
                bidIndex++;
                bidEvent = new WorkerPostEvent() { IsLot = false };
            }
        }

        if (bidEvent.Quantity > 0)
        {
            workerEvents.Add(bidEvent);
            totalBidEvents++;
        }

        if (lotEvent.Quantity > 0)
        {
            workerEvents.Add(lotEvent);
            totalLotEvents++;
        }

        // Apply changes
        foreach (var chunk in lots.Take(totalLotEvents).Chunk(BATCH_UPDATE_SIZE))
        {
            var ids = chunk.Select(e => e.Id).ToArray();

            // VALUES ($1, $2), ($3, $4), ...
            var valuesList = new List<string>();
            var parameters = new List<NpgsqlParameter>();

            for (var i = 0; i < chunk.Length; i++)
            {
                var e = chunk[i];
                var currentParamIndex1 = parameters.Count;
                var currentParamIndex2 = parameters.Count + 1;
                var currentParamIndex3 = parameters.Count + 2;

                valuesList.Add($"(@p{currentParamIndex1}, @p{currentParamIndex2}, @p{currentParamIndex3})");
                parameters.Add(new NpgsqlParameter($"p{currentParamIndex1}", e.Id));
                parameters.Add(new NpgsqlParameter($"p{currentParamIndex2}", e.RemainingQuantity));
                parameters.Add(new NpgsqlParameter($"p{currentParamIndex3}", e.ExtraSoldoutMoney));
            }

            await aucyDb.Database.ExecuteSqlRawAsync($@"
                UPDATE ""Lots"" AS l
                SET ""RemainingQuantity"" = v.qty,
                ""ExtraSoldoutMoney"" = v.ext
                FROM (VALUES {string.Join(", ", valuesList)}) AS v(id, qty, ext)
                WHERE l.""Id"" = v.id",
                parameters);
        }

        foreach (var chunk in bids.Take(totalBidEvents).Chunk(BATCH_UPDATE_SIZE))
        {
            var qtyValuesList = new List<string>();
            var qtyParameters = new List<NpgsqlParameter>();
            var moneyValuesList = new List<string>();
            var moneyParameters = new List<NpgsqlParameter>();

            for (var i = 0; i < chunk.Length; i++)
            {
                var e = chunk[i];
                var idx = qtyParameters.Count;

                qtyValuesList.Add($"(@p{idx}, @p{idx + 1})");
                qtyParameters.Add(new NpgsqlParameter($"p{idx}", e.Id));
                qtyParameters.Add(new NpgsqlParameter($"p{idx + 1}", e.RemainingQuantity));

                if (e.ExtraBuyoutMoney <= 0) 
                    continue;

                var midx = moneyParameters.Count;
                var midx2 = moneyParameters.Count + 1;

                moneyValuesList.Add($"(@p{midx}, @p{midx2})");
                moneyParameters.Add(new NpgsqlParameter($"p{midx}", e.OwnerUserId));
                moneyParameters.Add(new NpgsqlParameter($"p{midx2}", e.ExtraBuyoutMoney));
            }

            await aucyDb.Database.ExecuteSqlRawAsync($@"
                UPDATE ""Bids"" AS b
                SET ""RemainingQuantity"" = v.qty
                FROM (VALUES {string.Join(", ", qtyValuesList)}) AS v(id, qty)
                WHERE b.""Id"" = v.id",
                qtyParameters);

            if (moneyValuesList.Count > 0)
            {
                await aucyDb.Database.ExecuteSqlRawAsync($@"
                    UPDATE ""Inventories"" AS i
                    SET ""Money"" = i.""Money"" + v.extra_money
                    FROM (VALUES {string.Join(", ", moneyValuesList)}) AS v(owner_id, extra_money)
                    WHERE i.""Id"" = v.owner_id",
                    moneyParameters);
            }
        }

        _logger.LogInformation("Total matched {MatchedLotsCount} lots with {MatchedBidsCount} bids", totalLotEvents, totalBidEvents);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Matched quantity '{MatchQuantity}' of ItemConfig '{ItemConfigId}' from Owner '{OwnerUserId}' to Bidder '{BidderUserId}' for price {ClearingPrice}")]
    static partial void LogSaleInformation(
        ILogger logger,
        int matchQuantity,
        Guid itemConfigId,
        Guid ownerUserId,
        Guid bidderUserId,
        long clearingPrice
    );
}