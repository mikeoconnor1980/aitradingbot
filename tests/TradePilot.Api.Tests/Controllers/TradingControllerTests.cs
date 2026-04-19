using System.Text.Json;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TradePilot.Api.Controllers;
using TradePilot.Api.Tests.Infrastructure;
using TradePilot.Application.Agent.Models;
using TradePilot.Application.Agent.Services;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Serialization;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;
using TradePilot.Domain.Trading;
using TradePilot.Persistence;

namespace TradePilot.Api.Tests.Controllers;

[TestClass]
public sealed class TradingControllerTests : BaseControllerTests
{
    private const string AgentId = "worker-1";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Hyperliquid:PrivateKey", "0x4c0883a69102937d6231471b5dbb6204fe512961708279f2a4c5890a0c1f9b2e");
        builder.UseSetting("Hyperliquid:BaseUrl", "https://api.hyperliquid-testnet.xyz");
        builder.UseSetting("Hyperliquid:Network", "testnet");
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<IHostedService>();

        var store = new AgentCommandStore();
        store.ProcessHeartbeat(new AgentHeartbeat
        {
            AgentId = AgentId,
            State = AgentState.Idle,
            MachineName = "test-machine",
            TimestampUtc = DateTimeOffset.UtcNow,
        });

        services.RemoveAll<AgentCommandStore>();
        services.AddSingleton(store);
    }

    [TestMethod]
    public async Task GivenDcaStrategy_WhenStartTrading_ThenQueuesCommand()
    {
        var strategy = await SeedStrategyAsync(CreateDcaConfig(), "DcaStrategy");
        var client = GetTestClient();

        var response = await client.PostAsJsonAsync(
            $"api/trading/{AgentId}/start",
            new StartTradingRequest(strategy.Id));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        using var scope = GetCurrentFactory().Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<AgentCommandStore>();
        var pendingCommands = store.GetPendingCommands(AgentId);
        var db = scope.ServiceProvider.GetRequiredService<TradePilotDbContext>();
        var updatedStrategy = await db.Strategies.FindAsync(strategy.Id);

        pendingCommands.Should().ContainSingle();
        pendingCommands[0].Type.Should().Be(AgentCommandType.Start);
        pendingCommands[0].StrategyConfig!.StrategyMode.Should().Be(StrategyMode.Dca);
        pendingCommands[0].StrategyId.Should().Be(strategy.Id);
        updatedStrategy.Should().NotBeNull();
        updatedStrategy!.IsRunning.Should().BeTrue();
        updatedStrategy.AssignedAgentId.Should().Be(AgentId);
    }

    [TestMethod]
    public async Task GivenGridStrategy_WhenStartTrading_ThenQueuesCommand()
    {
        var strategy = await SeedStrategyAsync(CreateGridConfig(), "GridStrategy");
        var client = GetTestClient();

        var response = await client.PostAsJsonAsync(
            $"api/trading/{AgentId}/start",
            new StartTradingRequest(strategy.Id));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        using var scope = GetCurrentFactory().Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<AgentCommandStore>();
        var pendingCommands = store.GetPendingCommands(AgentId);

        pendingCommands.Should().ContainSingle();
        pendingCommands[0].Type.Should().Be(AgentCommandType.Start);
        pendingCommands[0].StrategyConfig!.StrategyMode.Should().Be(StrategyMode.Grid);
        pendingCommands[0].StrategyId.Should().Be(strategy.Id);
    }

    [TestMethod]
    public async Task GivenRunningAssignedStrategy_WhenStopTrading_ThenClearsPersistedRunningStateAndQueuesStop()
    {
        var strategy = await SeedStrategyAsync(CreateGridConfig(), "GridStrategy");

        using (var seedDb = CreateTestDbContext())
        {
            var tracked = await seedDb.Strategies.FindAsync(strategy.Id);
            tracked.Should().NotBeNull();
            tracked!.AssignToAgentAndStart(AgentId);
            await seedDb.SaveChangesAsync();
        }

        var client = GetTestClient();
        var response = await client.PostAsJsonAsync($"api/trading/{AgentId}/stop", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        using var scope = GetCurrentFactory().Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<AgentCommandStore>();
        var pendingCommands = store.GetPendingCommands(AgentId);
        var db = scope.ServiceProvider.GetRequiredService<TradePilotDbContext>();
        var updatedStrategy = await db.Strategies.FindAsync(strategy.Id);

        pendingCommands.Should().ContainSingle();
        pendingCommands[0].Type.Should().Be(AgentCommandType.Stop);
        updatedStrategy.Should().NotBeNull();
        updatedStrategy!.IsRunning.Should().BeFalse();
        updatedStrategy.AssignedAgentId.Should().BeNull();
    }

    private async Task<Strategy> SeedStrategyAsync(StrategyConfig config, string strategyType)
    {
        await using var db = CreateTestDbContext();
        var strategy = Strategy.Create(
            "dev-user",
            config.StrategyName,
            strategyType,
            JsonSerializer.Serialize(config, StrategyJsonOptions.Default));

        db.Strategies.Add(strategy);
        await db.SaveChangesAsync();

        return strategy;
    }

    private WebApplicationFactory<Program> GetCurrentFactory()
    {
        var factoryField = typeof(BaseControllerTests)
            .GetField("_factory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var factory = (WebApplicationFactory<Program>?)factoryField?.GetValue(this);
        factory.Should().NotBeNull();
        return factory!;
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

    private static StrategyConfig CreateDcaConfig() => new()
    {
        SchemaVersion = 1,
        StrategyMode = StrategyMode.Dca,
        StrategyName = "BTC DCA",
        Exchange = "Hyperliquid",
        AssetType = AssetType.Spot,
        Market = "BTC-USD",
        Timeframe = "1h",
        Direction = Direction.Long,
        Dca = new DcaConfig
        {
            Interval = DcaInterval.Hourly,
            TimeOfDayUtc = "00:00",
            BaseAmountUsd = 100m,
            Allocations =
            [
                new DcaAllocation
                {
                    Market = "BTC-USD",
                    WeightPercent = 100m,
                }
            ],
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