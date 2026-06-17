using MagicOnion;
using System.Threading.Tasks;

namespace Aucy.Contracts.OnionClient
{
    public interface IOrderHub : IStreamingHub<IOrderHub, IOrderHubReceiver>
    {
        ValueTask CreateLot(CreateLotDto request);
        ValueTask CollectLot(CollectLotDto request);
        ValueTask RemoveLot(RemoveLotDto request);
        ValueTask CreateBid(CreateBidDto request);
        ValueTask CollectBid(CollectBidDto request);
        ValueTask RemoveBid(RemoveBidDto request);
    }
}
