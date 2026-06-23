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

    //public ItemConfigWithRawListingDto GetItemConfigWithRawListing(Guid itemConfigId)
    //{
    //    throw new NotImplementedException();
    //}

    //public ItemConfigWithRichListingDto GetItemConfigWithRichListing(Guid itemConfigId)
    //{
    //    throw new NotImplementedException();
    //}

    public async Task<ServerStreamingResult<RawItemListingDto>> GetItemListingStream(Guid itemConfigId)
    {
        var stream = GetServerStreamingContext<RawItemListingDto>();
        var cancellationToken = Context.CallContext.CancellationToken;

        try
        {
            // TODO: raw SQL or Redis with pre-calc in BackgroundWorker

            while (!cancellationToken.IsCancellationRequested)
            {
                var lots = await _aucyDb.Lots
                    .Where(l => l.ItemConfigId == itemConfigId && l.RemainingQuantity > 0 && l.IsMatched)
                    .Select(l => new { l.UnitPrice, l.RemainingQuantity })
                    .GroupBy(l => l.UnitPrice)
                    .OrderByDescending(g => g.Key)
                    .Select(g => new RawLotTuple(g.Key, g.Sum(l => l.RemainingQuantity)))
                    .ToArrayAsync(cancellationToken);
                
                var bids = await _aucyDb.Bids
                    .Where(b => b.ItemConfigId == itemConfigId && b.RemainingQuantity > 0 && b.IsMatched)
                    .Select(b => new { b.BidPrice, b.RemainingQuantity })
                    .GroupBy(b => b.BidPrice)
                    .OrderByDescending(g => g.Key)
                    .Select(g => new RawBidTuple(g.Key, g.Sum(b => b.RemainingQuantity)))
                    .ToArrayAsync(cancellationToken);

                var currentListing = new RawItemListingDto()
                {
                    RawLots = lots,
                    RawBids = bids
                };

                await stream.WriteAsync(currentListing);

                await Task.Delay(10_000, cancellationToken);
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
}