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
    private const int NOTIFY_COOLDOWN_MS = 10_000;

    public async Task<ServerStreamingResult<RawItemListingDto>> GetItemListingStream(Guid itemConfigId)
    {
        var stream = GetServerStreamingContext<RawItemListingDto>();
        var cancellationToken = Context.CallContext.CancellationToken;

        try
        {
            // TODO: raw SQL or Redis with pre-calc in BackgroundWorker

            var cfg = await _aucyDb.ItemConfigs
                .Where(x => x.Id == itemConfigId)
                .Select(x => new { x.MinPrice, x.MaxPrice, x.PriceTierCount })
                .FirstOrDefaultAsync()
                    ?? throw new RpcException(new Status(StatusCode.NotFound, "Item config doesn`t exist"));

            long priceRange = cfg.MaxPrice - cfg.MinPrice;
            int quantizationSize = cfg.PriceTierCount;
            int cappedDesired = cfg.PriceTierCount;
            double ratio = (double)quantizationSize / priceRange; // Scale factor to map prices cleanly into [0 ... QUANTIZATION_SIZE - 1]

            while (!cancellationToken.IsCancellationRequested)
            {
                var currentListing = await FetchVolumeListingAsync(itemConfigId,
                    minPrice: cfg.MinPrice,
                    maxPrice: cfg.MaxPrice,
                    ratio: ratio,
                    quantizationSize: quantizationSize,
                    cancellationToken);

                await stream.WriteAsync(currentListing);

                await Task.Delay(NOTIFY_COOLDOWN_MS, cancellationToken);
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

    private async Task<RawItemListingDto> FetchVolumeListingAsync(Guid itemConfigId,
        long minPrice, long maxPrice, double ratio, int quantizationSize, CancellationToken ct)
    {
        // 1. Group Lots safely with SQL CASE WHEN translation
        var lotBuckets = await _aucyDb.Lots
            .Where(l => l.ItemConfigId == itemConfigId && l.RemainingQuantity > 0 && l.IsMatched)
            .GroupBy(l => (int)((l.UnitPrice - minPrice) * ratio) >= quantizationSize
                ? quantizationSize - 1
                : (int)((l.UnitPrice - minPrice) * ratio))
            .Select(g => new { BucketIndex = g.Key, Volume = g.Sum(l => (long)l.RemainingQuantity) })
            .OrderBy(x => x.BucketIndex)
            .ToArrayAsync(ct);

        // 2. Group Bids safely with SQL CASE WHEN translation
        var bidBuckets = await _aucyDb.Bids
            .Where(b => b.ItemConfigId == itemConfigId && b.RemainingQuantity > 0 && b.IsMatched)
            .GroupBy(b => (int)((b.BidPrice - minPrice) * ratio) >= quantizationSize
                ? quantizationSize - 1
                : (int)((b.BidPrice - minPrice) * ratio))
            .Select(g => new { BucketIndex = g.Key, Volume = g.Sum(b => (long)b.RemainingQuantity) })
            .OrderBy(x => x.BucketIndex)
            .ToArrayAsync(ct);

        var levels = new RawPriceLevelTuple[quantizationSize];
        int li = 0;
        int bi = 0;

        // 3. Reconstruct intervals perfectly on the application side
        for (var i = 0; i < quantizationSize; i++)
        {
            // Reverse engineer the min/max price boundaries for this specific bucket
            long currentMin = minPrice + (long)Math.Ceiling(i / ratio);

            // Ensure the last bucket seals perfectly up to maxPrice
            long currentMax;

            if (i == quantizationSize - 1)
                currentMax = maxPrice;
            else
                currentMax = minPrice + (long)Math.Ceiling((i + 1) / ratio) - 1;

            // Use the currentMin as the representative price for the tier
            var levelTuple = new RawPriceLevelTuple(
                price: currentMin,
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