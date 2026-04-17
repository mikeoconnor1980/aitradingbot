using Microsoft.Extensions.Logging;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Services;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Tests.StrategyAuthoring.Services;

[TestClass]
public sealed class TrendFilterEvaluatorTests
{
    private const long CandleTimestamp = 1_000_000;

    private TrendFilterEvaluator _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _sut = new TrendFilterEvaluator(new Mock<ILogger<TrendFilterEvaluator>>().Object);
    }

    [TestMethod]
    public void GivenEmaCrossGt_WhenFastAboveSlow_ThenPassed()
    {
        var result = _sut.Evaluate(
            CreateEmaCrossFilter(TrendOperator.Gt, 50, 200),
            Direction.Long,
            CreateEmaContext((50, 42_500m, null), (200, 42_000m, null)),
            CreateMarketContext());

        result.Passed.Should().BeTrue();
    }

    [TestMethod]
    public void GivenEmaCrossGt_WhenFastBelowSlow_ThenFailed()
    {
        var result = _sut.Evaluate(
            CreateEmaCrossFilter(TrendOperator.Gt, 50, 200),
            Direction.Long,
            CreateEmaContext((50, 41_500m, null), (200, 42_000m, null)),
            CreateMarketContext());

        result.Passed.Should().BeFalse();
    }

    [TestMethod]
    public void GivenEmaCrossLt_WhenFastBelowSlow_ThenPassed()
    {
        var result = _sut.Evaluate(
            CreateEmaCrossFilter(TrendOperator.Lt, 50, 200),
            Direction.Long,
            CreateEmaContext((50, 41_500m, null), (200, 42_000m, null)),
            CreateMarketContext());

        result.Passed.Should().BeTrue();
    }

    [TestMethod]
    public void GivenEmaCrossCrossAbove_WhenPrevBelowAndCurrentAbove_ThenPassed()
    {
        var result = _sut.Evaluate(
            CreateEmaCrossFilter(TrendOperator.CrossAbove, 50, 200),
            Direction.Long,
            CreateEmaContext((50, 42_500m, 41_800m), (200, 42_000m, 42_100m)),
            CreateMarketContext());

        result.Passed.Should().BeTrue();
    }

    [TestMethod]
    public void GivenEmaCrossCrossBelow_WhenPrevAboveAndCurrentBelow_ThenPassed()
    {
        var result = _sut.Evaluate(
            CreateEmaCrossFilter(TrendOperator.CrossBelow, 50, 200),
            Direction.Long,
            CreateEmaContext((50, 41_900m, 42_200m), (200, 42_000m, 42_100m)),
            CreateMarketContext());

        result.Passed.Should().BeTrue();
    }

    [TestMethod]
    public void GivenSmaCrossGt_WhenFastAboveSlow_ThenPassed()
    {
        var indicators = new IndicatorContext();
        indicators.SetSma(20, 42_500m);
        indicators.SetSma(50, 42_000m);

        var result = _sut.Evaluate(
            CreateSmaCrossFilter(TrendOperator.Gt, 20, 50),
            Direction.Long,
            indicators,
            CreateMarketContext());

        result.Passed.Should().BeTrue();
    }

    [TestMethod]
    public void GivenPriceAboveEmaAbove_WhenPriceAbove_ThenPassed()
    {
        var result = _sut.Evaluate(
            CreatePriceAboveEmaFilter(TrendOperator.Above, 200),
            Direction.Long,
            CreateEmaContext((200, 42_000m, null)),
            CreateMarketContext(close: 42_500m));

        result.Passed.Should().BeTrue();
    }

    [TestMethod]
    public void GivenPriceAboveEmaAbove_WhenPriceBelow_ThenFailed()
    {
        var result = _sut.Evaluate(
            CreatePriceAboveEmaFilter(TrendOperator.Above, 200),
            Direction.Long,
            CreateEmaContext((200, 42_000m, null)),
            CreateMarketContext(close: 41_500m));

        result.Passed.Should().BeFalse();
    }

    [TestMethod]
    public void GivenPriceAboveEmaCrossAbove_WhenPrevBelowAndCurrentAbove_ThenPassed()
    {
        var result = _sut.Evaluate(
            CreatePriceAboveEmaFilter(TrendOperator.CrossAbove, 50),
            Direction.Long,
            CreateEmaContext((50, 42_000m, 41_900m)),
            CreateMarketContext(close: 42_100m, previousClose: 41_800m));

        result.Passed.Should().BeTrue();
    }

    [TestMethod]
    public void GivenDisabledFilter_WhenEvaluated_ThenPassed()
    {
        var result = _sut.Evaluate(
            new TrendFilterConfig { Enabled = false, Type = TrendFilterType.EmaCross },
            Direction.Long,
            new IndicatorContext(),
            CreateMarketContext());

        result.Passed.Should().BeTrue();
        result.Reason.Should().Contain("disabled");
    }

    [TestMethod]
    public void GivenNullFilter_WhenEvaluated_ThenPassed()
    {
        var result = _sut.Evaluate(null, Direction.Long, new IndicatorContext(), CreateMarketContext());

        result.Passed.Should().BeTrue();
    }

    [TestMethod]
    public void GivenAppliesToShort_WhenDirectionLong_ThenPassed()
    {
        var filter = CreateEmaCrossFilter(TrendOperator.Gt, 50, 200) with { AppliesTo = Direction.Short };

        var result = _sut.Evaluate(filter, Direction.Long, new IndicatorContext(), CreateMarketContext());

        result.Passed.Should().BeTrue();
        result.Reason.Should().Contain("skipped");
    }

    [TestMethod]
    public void GivenInsufficientData_WhenEmaCrossEvaluated_ThenFailed()
    {
        var result = _sut.Evaluate(
            CreateEmaCrossFilter(TrendOperator.Gt, 50, 200),
            Direction.Long,
            new IndicatorContext(),
            CreateMarketContext());

        result.Passed.Should().BeFalse();
        result.Reason.Should().Contain("not available");
    }

    [TestMethod]
    public void GivenUnknownTrendFilterType_WhenEvaluated_ThenFailsClosed()
    {
        var result = _sut.Evaluate(
            new TrendFilterConfig
            {
                Enabled = true,
                Type = TrendFilterType.EmaSingle,
                FastPeriod = 50,
                SlowPeriod = 200,
                Operator = TrendOperator.Gt,
                AppliesTo = Direction.Long,
            },
            Direction.Long,
            CreateEmaContext((50, 42_500m, null), (200, 42_000m, null)),
            CreateMarketContext());

        result.Passed.Should().BeFalse();
        result.Reason.Should().Contain("fails closed");
    }

    private static TrendFilterConfig CreateEmaCrossFilter(TrendOperator op, int fast, int slow)
    {
        return new TrendFilterConfig
        {
            Enabled = true,
            Type = TrendFilterType.EmaCross,
            FastPeriod = fast,
            SlowPeriod = slow,
            Operator = op,
            AppliesTo = Direction.Long,
        };
    }

    private static TrendFilterConfig CreateSmaCrossFilter(TrendOperator op, int fast, int slow)
    {
        return new TrendFilterConfig
        {
            Enabled = true,
            Type = TrendFilterType.SmaCross,
            FastPeriod = fast,
            SlowPeriod = slow,
            Operator = op,
            AppliesTo = Direction.Long,
        };
    }

    private static TrendFilterConfig CreatePriceAboveEmaFilter(TrendOperator op, int period)
    {
        return new TrendFilterConfig
        {
            Enabled = true,
            Type = TrendFilterType.PriceAboveEma,
            Period = period,
            Operator = op,
            AppliesTo = Direction.Long,
        };
    }

    private static IndicatorContext CreateEmaContext(params (int Period, decimal Current, decimal? Previous)[] values)
    {
        var context = new IndicatorContext();

        foreach (var (period, current, previous) in values)
        {
            context.SetEma(period, current, previous);
        }

        return context;
    }

    private static MarketContext CreateMarketContext(decimal close = 42_000m, decimal? previousClose = null)
    {
        return new MarketContext
        {
            Symbol = "BTC-USD",
            TimestampUtc = CandleTimestamp,
            CurrentCandle = CreateCandle(CandleTimestamp, close),
            PreviousCandle = previousClose.HasValue ? CreateCandle(CandleTimestamp - 60_000L, previousClose.Value) : null,
            Indicators = new IndicatorSnapshot(),
        };
    }

    private static Candle CreateCandle(long timestamp, decimal close)
    {
        return Candle.Create(
            "Binance",
            "BTC-USD",
            "15m",
            timestamp,
            close - 100m,
            close + 100m,
            close - 200m,
            close,
            1_000m,
            10);
    }
}