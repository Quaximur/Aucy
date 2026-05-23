using Aucy.Contracts.OnionContracts;
using MagicOnion;
using MagicOnion.Server;

namespace Aucy.Application;

public class AuctionService : ServiceBase<IAuctionService>, IAuctionService
{
    public UnaryResult<BidDto> CreateBid(CreateBidDto request)
    {
        throw new NotImplementedException();
    }

    public async UnaryResult<LotDto> CreateLot(CreateLotDto createlotDto)
    {
        throw new NotImplementedException();
    }

    public UnaryResult<LotDto> GetLotInfo(GetLotDto request)
    {
        throw new NotImplementedException();
    }

    public UnaryResult<LotsDto> GetLots(GetLotsDto request)
    {
        throw new NotImplementedException();
    }

    public UnaryResult<LotsDto> GetMyLots(GetUserLotsDto request)
    {
        throw new NotImplementedException();
    }

    public UnaryResult<string> RemoveLot(RemoveLotDto request)
    {
        throw new NotImplementedException();
    }
}
