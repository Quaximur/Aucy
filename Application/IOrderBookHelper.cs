using MagicOnion;
using System;

namespace Aucy.Contracts.OnionClient
{
    public interface IOrderBookHelper
    {
        public UnaryResult<LotCreatedDto> CreateLot(Guid userId, CreateLotDto request);
        public UnaryResult<OwnLotsDto> GetOwnLots(Guid userId);
        public UnaryResult<LotRemovedDto> RemoveLot(Guid userId, RemoveLotDto request); // TODO: partial money collecting. Only ExtraMoney withdraw.
        public UnaryResult<LotCollectedDto> CollectLot(Guid userId, CollectLotDto request);
        public UnaryResult<OwnBidsDto> GetOwnBids(Guid userId);
        public UnaryResult<BidCreatedDto> CreateBid(Guid userId, CreateBidDto request);
        public UnaryResult<BidCollectedDto> CollectBid(Guid userId, CollectBidDto request);
        public UnaryResult<BidRemovedDto> RemoveBid(Guid userId, RemoveBidDto request); // TODO: partial items collecting. Only ExtraMoney withdraw.
    }
}