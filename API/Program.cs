using Aucy.API.Extensions;
using Aucy.Application;
using Aucy.Application.Auth;
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

services.AddMagicOnion();

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

app.MapMagicOnionService<AuctionService>();
app.MapMagicOnionService<InventoryService>();

app.Run();
