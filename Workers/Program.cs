using Aucy.Application.Helpers;
using Aucy.Data;
using Aucy.Domain.Entities;
using Aucy.Workers.OrderBook;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting workers...");

    var builder = Host.CreateApplicationBuilder(args);

    DotNetEnv.Env.TraversePath().Load();

    builder.Services.AddSerilog((services, loggerConfig) => loggerConfig
       .ReadFrom.Configuration(builder.Configuration)
       .ReadFrom.Services(services));

    builder.AddDataServices();

    builder.Services.AddSingleton<WorkerGroupNotifier>();
    builder.Services.AddSingleton<InventoryPropertiesHelper>();

    builder.Services.AddTransient<OrderBookWorker>();
    builder.Services.AddSingleton<IPriceTierQuantizer, PriceTierQuantizer>();

    var redisConnectionString =
               Environment.GetEnvironmentVariable("ConnectionStrings__Redis")
               ?? builder.Configuration.GetConnectionString("Redis");

    if (string.IsNullOrWhiteSpace(redisConnectionString))
        throw new NullReferenceException("Redis connection string not found");

    builder.Services.AddMagicOnion().UseRedisGroup(options =>
    {
        options.ConnectionString = redisConnectionString;
    }, registerAsDefault: true);

    var host = builder.Build();

    {
        using IServiceScope startupScope = host.Services.CreateScope();
        using AucyDbContext ctx = startupScope.ServiceProvider.GetRequiredService<AucyDbContext>();

        await InitializeItemConfigs(ctx);
    }

    host.Run();

    async Task InitializeItemConfigs(AucyDbContext ctx)
    {
        var defaultItems = new[]
        {
            ItemConfig.Create(
                id: Guid.Parse("00000000-0000-0000-0000-000000000001"),
                name: "bread",
                isUnary: false,
                icon: "bread",
                grade: 0,
                rarity: 0,
                weight: 0.1f,
                baseType: "consumable",
                extra: null,
                categoryPath: "food",
                minPrice: 50,
                maxPrice: 1000,
                maxSellQuantity: 5000,
                maxBuyQuantity: 5000,
                priceTierCount: 20
            ),

            ItemConfig.Create(
                id: Guid.Parse("00000000-0000-0000-0000-000000000002"),
                name: "iron_ore",
                isUnary: false,
                icon: "iron_ore",
                grade: 0,
                rarity: 0,
                weight: 1.2f,
                baseType: "iron_ore",
                extra: null,
                categoryPath: "materials/ore",
                minPrice: 50,
                maxPrice: 1000,
                maxSellQuantity: 5000,
                maxBuyQuantity: 5000,
                priceTierCount: 20
            ),

            ItemConfig.Create(
                id: Guid.Parse("00000000-0000-0000-0000-000000000003"),
                name: "iron_ore_uncommon",
                isUnary: false,
                icon: "iron_ore",
                grade: 0,
                rarity: 1,
                weight: 1.2f,
                baseType: "iron_ore",
                extra: null,
                categoryPath: "materials/ore",
                minPrice: 50,
                maxPrice: 1000,
                maxSellQuantity: 5000,
                maxBuyQuantity: 5000,
                priceTierCount: 20
            ),

            ItemConfig.Create(
                id: Guid.Parse("00000000-0000-0000-0000-000000000004"),
                name: "iron_ore_rare",
                isUnary: false,
                icon: "iron_ore",
                grade: 0,
                rarity: 2,
                weight: 1.2f,
                baseType: "iron_ore",
                extra: null,
                categoryPath: "materials/ore",
                minPrice: 50,
                maxPrice: 1000,
                maxSellQuantity: 5000,
                maxBuyQuantity: 5000,
                priceTierCount: 20
            ),

            ItemConfig.Create(
                id: Guid.Parse("00000000-0000-0000-0000-000000000005"),
                name: "silverleaf_herb",
                isUnary: false,
                icon: "silverleaf_herb",
                grade: 0,
                rarity: 0,
                weight: 0.05f,
                baseType: "silverleaf_herb",
                extra: null,
                categoryPath: "materials/herbs",
                minPrice: 50,
                maxPrice: 1000,
                maxSellQuantity: 1000,
                maxBuyQuantity: 1000,
                priceTierCount: 20
            ),

            ItemConfig.Create(
                id: Guid.Parse("00000000-0000-0000-0000-000000000006"),
                name: "wolf_fang",
                isUnary: false,
                icon: "wolf_fang",
                grade: 0,
                rarity: 0,
                weight: 0.15f,
                baseType: "wolf_fang",
                extra: null,
                categoryPath: "loot/beast",
                minPrice: 50,
                maxPrice: 1000,
                maxSellQuantity: 5000,
                maxBuyQuantity: 5000,
                priceTierCount: 20
            ),

            ItemConfig.Create(
                id: Guid.Parse("00000000-0000-0000-0000-000000000007"),
                name: "mana_crystal",
                isUnary: false,
                icon: "mana_crystal",
                grade: 0,
                rarity: 0,
                weight: 0.1f,
                baseType: "mana_crystal",
                extra: null,
                categoryPath: "materials/magic",
                minPrice: 50,
                maxPrice: 1000,
                maxSellQuantity: 10,
                maxBuyQuantity: 10,
                priceTierCount: 20
            ),

            ItemConfig.Create(
                id: Guid.Parse("00000000-0000-0000-0000-000000000008"),
                name: "ancient_coin",
                isUnary: false,
                icon: "ancient_coin",
                grade: 0,
                rarity: 0,
                weight: 0.02f,
                baseType: "ancient_coin",
                extra: null,
                categoryPath: "treasure",
                minPrice: 50,
                maxPrice: 1000,
                maxSellQuantity: 100,
                maxBuyQuantity: 100,
                priceTierCount: 20
            ),

            ItemConfig.Create(
                id: Guid.Parse("00000000-0000-0000-0000-000000000009"),
                name: "dirt",
                isUnary: false,
                icon: "dirt",
                grade: 0,
                rarity: 0,
                weight: 0.1f,
                baseType: "dirt",
                extra: null,
                categoryPath: "materials/nature",
                minPrice: 50,
                maxPrice: 1000,
                maxSellQuantity: 5000,
                maxBuyQuantity: 5000,
                priceTierCount: 20
            ),
        };

        var logger = host.Services.GetRequiredService<ILogger<Program>>();

        foreach (var item in defaultItems)
        {
            if (!await ctx.ItemConfigs.AnyAsync(x => x.Id == item.Id))
            {
                ctx.ItemConfigs.Add(item);
                logger.LogInformation("Added item config: {ItemName} with ID: {ItemId}", item.Name, item.Id);
            }
            else
            {
                logger.LogInformation("Item config already exists: {ItemName} with ID: {ItemId}", item.Name, item.Id);
            }
        }

        await ctx.SaveChangesAsync();

        foreach (var item in defaultItems)
        {
            var orderBookWorker = host.Services.GetRequiredService<OrderBookWorker>();
            orderBookWorker.RunAsync(item.Id);
        }
    }
}
catch (Exception e)
{
    Log.Fatal(e, "Workers terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}