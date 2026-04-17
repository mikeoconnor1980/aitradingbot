using System.Text.Json;
using Microsoft.Extensions.Logging;
using TradePilot.AI.Services;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.StrategyAuthoring.Models;

namespace TradePilot.AI.Tests.Services;

[TestClass]
public sealed class StrategyInterpreterTests
{
    private Mock<ILlmClient> _llmClientMock = default!;
    private Mock<ILogger<StrategyInterpreter>> _loggerMock = default!;
    private StrategyInterpreter _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _llmClientMock = new Mock<ILlmClient>();
        _loggerMock = new Mock<ILogger<StrategyInterpreter>>();
        _sut = new StrategyInterpreter(_llmClientMock.Object, _loggerMock.Object);
    }

    [TestMethod]
    public async Task GivenValidRsiResponse_WhenInterpretAsync_ThenReturnsSignalModeWithRsiCondition()
    {
        _llmClientMock
            .Setup(client => client.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSignalResponseJson());

        var result = await _sut.InterpretAsync("Buy ETH when RSI drops below 30 with 2% take profit", CancellationToken.None);

        result.Config.StrategyMode.Should().Be(StrategyMode.Signal);
        result.Config.EntryConditions.Should().ContainSingle();
        result.Config.EntryConditions![0].Type.Should().Be(EntryConditionType.Rsi);
        result.Config.EntryConditions[0].Params.Should().BeOfType<RsiParams>();
        ((RsiParams)result.Config.EntryConditions[0].Params!).Value.Should().Be(30m);
        result.Confidence.Should().Be(0.85m);
        result.Config.Source.Should().NotBeNull();
        result.Config.Source!.EntryPoint.Should().Be(StrategyEntryPoint.NaturalLanguage);
        result.Config.Source.SourceText.Should().Be("Buy ETH when RSI drops below 30 with 2% take profit");
    }

    [TestMethod]
    public async Task GivenValidGridResponse_WhenInterpretAsync_ThenReturnsGridMode()
    {
        _llmClientMock
            .Setup(client => client.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGridResponseJson());

        var result = await _sut.InterpretAsync("Set up a 5-level grid on BTC with 0.5% spacing", CancellationToken.None);

        result.Config.StrategyMode.Should().Be(StrategyMode.Grid);
        result.Config.Grid.Should().NotBeNull();
        result.Config.Grid!.Levels.Should().Be(5);
        result.Config.Grid.Spacing.Should().Be(0.5m);
        result.Config.EntryConditions.Should().BeNull();
    }

    [TestMethod]
    public async Task GivenMalformedLlmResponse_WhenInterpretAsync_ThenReturnsClarificationNeeded()
    {
        _llmClientMock
            .Setup(client => client.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("not valid json");

        var result = await _sut.InterpretAsync("trade BTC", CancellationToken.None);

        result.Confidence.Should().Be(0m);
        result.ClarificationNeeded.Should().Contain("Unable to interpret");
    }

    [TestMethod]
    public async Task GivenNumericAssumptionValue_WhenInterpretAsync_ThenCoercesAssumptionToString()
    {
        _llmClientMock
            .Setup(client => client.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSignalResponseJsonWithNumericAssumption());

        var result = await _sut.InterpretAsync("Buy ETH when RSI drops below 40", CancellationToken.None);

        result.Assumptions.Should().ContainSingle();
        result.Assumptions[0].AssumedValue.Should().Be("40");
    }

    [TestMethod]
    public async Task GivenLlmException_WhenInterpretAsync_ThenReturnsGracefulError()
    {
        _llmClientMock
            .Setup(client => client.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Service unavailable"));

        var result = await _sut.InterpretAsync("Buy BTC", CancellationToken.None);

        result.Confidence.Should().Be(0m);
        result.ClarificationNeeded.Should().Contain("temporarily unavailable");
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
            assumptions = new[]
            {
                new { fieldName = "RSI Period", assumedValue = "14", reason = "Standard default" },
            },
            clarificationNeeded = (string?)null,
        };

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    private static string CreateGridResponseJson()
    {
        var response = new
        {
            config = new
            {
                schemaVersion = 1,
                strategyMode = "grid",
                strategyName = "BTC Grid",
                exchange = "Hyperliquid",
                market = "BTC",
                timeframe = "15m",
                direction = "long",
                enabled = true,
                templateId = (string?)null,
                grid = new
                {
                    levels = 5,
                    spacing = 0.5m,
                    entryMode = "auto_from_signal_candle",
                    anchorPrice = (decimal?)null,
                    breakdownThreshold = 2m,
                },
                trendFilter = (object?)null,
                entryLogic = (string?)null,
                entryConditions = (object?)null,
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
            confidence = 0.8m,
            assumptions = Array.Empty<object>(),
            clarificationNeeded = (string?)null,
        };

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    private static string CreateSignalResponseJsonWithNumericAssumption()
    {
        var response = new
        {
            config = new
            {
                schemaVersion = 1,
                strategyMode = "signal",
                strategyName = "ETH RSI Buy",
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
                        @params = new { period = 14, @operator = "lt", value = 40 },
                    },
                },
                exit = new
                {
                    takeProfit = new { enabled = true, type = "fixed_percent", value = 3m, lookback = (int?)null },
                    stopLoss = new { enabled = true, type = "fixed_percent", value = 5m, lookback = (int?)null },
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
            confidence = 0.9m,
            assumptions = new[]
            {
                new { fieldName = "rsi.value", assumedValue = 40, reason = "Derived from the user's threshold" },
            },
            clarificationNeeded = (string?)null,
        };

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }
}