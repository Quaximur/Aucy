using Aucy.Data;
using Aucy.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Aucy.Tests.Integration;

public static class TradingSimulationSeeder
{
    public static async Task<IReadOnlyList<ItemConfig>> SeedAsync(
        AucyDbContext db,
        IReadOnlyList<TradingSimulationUser> users,
        int randomSeed = 17537)
    {
        var itemConfigs = CreateSimulationItemConfigs();

        db.ItemConfigs.AddRange(itemConfigs);

        var random = new Random(randomSeed);
        var inventories = new List<Inventory>(users.Count);
        var itemStacks = new List<ItemStack>(users.Count * itemConfigs.Count);

        foreach (var user in users)
        {
            long money = random.NextInt64(500_000, 10_000_000);
            var stackCount = random.Next(2, itemConfigs.Count + 1);
            var selectedItems = itemConfigs
                .OrderBy(_ => random.Next())
                .Take(stackCount)
                .ToArray();

            var usedWeight = 0f;

            foreach (var itemConfig in selectedItems)
            {
                var quantity = random.Next(1_000, 50_001);
                usedWeight += itemConfig.Weight * quantity;
                itemStacks.Add(ItemStack.Create(itemConfig.Id, quantity, user.UserId));
            }

            inventories.Add(new Inventory
            {
                Id = user.UserId,
                Money = money,
                Capacity = itemConfigs.Count + 100,
                UsedCapacity = selectedItems.Length,
                WeightLimit = 1_000_000,
                UsedWeight = usedWeight
            });
        }

        db.Inventories.AddRange(inventories);
        db.ItemStacks.AddRange(itemStacks);

        await db.SaveChangesAsync();

        return itemConfigs;
    }

    private static IReadOnlyList<ItemConfig> CreateSimulationItemConfigs()
    {
        return
        [
            CreateItem(1, "bread", "consumable", "food", 0.1f),
            CreateItem(2, "iron_ore", "iron_ore", "materials/ore", 1.2f),
            CreateItem(3, "silverleaf_herb", "silverleaf_herb", "materials/herbs", 0.05f),
            CreateItem(4, "wolf_fang", "wolf_fang", "loot/beast", 0.15f),
            CreateItem(5, "mana_crystal", "mana_crystal", "materials/magic", 0.1f),
            CreateItem(6, "ancient_coin", "ancient_coin", "treasure", 0.02f),
            CreateItem(7, "dirt", "dirt", "materials/nature", 0.1f),
            CreateItem(8, "oak_log", "oak_log", "materials/wood", 0.8f),
            CreateItem(9, "linen_cloth", "linen_cloth", "materials/cloth", 0.03f)
        ];
    }

    private static ItemConfig CreateItem(int id, string name, string baseType, string categoryPath, float weight)
    {
        return ItemConfig.Create(
            id: Guid.Parse($"00000000-0000-0000-0000-{id:000000000000}"),
            name: name,
            isUnary: false,
            icon: name,
            grade: 0,
            rarity: 0,
            weight: weight,
            baseType: baseType,
            extra: null,
            categoryPath: categoryPath,
            minPrice: 50,
            maxPrice: 1000,
            priceTierCount: 20,
            maxSellQuantity: 5000,
            maxBuyQuantity: 5000);
    }
}

