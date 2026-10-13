using Aucy.Application.Helpers;
using Aucy.Workers.OrderBook;

namespace Aucy.Workers.Extensions;

public static class WorkerDI
{
    public static void AddWorkerStuff(this IHostApplicationBuilder builder)
    {
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
    }
}
