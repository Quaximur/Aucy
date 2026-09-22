using Aucy.Application.Constants;
using Aucy.Data;
using Grpc.Core;
using MagicOnion;
using Microsoft.EntityFrameworkCore;

namespace Aucy.Application.Helpers;

public class InventoryPropertiesHelper()
{
    public async Task ExecuteInventoryUpdate(AucyDbContext aucyDb, Guid userId, float weightDiff, int capacityDiff)
    {
        var addUsedCapacity = capacityDiff != 0;
        var hasWeight = weightDiff != 0;

        if (hasWeight || addUsedCapacity)
        {
            var inventoryUpdateQuery = aucyDb.Inventories
                .Where(x => x.Id == userId);

            int updatedRows = 0;

            if (hasWeight && !addUsedCapacity)
            {
                updatedRows = await inventoryUpdateQuery.ExecuteUpdateAsync(x =>
                    x.SetProperty(i => i.UsedWeight, i => i.UsedWeight + weightDiff));
            }
            else if (!hasWeight)
            {
                updatedRows = await inventoryUpdateQuery.ExecuteUpdateAsync(x =>
                    x.SetProperty(i => i.UsedCapacity, i => i.UsedCapacity + capacityDiff));
            }
            else
            {
                updatedRows = await inventoryUpdateQuery.ExecuteUpdateAsync(x =>
                    x.SetProperty(i => i.UsedWeight, i => i.UsedWeight + weightDiff)
                        .SetProperty(i => i.UsedCapacity, i => i.UsedCapacity + capacityDiff));
            }

            if (updatedRows != 1)
                throw new ReturnStatusException(StatusCode.NotFound, AuctionErrorCodes.INVENTORY_NOT_FOUND);
        }
    }

    public async Task WithdrawMoney(AucyDbContext aucyDb, Guid userId, long amount)
    {
        var updatedRows = await aucyDb.Inventories
            .Where(x => x.Id == userId && x.Money >= amount)
            .ExecuteUpdateAsync(x => x.SetProperty(i => i.Money, i => i.Money - amount));

        if (updatedRows != 1)
            throw new ReturnStatusException(StatusCode.InvalidArgument, AuctionErrorCodes.INSUFFICIENT_MONEY);
    }

    public async Task DepositMoney(AucyDbContext aucyDb, Guid userId, long amount)
    {
        var updatedRows = await aucyDb.Inventories
            .Where(x => x.Id == userId)
            .ExecuteUpdateAsync(x => x.SetProperty(i => i.Money, i => i.Money + amount));

        if (updatedRows != 1)
            throw new ReturnStatusException(StatusCode.NotFound, AuctionErrorCodes.INVENTORY_NOT_FOUND);
    }
}
