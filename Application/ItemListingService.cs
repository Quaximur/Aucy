using Aucy.Contracts.OnionClient;
using Aucy.Data;
using Grpc.Core;
using MagicOnion;
using MagicOnion.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Aucy.Application;

[Authorize]
public class ItemListingService(AucyDbContext aucyDbContext) : ServiceBase<IItemListingService>,
#pragma warning disable CS8644
    IItemListingService
#pragma warning disable CS8644
{
    private readonly AucyDbContext _aucyDb = aucyDbContext;
    private const long QUANTIZATION_SIZE = 20;

    public async Task<ServerStreamingResult<RawItemListingDto>> GetItemListingStream(Guid itemConfigId)
    {
        var stream = GetServerStreamingContext<RawItemListingDto>();
        var cancellationToken = Context.CallContext.CancellationToken;

        try
        {
            // TODO: raw SQL or Redis with pre-calc in BackgroundWorker

            var minMaxPrice = await _aucyDb.ItemConfigs
                .Where(x => x.Id == itemConfigId)
                .Select(x => new { x.MinPrice, x.MaxPrice })
                .FirstOrDefaultAsync()
                    ?? throw new RpcException(new Status(StatusCode.NotFound, "Item config doesn`t exist"));

            while (!cancellationToken.IsCancellationRequested)
            {
                var currentListing = await FetchVolumeListingAsync(itemConfigId,
                    minPrice: minMaxPrice.MinPrice,
                    maxPrice: minMaxPrice.MaxPrice,
                    cancellationToken);

                await stream.WriteAsync(currentListing);

                await Task.Delay(10_000, cancellationToken); // 10s cooldown
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client side cancellation
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new RpcException(new Status(StatusCode.Internal, $"Listing stream has been interrupted: {ex.Message}"));
        }

        return stream.Result();
    }

    //private async Task<RawItemListingDto> FetchListingAsync(Guid itemConfigId, long minPrice, long maxPrice, 
    //    long levelSize, CancellationToken ct)
    //{
    //    var lots = await _aucyDb.Lots
    //        .Where(l => l.ItemConfigId == itemConfigId
    //                 && l.RemainingQuantity > 0
    //                 && l.IsMatched)
    //        .GroupBy(l => (l.UnitPrice - minPrice) / levelSize)
    //        .Select(g => new RawLotTuple(
    //            minPrice + g.Key * levelSize,
    //            minPrice + g.Key * levelSize + levelSize - 1,
    //            g.Sum(l => l.RemainingQuantity)))
    //        .ToArrayAsync(ct);

    //    var bids = await _aucyDb.Bids
    //        .Where(b => b.ItemConfigId == itemConfigId
    //                 && b.RemainingQuantity > 0
    //                 && b.IsMatched)
    //        .GroupBy(b => (b.BidPrice - minPrice) / levelSize)
    //        .Select(g => new RawBidTuple(
    //            minPrice + g.Key * levelSize,
    //            minPrice + g.Key * levelSize + levelSize - 1,
    //            g.Sum(b => b.RemainingQuantity)))
    //        .ToArrayAsync(ct);

    //    return new RawItemListingDto { RawLots = lots, RawBids = bids };
    //}

    private async Task<RawItemListingDto> FetchVolumeListingAsync(Guid itemConfigId,
    long minPrice, long maxPrice, CancellationToken ct)
    {
        // Prevent Division by Zero if minPrice == maxPrice
        long priceRange = Math.Max(1, maxPrice - minPrice);

        // Scale factor to map prices cleanly into [0 ... QUANTIZATION_SIZE - 1]
        double scale = (double)QUANTIZATION_SIZE / priceRange;

        // 1. Group Lots safely with SQL CASE WHEN translation
        var lotBuckets = await _aucyDb.Lots
            .Where(l => l.ItemConfigId == itemConfigId && l.RemainingQuantity > 0 && l.IsMatched)
            .GroupBy(l => (int)((l.UnitPrice - minPrice) * scale) >= QUANTIZATION_SIZE
                ? (int)QUANTIZATION_SIZE - 1
                : (int)((l.UnitPrice - minPrice) * scale))
            .Select(g => new { BucketIndex = g.Key, Volume = g.Sum(l => l.RemainingQuantity) })
            .OrderBy(x => x.BucketIndex)
            .ToArrayAsync(ct);

        // 2. Group Bids safely with SQL CASE WHEN translation
        var bidBuckets = await _aucyDb.Bids
            .Where(b => b.ItemConfigId == itemConfigId && b.RemainingQuantity > 0 && b.IsMatched)
            .GroupBy(b => (int)((b.BidPrice - minPrice) * scale) >= QUANTIZATION_SIZE
                ? (int)QUANTIZATION_SIZE - 1
                : (int)((b.BidPrice - minPrice) * scale))
            .Select(g => new { BucketIndex = g.Key, Volume = g.Sum(b => b.RemainingQuantity) })
            .OrderBy(x => x.BucketIndex)
            .ToArrayAsync(ct);

        var levels = new RawPriceLevelTuple[QUANTIZATION_SIZE];
        int li = 0;
        int bi = 0;

        // 3. Reconstruct intervals perfectly on the application side
        for (int i = 0; i < QUANTIZATION_SIZE; i++)
        {
            // Reverse engineer the min/max price boundaries for this specific bucket
            long currentMin = minPrice + (long)Math.Ceiling(i / scale);

            // Ensure the last bucket seals perfectly up to maxPrice
            long currentMax;

            if (i == QUANTIZATION_SIZE - 1)
                currentMax = maxPrice;
            else
                currentMax = minPrice + (long)Math.Ceiling((i + 1) / scale) - 1;

            var levelTuple = new RawPriceLevelTuple(
                minPrice: currentMin,
                maxPrice: currentMax,
                lotQuantity: 0,
                bidQuantity: 0);

            if (li < lotBuckets.Length && lotBuckets[li].BucketIndex == i)
            {
                levelTuple.LotQuantity = lotBuckets[li].Volume;
                li++;
            }

            if (bi < bidBuckets.Length && bidBuckets[bi].BucketIndex == i)
            {
                levelTuple.BidQuantity = bidBuckets[bi].Volume;
                bi++;
            }

            levels[i] = levelTuple;
        }

        return new RawItemListingDto { Levels = levels };
    }
}