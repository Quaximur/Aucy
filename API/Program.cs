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

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

DotNetEnv.Env.TraversePath().Load();

builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(IPAddress.Any, 8765, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
});

builder.AddDataServices();

builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

services.AddMagicOnion();
services.AddSingleton<IOrderBookHelper, OrderBookHelper>();

services.AddTransient<OrderBookWorker>();

services.AddSingleton<InventoryPropertiesHelper>();
services.AddSingleton<ItemsHelper>();
services.AddSingleton<WorkerGroupService>();

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
    using IServiceScope s = app.Services.CreateScope();

    using AucyDbContext ctx = s.ServiceProvider.GetRequiredService<AucyDbContext>();

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

//app.MapMagicOnionService<OrderBookService>();
app.MapMagicOnionService<InventoryService>();
//app.MapMagicOnionService<InventoryLayoutService>();
app.MapMagicOnionService<ItemSearchService>();
app.MapMagicOnionService<ItemListingService>();
app.MapMagicOnionService<OrderHub>();

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
            categoryPath: "food"
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
            categoryPath: "materials/ore"
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
            categoryPath: "materials/ore"
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
            categoryPath: "materials/ore"
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
            categoryPath: "materials/herbs"
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
            categoryPath: "loot/beast"
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
            categoryPath: "materials/magic"
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
            categoryPath: "treasure"
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