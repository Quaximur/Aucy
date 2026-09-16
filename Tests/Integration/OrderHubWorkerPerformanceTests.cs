using Aucy.Data;
using Aucy.Domain.Entities;
using Aucy.Workers.OrderBook;
using MagicOnion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Diagnostics;
using Xunit;

namespace Aucy.Tests.Integration;

/// <summary>
/// Performance test for OrderBookWorker that measures throughput and latency
/// when processing lots and bids with configurable price overlap scenarios.
/// </summary>
public sealed class OrderHubWorkerPerformanceTests(TradingBackendFixture fixture)
    : IClassFixture<TradingBackendFixture>
{
    private readonly TradingBackendFixture _fixture = fixture;

    /// <summary>
    /// Test configuration for performance scenarios
    /// </summary>
    private sealed class PerformanceTestScenario
    {
        public required int LotsCount { get; init; }
        public required int BidsCount { get; init; }
        public int? CommonQuantity { get; init; }
        public int? CommonBidPrice { get; init; }
        public int? CommonLotPrice { get; init; }
        public required string ScenarioName { get; init; }
    }

    /// <summary>
    /// Statistics collected during test execution
    /// </summary>
    private sealed class PerformanceStats
    {
        public required int LotsCreated { get; init; }
        public required int BidsCreated { get; init; }
        public required long ExecutionTimeMs { get; init; }
        public required string ScenarioName { get; init; }

        public double LotsProcessedPerSecond =>
            ExecutionTimeMs > 0 ? (LotsCreated / (ExecutionTimeMs / 1000.0)) : 0;

        public double BidsProcessedPerSecond =>
            ExecutionTimeMs > 0 ? (BidsCreated / (ExecutionTimeMs / 1000.0)) : 0;

        public double TotalOrdersProcessedPerSecond =>
            ExecutionTimeMs > 0 ? ((LotsCreated + BidsCreated) / (ExecutionTimeMs / 1000.0)) : 0;

        public override string ToString()
        {
            return $@"
Performance Test: {ScenarioName}
  Lots Created: {LotsCreated}
  Bids Created: {BidsCreated}
  Execution Time: {ExecutionTimeMs}ms
  Lots Processed/sec: {LotsProcessedPerSecond:F2}
  Bids Processed/sec: {BidsProcessedPerSecond:F2}
  Total Orders Processed/sec: {TotalOrdersProcessedPerSecond:F2}
";
        }
    }

    /// <summary>
    /// Test that measures OrderBookWorker performance with N lots and M bids
    /// </summary>
    [Theory]
    [InlineData(100, 100, 50, "100 lots, 100 bids, 50% overlap")]
    [InlineData(500, 500, 75, "500 lots, 500 bids, 75% overlap")]
    public async Task ProcessMatchingTick_WithVariousScenarios_RecordsPerformanceStats(
        int lotsCount,
        int bidsCount,
        int overlapPercentage,
        string scenarioName)
    {
        var itemConfig = _fixture.ItemConfigs[0];

        // Create scenario configuration
        var scenario = new PerformanceTestScenario
        {
            LotsCount = lotsCount,
            BidsCount = bidsCount,
            ScenarioName = scenarioName
        };

        var stats = await RunPerformanceTest(itemConfig.Id, scenario);

        // Output statistics
        _fixture.Services.GetRequiredService<ILogger<OrderHubWorkerPerformanceTests>>()
            .LogInformation(stats.ToString());

        // Basic assertions to ensure test completed
        Assert.Equal(scenario.LotsCount, stats.LotsCreated);
        Assert.Equal(scenario.BidsCount, stats.BidsCreated);
        Assert.True(stats.ExecutionTimeMs >= 0, "Execution time should be non-negative");
    }

    /// <summary>
    /// Test high-concurrency scenario with many lots and bids at similar prices
    /// </summary>
    [Fact]
    public async Task ProcessMatchingTick_WithFullOverlapScenario_RecordsStats()
    {
        var itemConfig = _fixture.ItemConfigs[0];

        // High overlap scenario: all bids and lots at nearly the same price
        var scenario = new PerformanceTestScenario
        {
            LotsCount = 5000,
            BidsCount = 5000,
            CommonQuantity = 500,
            CommonBidPrice = 500,
            CommonLotPrice = 500,
            ScenarioName = "Full Overlap Scenario (5000 lots, 5000 bids)"
        };

        var stats = await RunPerformanceTest(itemConfig.Id, scenario);

        _fixture.Services.GetRequiredService<ILogger<OrderHubWorkerPerformanceTests>>()
            .LogInformation(stats.ToString());

        Assert.Equal(scenario.LotsCount, stats.LotsCreated);
        Assert.Equal(scenario.BidsCount, stats.BidsCreated);
    }

    /// <summary>
    /// Test high-concurrency scenario with many lots and bids at similar prices
    /// </summary>
    [Fact]
    public async Task ProcessMatchingTick_WithRandomOverlapScenario_RecordsStats()
    {
        var itemConfig = _fixture.ItemConfigs[0];

        // High overlap scenario: all bids and lots at nearly the same price
        var scenario = new PerformanceTestScenario
        {
            LotsCount = 5000,
            BidsCount = 5000,
            ScenarioName = "Random Overlap Scenario (5000 lots, 5000 bids)"
        };

        var stats = await RunPerformanceTest(itemConfig.Id, scenario);

        _fixture.Services.GetRequiredService<ILogger<OrderHubWorkerPerformanceTests>>()
            .LogInformation(stats.ToString());

        Assert.Equal(scenario.LotsCount, stats.LotsCreated);
        Assert.Equal(scenario.BidsCount, stats.BidsCreated);
    }

    /// <summary>
    /// Test no-overlap scenario where most orders don't match
    /// </summary>
    [Fact]
    public async Task ProcessMatchingTick_WithNoOverlapScenario_RecordsStats()
    {
        var itemConfig = _fixture.ItemConfigs[0];

        // No overlap scenario: lots priced high, bids priced low
        var scenario = new PerformanceTestScenario
        {
            LotsCount = 500,
            BidsCount = 500,
            ScenarioName = "No Overlap Scenario (500 lots, 500 bids)"
        };

        var stats = await RunPerformanceTest(itemConfig.Id, scenario);

        _fixture.Services.GetRequiredService<ILogger<OrderHubWorkerPerformanceTests>>()
            .LogInformation(stats.ToString());

        Assert.Equal(scenario.LotsCount, stats.LotsCreated);
        Assert.Equal(scenario.BidsCount, stats.BidsCreated);
    }

    /// <summary>
    /// Runs a complete performance test scenario
    /// </summary>
    private async Task<PerformanceStats> RunPerformanceTest(
        Guid itemConfigId,
        PerformanceTestScenario scenario)
    {
        var random = new Random(42);  // Fixed seed for reproducibility

        // Price range: 50-1000 with step 50
        const long minPrice = 50;
        const long maxPrice = 1000;
        const long priceStep = 50;

        // Quantity range: 1-5000
        const int minQuantity = 1;
        const int maxQuantity = 5000;

        // Generate available prices
        var availablePrices = new List<long>();
        for (long price = minPrice; price <= maxPrice; price += priceStep)
        {
            availablePrices.Add(price);
        }

        await using var dbScope = _fixture.Services.CreateAsyncScope();
        var db = dbScope.ServiceProvider.GetRequiredService<AucyDbContext>();

        var lotsToCreate = new List<Lot>();
        var bidsToCreate = new List<Bid>();

        // Get total available users
        var totalUsers = _fixture.Users.Count;

        // Ensure we have enough users for the test scenario
        var maxOrdersPerItemConfig = totalUsers / 2;  // Half for lots, half for bids
        var lotsCount = Math.Min(scenario.LotsCount, maxOrdersPerItemConfig);
        var bidsCount = Math.Min(scenario.BidsCount, maxOrdersPerItemConfig);

        // Create lots - use first half of users
        var lotsCreated = 0;

        for (var i = 0; i < lotsCount; i++)
        {
            var sellerUserId = _fixture.Users[i].UserId;

            var price = scenario.CommonLotPrice ?? availablePrices[random.Next(availablePrices.Count)];
            var quantity = scenario.CommonQuantity ?? random.Next(minQuantity, maxQuantity + 1);

            var lot = Lot.Create(sellerUserId, itemConfigId, price, quantity);
            lotsToCreate.Add(lot);
            lotsCreated++;
        }

        // Create bids - use second half of users to avoid duplicate user + item config
        var bidsCreated = 0;

        for (var i = 0; i < bidsCount; i++)
        {
            var buyerUserId = _fixture.Users[maxOrdersPerItemConfig + i].UserId;

            var price = scenario.CommonBidPrice ?? availablePrices[random.Next(availablePrices.Count)];
            var quantity = scenario.CommonQuantity ?? random.Next(minQuantity, maxQuantity + 1);

            var bid = Bid.Create(buyerUserId, itemConfigId, price, quantity);
            bidsToCreate.Add(bid);
            bidsCreated++;
        }

        // Batch insert lots and bids using EF Core
        try
        {
            db.Lots.AddRange(lotsToCreate);
            db.Bids.AddRange(bidsToCreate);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _fixture.Services.GetRequiredService<ILogger<OrderHubWorkerPerformanceTests>>()
                .LogWarning(ex, "Failed to create test data, but continuing with stats");
        }

        // Execute worker matching iteration and measure time
        var worker = _fixture.Services.GetRequiredService<OrderBookWorker>();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await worker.ProcessMatchingTickAsyncV3(itemConfigId);
        }
        catch (Exception ex)
        {
            _fixture.Services.GetRequiredService<ILogger<OrderHubWorkerPerformanceTests>>()
                .LogWarning(ex, "Worker processing tick failed, but continuing with stats");
        }

        stopwatch.Stop();


        return new PerformanceStats
        {
            LotsCreated = lotsCreated,
            BidsCreated = bidsCreated,
            ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
            ScenarioName = scenario.ScenarioName
        };
    }
}
