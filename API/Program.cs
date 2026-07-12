using Aucy.API.Extensions;
using Aucy.Application;
using Aucy.Application.Auth;
using Aucy.Application.Helpers;
using Aucy.Application.Mappings;
using Aucy.Contracts.OnionClient;
using Aucy.Data;
using Aucy.Domain.Entities;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using System.Net;

/// Configuration:
/// 1 <= maxSellQuantity <= 5000
/// 1 <= maxBuyQuantity <= 5000
/// 1 <= minPrice <= 10^15 (both lot and bid)
/// 1 <= maxPrice <= 10^15 (both lot and bid)
/// NonUnary items pre-created on the server side
///


var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

DotNetEnv.Env.TraversePath().Load();

builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(IPAddress.Any, 7537, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
});

builder.AddDataServices();

builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

services.AddSingleton<RedisStartupCleanupService>();

var redisConnectionString =
           Environment.GetEnvironmentVariable("ConnectionStrings__Redis")
           ?? builder.Configuration.GetConnectionString("Redis");

if (string.IsNullOrWhiteSpace(redisConnectionString))
    throw new NullReferenceException("Redis connection string not found");

services.AddMagicOnion().UseRedisGroup(options =>
{
    options.ConnectionString = redisConnectionString;
}, registerAsDefault: true);

services.AddSingleton<IOrderBookHelper, OrderBookHelper>();

services.AddTransient<OrderBookWorker>();

services.AddSingleton<InventoryPropertiesHelper>();
services.AddSingleton<ItemsHelper>();
services.AddSingleton<WorkerGroupService>();

services.AddGrpc();

services.AddApiAuth();

services.AddAutoMapper(x =>
{
    x.AddProfile<ItemConfigMapping>();
    x.AddProfile<LotMapping>();
    x.AddProfile<BidMapping>();
});

if (builder.Environment.IsDevelopment())
{
    //services.AddSwaggerGen();
}


var app = builder.Build();

