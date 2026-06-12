using Grpc.Core;
using MagicOnion;
using MagicOnion.Server;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Threading.Tasks;

namespace Aucy.Contracts.OnionClient;

[Authorize]
public class ItemListingService : ServiceBase<IItemListingService>, IItemListingService
{
    public ItemConfigWithRawListingDto GetItemConfigWithRawListing(Guid ItemConfigId)
    {
        throw new NotImplementedException();
    }

    public async Task<ServerStreamingResult<RawItemListingDto>> GetItemListingStream(Guid ItemConfigId)
    {
        var stream = GetServerStreamingContext<RawItemListingDto>();
        var cancellationToken = Context.CallContext.CancellationToken;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var update = new RawItemListingDto(); // stub

                await stream.WriteAsync(update);

                await Task.Delay(1000, cancellationToken: Context.CallContext.CancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled by the client
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, $"Listing stream has been interrupted. {ex.Message}"));
        }

        return stream.Result();
    }
}