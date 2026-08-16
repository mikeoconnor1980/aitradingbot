using TradePilot.Application.MarketAnalysis.Models;
using TradePilot.Application.MarketAnalysis.Services;

namespace TradePilot.Application.Tests.MarketAnalysis.Services;

[TestClass]
public sealed class MarketAnalysisPolicyTests
{
    [TestMethod]
    public void GivenEmaAlignment_WhenClassifyingTrend_ThenUsesStrictOrdering()
    {
        MarketAnalysisPolicy.ClassifyTrend(120m, 115m, 110m, 100m)
            .Should().Be(MarketTrend.Bullish);
        MarketAnalysisPolicy.ClassifyTrend(80m, 85m, 90m, 100m)
            .Should().Be(MarketTrend.Bearish);
        MarketAnalysisPolicy.ClassifyTrend(120m, 105m, 110m, 100m)
            .Should().Be(MarketTrend.Neutral);
        MarketAnalysisPolicy.ClassifyTrend(100m, 100m, 90m, 80m)
            .Should().Be(MarketTrend.Neutral);
    }

    [TestMethod]
    public void GivenMomentumThresholdBoundaries_WhenClassifying_ThenThresholdsAreInclusiveNeutral()
    {
        var cases = new[]
        {
            (44.999m, MarketMomentum.Bearish),
            (45m, MarketMomentum.Neutral),
            (45.001m, MarketMomentum.Neutral),
            (54.999m, MarketMomentum.Neutral),
            (55m, MarketMomentum.Neutral),
            (55.001m, MarketMomentum.Bullish),
        };

        foreach (var (rsi, expected) in cases)
        {
            MarketAnalysisPolicy.ClassifyMomentum(rsi).Should().Be(expected);
        }
    }

    [TestMethod]
    public void GivenVolatilityThresholdBoundaries_WhenClassifying_ThenThresholdsAreInclusiveNormal()
    {
        var cases = new[]
        {
            (0.999m, VolatilityRegime.Low),
            (1m, VolatilityRegime.Normal),
            (1.001m, VolatilityRegime.Normal),
            (2.999m, VolatilityRegime.Normal),
            (3m, VolatilityRegime.Normal),
            (3.001m, VolatilityRegime.High),
        };

        foreach (var (atrPercent, expected) in cases)
        {
            MarketAnalysisPolicy.ClassifyVolatility(atrPercent).Should().Be(expected);
        }
    }
}
