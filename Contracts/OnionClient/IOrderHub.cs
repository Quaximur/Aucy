using MagicOnion;
using System.Threading.Tasks;

namespace Aucy.Contracts.OnionClient
{
    public interface IOrderHub : IStreamingHub<IOrderHub, IOrderHubReceiver>
    {
        public ValueTask CreateLot(CreateLotDto request);
        public ValueTask CollectLot(CollectLotDto request);
        public ValueTask RemoveLot(RemoveLotDto request);
        public ValueTask CreateBid(CreateBidDto request);
        public ValueTask CollectBid(CollectBidDto request);
        public ValueTask RemoveBid(RemoveBidDto request);
    }
}
