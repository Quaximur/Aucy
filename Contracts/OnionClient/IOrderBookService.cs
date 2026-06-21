using MagicOnion;

namespace Aucy.Contracts.OnionClient
{
    public interface IOrderBookService : IService<IOrderBookService>
    {
        public UnaryResult<LotCreatedDto> CreateLot(CreateLotDto request);
        public UnaryResult<OwnLotsDto> GetOwnLots();
        public UnaryResult<LotRemovedDto> RemoveLot(RemoveLotDto request); // TODO: partial money collecting. Only ExtraMoney withdraw.
        public UnaryResult<LotCollectedDto> CollectLot(CollectLotDto request);
        public UnaryResult<OwnBidsDto> GetOwnBids();
        public UnaryResult<BidCreatedDto> CreateBid(CreateBidDto request);
        public UnaryResult<BidCollectedDto> CollectBid(CollectBidDto request);
        public UnaryResult<BidRemovedDto> RemoveBid(RemoveBidDto request); // TODO: partial items collecting. Only ExtraMoney withdraw.
    }
}