using Aucy.Contracts.OnionClient;
using Aucy.Data;
using Grpc.Core;
using MagicOnion;
using MagicOnion.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Aucy.Application.Helpers;

namespace Aucy.Application;

[Authorize]
public class ItemListingService(AucyDbContext aucyDbContext, IPriceTierQuantizer quantizer) : ServiceBase<IItemListingService>,
#pragma warning disable CS8644
    IItemListingService
#pragma warning disable CS8644
{
    private readonly AucyDbContext _aucyDb = aucyDbContext;
    private const int NOTIFY_COOLDOWN_MS = 10_000;
    private readonly IPriceTierQuantizer _quantizer = quantizer;

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

            while (!cancellationToken.IsCancellationRequested)
            {
                var currentListing = await FetchVolumeListingAsync(itemConfigId, _quantizer, cfg.MinPrice, cfg.MaxPrice, cfg.PriceTierCount, cancellationToken);

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
        IPriceTierQuantizer quantizer, long minPrice, long maxPrice, int priceTierCount, CancellationToken ct)
    {
        // 1. Group Lots by existing quantized price (server expects quantized prices in DB)
        var lotBuckets = await _aucyDb.Lots
            .Where(l => l.ItemConfigId == itemConfigId && l.RemainingQuantity > 0 && l.IsMatched)
            .GroupBy(l => l.UnitPrice)
            .Select(g => new { Price = g.Key, Volume = g.Sum(l => (long)l.RemainingQuantity) })
            .ToArrayAsync(ct);

        // 2. Group Bids by existing quantized bid price
        var bidBuckets = await _aucyDb.Bids
            .Where(b => b.ItemConfigId == itemConfigId && b.RemainingQuantity > 0 && b.IsMatched)
            .GroupBy(b => b.BidPrice)
            .Select(g => new { Price = g.Key, Volume = g.Sum(b => (long)b.RemainingQuantity) })
            .ToArrayAsync(ct);

        var levels = new RawPriceLevelTuple[priceTierCount];

        // initialize representative prices
        for (var i = 0; i < priceTierCount; i++)
        {
            levels[i] = new RawPriceLevelTuple(price: quantizer.GetRepresentativePriceForIndex(minPrice, maxPrice, priceTierCount, i), lotQuantity: 0, bidQuantity: 0);
        }

        // populate lot volumes
        foreach (var lb in lotBuckets)
        {
            try
            {
                var idx = quantizer.GetIndexForPrice(minPrice, maxPrice, priceTierCount, lb.Price);
                levels[idx].LotQuantity += lb.Volume;
            }
            catch
            {
                // ignore out-of-range prices; they are unexpected if DB prices are quantized
            }
        }

        // populate bid volumes
        foreach (var bb in bidBuckets)
        {
            try
            {
                var idx = quantizer.GetIndexForPrice(minPrice, maxPrice, priceTierCount, bb.Price);
                levels[idx].BidQuantity += bb.Volume;
            }
            catch
            {
                // ignore out-of-range prices
            }
        }

        return new RawItemListingDto { Levels = levels };
    }
}