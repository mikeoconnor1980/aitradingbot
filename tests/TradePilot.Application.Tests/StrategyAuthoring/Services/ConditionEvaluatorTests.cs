using Microsoft.Extensions.Logging;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Services;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Tests.StrategyAuthoring.Services;

[TestClass]
public sealed class ConditionEvaluatorTests
{
    private const long CandleTimestamp = 1_000_000;

    private Mock<ILogger<ConditionEvaluator>> _loggerMock = default!;
    private ConditionEvaluator _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<ConditionEvaluator>>();
        var handlers = new IConditionHandler[]
        {
            new RsiConditionHandler(),
            new PriceVsEmaConditionHandler(new Mock<ILogger<PriceVsEmaConditionHandler>>().Object)
        };
        _sut = new ConditionEvaluator(handlers, _loggerMock.Object);
    }

    [TestMethod]
    public void GivenSignalModeRsiLt40_WhenRsi35_ThenSetupDetected()
    {
        var config = CreateSignalConfig(EntryLogic.All, CreateRsiCondition("lt", 40m));
        var context = CreateMarketContextWithIndicators(rsiPeriod: 14, currentRsi: 35m);

        var result = _sut.Evaluate(config, context);

        result.SetupDetected.Should().BeTrue();
        result.ConditionResults.Should().ContainSingle();
    }

    [TestMethod]
    public void GivenSignalModeRsiLt40_WhenRsi45_ThenNoSetup()
    {
        var config = CreateSignalConfig(EntryLogic.All, CreateRsiCondition("lt", 40m));
        var context = CreateMarketContextWithIndicators(rsiPeriod: 14, currentRsi: 45m);

        var result = _sut.Evaluate(config, context);

        result.SetupDetected.Should().BeFalse();
    }

    [TestMethod]
    public void GivenEntryLogicAll_WhenRsiPassesAndUnknownType_ThenSetupDetected()
    {
        var rsiCondition = CreateRsiCondition("lt", 40m);
        var unknownCondition = CreateUnknownCondition();
        var config = CreateSignalConfig(EntryLogic.All, rsiCondition, unknownCondition);
        var context = CreateMarketContextWithIndicators(rsiPeriod: 14, currentRsi: 35m);

        var result = _sut.Evaluate(config, context);

        result.SetupDetected.Should().BeTrue();
        result.ConditionResults.Should().HaveCount(2);
        result.ConditionResults.Should().ContainSingle(r => r.ConditionId == "unknown-1" && r.Passed);
        VerifyWarningLogged("No handler registered for condition type");
    }

    [TestMethod]
    public void GivenEntryLogicAny_WhenRsiFails_ThenNoSetup()
    {
        var config = CreateSignalConfig(EntryLogic.Any, CreateRsiCondition("lt", 40m));
        var context = CreateMarketContextWithIndicators(rsiPeriod: 14, currentRsi: 45m);

        var result = _sut.Evaluate(config, context);

        result.SetupDetected.Should().BeFalse();
    }

    [TestMethod]
    public void GivenEntryLogicAny_WhenOneConditionPasses_ThenSetupDetected()
    {
        var config = CreateSignalConfig(
            EntryLogic.Any,
            CreateRsiCondition("lt", 40m, id: "rsi-fail"),
            CreateRsiCondition("gt", 30m, id: "rsi-pass"));
        var context = CreateMarketContextWithIndicators(rsiPeriod: 14, currentRsi: 35m);

        var result = _sut.Evaluate(config, context);

        result.SetupDetected.Should().BeTrue();
        result.OverallReason.Should().Contain("conditions passed (any mode)");
    }

    [TestMethod]
    public void GivenDisabledConditionOnly_WhenEntryLogicAll_ThenNoSetup()
    {
        var disabledCondition = CreateRsiCondition("lt", 40m) with { Enabled = false };
        var config = CreateSignalConfig(EntryLogic.All, disabledCondition);
        var context = CreateMarketContextWithIndicators(rsiPeriod: 14, currentRsi: 35m);

        var result = _sut.Evaluate(config, context);

        result.SetupDetected.Should().BeFalse();
        result.OverallReason.Should().Contain("No enabled");
    }

    [TestMethod]
    public void GivenNoEntryConditions_WhenEvaluated_ThenNoSetup()
    {
        var config = new StrategyConfig
        {
            StrategyMode = StrategyMode.Signal,
            StrategyName = "Test",
            Market = "BTC-USD",
            EntryLogic = EntryLogic.All,
            EntryConditions = null,
            Risk = new RiskConfig { PositionSizeValue = 100m }
        };
        var context = CreateMarketContextWithIndicators(rsiPeriod: 14, currentRsi: 35m);

        var result = _sut.Evaluate(config, context);

        result.SetupDetected.Should().BeFalse();
        result.ConditionResults.Should().BeEmpty();
    }

    [TestMethod]
    public void GivenMissingIndicatorContext_WhenEvaluated_ThenNoSetup()
    {
        var config = CreateSignalConfig(EntryLogic.All, CreateRsiCondition("lt", 40m));
        var context = CreateMarketContext(indicatorContext: null);

        var result = _sut.Evaluate(config, context);

        result.SetupDetected.Should().BeFalse();
        result.OverallReason.Should().Contain("Indicator context not available");
    }

    [TestMethod]
    public void GivenCrossAboveRsi_WhenPreviousBelowCurrentAbove_ThenSetupDetected()
    {
        var config = CreateSignalConfig(EntryLogic.All, CreateRsiCondition("cross_above", 30m));
        var context = CreateMarketContextWithIndicators(rsiPeriod: 14, currentRsi: 32m, previousRsi: 28m);

        var result = _sut.Evaluate(config, context);

        result.SetupDetected.Should().BeTrue();
    }

    [TestMethod]
    public void GivenPriceVsEmaNearCondition_WhenPriceIsNearEma_ThenSetupDetected()
    {
        var config = CreateSignalConfig(
            EntryLogic.All,
            new EntryConditionConfig
            {
                Id = "ema-1",
                Enabled = true,
                Type = EntryConditionType.PriceVsEma,
                Label = "Price near EMA(50)",
                Params = new PriceVsEmaParams
                {
                    Period = 50,
                    Operator = "near",
                    DistanceType = "percent",
                    DistanceValue = 0.5m,
                }
            });
        var context = CreateMarketContextWithIndicators(rsiPeriod: 14, currentRsi: 32m, previousRsi: 28m, close: 100.2m);
        context.IndicatorContext!.SetEma(50, 100m);

        var result = _sut.Evaluate(config, context);

        result.SetupDetected.Should().BeTrue();
        result.ConditionResults.Should().ContainSingle();
    }

    [TestMethod]
    public void GivenUnknownConditionsOnly_WhenEvaluated_ThenSetupDetected()
    {
        var config = CreateSignalConfig(EntryLogic.All, CreateUnknownCondition());
        var context = CreateMarketContextWithIndicators(rsiPeriod: 14, currentRsi: 35m);

        var result = _sut.Evaluate(config, context);

        result.SetupDetected.Should().BeTrue();
        result.OverallReason.Should().Be("All conditions skipped (unknown types).");
    }

    private void VerifyWarningLogged(string messageFragment)
    {
        _loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((value, _) => value.ToString()!.Contains(messageFragment)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    private static StrategyConfig CreateSignalConfig(EntryLogic logic, params EntryConditionConfig[] conditions)
    {
        return new StrategyConfig
        {
            StrategyMode = StrategyMode.Signal,
            StrategyName = "Test Signal Strategy",
            Market = "BTC-USD",
            EntryLogic = logic,
            EntryConditions = conditions.ToList(),
            Risk = new RiskConfig { PositionSizeValue = 100m }
        };
    }

    private static EntryConditionConfig CreateRsiCondition(string op, decimal value, int period = 14, string id = "rsi-1")
    {
        return new EntryConditionConfig
        {
            Id = id,
            Enabled = true,
            Type = EntryConditionType.Rsi,
            Label = $"RSI({period})",
            Params = new RsiParams
            {
                Period = period,
                Operator = op,
                Value = value
            }
        };
    }

    private static EntryConditionConfig CreateUnknownCondition()
    {
        return new EntryConditionConfig
        {
            Id = "unknown-1",
            Enabled = true,
            Type = EntryConditionType.Unknown,
            Label = "Unknown",
            Params = new UnknownConditionParams()
        };
    }

    private static MarketContext CreateMarketContextWithIndicators(
        int rsiPeriod = 14,
        decimal currentRsi = 50m,
        decimal? previousRsi = null,
        decimal close = 102m)
    {
        var indicatorContext = new IndicatorContext();
        indicatorContext.SetRsi(rsiPeriod, currentRsi, previousRsi);

        return CreateMarketContext(indicatorContext, close);
    }

    private static MarketContext CreateMarketContext(IndicatorContext? indicatorContext, decimal close = 102m)
    {
        return new MarketContext
        {
            Symbol = "BTC-USD",
            TimestampUtc = CandleTimestamp,
            CurrentCandle = CreateCandle(close),
            Indicators = new IndicatorSnapshot(),
            IndicatorContext = indicatorContext
        };
    }

    private static Candle CreateCandle(decimal close)
    {
        return Candle.Create(
            "Binance",
            "BTC-USD",
            "15m",
            CandleTimestamp,
            100m,
            105m,
            95m,
            close,
            1_000m,
            10);
    }
}