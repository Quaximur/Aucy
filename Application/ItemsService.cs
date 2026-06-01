using Aucy.Data;
using Aucy.Domain.Entities;
using AutoMapper;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;

namespace Aucy.Application;

public class ItemsService(AucyDbContext aucyDbContext,
    InventoryPropertiesService inventoryPropertiesService,
    IMapper mapper)
{
    private readonly AucyDbContext _aucyDb = aucyDbContext;
    private readonly InventoryPropertiesService _inventoryPropertiesService = inventoryPropertiesService;
    private readonly IMapper _mapper = mapper;

    public async Task<int> UpsertItemStack(Guid itemConfigId, int quantity, Guid inventoryId)
    {
        // Try to update an existing ItemStack with the same ConfigId or create new one if not found
        var usedCapacityDiff = 0;

        var updatedItemStacks = await _aucyDb.ItemStacks
                .Where(i => i.InventoryId == inventoryId && i.ConfigId == itemConfigId)
                .ExecuteUpdateAsync(x => x.SetProperty(i => i.Quantity, i => i.Quantity + quantity));

        if (updatedItemStacks == 0) // If not found, create a new ItemStack
        {
            var itemStack = ItemStack.Create(itemConfigId, quantity, inventoryId);

            await _aucyDb.ItemStacks.AddAsync(itemStack);

            usedCapacityDiff = 1;
        }
        else if (updatedItemStacks != 1)
        {
            throw new RpcException(new Status(StatusCode.NotFound,
                $"ItemStack is not determined. Entries found: {updatedItemStacks}"));
        }

        return usedCapacityDiff;
    }
}
