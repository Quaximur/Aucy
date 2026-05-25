using Aucy.Contracts.OnionContracts;
using MagicOnion;
using MagicOnion.Server;

namespace Aucy.Application;

public class AuctionService : ServiceBase<IAuctionService>, IAuctionService
{
    public UnaryResult<ItemConfigWithLotsAndBidsDto> GetItemConfig(GetItemConfigDto request)
    {
        throw new NotImplementedException();
    }

    public UnaryResult<ItemConfigsDto> GetItemConfigs(GetItemConfigsDto request)
    {
        throw new NotImplementedException();
    }

    public UnaryResult<LotDto> CreateLot(CreateLotDto request)
    {
        throw new NotImplementedException();
    }

    public UnaryResult<LotsWithItemConfigsDto> GetOwnLots()
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

    public UnaryResult<BidsWithItemConfigsDto> GetOwnBids()
    {
        throw new NotImplementedException();
    }

    public UnaryResult<string> RemoveBid(RemoveBidDto request)
    {
        throw new NotImplementedException();
    }
}
