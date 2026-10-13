using System;
using System.Text.Json.Serialization;

namespace Aucy.Tests.Integration;

public sealed record TradingSimulationUser(
    [property: JsonPropertyName("userId")] Guid UserId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("password")] string Password);

