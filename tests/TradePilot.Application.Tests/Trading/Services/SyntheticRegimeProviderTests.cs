using TradePilot.Application.Trading.Models;
using TradePilot.Application.Trading.Services;

namespace TradePilot.Application.Tests.Trading.Services;

[TestClass]
public sealed class SyntheticRegimeProviderTests
{
    [TestMethod]
    public void GivenBullishEmaStack_WhenEvaluated_ThenRegimeIsAggressive()
    {
        var sut = new SyntheticRegimeProvider();
        FeedAtr(sut, 50, 1.5m); // moderate ATR

        var indicators = new IndicatorSnapshot
        {
            EmaFast = 110m,
            EmaSlow = 105m,
            EmaTrend = 100m,
            Rsi = 60m,
            Atr = 1.5m
        };

        var result = sut.Evaluate(indicators, 1_000_000);

        result.DerivedRegime.Should().Be(MarketRegime.Aggressive);
        result.MacroRegime.Should().Be("Bullish");
        result.MarketSentiment.Should().Be("Bullish");
    }

    [TestMethod]
    public void GivenBearishEmaStackWithHighVolatility_WhenEvaluated_ThenRegimeIsRiskOff()
    {
        var sut = new SyntheticRegimeProvider();

        // Feed 96 low ATR values, then spike
        FeedAtr(sut, 90, 1.0m);
        FeedAtr(sut, 6, 5.0m); // last values are high → high percentile

        var indicators = new IndicatorSnapshot
        {
            EmaFast = 90m,
            EmaSlow = 95m,
            EmaTrend = 100m,
            Rsi = 35m,
            Atr = 5.0m
        };

        var result = sut.Evaluate(indicators, 1_000_000);

        result.DerivedRegime.Should().Be(MarketRegime.RiskOff);
        result.MacroRegime.Should().Be("Bearish");
        result.EventRisk.Should().Be("High");
    }

    [TestMethod]
    public void GivenNeutralTrend_WhenEvaluated_ThenRegimeIsNormal()
    {
        var sut = new SyntheticRegimeProvider();
        FeedAtr(sut, 50, 1.5m);

        // Mixed EMA order (not a clean stack)
        var indicators = new IndicatorSnapshot
        {
            EmaFast = 102m,
            EmaSlow = 100m,
            EmaTrend = 101m, // slow < trend but fast > slow
            Rsi = 50m,
            Atr = 1.5m
        };

        var result = sut.Evaluate(indicators, 1_000_000);

        result.DerivedRegime.Should().Be(MarketRegime.Normal);
        result.MacroRegime.Should().Be("Neutral");
    }

    [TestMethod]
    public void GivenBearishTrendLowVolatility_WhenEvaluated_ThenRegimeIsDefensive()
    {
        var sut = new SyntheticRegimeProvider();
        FeedAtr(sut, 96, 1.0m);

        var indicators = new IndicatorSnapshot
        {
            EmaFast = 90m,
            EmaSlow = 95m,
            EmaTrend = 100m,
            Rsi = 40m,
            Atr = 1.0m
        };

        var result = sut.Evaluate(indicators, 1_000_000);

        result.DerivedRegime.Should().Be(MarketRegime.Defensive);
        result.MacroRegime.Should().Be("Bearish");
    }

    [TestMethod]
    public void GivenInsufficientData_WhenEvaluated_ThenConfidenceIsLow()
    {
        var sut = new SyntheticRegimeProvider();
        FeedAtr(sut, 10, 1.0m); // not mature (need 96)

        var indicators = new IndicatorSnapshot
        {
            EmaFast = 110m,
            EmaSlow = 105m,
            EmaTrend = 100m,
            Rsi = 55m,
            Atr = 1.0m
        };

        var result = sut.Evaluate(indicators, 1_000_000);

        result.Confidence.Should().Be(0.5m);
    }

    [TestMethod]
    public void GivenMatureData_WhenEvaluated_ThenConfidenceIsHigher()
    {
        var sut = new SyntheticRegimeProvider();
        FeedAtr(sut, 96, 1.0m);

        var indicators = new IndicatorSnapshot
        {
            EmaFast = 110m,
            EmaSlow = 105m,
            EmaTrend = 100m,
            Rsi = 55m,
            Atr = 1.0m
        };

        var result = sut.Evaluate(indicators, 1_000_000);

        result.Confidence.Should().Be(0.75m);
    }

    [TestMethod]
    public void GivenZeroIndicators_WhenEvaluated_ThenRegimeIsNormal()
    {
        var sut = new SyntheticRegimeProvider();

        var indicators = new IndicatorSnapshot
        {
            EmaFast = 0m,
            EmaSlow = 0m,
            EmaTrend = 0m,
            Rsi = 50m,
            Atr = 0m
        };

        var result = sut.Evaluate(indicators, 1_000_000);

        result.DerivedRegime.Should().Be(MarketRegime.Normal);
        result.MacroRegime.Should().Be("Neutral");
    }

    [TestMethod]
    public void GivenBullishTrendWithWeakRsi_WhenEvaluated_ThenSentimentIsNeutral()
    {
        var sut = new SyntheticRegimeProvider();
        FeedAtr(sut, 50, 1.0m);

        var indicators = new IndicatorSnapshot
        {
            EmaFast = 110m,
            EmaSlow = 105m,
            EmaTrend = 100m,
            Rsi = 40m, // below 50 — weak
            Atr = 1.0m
        };

        var result = sut.Evaluate(indicators, 1_000_000);

        result.MarketSentiment.Should().Be("Neutral");
    }

    private static void FeedAtr(SyntheticRegimeProvider sut, int count, decimal value)
    {
        for (var i = 0; i < count; i++)
        {
            sut.Update(value);
        }
    }
}
