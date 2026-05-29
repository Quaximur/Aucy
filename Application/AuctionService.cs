using Aucy.Contracts.OnionContracts;
using Aucy.Data;
using Aucy.Domain.Entities;
using AutoMapper;
using Grpc.Core;
using MagicOnion;
using MagicOnion.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Aucy.Application;

[Authorize]
public class AuctionService(AucyDbContext auctionDbContext, IMapper mapper) : 
    ServiceBase<IAuctionService>, IAuctionService
{
    private readonly AucyDbContext _aucyDb = auctionDbContext;
    private readonly IMapper _mapper = mapper;

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

        // TODO: process path filter correctly (e.g. with splitting by '>' and checking each part separately, or with regex)
        if (!string.IsNullOrWhiteSpace(request.CategoryPathFilter))
            query = query.Where(x => x.CategoryPath.Contains(request.CategoryPathFilter, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(request.NameFilter))
            query = query.Where(x => x.Name.Contains(request.NameFilter, StringComparison.OrdinalIgnoreCase));

        var itemConfigsList = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => _mapper.Map<ItemConfigDto>(x))
            .ToListAsync();

        // TODO: discard unique items without lots
        // TODO: separate items with the same id but different grades (will id change with grade? or make combined PK with baseType).

        return new ItemConfigsDto(itemConfigsList);
    }

    public async UnaryResult<LotDto> CreateLot(CreateLotDto request)
    {
        var userId = GetUserIdFromContext();

        Console.WriteLine($"Creating lot request for user: {userId}");

        var itemConfigGuid = request.ItemConfig.Id;

        // TODO: check that user doesnt`t have a lot with specified itemConfig

        // Find or create the ItemConfig
        var itemConfig = await _aucyDb.ItemConfigs
            .FindAsync(itemConfigGuid);

        if (itemConfig == null)
        {
            itemConfig = _mapper.Map<ItemConfig>(request.ItemConfig);

            await _aucyDb.ItemConfigs.AddAsync(itemConfig);

            Console.WriteLine($"ItemConfig not found, created new one: {itemConfig.Id}, {itemConfig.Name}");
        }
        else
        {
            Console.WriteLine($"ItemConfig found: {itemConfig.Id}, {itemConfig.Name}");
        }

        var lot = Lot.Create(ownerUserId: userId,
            itemConfigId: itemConfig.Id,
            unitPrice: request.UnitPrice,
            itemQuantity: request.ItemQuantity);

        await _aucyDb.Lots.AddAsync(lot);
        await _aucyDb.SaveChangesAsync();

        Console.WriteLine($"Created lot: {lot.Id} for user: {userId} with itemConfig: {itemConfig.Id}");

        return _mapper.Map<LotDto>(lot);
    }

    public async UnaryResult<LotsWithItemConfigsDto> GetOwnLots()
    {
        var userId = GetUserIdFromContext();

        var lotsWithItemConfigs = await _aucyDb.Lots
            .Where(l => l.OwnerUserId == userId)
            .Include(l => l.ItemConfig)
            .Select(l => _mapper.Map<LotWithItemConfigDto>(l))
            .ToListAsync();

        return new LotsWithItemConfigsDto(lotsWithItemConfigs);
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

    private Guid GetUserIdFromContext()
    {
        var userIdText = Context.CallContext.GetHttpContext()?.User.FindFirstValue("user_id");

        if (string.IsNullOrWhiteSpace(userIdText))
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Token is not valid"));

        if (!Guid.TryParse(userIdText, out var userGuid))
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Token is not valid"));

        return userGuid;
    }
}