{
    using IServiceScope startupScope = app.Services.CreateScope();

    var cleanup = startupScope.ServiceProvider.GetRequiredService<RedisStartupCleanupService>();
    await cleanup.InitCleanupAsync(CancellationToken.None);

    using AucyDbContext ctx = startupScope.ServiceProvider.GetRequiredService<AucyDbContext>();

    if (app.Environment.IsDevelopment())
    {
        //app.UseSwagger();
        //app.UseSwaggerUI();

        ctx.Database.Migrate();

        app.MapGroup("/dev").MapGet("/token/{userId}", (IJwtProvider jwtProvider, Guid userId) =>
        {
            return jwtProvider.GenerateToken(userId);
        });
    }

    await InitializeItemConfigs(ctx);
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapMagicOnionService<InventoryService>();
app.MapMagicOnionService<ItemSearchService>();
app.MapMagicOnionService<ItemListingService>();
app.MapMagicOnionService<OrderHub>();

app.MapGrpcService<WorkerEventPublisherService>(); // TODO: mTLS

app.Run();

async Task InitializeItemConfigs(AucyDbContext ctx)
{
    var defaultItems = new[]
    {
        ItemConfig.Create(
            id: Guid.Parse("00000000-0000-0000-0000-000000000001"),
            name: "bread",
            isUnary: false,
            icon: "bread",
            grade: null,
            rarity: 0,
            weight: 0.1f,
            baseType: "consumable",
            extra: null,
            categoryPath: "food",
            minPrice: 1,
            maxPrice: 11,
            maxSellQuantity: 5000,
            maxBuyQuantity: 5000
        ),

        ItemConfig.Create(
            id: Guid.Parse("00000000-0000-0000-0000-000000000002"),
            name: "iron_ore",
            isUnary: false,
            icon: "iron_ore",
            grade: null,
            rarity: null,
            weight: 1.2f,
            baseType: "iron_ore",
            extra: null,
            categoryPath: "materials/ore",
            minPrice: 10,
            maxPrice: 40,
            maxSellQuantity: 5000,
            maxBuyQuantity: 5000
        ),

        ItemConfig.Create(
            id: Guid.Parse("00000000-0000-0000-0000-000000000003"),
            name: "iron_ore_uncommon",
            isUnary: false,
            icon: "iron_ore",
            grade: null,
            rarity: 1,
            weight: 1.2f,
            baseType: "iron_ore",
            extra: null,
            categoryPath: "materials/ore",
            minPrice: 30,
            maxPrice: 92,
            maxSellQuantity: 5000,
            maxBuyQuantity: 5000
        ),

        ItemConfig.Create(
            id: Guid.Parse("00000000-0000-0000-0000-000000000004"),
            name: "iron_ore_rare",
            isUnary: false,
            icon: "iron_ore",
            grade: null,
            rarity: 2,
            weight: 1.2f,
            baseType: "iron_ore",
            extra: null,
            categoryPath: "materials/ore",
            minPrice: 90,
            maxPrice: 235,
            maxSellQuantity: 5000,
            maxBuyQuantity: 5000
        ),

        ItemConfig.Create(
            id: Guid.Parse("00000000-0000-0000-0000-000000000005"),
            name: "silverleaf_herb",
            isUnary: false,
            icon: "silverleaf_herb",
            grade: null,
            rarity: null,
            weight: 0.05f,
            baseType: "silverleaf_herb",
            extra: null,
            categoryPath: "materials/herbs",
            minPrice: 100,
            maxPrice: 550,
            maxSellQuantity: 1000,
            maxBuyQuantity: 1000
        ),

        ItemConfig.Create(
            id: Guid.Parse("00000000-0000-0000-0000-000000000006"),
            name: "wolf_fang",
            isUnary: false,
            icon: "wolf_fang",
            grade: null,
            rarity: null,
            weight: 0.15f,
            baseType: "wolf_fang",
            extra: null,
            categoryPath: "loot/beast",
            minPrice: 60,
            maxPrice: 188,
            maxSellQuantity: 5000,
            maxBuyQuantity: 5000
        ),

        ItemConfig.Create(
            id: Guid.Parse("00000000-0000-0000-0000-000000000007"),
            name: "mana_crystal",
            isUnary: false,
            icon: "mana_crystal",
            grade: null,
            rarity: null,
            weight: 0.1f,
            baseType: "mana_crystal",
            extra: null,
            categoryPath: "materials/magic",
            minPrice: 1000,
            maxPrice: 3355,
            maxSellQuantity: 10,
            maxBuyQuantity: 10
        ),

        ItemConfig.Create(
            id: Guid.Parse("00000000-0000-0000-0000-000000000008"),
            name: "ancient_coin",
            isUnary: false,
            icon: "ancient_coin",
            grade: null,
            rarity: null,
            weight: 0.02f,
            baseType: "ancient_coin",
            extra: null,
            categoryPath: "treasure",
            minPrice: 1,
            maxPrice: 555,
            maxSellQuantity: 100,
            maxBuyQuantity: 100
        ),

        ItemConfig.Create(
            id: Guid.Parse("00000000-0000-0000-0000-000000000009"),
            name: "dirt",
            isUnary: false,
            icon: "dirt",
            grade: null,
            rarity: null,
            weight: 0.1f,
            baseType: "dirt",
            extra: null,
            categoryPath: "materials/nature",
            minPrice: 10,
            maxPrice: 200,
            maxSellQuantity: 5000,
            maxBuyQuantity: 5000
        ),
    };

    foreach (var item in defaultItems)
    {
        if (!await ctx.ItemConfigs.AnyAsync(x => x.Id == item.Id))
        {
            ctx.ItemConfigs.Add(item);
            Console.WriteLine($"Added item config: {item.Name} with ID: {item.Id}");
        }
        else
        {
            Console.WriteLine($"Item config already exists: {item.Name} with ID: {item.Id}");
        }
    }

    await ctx.SaveChangesAsync();

    foreach (var item in defaultItems)
    {
        var orderBookWorker = app.Services.GetRequiredService<OrderBookWorker>();
        orderBookWorker.RunAsync(item.Id);
    }
}