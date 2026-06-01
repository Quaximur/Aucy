using Aucy.Contracts.OnionContracts;
using Aucy.Data;
using Aucy.Domain.Entities;
using AutoMapper;
using Grpc.Core;
using MagicOnion;
using MagicOnion.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Claims;

namespace Aucy.Application;

[Authorize]
public class AuctionService(AucyDbContext aucyDbContext,
    InventoryPropertiesService inventoryPropertiesService,
    ItemsService itemsService,
    IMapper mapper) : ServiceBase<IAuctionService>,
#pragma warning disable CS8644 // Type does not implement interface member. Nullability of reference types in interface implemented by the base type doesn't match.
    IAuctionService
#pragma warning restore CS8644 // Type does not implement interface member. Nullability of reference types in interface implemented by the base type doesn't match.
{
    private readonly AucyDbContext _aucyDb = aucyDbContext;
    private readonly InventoryPropertiesService _inventoryProperties = inventoryPropertiesService;
    private readonly ItemsService _itemsService = itemsService;
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

        if (request.UnitPrice <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Unit price should be greater than 0"));

        if (request.TotalQuantity <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Total quantity should be greater than 0"));

        var itemConfig = request.ItemConfig;
        var itemConfigId = itemConfig.Id;

        using var transaction = await _aucyDb.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);

        try
        {
            var lotExists = await _aucyDb.Lots.AnyAsync(l =>
                l.OwnerUserId == userId &&
                l.ItemConfigId == itemConfigId);

            if (lotExists)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "You already have a lot with this item"));

            // Check items existence and quantity in inventory
            var itemStack = await _aucyDb.ItemStacks
                .Where(s => s.InventoryId == userId &&
                    s.ConfigId == itemConfigId &&
                    s.Quantity >= request.TotalQuantity)
                .Select(s => new { s.Id, s.InventoryId, s.ConfigId, s.Quantity })
                .FirstOrDefaultAsync() ?? throw new RpcException(new Status(StatusCode.InvalidArgument,
                    "There are not enough items in your inventory"));

            // Reduce the quantity or remove the ItemStack
            var usedCapacityDiff = 0;
            int updatedItemStacks = 0;

            if (itemStack.Quantity > request.TotalQuantity)
            {
                updatedItemStacks = await _aucyDb.ItemStacks
                    .Where(x => x.Id == itemStack.Id)
                    .ExecuteUpdateAsync(x => x.SetProperty(i => i.Quantity, i => i.Quantity - request.TotalQuantity));
            }
            else
            {
                updatedItemStacks = await _aucyDb.ItemStacks
                    .Where(x => x.Id == itemStack.Id)
                    .ExecuteDeleteAsync();

                usedCapacityDiff = -1;
            }

            if (updatedItemStacks != 1)
                throw new RpcException(new Status(StatusCode.NotFound,
                    $"ItemStack is not determined. Entries found: {updatedItemStacks}"));

            // Alter inventory UsedWeight
            float usedWeightDiff = 0;

            // Can be applied by parts in cycle if numbers are too big.
            if (itemConfig.Weight > 0)
                usedWeightDiff = checked(-itemConfig.Weight * request.TotalQuantity);

            await _inventoryProperties.ExecuteInventoryUpdate(userId, usedWeightDiff, usedCapacityDiff);

            // TIP: If you don`t want to create lot after instant soldout,
            // when you have to validate inventory properties
            var lot = Lot.Create(ownerUserId: userId,
                itemConfigId: itemConfigId,
                unitPrice: request.UnitPrice,
                totalQuantity: request.TotalQuantity);

            await MatchLotWithBids(lot);

            if (lot.RemainingQuantity > 0)
                await _aucyDb.Lots.AddAsync(lot);

            await _aucyDb.SaveChangesAsync();
            await transaction.CommitAsync();

            Console.WriteLine($"Created lot: {lot.Id} for user: {userId} with itemConfig: {itemConfigId}");

            return _mapper.Map<LotDto>(lot);
        }
        catch (OverflowException)
        {
            await transaction.RollbackAsync();
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Lot total width is too large"));
        }
        catch (RpcException)
        {
            await transaction.RollbackAsync();
            // TODO: try repeat
            throw;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            // TODO: try repeat
            throw new RpcException(new Status(StatusCode.Internal,
                $"An error occurred while creating the lot: {ex.Message}"));
        }
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

    public async UnaryResult<Guid> RemoveLot(RemoveLotDto request)
    {
        var userId = GetUserIdFromContext();

        var transaction = await _aucyDb.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);

        try
        {
            var lotFields = await _aucyDb.Lots
                .Where(b => b.Id == request.LotId && b.OwnerUserId == userId)
                .Select(b => new
                {
                    b.Id,
                    b.ItemConfigId,
                    ItemConfigWeight = b.ItemConfig.Weight,
                    b.TotalQuantity,
                    b.RemainingQuantity,
                    b.UnitPrice,
                    b.ExtraSoldoutMoney
                })
                .FirstOrDefaultAsync() ?? throw new RpcException(new Status(StatusCode.NotFound, "Lot doesn`t exist"));

            var deletedLots = await _aucyDb.Lots
                .Where(b => b.Id == request.LotId && b.OwnerUserId == userId)
                .ExecuteDeleteAsync();

            if (deletedLots != 1)
                throw new RpcException(new Status(StatusCode.NotFound,
                    $"Lot is not determined. Entries found: {deletedLots}"));

            var soldQuantity = lotFields.TotalQuantity - lotFields.RemainingQuantity;

            var returningMoney = checked(lotFields.UnitPrice * soldQuantity + lotFields.ExtraSoldoutMoney);
            // TODO: apply commission

            if (returningMoney > 0)
                await _inventoryProperties.DepositMoney(userId, returningMoney);

            // If some items were not sold, return them to the inventory
            if (lotFields.RemainingQuantity > 0)
            {
                // Try to update an existing ItemStack with the same ConfigId or create new one if not found
                var usedCapacityDiff = await _itemsService.UpsertItemStack(lotFields.ItemConfigId,
                    lotFields.RemainingQuantity, userId);

                // Alter inventory UsedWeight
                float usedWeightDiff = 0;

                // Can be applied by parts in cycle if numbers are too large
                if (lotFields.ItemConfigWeight > 0)
                    usedWeightDiff = checked(lotFields.ItemConfigWeight * lotFields.RemainingQuantity);

                await _inventoryProperties.ExecuteInventoryUpdate(userId, usedWeightDiff, usedCapacityDiff);
            }

            await transaction.CommitAsync();

            return request.LotId;
        }
        catch (OverflowException)
        {
            await transaction.RollbackAsync();
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Amount is too large"));
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
                $"An error occurred while removing the lot: {ex.Message}"));
        }
    }

    public async UnaryResult<BidDto> CreateBid(CreateBidDto request)
    {
        var userId = GetUserIdFromContext();

        if (request.BidPrice <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Bid price should be greater than 0"));

        if (request.TotalQuantity <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Total quantity should be greater than 0"));

        using var transaction = await _aucyDb.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);

        try
        {
            var itemConfig = await _aucyDb.ItemConfigs
                .AsTracking()
                .FirstOrDefaultAsync(x => x.Id == request.ItemConfigId)
                     ?? throw new RpcException(new Status(StatusCode.NotFound, "Item config doesn`t exist"));

            var bidExists = await _aucyDb.Bids.AnyAsync(b =>
                b.BidderUserId == userId &&
                b.ItemConfigId == request.ItemConfigId);

            if (bidExists)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "You already have a bid with this item"));

            var reservedMoney = checked(request.BidPrice * request.TotalQuantity);
            await _inventoryProperties.WithdrawMoney(userId, reservedMoney);

            var bid = Bid.Create(
                bidderUserId: userId,
                itemConfigId: request.ItemConfigId,
                bidPrice: request.BidPrice,
                totalQuantity: request.TotalQuantity);

            await MatchBidWithLots(bid, itemConfig.Weight);

            if (bid.RemainingQuantity > 0)
                await _aucyDb.Bids.AddAsync(bid);

            await _aucyDb.SaveChangesAsync();
            await transaction.CommitAsync();

            return _mapper.Map<BidDto>(bid);
        }
        catch (OverflowException)
        {
            await transaction.RollbackAsync();
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Bid total price is too large"));
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
                $"An error occurred while creating the bid: {ex.Message}"));
        }
    }

    public async UnaryResult<OwnBidsDto> GetOwnBids()
    {
        var userId = GetUserIdFromContext();

        var ownBids = await _aucyDb.Bids
            .Where(b => b.BidderUserId == userId)
            .Include(b => b.ItemConfig)
            .Select(b => _mapper.Map<OwnBidDto>(b))
            .ToListAsync();

        return new OwnBidsDto(ownBids);
    }

    public async UnaryResult<Guid> RemoveBid(RemoveBidDto request)
    {
        var userId = GetUserIdFromContext();

        var transaction = await _aucyDb.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);

        try
        {
            var bidFields = await _aucyDb.Bids
                .Where(b => b.Id == request.BidId && b.BidderUserId == userId)
                .Select(b => new
                {
                    b.Id,
                    b.ItemConfigId,
                    ItemConfigWeight = b.ItemConfig.Weight,
                    b.TotalQuantity,
                    b.RemainingQuantity,
                    b.BidPrice,
                    b.ExtraBuyoutMoney
                })
                .FirstOrDefaultAsync() ?? throw new RpcException(new Status(StatusCode.NotFound, "Bid doesn`t exist"));

            var deletedBids = await _aucyDb.Bids
                .Where(b => b.Id == request.BidId && b.BidderUserId == userId)
                .ExecuteDeleteAsync();

            if (deletedBids != 1)
                throw new RpcException(new Status(StatusCode.NotFound,
                    $"Bid is not determined. Entries found: {deletedBids}"));

            var returningMoney = checked(bidFields.BidPrice * bidFields.RemainingQuantity + bidFields.ExtraBuyoutMoney);
            if (returningMoney > 0)
                await _inventoryProperties.DepositMoney(userId, returningMoney);

            // If some items were bought
            if (bidFields.RemainingQuantity < bidFields.TotalQuantity)
            {
                var boughtQuantity = bidFields.TotalQuantity - bidFields.RemainingQuantity;

                // Try to update an existing ItemStack with the same ConfigId or create new one if not found
                var usedCapacityDiff = await _itemsService.UpsertItemStack(bidFields.ItemConfigId,
                    boughtQuantity, userId);

                // Alter inventory UsedWeight
                float usedWeightDiff = 0;

                // Can be applied by parts in cycle if numbers are too big.
                if (bidFields.ItemConfigWeight > 0)
                    usedWeightDiff = checked(bidFields.ItemConfigWeight * boughtQuantity);

                await _inventoryProperties.ExecuteInventoryUpdate(userId, usedWeightDiff, usedCapacityDiff);
            }

            await transaction.CommitAsync();

            return request.BidId;
        }
        catch (OverflowException)
        {
            await transaction.RollbackAsync();
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Returning amount is too large"));
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
                $"An error occurred while removing the bid: {ex.Message}"));
        }
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

    private async Task MatchLotWithBids(Lot lot)
    {
        // TODO: optimize. ExecuteUpdate. Take by parts mb.
        var bids = await _aucyDb.Bids
            .AsTracking()
            .Where(x =>
                x.ItemConfigId == lot.ItemConfigId &&
                x.RemainingQuantity > 0 &&
                x.BidPrice >= lot.UnitPrice)
            .OrderByDescending(x => x.BidPrice)
            .ThenBy(x => x.CreatedAt) // TODO: random order
            .ToListAsync();

        foreach (var bid in bids)
        {
            if (lot.RemainingQuantity == 0)
                break;

            var tradeQuantity = Math.Min(lot.RemainingQuantity, bid.RemainingQuantity);

            bid.RemainingQuantity -= tradeQuantity;
            lot.RemainingQuantity -= tradeQuantity;

            if (lot.UnitPrice < bid.BidPrice)
                lot.ExtraSoldoutMoney += checked((bid.BidPrice - lot.UnitPrice) * tradeQuantity);
        }
    }

    private async Task MatchBidWithLots(Bid bid, float itemWeight)
    {
        // TODO: optimize. ExecuteUpdate. Take by parts mb.
        var lots = await _aucyDb.Lots
            .AsTracking()
            .Where(x =>
                x.ItemConfigId == bid.ItemConfigId &&
                x.RemainingQuantity > 0 &&
                x.UnitPrice <= bid.BidPrice)
            .OrderBy(x => x.UnitPrice)
            .ThenBy(x => x.CreatedAt) // TODO: random order
            .ToListAsync();

        foreach (var lot in lots)
        {
            if (bid.RemainingQuantity == 0)
                break;

            var tradeQuantity = Math.Min(bid.RemainingQuantity, lot.RemainingQuantity);

            lot.RemainingQuantity -= tradeQuantity;
            bid.RemainingQuantity -= tradeQuantity;

            if (bid.BidPrice > lot.UnitPrice)
                bid.ExtraBuyoutMoney += checked((bid.BidPrice - lot.UnitPrice) * tradeQuantity);
        }
    }
}
