using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Services;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Tests.StrategyAuthoring.Services;

[TestClass]
public sealed class RsiConditionHandlerTests
{
    private const long CandleTimestamp = 1_000_000;

    private RsiConditionHandler _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _sut = new RsiConditionHandler();
    }

    [TestMethod]
    public void GivenRsiBelow40_WhenOperatorLt40_ThenPassed()
    {
        var condition = CreateRsiCondition("lt", 40m, 14);
        var indicators = CreateIndicatorContext(rsiPeriod: 14, currentRsi: 35m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeTrue();
        result.Reason.Should().Contain("RSI(14) = 35 < 40");
    }

    [TestMethod]
    public void GivenRsiEqualThreshold_WhenOperatorLte_ThenPassed()
    {
        var condition = CreateRsiCondition("lte", 40m, 14);
        var indicators = CreateIndicatorContext(rsiPeriod: 14, currentRsi: 40m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeTrue();
    }

    [TestMethod]
    public void GivenRsiAboveThreshold_WhenOperatorGt_ThenPassed()
    {
        var condition = CreateRsiCondition("gt", 40m, 14);
        var indicators = CreateIndicatorContext(rsiPeriod: 14, currentRsi: 45m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeTrue();
    }

    [TestMethod]
    public void GivenRsiEqualThreshold_WhenOperatorGte_ThenPassed()
    {
        var condition = CreateRsiCondition("gte", 40m, 14);
        var indicators = CreateIndicatorContext(rsiPeriod: 14, currentRsi: 40m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeTrue();
    }

    [TestMethod]
    public void GivenRsiAbove40_WhenOperatorLt40_ThenFailed()
    {
        var condition = CreateRsiCondition("lt", 40m, 14);
        var indicators = CreateIndicatorContext(rsiPeriod: 14, currentRsi: 45m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeFalse();
    }

    [TestMethod]
    public void GivenCrossAboveThreshold_WhenPreviousBelowCurrentAbove_ThenPassed()
    {
        var condition = CreateRsiCondition("cross_above", 30m, 14);
        var indicators = CreateIndicatorContext(rsiPeriod: 14, currentRsi: 32m, previousRsi: 28m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeTrue();
    }

    [TestMethod]
    public void GivenCrossAboveThreshold_WhenBothAbove_ThenFailed()
    {
        var condition = CreateRsiCondition("cross_above", 30m, 14);
        var indicators = CreateIndicatorContext(rsiPeriod: 14, currentRsi: 35m, previousRsi: 32m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeFalse();
    }

    [TestMethod]
    public void GivenCrossBelowThreshold_WhenPreviousAboveCurrentBelow_ThenPassed()
    {
        var condition = CreateRsiCondition("cross_below", 70m, 14);
        var indicators = CreateIndicatorContext(rsiPeriod: 14, currentRsi: 68m, previousRsi: 72m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeTrue();
    }

    [TestMethod]
    public void GivenCrossBelowThreshold_WhenPreviousBelowCurrentBelow_ThenFailed()
    {
        var condition = CreateRsiCondition("cross_below", 70m, 14);
        var indicators = CreateIndicatorContext(rsiPeriod: 14, currentRsi: 68m, previousRsi: 65m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeFalse();
    }

    [TestMethod]
    public void GivenMissingRsiData_WhenEvaluated_ThenFailed()
    {
        var condition = CreateRsiCondition("lt", 40m, 14);
        var indicators = new IndicatorContext();

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeFalse();
        result.Reason.Should().Contain("not available");
    }

    [TestMethod]
    public void GivenMissingPreviousRsiData_WhenCrossOperatorEvaluated_ThenFailed()
    {
        var condition = CreateRsiCondition("cross_above", 30m, 14);
        var indicators = CreateIndicatorContext(rsiPeriod: 14, currentRsi: 32m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeFalse();
        result.Reason.Should().Contain("previous value not available");
    }

    [TestMethod]
    public void GivenUnknownOperator_WhenEvaluated_ThenFailed()
    {
        var condition = CreateRsiCondition("invalid_op", 40m, 14);
        var indicators = CreateIndicatorContext(rsiPeriod: 14, currentRsi: 35m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeFalse();
        result.Reason.Should().Contain("Unknown RSI operator");
    }

    private static EntryConditionConfig CreateRsiCondition(string op, decimal value, int period = 14)
    {
        return new EntryConditionConfig
        {
            Id = "rsi-1",
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

    private static IndicatorContext CreateIndicatorContext(int rsiPeriod = 14, decimal currentRsi = 50m, decimal? previousRsi = null)
    {
        var context = new IndicatorContext();
        context.SetRsi(rsiPeriod, currentRsi, previousRsi);
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
            Indicators = new IndicatorSnapshot()
        };
    }
}