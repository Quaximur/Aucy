using MagicOnion;
using System;

namespace Aucy.Contracts.OnionClient
{
    public interface IOrderBookService : IService<IOrderBookService>
    {
        public UnaryResult<LotDto> CreateLot(CreateLotDto request);
        public UnaryResult<OwnLotsDto> GetOwnLots();
        public UnaryResult<Guid> RemoveLot(RemoveLotDto request); // TODO: partial money collecting. Only ExtraMoney withdraw.
        public UnaryResult<Guid> CollectLot(CollectLotDto request);
        public UnaryResult<BidDto> CreateBid(CreateBidDto request);
        public UnaryResult<Guid> CollectBid(CollectBidDto request);
        public UnaryResult<OwnBidsDto> GetOwnBids();
        public UnaryResult<Guid> RemoveBid(RemoveBidDto request); // TODO: partial items collecting. Only ExtraMoney withdraw.
    }
}