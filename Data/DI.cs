using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace Aucy.Data;

public static class DI
{
    public static void AddDataServices(this IHostApplicationBuilder builder)
    {
        var dbContextConnectionString =
           Environment.GetEnvironmentVariable("ConnectionStrings__AucyDbContext_MS")
           ?? builder.Configuration.GetConnectionString("AucyDbContext");

        builder.Services.AddDbContext<AucyDbContext>(options =>
        {
            options.UseSqlServer(dbContextConnectionString);
            //options.UseNpgsql(connectionString);
        }, optionsLifetime: ServiceLifetime.Singleton);

        builder.Services.AddDbContextFactory<AucyDbContext>(options =>
        {
            options.UseSqlServer(dbContextConnectionString);
        });

        var redisConnectionString =
           Environment.GetEnvironmentVariable("ConnectionStrings__Redis")
           ?? builder.Configuration.GetConnectionString("Redis");

        if (string.IsNullOrWhiteSpace(redisConnectionString))
            throw new NullReferenceException("Redis connection string not found");

        builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConnectionString));

        builder.Services.AddSingleton<RedisDistributedSynchronizationProvider>(sp =>
        {
            var connection = sp.GetRequiredService<IConnectionMultiplexer>();
            return new RedisDistributedSynchronizationProvider(connection.GetDatabase());
        });

        builder.Services.AddSingleton<IDistributedLockProvider>(sp =>
            sp.GetRequiredService<RedisDistributedSynchronizationProvider>());

        builder.Services.AddSingleton<IDistributedReaderWriterLockProvider>(sp =>
            sp.GetRequiredService<RedisDistributedSynchronizationProvider>());
    }
}
