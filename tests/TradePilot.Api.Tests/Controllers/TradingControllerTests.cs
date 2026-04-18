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
using TradePilot.Domain.Enums;
using TradePilot.Domain.Trading;

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
    public async Task GivenDcaStrategy_WhenStartTrading_ThenReturnsBadRequest()
    {
        var client = GetTestClient();

        var response = await client.PostAsJsonAsync(
            $"api/trading/{AgentId}/start",
            new StartTradingRequest(CreateDcaConfig()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(BaseControllerTestsJson.Options);
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("Unsupported live strategy");
        problem.Detail.Should().Be("Live DCA spot execution is not implemented yet. Use backtesting for DCA strategies.");
    }

    [TestMethod]
    public async Task GivenGridStrategy_WhenStartTrading_ThenQueuesCommand()
    {
        var client = GetTestClient();

        var response = await client.PostAsJsonAsync(
            $"api/trading/{AgentId}/start",
            new StartTradingRequest(CreateGridConfig()));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        using var scope = GetCurrentFactory().Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<AgentCommandStore>();
        var pendingCommands = store.GetPendingCommands(AgentId);

        pendingCommands.Should().ContainSingle();
        pendingCommands[0].Type.Should().Be(AgentCommandType.Start);
        pendingCommands[0].StrategyConfig!.StrategyMode.Should().Be(StrategyMode.Grid);
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