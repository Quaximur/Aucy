using Aucy.Contracts.OnionContracts;
using Aucy.Data;
using Grpc.Core;
using MagicOnion;
using MagicOnion.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Aucy.Application;

//[Authorize]
public class AuctionService : ServiceBase<IAuctionService>, IAuctionService
{
    private readonly AuctionDbContext _auctionDb;

    public AuctionService(AuctionDbContext auctionDbContext)
    {
        _auctionDb = auctionDbContext;
    }

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

    public async UnaryResult<LotsWithItemConfigsDto> GetOwnLots()
    {
        var userId = Context.CallContext.GetHttpContext().User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userGuid = Guid.Parse(userId);

        var lotsWithItemConfigs = await _auctionDb.Lots
            .Where(l => l.OwnerUserId == userGuid)
            .Include(l => l.ItemConfig)
            .Select(l => new LotWithItemConfigDto
            (
                id: l.Id.ToString(),
                ownerUserId: userId,
                itemConfigPrice: l.ItemConfigPrice,
                itemConfigAmount: l.ItemConfigAmount,
                createdAt: l.CreatedAt,
                itemConfig: new ItemConfigDto
                (
                    id: l.ItemConfig.Id.ToString(),
                    definition: l.ItemConfig.Definition,
                    name: l.ItemConfig.Name
                )
            ))
            .ToListAsync();

        return new LotsWithItemConfigsDto(lotsWithItemConfigs ?? []);
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
