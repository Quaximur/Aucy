using Aucy.API.Extensions;
using Aucy.Application;
using Aucy.Application.Auth;
using Aucy.Application.Helpers;
using Aucy.Application.Mappings;
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

services.AddTransient<OrderBookWorker>();

services.AddScoped<InventoryPropertiesService>();
services.AddScoped<ItemsService>();

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

app.MapMagicOnionService<OrderBookService>();
app.MapMagicOnionService<InventoryService>();
app.MapMagicOnionService<ItemSearchService>();

// TEST Worker
var orderBookWorker = app.Services.GetRequiredService<OrderBookWorker>();
var breadId = Guid.Parse("00000000-0000-0000-0000-000000000001");
orderBookWorker.RunAsync(breadId);

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
            name: "milk",
            isUnary: false,
            icon: "milk",
            grade: null,
            rarity: 1,
            weight: 0.2f,
            baseType: "consumable",
            extra: null,
            categoryPath: "food/dairy"
        ),
        ItemConfig.Create(
            id: Guid.Parse("00000000-0000-0000-0000-000000000003"),
            name: "iron_sword",
            isUnary: true,
            icon: "iron_sword",
            grade: 1,
            rarity: 2,
            weight: 1.2f,
            baseType: "weapon",
            extra: null,
            categoryPath: "weapons/melee"
        )
    };

    foreach (var item in defaultItems)
    {
        if (!await ctx.ItemConfigs.AnyAsync(x => x.Id == item.Id))
        {
            await ctx.ItemConfigs.AddAsync(item);
            Console.WriteLine($"Added item config: {item.Name} with ID: {item.Id}");
        }
        else
        {
            Console.WriteLine($"Item config already exists: {item.Name} with ID: {item.Id}");
        }
    }

    await ctx.SaveChangesAsync();
}