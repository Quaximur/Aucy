using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Aucy.Application;

public class RedisStartupCleanup(
    IConnectionMultiplexer redis,
    ILogger<RedisStartupCleanup> logger)
{
    private readonly IDatabase _db = redis.GetDatabase();
    private readonly IServer _server = redis.GetServers().First(s => s.IsConnected);
    private readonly ILogger<RedisStartupCleanup> _logger = logger;

    public async Task InitCleanupAsync(CancellationToken cancellationToken)
    {
        var clearFlag = Environment.GetEnvironmentVariable("CLEAR_REDIS_ON_START"); // For Multi-node deployments
        if (clearFlag == null || !clearFlag.Equals("true", StringComparison.OrdinalIgnoreCase))
            return;

        await ClearLocksAsync(cancellationToken);
    }

    private async Task ClearLocksAsync(CancellationToken cancellationToken)
    {
        // Match all known lock key patterns
        string[] patterns =
        [
            "item_matching_*",
            "item_unary_*",
            "item_non_unary_user_*",
            "item_unary_user_*",
            "orders_*",
            "user_conns_*"
        ];

        foreach (var pattern in patterns)
        {
            var deleted = await DeleteByPatternAsync(pattern);
            _logger.LogInformation("Cleared {Count} stale lock keys matching '{Pattern}'", deleted, pattern);
        }
    }

    private async Task<int> DeleteByPatternAsync(string pattern)
    {
        var keys = _server.KeysAsync(pattern: pattern);
        int count = 0;

        await foreach (var key in keys)
        {
            await _db.KeyDeleteAsync(key);
            count++;
        }

        return count;
    }
}