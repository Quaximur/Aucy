using Aucy.API.Extensions;
using Aucy.Application;
using Aucy.Application.Auth;
using Aucy.Application.Helpers;
using Aucy.Application.Mappings;
using Aucy.Data;
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

if (app.Environment.IsDevelopment())
{
    //app.UseSwagger();
    //app.UseSwaggerUI();

    using IServiceScope s = app.Services.CreateScope();

    using AucyDbContext ctx = s.ServiceProvider.GetRequiredService<AucyDbContext>();

    ctx.Database.Migrate();

    app.MapGroup("/dev").MapGet("/token/{userId}", (IJwtProvider jwtProvider, Guid userId) =>
    {
        return jwtProvider.GenerateToken(userId);
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapMagicOnionService<OrderBookService>();
app.MapMagicOnionService<InventoryService>();
app.MapMagicOnionService<ItemSearchService>();

// TEST Worker
var orderBookWorker = app.Services.GetRequiredService<OrderBookWorker>();
var breadId = new Guid(1, 0, 0, new byte[8]);
orderBookWorker.RunAsync(breadId);

app.Run();
