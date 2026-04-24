using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TradePilot.Api.Tests.Infrastructure;
using TradePilot.Application.Agent.Models;
using TradePilot.Application.Agent.Services;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Serialization;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;

namespace TradePilot.Api.Tests.Controllers;

[TestClass]
public sealed class AgentControllerTests : BaseControllerTests
{
    private const string AgentId = "worker-1";

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<AgentCommandStore>();
        services.AddSingleton(new AgentCommandStore());
    }

    [TestMethod]
    public async Task GivenRunningAssignedStrategy_WhenIdleHeartbeat_ThenReturnsAutoResumeStartCommand()
    {
        var strategy = await SeedRunningStrategyAsync();
        var client = GetTestClient(authenticate: false);

        var response = await client.PostAsJsonAsync("api/agent/heartbeat", new AgentHeartbeat
        {
            AgentId = AgentId,
            State = AgentState.Idle,
            MachineName = "test-machine",
            TimestampUtc = DateTimeOffset.UtcNow,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        payload.Should().NotBeNull();

        var commands = payload!["pendingCommands"]!.AsArray();
        commands.Should().HaveCount(1);
        commands[0]!["strategyId"]!.GetValue<Guid>().Should().Be(strategy.Id);
    }

    [TestMethod]
    public async Task GivenStoppedStrategy_WhenIdleHeartbeat_ThenDoesNotAutoResume()
    {
        await SeedStoppedStrategyAsync();
        var client = GetTestClient(authenticate: false);

        var response = await client.PostAsJsonAsync("api/agent/heartbeat", new AgentHeartbeat
        {
            AgentId = AgentId,
            State = AgentState.Idle,
            MachineName = "test-machine",
            TimestampUtc = DateTimeOffset.UtcNow,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        payload.Should().NotBeNull();
        payload!["pendingCommands"]!.AsArray().Should().BeEmpty();
    }

    private async Task<Strategy> SeedRunningStrategyAsync()
    {
        await using var db = CreateTestDbContext();
        var config = CreateGridConfig();
        var strategy = Strategy.Create(
            "dev-user",
            config.StrategyName,
            "GridStrategy",
            JsonSerializer.Serialize(config, StrategyJsonOptions.Default));

        strategy.AssignToAgentAndStart(AgentId);
        db.Strategies.Add(strategy);
        await db.SaveChangesAsync();
        return strategy;
    }

    private async Task SeedStoppedStrategyAsync()
    {
        await using var db = CreateTestDbContext();
        var config = CreateGridConfig();
        var strategy = Strategy.Create(
            "dev-user",
            config.StrategyName,
            "GridStrategy",
            JsonSerializer.Serialize(config, StrategyJsonOptions.Default));

        strategy.AssignToAgentAndStart(AgentId);
        strategy.StopLiveTrading();
        db.Strategies.Add(strategy);
        await db.SaveChangesAsync();
    }

    private static StrategyConfig CreateGridConfig() => new()
    {
        SchemaVersion = 1,
        StrategyMode = StrategyMode.Grid,
        StrategyName = "BTC Grid",
        Exchange = "Hyperliquid",
        Market = "BTC-USD",
        Timeframe = "15m",
        Direction = Direction.Long,
        Grid = new GridConfig
        {
            Levels = 5,
            Spacing = 0.5m,
            BreakdownThreshold = 2m,
        },
        Exit = new ExitConfig(),
        Risk = new RiskConfig
        {
            PositionSizeType = PositionSizeType.FixedNotional,
            PositionSizeValue = 100m,
            Leverage = 1m,
            MaxOpenTrades = 1,
        },
    };
}
