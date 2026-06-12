using Aucy.Application.Exceptions;
using Aucy.Application.Helpers;
using Aucy.Contracts.OnionClient;
using Aucy.Contracts.OnionServer;
using Aucy.Data;
using Aucy.Domain.Entities;
using AutoMapper;
using Grpc.Core;
using MagicOnion;
using MagicOnion.Server;
using MessagePack;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;
using System.Data;

namespace Aucy.Application;

[Authorize]
public class InventoryService(AucyDbContext aucyDbContext,
    InventoryPropertiesService inventoryPropertiesService,
    ItemsService itemsService,
    IMapper mapper) : ServiceBase<IInventoryService>,
#pragma warning disable CS8644 // Type does not implement interface member. Nullability of reference types in interface implemented by the base type doesn't match.
    IInventoryService
#pragma warning restore CS8644 // Type does not implement interface member. Nullability of reference types in interface implemented by the base type doesn't match.

{
    private readonly AucyDbContext _aucyDb = aucyDbContext;
    private readonly InventoryPropertiesService _inventoryProperties = inventoryPropertiesService;
    private readonly ItemsService _itemsService = itemsService;
    private readonly IMapper _mapper = mapper;

    public async UnaryResult<Nil> AddItemStack(AddItemStackDto request)
    {
        var userId = Context.GetUserId();
        var itemConfigId = request.ItemConfig.Id;

        if (request.Quantity <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Quantity must be greater than zero"));

        if (request.ItemConfig == null)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "ItemConfig must be provided"));

        if (request.ItemConfig.IsUnary && request.Quantity != 1)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Quantity must be 1 for unary items"));
       
        return await GetRetryPolicy().ExecuteAsync(async () =>
        {
            using var transaction = await _aucyDb.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

            try
            {
                var inventoryFields = await _aucyDb.Inventories
                    .Where(x => x.Id == userId)
                    .Select(x => new { x.Id, x.Capacity, x.UsedCapacity, x.WeightLimit, x.UsedWeight })
                    .FirstOrDefaultAsync() ?? throw new NoRetryRpcException(new Status(StatusCode.NotFound,
                        "Inventory doesn`t exist"));

                // Find or create the ItemConfig
                // Mostly used for Unary items
                var itemConfig = await _aucyDb.ItemConfigs.FirstOrDefaultAsync(x => x.Id == itemConfigId);

                if (itemConfig == null)
                {
                    itemConfig = _mapper.Map<ItemConfig>(request.ItemConfig);

                    _aucyDb.ItemConfigs.Add(itemConfig);
                    await _aucyDb.SaveChangesAsync(); // save instantly to use for ItemStack update

                    Console.WriteLine($"ItemConfig not found, created new one: {itemConfig.Id}, {itemConfig.Name}");
                }
                else
                {
                    Console.WriteLine($"ItemConfig found: {itemConfig.Id}, {itemConfig.Name}");
                }

                if (inventoryFields.UsedCapacity + request.Quantity > inventoryFields.Capacity)
                    throw new RpcException(new Status(StatusCode.ResourceExhausted, "Inventory capacity limit exceeded"));

                float usedWeightDiff = 0;

                if (itemConfig.Weight > 0)
                {
                    usedWeightDiff = itemConfig.Weight * request.Quantity;

                    if (inventoryFields.UsedWeight + usedWeightDiff > inventoryFields.WeightLimit)
                        throw new NoRetryRpcException(new Status(StatusCode.ResourceExhausted, "Inventory weight limit exceeded"));
                }

                // Try to update an existing ItemStack with the same ConfigId or create new one if not found
                var usedCapacityDiff = await _itemsService.UpdateOrInsertItemStack(itemConfig.Id, request.Quantity,
                    inventoryFields.Id);

                await _inventoryProperties.ExecuteInventoryUpdate(userId, usedWeightDiff, usedCapacityDiff);

                await transaction.CommitAsync();

                return Nil.Default;
            }
            catch (RpcException)
            {
                await transaction.RollbackAsync();
                throw;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new RpcException(new Status(StatusCode.Internal, $"Failed to add item stack: {ex.Message}"));
            }
        });
    }

    public async UnaryResult<Nil> AddMoney(long moneyToAdd)
    {
        var userId = Context.GetUserId();

        try
        {
            await _inventoryProperties.DepositMoney(userId, moneyToAdd);

            return Nil.Default;
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, $"Failed to add money: {ex.Message}"));
        }
    }

    public async UnaryResult<Nil> RemoveMoney(long moneyToTake)
    {
        var userId = Context.GetUserId();

        try
        {
            await _inventoryProperties.DepositMoney(userId, moneyToTake);

            return Nil.Default;
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, $"Failed to take money: {ex.Message}"));
        }
    }

    public async UnaryResult<InventoryDto> CreateInventory(CreateInventoryDto request)
    {
        var userId = Context.GetUserId();
        var newInventory = new Inventory()
        {
            Id = userId,
            Capacity = request.Capacity,
            UsedCapacity = 0,
            WeightLimit = request.WeightLimit,
            UsedWeight = 0
        };

        _aucyDb.Inventories.Add(newInventory); // catch already existing inventory exception
        await _aucyDb.SaveChangesAsync();

        return new InventoryDto()
        {
            Capacity = newInventory.Capacity,
            UsedCapacity = newInventory.UsedCapacity,
            WeightLimit = newInventory.WeightLimit,
            UsedWeight = newInventory.UsedWeight,
            ItemStacks = []
        };
    }

    public async UnaryResult<Nil> RemoveItemStack(RemoveItemStackDto request)
    {
        var userId = Context.GetUserId();

        // Block specified Inventory and ItemStack of a SINGLE User (it`s ok because other users don`t even use it)
        using var transaction = await _aucyDb.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);

        try
        {
            var itemStackFields = await _aucyDb.ItemStacks
                .Where(i => i.Id == request.ItemStackId && i.InventoryId == userId)
                .Select(x => new { x.Id, x.Quantity, ConfigWeight = x.Config.Weight })
                .FirstOrDefaultAsync() ?? throw new RpcException(new Status(StatusCode.NotFound,
                    "ItemStack not found in inventory"));

            if (itemStackFields.Quantity < request.Quantity)
                throw new RpcException(new Status(StatusCode.InvalidArgument,
                    $"Cannot remove {request.Quantity} items, only {itemStackFields.Quantity} available"));

            var itemStackId = itemStackFields.Id;
            int usedCapacityDiff = 0;
            var updatedItemStacks = 0;

            if (itemStackFields.Quantity == request.Quantity)
            {
                updatedItemStacks = await _aucyDb.ItemStacks.Where(x => x.Id == itemStackFields.Id)
                    .ExecuteDeleteAsync();

                usedCapacityDiff = -1;
            }
            else
            {
                updatedItemStacks = await _aucyDb.ItemStacks
                    .Where(x => x.Id == itemStackId)
                    .ExecuteUpdateAsync(x => x.SetProperty(i => i.Quantity, i => i.Quantity - request.Quantity));
            }

            if (updatedItemStacks != 1)
                throw new RpcException(new Status(StatusCode.NotFound, $"ItemStack is not determined. Entries found: {updatedItemStacks}"));

            float usedWeightDiff = 0;
            if (itemStackFields.ConfigWeight > 0)
                usedWeightDiff = -itemStackFields.ConfigWeight * request.Quantity;

            await _inventoryProperties.ExecuteInventoryUpdate(userId, usedWeightDiff, usedCapacityDiff);

            await transaction.CommitAsync();

            return Nil.Default;
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

    private AsyncRetryPolicy GetRetryPolicy()
    {
        return Policy
            .Handle<Exception>(ex => ex is not NoRetryRpcException)
            .RetryAsync(3);
    }
}
