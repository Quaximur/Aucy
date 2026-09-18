using Aucy.Data;
using Aucy.Domain.Entities;
using Aucy.Workers.OrderBook;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Aucy.Workers;

public static class WorkerBoot
{
    const string DEFAULT_ITEMS_FILE = "default_items.json";

    public static async Task InitItemsProcessing(IHost host)
    {
        var defaultItems = await LoadItemConfigsAsync();

        {
            using IServiceScope startupScope = host.Services.CreateScope();
            using AucyDbContext aucyDb = startupScope.ServiceProvider.GetRequiredService<AucyDbContext>();
            var logger = startupScope.ServiceProvider.GetRequiredService<ILogger>();

            await EnsureItemsCreatedAsync(defaultItems, aucyDb, logger);
        }

        foreach (var item in defaultItems)
        {
            var orderBookWorker = host.Services.GetRequiredService<OrderBookWorker>();
            orderBookWorker.RunAsync(item.Id);
        }
    }

    private static async Task<ItemConfig[]> LoadItemConfigsAsync()
    {
        var jsonPath = Path.Combine(AppContext.BaseDirectory, "Configs", DEFAULT_ITEMS_FILE);
        var json = await File.ReadAllTextAsync(jsonPath);

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        var defaultItems = JsonSerializer.Deserialize<ItemConfig[]>(json, jsonOptions)
                   ?? throw new InvalidOperationException($"{DEFAULT_ITEMS_FILE} is empty or invalid");

        return defaultItems;
    }

    private static async Task EnsureItemsCreatedAsync(IReadOnlyCollection<ItemConfig> defaultItems, AucyDbContext aucyDb, ILogger logger)
    {
        foreach (var item in defaultItems)
        {
            if (!await aucyDb.ItemConfigs.AnyAsync(x => x.Id == item.Id))
            {
                aucyDb.ItemConfigs.Add(item);
                logger.LogInformation("Added item config: {ItemName} with ID: {ItemId}", item.Name, item.Id);
            }
            else
            {
                logger.LogInformation("Item config already exists: {ItemName} with ID: {ItemId}", item.Name, item.Id);
            }
        }

        await aucyDb.SaveChangesAsync();
    }
}
