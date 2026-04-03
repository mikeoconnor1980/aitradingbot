using Microsoft.Extensions.Logging;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.StrategyAuthoring.Services;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Tests.StrategyAuthoring.Services;

[TestClass]
public sealed class PriceVsEmaConditionHandlerTests
{
    private const long CandleTimestamp = 1_000_000;

    private PriceVsEmaConditionHandler _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _sut = new PriceVsEmaConditionHandler(new Mock<ILogger<PriceVsEmaConditionHandler>>().Object);
    }

    [TestMethod]
    public void GivenNearPercent_WhenWithinDistance_ThenPassed()
    {
        var result = _sut.Evaluate(
            CreatePriceVsEmaCondition("near", 50, "percent", 0.25m),
            CreateIndicatorContext(50, 42_050m),
            CreateMarketContext(close: 42_150m));

        result.Passed.Should().BeTrue();
    }

    [TestMethod]
    public void GivenNearPercent_WhenOutsideDistance_ThenFailed()
    {
        var result = _sut.Evaluate(
            CreatePriceVsEmaCondition("near", 50, "percent", 0.25m),
            CreateIndicatorContext(50, 42_050m),
            CreateMarketContext(close: 43_000m));

        result.Passed.Should().BeFalse();
    }

    [TestMethod]
    public void GivenNearAbsolute_WhenWithinDistance_ThenPassed()
    {
        var result = _sut.Evaluate(
            CreatePriceVsEmaCondition("near", 50, "absolute", 150m),
            CreateIndicatorContext(50, 42_000m),
            CreateMarketContext(close: 42_100m));

        result.Passed.Should().BeTrue();
    }

    [TestMethod]
    public void GivenNearAtrMultiple_WhenEvaluated_ThenFailed()
    {
        var result = _sut.Evaluate(
            CreatePriceVsEmaCondition("near", 50, "atr_multiple", 1m),
            CreateIndicatorContext(50, 42_000m),
            CreateMarketContext(close: 42_050m));

        result.Passed.Should().BeFalse();
        result.Reason.Should().Contain("not yet supported");
    }

    [TestMethod]
    public void GivenTouch_WhenWickSpansEma_ThenPassed()
    {
        var result = _sut.Evaluate(
            CreatePriceVsEmaCondition("touch", 50),
            CreateIndicatorContext(50, 42_000m),
            CreateMarketContext(close: 42_050m, high: 42_100m, low: 41_900m));

        result.Passed.Should().BeTrue();
    }

    [TestMethod]
    public void GivenTouch_WhenWickDoesNotSpanEma_ThenFailed()
    {
        var result = _sut.Evaluate(
            CreatePriceVsEmaCondition("touch", 50),
            CreateIndicatorContext(50, 42_000m),
            CreateMarketContext(close: 42_300m, high: 42_500m, low: 42_100m));

        result.Passed.Should().BeFalse();
    }

    [TestMethod]
    public void GivenAbove_WhenPriceAboveEma_ThenPassed()
    {
        var result = _sut.Evaluate(
            CreatePriceVsEmaCondition("above", 50),
            CreateIndicatorContext(50, 42_000m),
            CreateMarketContext(close: 42_500m));

        result.Passed.Should().BeTrue();
    }

    [TestMethod]
    public void GivenBelow_WhenPriceBelowEma_ThenPassed()
    {
        var result = _sut.Evaluate(
            CreatePriceVsEmaCondition("below", 50),
            CreateIndicatorContext(50, 42_000m),
            CreateMarketContext(close: 41_500m));

        result.Passed.Should().BeTrue();
    }

    [TestMethod]
    public void GivenCrossAbove_WhenPreviousBelowAndCurrentAbove_ThenPassed()
    {
        var result = _sut.Evaluate(
            CreatePriceVsEmaCondition("cross_above", 50),
            CreateIndicatorContext(50, 42_000m, 41_900m),
            CreateMarketContext(close: 42_100m, previousClose: 41_800m));

        result.Passed.Should().BeTrue();
    }

    [TestMethod]
    public void GivenCrossBelow_WhenPreviousAboveAndCurrentBelow_ThenPassed()
    {
        var result = _sut.Evaluate(
            CreatePriceVsEmaCondition("cross_below", 50),
            CreateIndicatorContext(50, 42_000m, 42_100m),
            CreateMarketContext(close: 41_900m, previousClose: 42_200m));

        result.Passed.Should().BeTrue();
    }

    [TestMethod]
    public void GivenMissingEmaData_WhenEvaluated_ThenFailed()
    {
        var result = _sut.Evaluate(
            CreatePriceVsEmaCondition("near", 50, "percent", 0.25m),
            new IndicatorContext(),
            CreateMarketContext(close: 42_000m));

        result.Passed.Should().BeFalse();
        result.Reason.Should().Contain("not available");
    }

    [TestMethod]
    public void GivenUnknownOperator_WhenEvaluated_ThenFailed()
    {
        var result = _sut.Evaluate(
            CreatePriceVsEmaCondition("invalid_op", 50),
            CreateIndicatorContext(50, 42_000m),
            CreateMarketContext(close: 42_000m));

        result.Passed.Should().BeFalse();
        result.Reason.Should().Contain("Unknown");
    }

    private static EntryConditionConfig CreatePriceVsEmaCondition(
        string op,
        int period,
        string distanceType = "",
        decimal? distanceValue = null)
    {
        return new EntryConditionConfig
        {
            Id = "ema-1",
            Enabled = true,
            Type = EntryConditionType.PriceVsEma,
            Label = $"Price vs EMA({period})",
            Params = new PriceVsEmaParams
            {
                Period = period,
                Operator = op,
                DistanceType = distanceType,
                DistanceValue = distanceValue,
            },
        };
    }

    private static IndicatorContext CreateIndicatorContext(int period, decimal currentEma, decimal? previousEma = null)
    {
        var context = new IndicatorContext();
        context.SetEma(period, currentEma, previousEma);
        return context;
    }

    private static MarketContext CreateMarketContext(
        decimal close,
        decimal? previousClose = null,
        decimal? high = null,
        decimal? low = null)
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
                close,
                high ?? close + 100m,
                low ?? close - 100m,
                close,
                1_000m,
                10),
            PreviousCandle = previousClose.HasValue
                ? Candle.Create(
                    "Binance",
                    "BTC-USD",
                    "15m",
                    CandleTimestamp - 60_000L,
                    previousClose.Value,
                    previousClose.Value + 100m,
                    previousClose.Value - 100m,
                    previousClose.Value,
                    1_000m,
                    10)
                : null,
            Indicators = new IndicatorSnapshot(),
        };
    }
}