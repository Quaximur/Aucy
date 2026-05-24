using MagicOnion;

namespace Aucy.Contracts.OnionContracts;

public interface IAuctionService : IService<IAuctionService>
{
    public UnaryResult<ItemWithLotsAndBidsDto> GetItem(GetItemDto request);
    public UnaryResult<ItemsDto> GetItems(GetItemsDto request);
    public UnaryResult<LotDto> CreateLot(CreateLotDto request);
    public UnaryResult<LotsWithItemsDto> GetOwnLots();
    public UnaryResult<string> RemoveLot(RemoveLotDto request);
    public UnaryResult<BidDto> CreateBid(CreateBidDto request);
    public UnaryResult<BidsWithItemsDto> GetOwnBids();
    public UnaryResult<string> RemoveBid(RemoveBidDto request);
}