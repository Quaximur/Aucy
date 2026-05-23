using MagicOnion;

namespace Aucy.Contracts.OnionContracts;

public interface IAuctionService : IService<IAuctionService>
{
    UnaryResult<LotDto> CreateLot(CreateLotDto request);
    UnaryResult<string> RemoveLot(RemoveLotDto request);
    UnaryResult<BidDto> CreateBid(CreateBidDto request);
    UnaryResult<LotDto> GetLotInfo(GetLotDto request);
    UnaryResult<LotsDto> GetLots(GetLotsDto request);
    UnaryResult<LotsDto> GetMyLots(GetUserLotsDto request);
}