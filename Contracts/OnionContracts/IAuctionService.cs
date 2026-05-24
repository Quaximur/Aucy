using MagicOnion;

namespace Aucy.Contracts.OnionContracts;

public interface IAuctionService : IService<IAuctionService>
{
    public UnaryResult<LotDto> CreateLot(CreateLotDto request);
    public UnaryResult<LotWithBidsDto> GetLot(GetLotDto request);
    public UnaryResult<LotsDto> GetLots(GetLotsDto request);
    public UnaryResult<LotsDto> GetOwnLots();
    public UnaryResult<string> RemoveLot(RemoveLotDto request);
    public UnaryResult<BidDto> CreateBid(CreateBidDto request);
    public UnaryResult<BidsWithItemsDto> GetOwnBids();
    public UnaryResult<string> RemoveBid(RemoveBidDto request);
}