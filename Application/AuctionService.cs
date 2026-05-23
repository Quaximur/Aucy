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
}
