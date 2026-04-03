using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TradingApp.Api.Tests.Infrastructure;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.StrategyAuthoring.Models;

namespace TradingApp.Api.Tests.Controllers;

[TestClass]
public sealed class InterpretStrategyTests : BaseControllerTests
{
    private const string BaseUrl = "/api/strategies/interpret";
    private const string TestPrivateKey = "0x4c0883a69102937d6231471b5dbb6204fe512961708279f2a4c5890a0c1f9b2e";

    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"tradingapp-interpret-tests-{Guid.NewGuid():N}.db");

    private Mock<ILlmClient> _llmClientMock = default!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Hyperliquid:PrivateKey", TestPrivateKey);
        builder.UseSetting("Hyperliquid:BaseUrl", "https://api.hyperliquid-testnet.xyz");
        builder.UseSetting("Hyperliquid:Network", "testnet");
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_databasePath}");
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<IHostedService>();
        services.RemoveAll<ILlmClient>();

        _llmClientMock = new Mock<ILlmClient>();
        _llmClientMock
            .Setup(client => client.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSignalResponseJson());

        services.AddSingleton(_llmClientMock.Object);
    }

    [TestMethod]
    public async Task GivenValidText_WhenInterpretStrategy_ThenReturns200WithResult()
    {
        var client = GetTestClient();

        var response = await client.PostAsJsonAsync(BaseUrl, new { text = "Buy ETH when RSI drops below 30 with 2% take profit" });

        var result = await response.ReadAndAssertSuccessAsync<StrategyIntentDto>();
        result.Confidence.Should().Be(0.85m);
        result.Config.StrategyMode.Should().Be(StrategyMode.Signal);
        result.Config.Source.Should().NotBeNull();
        result.Config.Source!.SourceText.Should().Be("Buy ETH when RSI drops below 30 with 2% take profit");
        _llmClientMock.Verify(
            client => client.CompleteAsync(It.IsAny<string>(), "Buy ETH when RSI drops below 30 with 2% take profit", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenEmptyText_WhenInterpretStrategy_ThenReturns400()
    {
        var client = GetTestClient();

        var response = await client.PostAsJsonAsync(BaseUrl, new { text = "   " });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _llmClientMock.Verify(
            client => client.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GivenTextExceeding500Chars_WhenInterpretStrategy_ThenReturns400()
    {
        var client = GetTestClient();

        var response = await client.PostAsJsonAsync(BaseUrl, new { text = new string('a', 501) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _llmClientMock.Verify(
            client => client.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GivenElevenRapidRequests_WhenInterpretStrategy_ThenEleventhReturns429()
    {
        var client = GetTestClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.10");

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var successResponse = await client.PostAsJsonAsync(BaseUrl, new { text = $"Interpret request {attempt}" });
            successResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var throttledResponse = await client.PostAsJsonAsync(BaseUrl, new { text = "Interpret request 10" });

        throttledResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        throttledResponse.Headers.RetryAfter.Should().NotBeNull();

        var body = await throttledResponse.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorCode").GetString().Should().Be("rate_limit");
        _llmClientMock.Verify(
            client => client.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(10));
    }

    private static string CreateSignalResponseJson()
    {
        var response = new
        {
            config = new
            {
                schemaVersion = 1,
                strategyMode = "signal",
                strategyName = "ETH RSI Dip Buy",
                exchange = "Hyperliquid",
                market = "ETH",
                timeframe = "15m",
                direction = "long",
                enabled = true,
                templateId = (string?)null,
                grid = (object?)null,
                trendFilter = (object?)null,
                entryLogic = "all",
                entryConditions = new[]
                {
                    new
                    {
                        id = "cond-1",
                        enabled = true,
                        type = "rsi",
                        label = "RSI Oversold",
                        @params = new { period = 14, @operator = "lt", value = 30 },
                    },
                },
                exit = new
                {
                    takeProfit = new { enabled = true, type = "fixed_percent", value = 2m, lookback = (int?)null },
                    stopLoss = new { enabled = true, type = "fixed_percent", value = 1.5m, lookback = (int?)null },
                    exitOnOppositeSignal = false,
                },
                risk = new
                {
                    positionSizeType = "percent_wallet",
                    positionSizeValue = 10m,
                    leverage = 1m,
                    maxOpenTrades = 1,
                    cooldownValue = 0,
                    cooldownUnit = "candles",
                    allowSameCandleReentry = false,
                },
                metadata = (object?)null,
                source = (object?)null,
            },
            confidence = 0.85m,
            assumptions = Array.Empty<object>(),
            clarificationNeeded = (string?)null,
        };

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }
}