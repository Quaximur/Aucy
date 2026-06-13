using Aucy.Data;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;

namespace Aucy.Application.Helpers;

public class InventoryPropertiesService(AucyDbContext aucyDbContext)
{
    private readonly AucyDbContext _aucyDb = aucyDbContext;

    public async Task ExecuteInventoryUpdate(Guid userId, float weightDiff, int capacityDiff)
    {
        var addUsedCapacity = capacityDiff != 0;
        var hasWeight = weightDiff != 0;

        if (hasWeight || addUsedCapacity)
        {
            var inventoryUpdateQuery = _aucyDb.Inventories
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
                throw new RpcException(new Status(StatusCode.NotFound, "Inventory is not determined. Can`t update properties"));
        }
    }

    public async Task WithdrawMoney(Guid userId, long amount)
    {
        var updatedRows = await _aucyDb.Inventories
            .Where(x => x.Id == userId && x.Money >= amount)
            .ExecuteUpdateAsync(x => x.SetProperty(i => i.Money, i => i.Money - amount));

        if (updatedRows != 1)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Not enough money"));
    }

    public async Task DepositMoney(Guid userId, long amount)
    {
        var updatedRows = await _aucyDb.Inventories
            .Where(x => x.Id == userId)
            .ExecuteUpdateAsync(x => x.SetProperty(i => i.Money, i => i.Money + amount));

        if (updatedRows != 1)
            throw new RpcException(new Status(StatusCode.NotFound, "Inventory is not determined. Can`t add money"));
    }
}
