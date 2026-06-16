using Aucy.Data;
using Aucy.Domain.Entities;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;

namespace Aucy.Application.Helpers;

public class ItemsHelper()
{
    public async Task<int> UpdateOrInsertItemStack(AucyDbContext aucyDb, Guid itemConfigId, int quantity, Guid inventoryId)
    {
        // Try to update an existing ItemStack with the same ConfigId or create new one if not found
        var usedCapacityDiff = 0;

        var updatedItemStacks = await aucyDb.ItemStacks
                .Where(i => i.InventoryId == inventoryId && i.ConfigId == itemConfigId)
                .ExecuteUpdateAsync(x => x.SetProperty(i => i.Quantity, i => i.Quantity + quantity));

        if (updatedItemStacks == 0) // If not found, create a new ItemStack
        {
            var itemStack = ItemStack.Create(itemConfigId, quantity, inventoryId);

            aucyDb.ItemStacks.Add(itemStack);
            await aucyDb.SaveChangesAsync();

            usedCapacityDiff = 1;
        }
        else if (updatedItemStacks != 1)
        {
            throw new RpcException(new Status(StatusCode.NotFound,
                $"ItemStack is not determined. Entries found: {updatedItemStacks}"));
        }

        return usedCapacityDiff;
    }

    public async Task<int> UpdateOrDeleteItemStack(AucyDbContext aucyDb, Guid itemConfigId, int quantity, Guid inventoryId)
    {
        // Reduce the quantity or remove the ItemStack
        var usedCapacityDiff = 0;

        var updatedItemStacks = await aucyDb.ItemStacks
                .Where(x => x.ConfigId == itemConfigId && x.InventoryId == inventoryId && x.Quantity > quantity)
                .ExecuteUpdateAsync(x => x.SetProperty(i => i.Quantity, i => i.Quantity - quantity));

        if (updatedItemStacks == 0)
        {
            updatedItemStacks = await aucyDb.ItemStacks
                 .Where(x => x.ConfigId == itemConfigId && x.InventoryId == inventoryId && x.Quantity == quantity)
                 .ExecuteDeleteAsync();

            usedCapacityDiff = -1;
        }

        if (updatedItemStacks != 1)
        {
            throw new RpcException(new Status(StatusCode.NotFound,
                $"ItemStack with specified quantity is not determined. Entries found: {updatedItemStacks}"));
        }

        return usedCapacityDiff;
    }
}
