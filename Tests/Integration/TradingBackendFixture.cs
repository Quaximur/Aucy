using Aucy.Application;
using Aucy.Application.Helpers;
using Aucy.Application.Mappings;
using Aucy.Contracts.OnionClient;
using Aucy.Data;
using Aucy.Domain.Entities;
using Aucy.Workers.OrderBook;
using MagicOnion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace Aucy.Tests.Integration;

public sealed class TradingBackendFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .WithDatabase("aucy_tests")
        .WithUsername("aucy")
        .WithPassword("aucy")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7.4-alpine")
        .Build();

    public IServiceProvider Services { get; private set; } = null!;
    public IReadOnlyList<TradingSimulationUser> Users { get; private set; } = [];
    public IReadOnlyList<ItemConfig> ItemConfigs { get; private set; } = [];

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _redis.StartAsync();

        Environment.SetEnvironmentVariable("ConnectionStrings__AucyDbContext_Postgres", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", _redis.GetConnectionString());

        var users = new List<TradingSimulationUser>();
        for (var i = 0; i < 10000; i++)
        {
            var iString = i.ToString();
            users.Add(new TradingSimulationUser(UserId: Guid.NewGuid(), Name: iString, Password: iString));
        }

        Users = users;

        var builder = Host.CreateApplicationBuilder();

        builder.AddDataServices();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .WriteTo.Console()
            .CreateLogger();

        builder.Services.AddSerilog((services, loggerConfig) => loggerConfig
           .ReadFrom.Configuration(builder.Configuration)
           .ReadFrom.Services(services));

        builder.Services.AddSingleton<InventoryPropertiesHelper>();
        builder.Services.AddSingleton<ItemsHelper>();
        builder.Services.AddSingleton<IOrderBookHelper, OrderBookHelper>();
        builder.Services.AddSingleton<IPriceTierQuantizer, PriceTierQuantizer>();
        builder.Services.AddSingleton<WorkerGroupNotifier>();
        builder.Services.AddSingleton<OrderBookWorker>();

        var redisConnectionString =
              Environment.GetEnvironmentVariable("ConnectionStrings__Redis")
              ?? builder.Configuration.GetConnectionString("Redis");

        if (string.IsNullOrWhiteSpace(redisConnectionString))
            throw new NullReferenceException("Redis connection string not found");

        builder.Services.AddMagicOnion().UseRedisGroup(options =>
            {
                options.ConnectionString = redisConnectionString;
            }, registerAsDefault: true);

        builder.Services.AddAutoMapper(x =>
        {
            x.AddProfile<ItemConfigMapping>();
            x.AddProfile<LotMapping>();
            x.AddProfile<BidMapping>();
            x.AddProfile<ItemStackMapping>();
        });

        Services = builder.Build().Services;

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AucyDbContext>();

        await db.Database.MigrateAsync();
        ItemConfigs = await TradingSimulationSeeder.SeedWithEmptyInventoriesAsync(db, Users);
    }

    public async Task DisposeAsync()
    {
        if (Services is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else if (Services is IDisposable disposable)
            disposable.Dispose();

        await _redis.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
