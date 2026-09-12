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
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, $"Listing stream has been interrupted: {ex.Message}"));
        }

        return stream.Result();
    }

    private async Task<RawItemListingDto> FetchVolumeListingAsync(
        Guid itemConfigId,
        IPriceTierQuantizer quantizer,
        long minPrice,
        long maxPrice,
        int priceTierCount,
        CancellationToken ct)
    {
        var volumes = await _aucyDb.Lots
            .Where(l =>
                l.ItemConfigId == itemConfigId &&
                l.RemainingQuantity > 0 &&
                l.IsMatched)
            .Select(l => new
            {
                Price = l.UnitPrice,
                LotQuantity = (long)l.RemainingQuantity,
                BidQuantity = 0L
            })
            .Concat(
                _aucyDb.Bids
                    .Where(b =>
                        b.ItemConfigId == itemConfigId &&
                        b.RemainingQuantity > 0 &&
                        b.IsMatched)
                    .Select(b => new
                    {
                        Price = b.BidPrice,
                        LotQuantity = 0L,
                        BidQuantity = (long)b.RemainingQuantity
                    }))
            .GroupBy(x => x.Price)
            .Select(g => new
            {
                Price = g.Key,
                LotQuantity = g.Sum(x => x.LotQuantity),
                BidQuantity = g.Sum(x => x.BidQuantity)
            })
            .ToArrayAsync(ct);

        var levels = new RawPriceLevelTuple[priceTierCount];

        for (var i = 0; i < priceTierCount; i++)
        {
            quantizer.TryGetPriceForTier(
                minPrice,
                maxPrice,
                priceTierCount,
                i,
                out var price);

            levels[i] = new RawPriceLevelTuple(
                price: price,
                lotQuantity: 0,
                bidQuantity: 0);
        }

        foreach (var volume in volumes)
        {
            if (!quantizer.TryGetTierIndex(
                    minPrice,
                    maxPrice,
                    priceTierCount,
                    volume.Price,
                    out var tierIndex))
            {
                continue;
            }

            levels[tierIndex].LotQuantity += volume.LotQuantity;
            levels[tierIndex].BidQuantity += volume.BidQuantity;
        }

        return new RawItemListingDto
        {
            Levels = levels
        };
    }
}