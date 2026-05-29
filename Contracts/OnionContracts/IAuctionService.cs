using MagicOnion;
using System;

namespace Aucy.Contracts.OnionContracts
{
    public interface IAuctionService : IService<IAuctionService>
    {
        public UnaryResult<ItemConfigWithLotsAndBidsDto> GetItemConfig(GetItemConfigDto request);
        public UnaryResult<ItemConfigsDto> GetItemConfigs(GetItemConfigsDto request);
        public UnaryResult<LotDto> CreateLot(CreateLotDto request);
        public UnaryResult<OwnLotsDto> GetOwnLots();
        public UnaryResult<Guid> RemoveLot(RemoveLotDto request);
        public UnaryResult<BidDto> CreateBid(CreateBidDto request);
        public UnaryResult<OwnBidsDto> GetOwnBids();
        public UnaryResult<Guid> RemoveBid(RemoveBidDto request);
    }
}