using Aucy.Application.Constants;
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
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Polly;
using Polly.Retry;
using System.Data;
using Microsoft.Extensions.Logging;

namespace Aucy.Application;

[Authorize]
public class InventoryService(
    InventoryPropertiesHelper inventoryPropertiesService,
    ItemsHelper itemsService,
    IDbContextFactory<AucyDbContext> aucyDbFactory,
    IMapper mapper,
    ILogger<InventoryService> logger) : ServiceBase<IInventoryService>,
#pragma warning disable CS8644 // Type does not implement interface member. Nullability of reference types in interface implemented by the base type doesn't match.
    IInventoryService
#pragma warning restore CS8644 // Type does not implement interface member. Nullability of reference types in interface implemented by the base type doesn't match.

{
    private readonly IDbContextFactory<AucyDbContext> _aucyDbFactory = aucyDbFactory;
    private readonly InventoryPropertiesHelper _inventoryProperties = inventoryPropertiesService;
    private readonly ItemsHelper _itemsService = itemsService;
    private readonly IMapper _mapper = mapper;
    private readonly ILogger<InventoryService> _logger = logger;

    public async UnaryResult<Nil> AddItemStack(AddItemStackDto request)
    {
        var userId = Context.GetUserId();

        if (request.Quantity <= 0)
            throw new ReturnStatusException(StatusCode.InvalidArgument, AuctionErrorCodes.INVALID_QUANTITY);

        if (request.ItemConfig != null && request.ItemConfig.IsUnary && request.Quantity != 1)
            throw new NoRetryReturnStatusException(StatusCode.InvalidArgument, AuctionErrorCodes.INVALID_QUANTITY_FOR_UNARY_ITEM);

        var itemConfigId = request.ItemConfigId;

        return await GetRetryPolicy().ExecuteAsync(async () =>
        {
            using var aucyDb = await _aucyDbFactory.CreateDbContextAsync();

            using var transaction = await aucyDb.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

            try
            {
                var inventoryFields = await aucyDb.Inventories
                    .Where(x => x.Id == userId)
                    .Select(x => new { x.Id, x.Capacity, x.UsedCapacity, x.WeightLimit, x.UsedWeight })
                    .FirstOrDefaultAsync() ?? throw new NoRetryReturnStatusException(StatusCode.NotFound,
                        AuctionErrorCodes.INVENTORY_NOT_FOUND);

                // Find or create the ItemConfig
                // Mostly used for Unary items
                var itemConfig = await aucyDb.ItemConfigs.FirstOrDefaultAsync(x => x.Id == itemConfigId);

                if (itemConfig == null)
                {
                    if (request.ItemConfig == null)
                        throw new NoRetryReturnStatusException(StatusCode.InvalidArgument, AuctionErrorCodes.INVALID_ITEM_CONFIG);

                    itemConfig = _mapper.Map<ItemConfig>(request.ItemConfig);

                    aucyDb.ItemConfigs.Add(itemConfig);
                    await aucyDb.SaveChangesAsync(); // save instantly to use for ItemStack update

                    _logger.LogInformation("ItemConfig not found, created new one: {ItemConfigId}, {ItemConfigName}", 
                        itemConfig.Id, itemConfig.Name);
                }
                else
                {
                    if (itemConfig.IsUnary && request.Quantity != 1)
                        throw new NoRetryReturnStatusException(StatusCode.InvalidArgument, 
                            AuctionErrorCodes.INVALID_QUANTITY_FOR_UNARY_ITEM);

                    _logger.LogInformation("ItemConfig found: {ItemConfigId}, {ItemConfigName}", 
                        itemConfig.Id, itemConfig.Name);
                }

                // x > z - y <=> x + y > z (no overflow)
                if (inventoryFields.UsedCapacity > inventoryFields.Capacity - 1)
                    throw new NoRetryReturnStatusException(StatusCode.ResourceExhausted, 
                        AuctionErrorCodes.INVENTORY_CAPACITY_EXCEEDED);

                float usedWeightDiff = 0;

                if (itemConfig.Weight > 0)
                {
                    usedWeightDiff = checked(itemConfig.Weight * request.Quantity);

                    // x > z - y <=> x + y > z (no overflow)
                    if (inventoryFields.UsedWeight > inventoryFields.WeightLimit - usedWeightDiff)
                        throw new NoRetryReturnStatusException(StatusCode.ResourceExhausted, 
                            AuctionErrorCodes.INVENTORY_WEIGHT_LIMIT_EXCEEDED);
                }

                // Try to update an existing ItemStack with the same ConfigId or create new one if not found
                var usedCapacityDiff = await _itemsService.UpdateOrInsertItemStack(aucyDb, itemConfig.Id, request.Quantity,
                    inventoryFields.Id);

                await _inventoryProperties.ExecuteInventoryUpdate(aucyDb, userId, usedWeightDiff, usedCapacityDiff);

                await transaction.CommitAsync();

                return Nil.Default;
            }
            catch (ReturnStatusException)
            {
                await transaction.RollbackAsync();
                throw;
            }
            catch (OverflowException)
            {
                await transaction.RollbackAsync();
                throw new NoRetryReturnStatusException(StatusCode.InvalidArgument, AuctionErrorCodes.AMOUNT_TOO_LARGE);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw new ReturnStatusException(StatusCode.Internal, AuctionErrorCodes.FAILED_TO_ADD_ITEM_STACK);
            }
        });
    }

    public async UnaryResult<Nil> AddMoney(long moneyToAdd)
    {
        var userId = Context.GetUserId();

        try
        {
            using var aucyDb = await _aucyDbFactory.CreateDbContextAsync();

            await _inventoryProperties.DepositMoney(aucyDb, userId, moneyToAdd);

            return Nil.Default;
        }
        catch (ReturnStatusException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new ReturnStatusException(StatusCode.Internal, AuctionErrorCodes.FAILED_TO_ADD_MONEY);
        }
    }

    public async UnaryResult<Nil> RemoveMoney(long moneyToTake)
    {
        var userId = Context.GetUserId();

        try
        {
            using var aucyDb = await _aucyDbFactory.CreateDbContextAsync();

            await _inventoryProperties.DepositMoney(aucyDb, userId, moneyToTake);

            return Nil.Default;
        }
        catch (ReturnStatusException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new ReturnStatusException(StatusCode.Internal, AuctionErrorCodes.FAILED_TO_TAKE_MONEY);
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

        return await GetRetryPolicy().ExecuteAsync(async () =>
        {
            using var aucyDb = await _aucyDbFactory.CreateDbContextAsync();

            try
            {
                aucyDb.Inventories.Add(newInventory); // catch already existing inventory exception
                await aucyDb.SaveChangesAsync();

                return new InventoryDto
                (
                    money: newInventory.Money,
                    itemStacks: [],
                    capacity: newInventory.Capacity,
                    usedCapacity: newInventory.UsedCapacity,
                    weightLimit: newInventory.WeightLimit,
                    usedWeight: newInventory.UsedWeight
                );
            }
            catch (DbUpdateException ex) 
                when (ex.InnerException is PostgresException sqlEx && (sqlEx.SqlState == PostgresErrorCodes.UniqueViolation))
            {
                throw new NoRetryReturnStatusException(StatusCode.InvalidArgument, AuctionErrorCodes.INVENTORY_ALREADY_EXISTS);
            }
            catch (Exception)
            {
                throw new ReturnStatusException(StatusCode.Internal,
                    AuctionErrorCodes.FAILED_TO_CREATE_INVENTORY);
            }
        });
    }

    public async UnaryResult<Nil> RemoveItemStack(RemoveItemStackDto request)
    {
        var userId = Context.GetUserId();

        return await GetRetryPolicy().ExecuteAsync(async () =>
        {
            using var aucyDb = await _aucyDbFactory.CreateDbContextAsync();

            // Block specified Inventory and ItemStack of a SINGLE User (it`s ok because other users don`t even use it)
            // Prevent situation with concurrent removing and deleting of the same ItemStack
            // Ex.: (Quantity=10), Remove 9, Remove 10, Check 10 == 10 (true), Check 9 < 10 (True) ->
            // Apply transaction (Remove 9, Remove 10, Delete ItemStack with ONLY 1 item left (logic error))
            using var transaction = await aucyDb.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);

            try
            {
                var itemStackFields = await aucyDb.ItemStacks
                    .Where(i => i.Id == request.ItemStackId && i.InventoryId == userId && i.Quantity >= request.Quantity)
                    .Select(x => new { x.Id, x.Quantity, ConfigWeight = x.Config.Weight })
                    .FirstOrDefaultAsync() ?? throw new NoRetryReturnStatusException(StatusCode.NotFound,
                        AuctionErrorCodes.ITEM_STACK_NOT_FOUND);

                var itemStackId = itemStackFields.Id;
                int usedCapacityDiff = 0;
                var updatedItemStacks = 0;

                if (itemStackFields.Quantity == request.Quantity)
                {
                    updatedItemStacks = await aucyDb.ItemStacks.Where(x => x.Id == itemStackFields.Id && x.Quantity == request.Quantity)
                        .ExecuteDeleteAsync();

                    usedCapacityDiff = -1;
                }
                else
                {
                    updatedItemStacks = await aucyDb.ItemStacks
                        .Where(x => x.Id == itemStackId)
                        .ExecuteUpdateAsync(x => x.SetProperty(i => i.Quantity, i => i.Quantity - request.Quantity));
                }

                if (updatedItemStacks != 1)
                    throw new NoRetryReturnStatusException(StatusCode.NotFound, AuctionErrorCodes.ITEM_STACK_NOT_FOUND);

                float usedWeightDiff = 0;
                if (itemStackFields.ConfigWeight > 0)
                    usedWeightDiff = -itemStackFields.ConfigWeight * request.Quantity;

                await _inventoryProperties.ExecuteInventoryUpdate(aucyDb, userId, usedWeightDiff, usedCapacityDiff);

                await transaction.CommitAsync();

                return Nil.Default;
            }
            catch (ReturnStatusException)
            {
                await transaction.RollbackAsync();
                throw;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw new ReturnStatusException(StatusCode.Internal,
                    AuctionErrorCodes.FAILED_TO_REMOVE_ITEMSTACK);
            }
        });
    }

    private AsyncRetryPolicy GetRetryPolicy()
    {
        return Policy
            .Handle<Exception>(ex => ex is not NoRetryReturnStatusException)
            .RetryAsync(3);
    }
}
