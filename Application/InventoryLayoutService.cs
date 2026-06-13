using Aucy.Application.Helpers;
using Aucy.Contracts.OnionClient;
using Aucy.Data;
using AutoMapper;
using Grpc.Core;
using MagicOnion;
using MagicOnion.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Aucy.Application;

[Authorize]
public class InventoryLayoutService(AucyDbContext aucyDbContext, IMapper mapper) : ServiceBase<IInventoryLayoutService>,
#pragma warning disable CS8644
    IInventoryLayoutService
#pragma warning disable CS8644
{
    private readonly AucyDbContext _aucyDb = aucyDbContext;
    private readonly IMapper _mapper = mapper;

    public async UnaryResult<InventoryDto> GetOwnInventory(GetInventoryDto request)
    {
        var userId = Context.GetUserId();

        try
        {
            var inventory = await _aucyDb.Inventories
                .Where(x => x.Id == userId)
                .Select(x => new InventoryDto
                {
                    Capacity = x.Capacity,
                    UsedCapacity = x.UsedCapacity,
                    WeightLimit = x.WeightLimit,
                    UsedWeight = x.UsedWeight,
                    ItemStacks = x.ItemStacks
                        .Skip((request.Page - 1) * request.PageSize)
                        .Take(request.PageSize)
                        .Select(i => new ItemStackDto
                        {
                            ItemConfig = _mapper.Map<ItemConfigDto>(i.Config),
                            Quantity = i.Quantity,
                        })
                        .ToArray()
                })
                .FirstOrDefaultAsync() ?? throw new RpcException(new Status(StatusCode.NotFound,
                    "Inventory doesn't exist"));

            return inventory;
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal,
                $"Failed to get inventory: {ex.Message}"));
        }
    }
}
