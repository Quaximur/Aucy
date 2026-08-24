using Aucy.API.Extensions;
using Aucy.Application;
using Aucy.Application.Helpers;
using Aucy.Application.Mappings;
using Aucy.Contracts.OnionClient;
using Aucy.Data;
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
    x.AddProfile<ItemStackMapping>();
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
        ctx.Database.Migrate();
    }
}

app.UseAuthentication();
app.UseAuthorization();

app.MapMagicOnionService<InventoryService>();
app.MapMagicOnionService<ItemSearchService>();
app.MapMagicOnionService<ItemListingService>();
app.MapMagicOnionService<OrderHub>();

//app.MapGrpcService<WorkerEventPublisherService>();

app.Run();
