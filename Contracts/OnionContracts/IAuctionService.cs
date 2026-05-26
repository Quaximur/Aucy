using MagicOnion;

namespace Aucy.Contracts.OnionContracts
{
    public interface IAuctionService : IService<IAuctionService>
    {
        public UnaryResult<ItemConfigWithLotsAndBidsDto> GetItemConfig(GetItemConfigDto request);
        public UnaryResult<ItemConfigsDto> GetItemConfigs(GetItemConfigsDto request);
        public UnaryResult<LotDto> CreateLot(CreateLotDto request);
        public UnaryResult<LotsWithItemConfigsDto> GetOwnLots();
        public UnaryResult<string> RemoveLot(RemoveLotDto request);
        public UnaryResult<BidDto> CreateBid(CreateBidDto request);
        public UnaryResult<BidsWithItemConfigsDto> GetOwnBids();
        public UnaryResult<string> RemoveBid(RemoveBidDto request);
    }
}