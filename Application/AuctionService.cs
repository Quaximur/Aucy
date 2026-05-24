using Aucy.Contracts.OnionContracts;
using MagicOnion;
using MagicOnion.Server;

namespace Aucy.Application;

public class AuctionService : ServiceBase<IAuctionService>, IAuctionService
{
    public async UnaryResult<LotDto> CreateLot(CreateLotDto createlotDto)
    {
        throw new NotImplementedException();
    }

    public async UnaryResult<LotWithBidsDto> GetLot(GetLotDto request)
    {
        throw new NotImplementedException();
    }

    public async UnaryResult<LotsDto> GetLots(GetLotsDto request)
    {
        throw new NotImplementedException();
    }

    public UnaryResult<LotsDto> GetOwnLots()
    {
        throw new NotImplementedException();
    }

    public async UnaryResult<string> RemoveLot(RemoveLotDto request)
    {
        throw new NotImplementedException();
    }

    public async UnaryResult<BidDto> CreateBid(CreateBidDto request)
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
