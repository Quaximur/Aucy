//using Aucy.Application.Helpers;
//using Aucy.Contracts.OnionClient;
//using Aucy.Data;
//using AutoMapper;
//using Grpc.Core;
//using MagicOnion;
//using MagicOnion.Server;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.EntityFrameworkCore;

//namespace Aucy.Application;

//[Authorize]
//public class InventoryLayoutService(AucyDbContext aucyDbContext, IMapper mapper) : ServiceBase<IInventoryLayoutService>,
//#pragma warning disable CS8644
//    IInventoryLayoutService
//#pragma warning disable CS8644
//{
//    private readonly AucyDbContext _aucyDb = aucyDbContext;
//    private readonly IMapper _mapper = mapper;

//    public async UnaryResult<InventoryDto> GetOwnInventory(GetInventoryDto request)
//    {
//        var userId = Context.GetUserId();

//        try
//        {
//            var inventory = await _aucyDb.Inventories
//                .Where(x => x.Id == userId)
//                .Select(x => new InventoryDto
//                (
//                    money: x.Money,
//                    itemStacks: x.ItemStacks
//                        .OrderBy(x => x.CreatedAt)
//                        .Skip((request.Page - 1) * request.PageSize)
//                        .Take(request.PageSize)
//                        .Select(i => new ItemStackDto
//                        (
//                            itemConfig: _mapper.Map<ItemConfigDto>(i.Config),
//                            quantity: i.Quantity
//                        ))
//                        .ToArray(),
//                    capacity: x.Capacity,
//                    usedCapacity: x.UsedCapacity,
//                    weightLimit: x.WeightLimit,
//                    usedWeight: x.UsedWeight
//                ))
//                .FirstOrDefaultAsync() ?? throw new RpcException(new Status(StatusCode.NotFound,
//                    "Inventory doesn't exist"));

//            return inventory;
//        }
//        catch (RpcException)
//        {
//            throw;
//        }
//        catch (Exception ex)
//        {
//            throw new RpcException(new Status(StatusCode.Internal,
//                $"Failed to get inventory: {ex.Message}"));
//        }
//    }
//}
