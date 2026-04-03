using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.StrategyAuthoring.Services;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Tests.StrategyAuthoring.Services;

[TestClass]
public sealed class MacdConditionHandlerTests
{
    private const long CandleTimestamp = 1_000_000;

    private MacdConditionHandler _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _sut = new MacdConditionHandler();
    }

    [TestMethod]
    public void GivenCrossAboveSignal_WhenPreviousLineBelowSignalAndCurrentAbove_ThenPassed()
    {
        var condition = CreateMacdCondition("cross_above_signal");
        var indicators = CreateIndicatorContext(0.5m, 0.3m, 0.2m, -0.1m, 0.1m, -0.2m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeTrue();
        result.Reason.Should().Contain("cross_above_signal").And.Contain("condition met");
    }

    [TestMethod]
    public void GivenCrossAboveSignal_WhenPreviousLineAlreadyAboveSignal_ThenFailed()
    {
        var condition = CreateMacdCondition("cross_above_signal");
        var indicators = CreateIndicatorContext(0.5m, 0.3m, 0.2m, 0.4m, 0.2m, 0.2m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeFalse();
    }

    [TestMethod]
    public void GivenCrossBelowSignal_WhenPreviousLineAboveSignalAndCurrentBelow_ThenPassed()
    {
        var condition = CreateMacdCondition("cross_below_signal");
        var indicators = CreateIndicatorContext(-0.5m, -0.3m, -0.2m, 0.2m, 0.1m, 0.1m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeTrue();
        result.Reason.Should().Contain("cross_below_signal").And.Contain("condition met");
    }

    [TestMethod]
    public void GivenCrossBelowSignal_WhenPreviousLineAlreadyBelowSignal_ThenFailed()
    {
        var condition = CreateMacdCondition("cross_below_signal");
        var indicators = CreateIndicatorContext(-0.5m, -0.3m, -0.2m, -0.2m, -0.1m, -0.1m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeFalse();
    }

    [TestMethod]
    public void GivenAboveZero_WhenLinePositive_ThenPassed()
    {
        var condition = CreateMacdCondition("above_zero");
        var indicators = CreateIndicatorContext(0.4m, 0.2m, 0.2m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeTrue();
        result.Reason.Should().Contain("above_zero");
    }

    [TestMethod]
    public void GivenAboveZero_WhenLineNegative_ThenFailed()
    {
        var condition = CreateMacdCondition("above_zero");
        var indicators = CreateIndicatorContext(-0.4m, -0.2m, -0.2m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeFalse();
    }

    [TestMethod]
    public void GivenBelowZero_WhenLineNegative_ThenPassed()
    {
        var condition = CreateMacdCondition("below_zero");
        var indicators = CreateIndicatorContext(-0.4m, -0.2m, -0.2m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeTrue();
        result.Reason.Should().Contain("below_zero");
    }

    [TestMethod]
    public void GivenBelowZero_WhenLinePositive_ThenFailed()
    {
        var condition = CreateMacdCondition("below_zero");
        var indicators = CreateIndicatorContext(0.4m, 0.2m, 0.2m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeFalse();
    }

    [TestMethod]
    public void GivenHistogramRising_WhenCurrentHistogramAbovePrevious_ThenPassed()
    {
        var condition = CreateMacdCondition("histogram_rising");
        var indicators = CreateIndicatorContext(0.4m, 0.2m, 0.3m, 0.3m, 0.2m, 0.1m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeTrue();
        result.Reason.Should().Contain("histogram_rising").And.Contain("condition met");
    }

    [TestMethod]
    public void GivenHistogramRising_WhenCurrentHistogramNotAbovePrevious_ThenFailed()
    {
        var condition = CreateMacdCondition("histogram_rising");
        var indicators = CreateIndicatorContext(0.4m, 0.2m, 0.1m, 0.3m, 0.2m, 0.3m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeFalse();
    }

    [TestMethod]
    public void GivenHistogramFalling_WhenCurrentHistogramBelowPrevious_ThenPassed()
    {
        var condition = CreateMacdCondition("histogram_falling");
        var indicators = CreateIndicatorContext(-0.4m, -0.2m, -0.3m, -0.3m, -0.2m, -0.1m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeTrue();
        result.Reason.Should().Contain("histogram_falling").And.Contain("condition met");
    }

    [TestMethod]
    public void GivenHistogramFalling_WhenCurrentHistogramNotBelowPrevious_ThenFailed()
    {
        var condition = CreateMacdCondition("histogram_falling");
        var indicators = CreateIndicatorContext(-0.4m, -0.2m, -0.1m, -0.3m, -0.2m, -0.3m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeFalse();
    }

    [TestMethod]
    public void GivenMissingMacdData_WhenEvaluated_ThenFailedClosed()
    {
        var condition = CreateMacdCondition("above_zero");

        var result = _sut.Evaluate(condition, new IndicatorContext(), CreateMarketContext());

        result.Passed.Should().BeFalse();
        result.Reason.Should().Contain("data not available");
    }

    [TestMethod]
    public void GivenMissingPreviousValues_WhenCrossOperatorEvaluated_ThenFailed()
    {
        var condition = CreateMacdCondition("cross_above_signal");
        var indicators = CreateIndicatorContext(0.4m, 0.2m, 0.2m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeFalse();
        result.Reason.Should().Contain("previous values not available");
    }

    [TestMethod]
    public void GivenMissingPreviousHistogram_WhenHistogramDirectionEvaluated_ThenFailed()
    {
        var condition = CreateMacdCondition("histogram_rising");
        var indicators = CreateIndicatorContext(0.4m, 0.2m, 0.2m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeFalse();
        result.Reason.Should().Contain("previous histogram not available");
    }

    [TestMethod]
    public void GivenUnknownOperator_WhenEvaluated_ThenFailed()
    {
        var condition = CreateMacdCondition("invalid_op");
        var indicators = CreateIndicatorContext(0.4m, 0.2m, 0.2m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeFalse();
        result.Reason.Should().Contain("Unknown MACD operator");
    }

    [TestMethod]
    public void GivenWrongParamsType_WhenEvaluated_ThenFailed()
    {
        var condition = new EntryConditionConfig
        {
            Id = "macd-1",
            Enabled = true,
            Type = EntryConditionType.Macd,
            Label = "MACD",
            Params = new RsiParams
            {
                Period = 14,
                Operator = "lt",
                Value = 30m,
            },
        };
        var indicators = CreateIndicatorContext(0.4m, 0.2m, 0.2m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeFalse();
        result.Reason.Should().Contain("Expected MacdParams");
    }

    private static EntryConditionConfig CreateMacdCondition(string op)
    {
        return new EntryConditionConfig
        {
            Id = "macd-1",
            Enabled = true,
            Type = EntryConditionType.Macd,
            Label = "MACD(12,26,9)",
            Params = new MacdParams
            {
                FastPeriod = 12,
                SlowPeriod = 26,
                SignalPeriod = 9,
                Operator = op,
            },
        };
    }

    private static IndicatorContext CreateIndicatorContext(
        decimal line,
        decimal signal,
        decimal histogram,
        decimal? previousLine = null,
        decimal? previousSignal = null,
        decimal? previousHistogram = null)
    {
        var context = new IndicatorContext();
        context.SetMacd(12, 26, 9, line, signal, histogram, previousLine, previousSignal, previousHistogram);
        return context;
    }

    private static MarketContext CreateMarketContext()
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
                100m,
                105m,
                95m,
                102m,
                1_000m,
                10),
            Indicators = new IndicatorSnapshot(),
        };
    }
}