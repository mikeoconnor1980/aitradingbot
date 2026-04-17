using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Services;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Tests.StrategyAuthoring.Services;

[TestClass]
public sealed class SupportResistanceConditionHandlerTests
{
    private const long CandleTimestamp = 1_000_000;

    private SupportResistanceConditionHandler _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _sut = new SupportResistanceConditionHandler();
    }

    [TestMethod]
    public void GivenNearSupport_WhenPriceWithinTolerance_ThenPassed()
    {
        var condition = CreateCondition("near_support", tolerance: 0.5m);
        var indicators = CreateIndicatorContext(support: 100m, resistance: 110m);
        var context = CreateMarketContext(close: 100.3m);

        var result = _sut.Evaluate(condition, indicators, context);

        result.Passed.Should().BeTrue();
        result.Reason.Should().Contain("near").And.Contain("support").And.Contain("condition met");
    }

    [TestMethod]
    public void GivenNearSupport_WhenPriceOutsideTolerance_ThenFailed()
    {
        var condition = CreateCondition("near_support", tolerance: 0.5m);
        var indicators = CreateIndicatorContext(support: 100m, resistance: 110m);
        var context = CreateMarketContext(close: 102m);

        var result = _sut.Evaluate(condition, indicators, context);

        result.Passed.Should().BeFalse();
        result.Reason.Should().Contain("condition not met");
    }

    [TestMethod]
    public void GivenNearResistance_WhenPriceWithinTolerance_ThenPassed()
    {
        var condition = CreateCondition("near_resistance", tolerance: 0.5m);
        var indicators = CreateIndicatorContext(support: 90m, resistance: 100m);
        var context = CreateMarketContext(close: 99.8m);

        var result = _sut.Evaluate(condition, indicators, context);

        result.Passed.Should().BeTrue();
        result.Reason.Should().Contain("near").And.Contain("resistance").And.Contain("condition met");
    }

    [TestMethod]
    public void GivenAboveSupport_WhenPriceAbove_ThenPassed()
    {
        var condition = CreateCondition("above_support");
        var indicators = CreateIndicatorContext(support: 95m);
        var context = CreateMarketContext(close: 100m);

        var result = _sut.Evaluate(condition, indicators, context);

        result.Passed.Should().BeTrue();
        result.Reason.Should().Contain("above").And.Contain("support");
    }

    [TestMethod]
    public void GivenAboveSupport_WhenPriceBelow_ThenFailed()
    {
        var condition = CreateCondition("above_support");
        var indicators = CreateIndicatorContext(support: 105m);
        var context = CreateMarketContext(close: 100m);

        var result = _sut.Evaluate(condition, indicators, context);

        result.Passed.Should().BeFalse();
    }

    [TestMethod]
    public void GivenBelowResistance_WhenPriceBelow_ThenPassed()
    {
        var condition = CreateCondition("below_resistance");
        var indicators = CreateIndicatorContext(resistance: 110m);
        var context = CreateMarketContext(close: 105m);

        var result = _sut.Evaluate(condition, indicators, context);

        result.Passed.Should().BeTrue();
        result.Reason.Should().Contain("below").And.Contain("resistance");
    }

    [TestMethod]
    public void GivenBelowResistance_WhenPriceAbove_ThenFailed()
    {
        var condition = CreateCondition("below_resistance");
        var indicators = CreateIndicatorContext(resistance: 100m);
        var context = CreateMarketContext(close: 105m);

        var result = _sut.Evaluate(condition, indicators, context);

        result.Passed.Should().BeFalse();
    }

    [TestMethod]
    public void GivenBounceSupport_WhenWickedIntoSupportAndClosedAbove_ThenPassed()
    {
        var condition = CreateCondition("bounce_support", tolerance: 0.5m);
        var indicators = CreateIndicatorContext(support: 100m);
        // Candle with low=99.8 (in tolerance zone), close=101 (above support)
        var context = CreateMarketContext(close: 101m, low: 99.8m, high: 102m);

        var result = _sut.Evaluate(condition, indicators, context);

        result.Passed.Should().BeTrue();
        result.Reason.Should().Contain("bounce").And.Contain("support").And.Contain("condition met");
    }

    [TestMethod]
    public void GivenBounceSupport_WhenWickDidNotReachSupport_ThenFailed()
    {
        var condition = CreateCondition("bounce_support", tolerance: 0.5m);
        var indicators = CreateIndicatorContext(support: 95m);
        var context = CreateMarketContext(close: 101m, low: 99m, high: 102m);

        var result = _sut.Evaluate(condition, indicators, context);

        result.Passed.Should().BeFalse();
    }

    [TestMethod]
    public void GivenBounceResistance_WhenWickedIntoResistanceAndClosedBelow_ThenPassed()
    {
        var condition = CreateCondition("bounce_resistance", tolerance: 0.5m);
        var indicators = CreateIndicatorContext(resistance: 110m);
        // Candle with high=109.6 (in tolerance zone), close=108 (below resistance)
        var context = CreateMarketContext(close: 108m, low: 107m, high: 109.6m);

        var result = _sut.Evaluate(condition, indicators, context);

        result.Passed.Should().BeTrue();
        result.Reason.Should().Contain("bounce").And.Contain("resistance").And.Contain("condition met");
    }

    [TestMethod]
    public void GivenNoSupportLevel_WhenEvaluated_ThenFailed()
    {
        var condition = CreateCondition("near_support");
        var indicators = new IndicatorContext(); // No levels set
        var context = CreateMarketContext(close: 100m);

        var result = _sut.Evaluate(condition, indicators, context);

        result.Passed.Should().BeFalse();
        result.Reason.Should().Contain("No support level found");
    }

    [TestMethod]
    public void GivenNoResistanceLevel_WhenEvaluated_ThenFailed()
    {
        var condition = CreateCondition("near_resistance");
        var indicators = new IndicatorContext();
        var context = CreateMarketContext(close: 100m);

        var result = _sut.Evaluate(condition, indicators, context);

        result.Passed.Should().BeFalse();
        result.Reason.Should().Contain("No resistance level found");
    }

    [TestMethod]
    public void GivenUnknownOperator_WhenEvaluated_ThenFailed()
    {
        var condition = CreateCondition("invalid_op");
        var indicators = CreateIndicatorContext(support: 100m);
        var context = CreateMarketContext(close: 100m);

        var result = _sut.Evaluate(condition, indicators, context);

        result.Passed.Should().BeFalse();
        result.Reason.Should().Contain("Unknown support_resistance operator");
    }

    [TestMethod]
    public void GivenWrongParamsType_WhenEvaluated_ThenFailed()
    {
        var condition = new EntryConditionConfig
        {
            Id = "sr-1",
            Enabled = true,
            Type = EntryConditionType.SupportResistance,
            Label = "Support test",
            Params = new RsiParams { Period = 14, Operator = "lt", Value = 30m },
        };
        var indicators = CreateIndicatorContext(support: 100m);
        var context = CreateMarketContext(close: 100m);

        var result = _sut.Evaluate(condition, indicators, context);

        result.Passed.Should().BeFalse();
        result.Reason.Should().Contain("Expected SupportResistanceParams");
    }

    private static EntryConditionConfig CreateCondition(string op, decimal tolerance = 0.5m)
    {
        return new EntryConditionConfig
        {
            Id = "sr-1",
            Enabled = true,
            Type = EntryConditionType.SupportResistance,
            Label = "S/R Condition",
            Params = new SupportResistanceParams
            {
                Lookback = 50,
                Strength = 3,
                Operator = op,
                Tolerance = tolerance,
            },
        };
    }

    private static IndicatorContext CreateIndicatorContext(decimal? support = null, decimal? resistance = null)
    {
        var context = new IndicatorContext();
        if (support.HasValue)
        {
            context.SetSupport(50, support.Value);
        }

        if (resistance.HasValue)
        {
            context.SetResistance(50, resistance.Value);
        }

        return context;
    }

    private static MarketContext CreateMarketContext(
        decimal close = 100m,
        decimal low = 95m,
        decimal high = 105m)
    {
        return new MarketContext
        {
            Symbol = "BTC-USD",
            TimestampUtc = CandleTimestamp,
            CurrentCandle = Candle.Create(
                "Binance",
                "BTC-USD",
                "15m",
                CandleTimestamp,
                close, // open
                high,
                low,
                close,
                1_000m,
                10),
            Indicators = new IndicatorSnapshot(),
        };
    }
}
