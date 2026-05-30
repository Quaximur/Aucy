using Aucy.Contracts.OnionContracts;
using Aucy.Data;
using Aucy.Domain.Entities;
using AutoMapper;
using Grpc.Core;
using MagicOnion;
using MagicOnion.Server;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace Aucy.Application;

[Authorize]
public class InventoryService(AucyDbContext aucyDbContext, IMapper mapper) : ServiceBase<IInventoryService>,
#pragma warning disable CS8644 // Type does not implement interface member. Nullability of reference types in interface implemented by the base type doesn't match.
    IInventoryService
#pragma warning restore CS8644 // Type does not implement interface member. Nullability of reference types in interface implemented by the base type doesn't match.

{
    private readonly AucyDbContext _aucyDb = aucyDbContext;
    private readonly IMapper _mapper = mapper;

    public async UnaryResult<Guid> AddItemStack(AddItemStackDto request)
    {
        var userId = GetUserIdFromContext();
        var itemConfigId = request.ItemConfig.Id;

        // TODO: Transaction

        var inventory = await _aucyDb.Inventories
            .FindAsync(userId) ?? throw new RpcException(new Status(StatusCode.NotFound,
                "Inventory doesn`t exist")); // Tracked by FindAsync

        // Find or create the ItemConfig
        // Mostly used for Unary items
        var itemConfig = await _aucyDb.ItemConfigs
           .FindAsync(itemConfigId);

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

        if (inventory.UsedCapacity + request.Quantity >= inventory.Capacity)
            throw new RpcException(new Status(StatusCode.ResourceExhausted, "Inventory capacity limit exceeded"));

        float totalWeightToAdd = 0;
        var hasWeight = itemConfig.Weight > 0;

        if (hasWeight)
        {
            totalWeightToAdd = itemConfig.Weight * request.Quantity;

            if (inventory.UsedWeight + totalWeightToAdd > inventory.WeightLimit)
                throw new RpcException(new Status(StatusCode.ResourceExhausted, "Inventory weight limit exceeded"));
        }

        // Try to find an existing ItemStack with the same ConfigId in the inventory
        var itemStack = await _aucyDb.ItemStacks
            .AsTracking()
            .FirstOrDefaultAsync(i => i.InventoryId == inventory.Id &&
                i.ConfigId == itemConfig.Id);

        Guid itemStackId;
        if (itemStack != null) // If found, update the quantity
        {
            itemStackId = itemStack.Id;
            itemStack.Quantity += request.Quantity;
        }
        else // If not found, create a new ItemStack
        {
            itemStackId = Guid.NewGuid();
            itemStack = new ItemStack()
            {
                Id = itemStackId,
                ConfigId = itemConfig.Id,
                Quantity = request.Quantity,
                InventoryId = inventory.Id
            };

            await _aucyDb.ItemStacks.AddAsync(itemStack);

            inventory.UsedCapacity += 1;
        }


        if (hasWeight)
            inventory.UsedWeight += totalWeightToAdd;

        await _aucyDb.SaveChangesAsync();

        return itemStackId;
    }

    public UnaryResult<long> AddMoney(long moneyToAdd)
    {
        throw new NotImplementedException();
    }

    public UnaryResult<InventoryDto> CreateInventory(CreateInventoryDto request)
    {
        throw new NotImplementedException();
    }

    public UnaryResult<InventoryDto> GetOwnInventory(GetInventoryDto request)
    {
        throw new NotImplementedException();
    }

    public async UnaryResult<Guid> RemoveItemStack(RemoveItemStackDto request)
    {
        var userId = GetUserIdFromContext();

        using var transaction = await _aucyDb.Database.BeginTransactionAsync();

        try
        {
            var inventory = await _aucyDb.Inventories
                .FindAsync(userId) ?? throw new RpcException(new Status(StatusCode.NotFound,
                    "Inventory doesn't exist"));

            var itemStack = await _aucyDb.ItemStacks
                .AsTracking()
                .Include(x => x.Config)
                .FirstOrDefaultAsync(i => i.Id == request.ItemStackId && i.InventoryId == inventory.Id)
                ?? throw new RpcException(new Status(StatusCode.NotFound,
                    "ItemStack not found in inventory"));

            if (itemStack.Quantity < request.Quantity)
                throw new RpcException(new Status(StatusCode.InvalidArgument,
                    $"Cannot remove {request.Quantity} items, only {itemStack.Quantity} available"));

            var itemConfig = itemStack.Config ?? throw new RpcException(new Status(StatusCode.NotFound,
                    "ItemConfig not found")); ;

            var resultId = itemStack.Id;

            if (itemStack.Quantity == request.Quantity)
            {
                _aucyDb.ItemStacks.Remove(itemStack);
                inventory.UsedCapacity -= 1;
            }
            else
            {
                itemStack.Quantity -= request.Quantity;
            }

            if (itemConfig.Weight > 0)
            {
                inventory.UsedWeight -= itemConfig.Weight * request.Quantity;
            }

            await _aucyDb.SaveChangesAsync();
            await transaction.CommitAsync();

            return resultId;
        }
        catch (RpcException)
        {
            await transaction.RollbackAsync();
            throw;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw new RpcException(new Status(StatusCode.Internal,
                $"Failed to remove item stack: {ex.Message}"));
        }
    }

    public UnaryResult<long> RemoveMoney(long moneyToTake)
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
