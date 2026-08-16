using TradePilot.Application.MarketAnalysis.Models;
using TradePilot.Application.MarketAnalysis.Services;
using TradePilot.Application.MarketData.Models;

namespace TradePilot.Application.Tests.MarketAnalysis.Services;

[TestClass]
public sealed class ConfirmedSwingDetectorTests
{
    [TestMethod]
    public void GivenHigherHighAndHigherLowFixture_WhenClassifying_ThenReturnsBullishStructure()
    {
        var swings = ConfirmedSwingDetector.Detect(CreateStructureFixture(10m, 1m, 12m, 2m));

        swings.Highs.Should().ContainInOrder(10m, 12m);
        swings.Lows.Should().ContainInOrder(1m, 2m);
        MarketAnalysisPolicy.ClassifyStructure(swings).Should().Be(MarketStructure.HigherHighHigherLow);
    }

    [TestMethod]
    public void GivenLowerHighAndLowerLowFixture_WhenClassifying_ThenReturnsBearishStructure()
    {
        var swings = ConfirmedSwingDetector.Detect(CreateStructureFixture(10m, 2m, 9m, 1m));

        MarketAnalysisPolicy.ClassifyStructure(swings).Should().Be(MarketStructure.LowerHighLowerLow);
    }

    [TestMethod]
    public void GivenConflictingSwingFixtures_WhenClassifying_ThenReturnsMixedStructure()
    {
        var expanding = ConfirmedSwingDetector.Detect(CreateStructureFixture(10m, 2m, 12m, 1m));
        var contracting = ConfirmedSwingDetector.Detect(CreateStructureFixture(10m, 1m, 9m, 2m));

        MarketAnalysisPolicy.ClassifyStructure(expanding).Should().Be(MarketStructure.Mixed);
        MarketAnalysisPolicy.ClassifyStructure(contracting).Should().Be(MarketStructure.Mixed);
    }

    [TestMethod]
    public void GivenEqualConfirmedLevels_WhenClassifying_ThenReturnsRange()
    {
        var swings = ConfirmedSwingDetector.Detect(CreateStructureFixture(10m, 1m, 10m, 1m));

        MarketAnalysisPolicy.ClassifyStructure(swings).Should().Be(MarketStructure.Range);
    }

    [TestMethod]
    public void GivenFlatCandles_WhenDetecting_ThenEqualHighsAndLowsAreNotPivots()
    {
        var candles = Enumerable.Range(0, 10)
            .Select(index => CreateCandle(index, 10m, 5m))
            .ToList();

        var swings = ConfirmedSwingDetector.Detect(candles);

        swings.Highs.Should().BeEmpty();
        swings.Lows.Should().BeEmpty();
        MarketAnalysisPolicy.ClassifyStructure(swings).Should().Be(MarketStructure.Unknown);
    }

    [TestMethod]
    public void GivenUnconfirmedEdgePivot_WhenDetecting_ThenDoesNotUseFuturelessTurningPoint()
    {
        var candles = CreateStructureFixture(10m, 1m, 12m, 2m).ToList();
        candles[^1] = CreateCandle(candles.Count - 1, 20m, 4m);

        var swings = ConfirmedSwingDetector.Detect(candles);

        swings.Highs.Should().ContainInOrder(10m, 12m);
        swings.Highs.Should().NotContain(20m);
    }

    private static IReadOnlyList<CandleDto> CreateStructureFixture(
        decimal firstHigh,
        decimal firstLow,
        decimal secondHigh,
        decimal secondLow)
    {
        var highs = new[] { 6m, 8m, firstHigh, 8m, 6m, 7m, 6m, 8m, secondHigh, 8m, 6m, 7m, 6m, 8m };
        var lows = new[] { 6m, 5m, 6m, 5m, 4m, firstLow, 4m, 5m, 6m, 5m, 4m, secondLow, 4m, 5m };

        return highs
            .Select((high, index) => CreateCandle(index, high, lows[index]))
            .ToList();
    }

    private static CandleDto CreateCandle(int index, decimal high, decimal low)
    {
        return new CandleDto
        {
            Timestamp = 1_000L + index,
            Open = low,
            High = high,
            Low = low,
            Close = (high + low) / 2m,
        };
    }
}
