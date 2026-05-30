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
public class AuctionService(AucyDbContext aucyDbContext, IMapper mapper) : ServiceBase<IAuctionService>,
#pragma warning disable CS8644 // Type does not implement interface member. Nullability of reference types in interface implemented by the base type doesn't match.
    IAuctionService
#pragma warning restore CS8644 // Type does not implement interface member. Nullability of reference types in interface implemented by the base type doesn't match.
{
    private readonly AucyDbContext _aucyDb = aucyDbContext;
    private readonly IMapper _mapper = mapper;

    public UnaryResult<ItemConfigWithRawListing> GetItemConfig(GetItemConfigDto request)
    {
        throw new NotImplementedException();
    }

    public UnaryResult<ItemConfigWithRichListing> GetRichItemConfig(GetItemConfigDto request)
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
        // CAUTION: with substring filter, "Weapon>Sword" will also match "Weapon>SwordAndShield", which may not be desired
        if (!string.IsNullOrWhiteSpace(request.CategoryPathFilter))
            query = query.Where(x => x.CategoryPath.Contains(request.CategoryPathFilter, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(request.NameFilter))
            query = query.Where(x => x.Name.Contains(request.NameFilter, StringComparison.OrdinalIgnoreCase));

        var itemConfigsList = await query
            .Where(x => !x.IsUnary || x.Lots.Any()) // No zombie configs
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => _mapper.Map<ItemConfigDto>(x))
            .ToListAsync();

        return new ItemConfigsDto(itemConfigsList);
    }

    public async UnaryResult<LotDto> CreateLot(CreateLotDto request)
    {
        var userId = GetUserIdFromContext();

        Console.WriteLine($"Creating lot request for user: {userId}");

        var itemConfigId = request.ItemConfig.Id;

        //TODO: Transaction 

        var lotExists = await _aucyDb.Lots.AnyAsync(l => l.OwnerUserId == userId && l.ItemConfigId == itemConfigId);
        if (lotExists)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "You already have a lot with this item"));

        // Check items existence and quantity in inventory
        var itemStack = await _aucyDb.ItemStacks
            .AsTracking()
            .Include(x => x.Inventory)
            .FirstOrDefaultAsync(s => s.InventoryId == userId &&
                s.ConfigId == itemConfigId &&
                s.Quantity >= request.TotalQuantity) ?? throw new RpcException(new Status(StatusCode.InvalidArgument,
                "You don't have enough items in your inventory"));

        var inventory = itemStack.Inventory ?? throw new RpcException(new Status(StatusCode.NotFound, "Inventory not found"));

        // Reduce the quantity or remove the ItemStack
        var remainingQuantity = itemStack.Quantity - request.TotalQuantity;

        if (remainingQuantity > 0)
            itemStack.Quantity = remainingQuantity;
        else
        {
            _aucyDb.ItemStacks.Remove(itemStack);
            inventory.UsedCapacity -= 1;
            if (request.ItemConfig.Weight > 0)
                inventory.UsedWeight -= request.ItemConfig.Weight * request.TotalQuantity;
        }

        var lot = Lot.Create(ownerUserId: userId,
            itemConfigId: itemConfigId,
            unitPrice: request.UnitPrice,
            totalQuantity: request.TotalQuantity);

        await _aucyDb.Lots.AddAsync(lot);
        await _aucyDb.SaveChangesAsync();

        Console.WriteLine($"Created lot: {lot.Id} for user: {userId} with itemConfig: {itemConfigId}");

        return _mapper.Map<LotDto>(lot);
    }

    public async UnaryResult<OwnLotsDto> GetOwnLots()
    {
        var userId = GetUserIdFromContext();

        var lotsWithItemConfigs = await _aucyDb.Lots
            .Where(l => l.OwnerUserId == userId)
            .Include(l => l.ItemConfig)
            .Select(l => _mapper.Map<OwnLotDto>(l))
            .ToListAsync();

        return new OwnLotsDto(lotsWithItemConfigs);
    }

    public UnaryResult<Guid> RemoveLot(RemoveLotDto request)
    {
        throw new NotImplementedException();
    }

    public UnaryResult<BidDto> CreateBid(CreateBidDto request)
    {
        throw new NotImplementedException();
    }

    public UnaryResult<OwnBidsDto> GetOwnBids()
    {
        throw new NotImplementedException();
    }

    public UnaryResult<Guid> RemoveBid(RemoveBidDto request)
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
