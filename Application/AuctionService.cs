using Aucy.Contracts.OnionContracts;
using Aucy.Data;
using Aucy.Domain.Entities;
using Grpc.Core;
using MagicOnion;
using MagicOnion.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Aucy.Application;

[Authorize]
public class AuctionService : ServiceBase<IAuctionService>, IAuctionService
{
    private readonly AucyDbContext _auctionDb;

    public AuctionService(AucyDbContext auctionDbContext)
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

    public async UnaryResult<LotDto> CreateLot(CreateLotDto request)
    {
        var userId = Context.CallContext.GetHttpContext().User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userGuid = Guid.Parse(userId);

        var itemConfigGuid = Guid.Parse(request.ItemConfig.Id);

        // Find or create the ItemConfig
        var itemConfig = await _auctionDb.ItemConfigs
            .FindAsync(itemConfigGuid);

        if (itemConfig == null)
        {
            itemConfig = new ItemConfig
            {
                Id = itemConfigGuid,
                Definition = request.ItemConfig.Definition,
                Name = request.ItemConfig.Name
            };
            await _auctionDb.ItemConfigs.AddAsync(itemConfig);
        }

        // Create the Lot
        var lot = Lot.Create(ownerUserId: userGuid, 
            itemConfigId: itemConfigGuid, 
            itemConfig: itemConfig,
            itemConfigPrice: request.ItemConfigPrice, 
            itemConfigAmount: request.ItemConfigAmount);

        await _auctionDb.Lots.AddAsync(lot);
        await _auctionDb.SaveChangesAsync();

        return new LotDto
        (
            id: lot.Id.ToString(),
            ownerUserId: userId,
            itemConfigId: request.ItemConfig.Id,
            itemConfigPrice: lot.ItemConfigPrice,
            itemConfigAmount: lot.ItemConfigAmount,
            createdAt: lot.CreatedAt
        );
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
