using Aucy.Contracts.OnionContracts;
using MagicOnion;
using MagicOnion.Server;

namespace Aucy.Application;

public class AuctionService : ServiceBase<IAuctionService>, IAuctionService
{
    public UnaryResult<ItemWithLotsAndBidsDto> GetItem(GetItemDto request)
    {
        throw new NotImplementedException();
    }

    public UnaryResult<ItemsDto> GetItems(GetItemsDto request)
    {
        throw new NotImplementedException();
    }

    public UnaryResult<LotDto> CreateLot(CreateLotDto request)
    {
        throw new NotImplementedException();
    }

    public UnaryResult<LotsWithItemsDto> GetOwnLots()
    {
        throw new NotImplementedException();
    }
    public UnaryResult<string> RemoveLot(RemoveLotDto request)
    {
        throw new NotImplementedException();
    }

    public UnaryResult<BidDto> CreateBid(CreateBidDto request)
    {
        throw new NotImplementedException();
    }

    public UnaryResult<BidsWithItemsDto> GetOwnBids()
    {
        throw new NotImplementedException();
    }

    public UnaryResult<string> RemoveBid(RemoveBidDto request)
    {
        throw new NotImplementedException();
    }
}
