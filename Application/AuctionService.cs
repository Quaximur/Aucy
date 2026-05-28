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
    private readonly AucyDbContext _aucyDb;

    public AuctionService(AucyDbContext auctionDbContext)
    {
        _aucyDb = auctionDbContext;
    }

    public UnaryResult<ItemConfigWithLotsAndBidsDto> GetItemConfig(GetItemConfigDto request)
    {
        throw new NotImplementedException();
    }

    public async UnaryResult<ItemConfigsDto> GetItemConfigs(GetItemConfigsDto request)
    {
        Console.WriteLine($"{nameof(GetItemConfigs)} request");

        if (request.Page <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Page number should be greater than 0"));

        if (request.PageSize <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Page size should be greater than 0"));

        var query = _aucyDb.ItemConfigs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.DefinitionFilter))
            query = query.Where(x => x.Definition.Contains(request.DefinitionFilter, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(request.NameFilter))
            query = query.Where(x => x.Name.Contains(request.NameFilter, StringComparison.OrdinalIgnoreCase));

        var itemConfigsList = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new ItemConfigDto(id: x.Id.ToString(), definition: x.Definition, name: x.Name))
            .ToListAsync();

        return new ItemConfigsDto(itemConfigsList);
    }

    public async UnaryResult<LotDto> CreateLot(CreateLotDto request)
    {
        var (userIdText, userGuid) = GetUserIdFromContext();

        Console.WriteLine($"Creating lot request for user: {userIdText}");

        var itemConfigGuid = Guid.Parse(request.ItemConfig.Id);

        // Find or create the ItemConfig
        var itemConfig = await _aucyDb.ItemConfigs
            .FindAsync(itemConfigGuid);

        if (itemConfig == null)
        {
            itemConfig = new ItemConfig
            {
                Id = itemConfigGuid,
                Definition = request.ItemConfig.Definition,
                Name = request.ItemConfig.Name
            };

            await _aucyDb.ItemConfigs.AddAsync(itemConfig);

            Console.WriteLine($"ItemConfig not found, created new one: {itemConfig.Id}, {itemConfig.Name}");
        }
        else
        {
            Console.WriteLine($"ItemConfig found: {itemConfig.Id}, {itemConfig.Name}");
        }


        // Create the Lot
        var lot = Lot.Create(ownerUserId: userGuid,
            itemConfigId: itemConfig.Id,
            unitPrice: request.UnitPrice,
            itemQuantity: request.ItemQuantity);

        await _aucyDb.Lots.AddAsync(lot);
        await _aucyDb.SaveChangesAsync();

        Console.WriteLine($"Created lot: {lot.Id} for user: {userIdText} with itemConfig: {itemConfig.Id}");

        return new LotDto
        (
            id: lot.Id.ToString(),
            ownerUserId: userIdText,
            itemConfigId: request.ItemConfig.Id,
            unitPrice: lot.UnitPrice,
            itemQuantity: lot.ItemQuantity,
            createdAt: lot.CreatedAt
        );
    }

    public async UnaryResult<LotsWithItemConfigsDto> GetOwnLots()
    {
        var (userIdText, userGuid) = GetUserIdFromContext();

        var lotsWithItemConfigs = await _aucyDb.Lots
            .Where(l => l.OwnerUserId == userGuid)
            .Include(l => l.ItemConfig)
            .Select(l => new LotWithItemConfigDto
            (
                id: l.Id.ToString(),
                ownerUserId: userIdText,
                unitPrice: l.UnitPrice,
                itemQuantity: l.ItemQuantity,
                createdAt: l.CreatedAt,
                itemConfig: new ItemConfigDto
                (
                    id: l.ItemConfig!.Id.ToString(),
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

    private (string userIdText, Guid userGuid) GetUserIdFromContext()
    {
        var userIdText = Context.CallContext.GetHttpContext()?.User.FindFirstValue("user_id");

        if (string.IsNullOrEmpty(userIdText))
            throw new RpcException(new Status(StatusCode.PermissionDenied, "token is not valid"));

        if (!Guid.TryParse(userIdText, out var userGuid))
            throw new RpcException(new Status(StatusCode.PermissionDenied, "token is not valid"));

        return (userIdText, userGuid);
    }
}
