using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Aucy.Tests.Integration;

public static class TradingSimulationConfig
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<IReadOnlyList<TradingSimulationUser>> LoadUsersAsync(CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "trading-simulation-users.json");

        await using var stream = File.OpenRead(path);
        var users = await JsonSerializer.DeserializeAsync<List<TradingSimulationUser>>(stream, JsonOptions, cancellationToken);

        if (users is not { Count: 1000 })
            throw new InvalidOperationException($"Expected exactly 1000 simulation users in {path}.");

        return users;
    }
}

