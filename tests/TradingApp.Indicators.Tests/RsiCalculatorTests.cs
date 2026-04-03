using TradingApp.Indicators;

namespace TradingApp.Indicators.Tests;

[TestClass]
public sealed class RsiCalculatorTests
{
    private static readonly decimal[] KnownCloses =
    [
        44.34m, 44.09m, 44.15m, 43.61m, 44.33m,
        44.83m, 45.10m, 45.42m, 45.84m, 46.08m,
        45.89m, 46.03m, 45.61m, 46.28m, 46.28m
    ];

    [TestMethod]
    public void GivenInsufficientData_WhenCalculate_ThenReturnsNull()
    {
        var closes = KnownCloses.Take(14).ToList();

        var result = RsiCalculator.Calculate(closes, 14);

        result.Should().BeNull();
    }

    [TestMethod]
    public void GivenKnownDataset_WhenCalculateRsi14_ThenMatchesWilderSmoothedValue()
    {
        var result = RsiCalculator.Calculate(KnownCloses, 14);

        result.Should().NotBeNull();
        result!.Value.Should().BeApproximately(70.46m, 0.5m);
    }

    [TestMethod]
    public void GivenAllGains_WhenCalculate_ThenReturns100()
    {
        decimal[] allGains = [10m, 11m, 12m, 13m, 14m, 15m];

        var result = RsiCalculator.Calculate(allGains, 5);

        result.Should().Be(100m);
    }

    [TestMethod]
    public void GivenAllLosses_WhenCalculate_ThenReturnsZero()
    {
        decimal[] allLosses = [15m, 14m, 13m, 12m, 11m, 10m];

        var result = RsiCalculator.Calculate(allLosses, 5);

        result.Should().NotBeNull();
        result!.Value.Should().BeApproximately(0m, 0.01m);
    }

    [TestMethod]
    public void GivenNoMovement_WhenCalculate_ThenReturnsNeutral50()
    {
        decimal[] flat = [50m, 50m, 50m, 50m, 50m, 50m];

        var result = RsiCalculator.Calculate(flat, 5);

        result.Should().Be(50m);
    }

    [TestMethod]
    public void GivenEmptyList_WhenCalculate_ThenReturnsNull()
    {
        var result = RsiCalculator.Calculate([], 14);

        result.Should().BeNull();
    }

    [TestMethod]
    public void GivenKnownDataset_WhenCalculateSeries_ThenLastValueMatchesCalculate()
    {
        var series = RsiCalculator.CalculateSeries(KnownCloses, 14);
        var singleValue = RsiCalculator.Calculate(KnownCloses, 14);

        series.Should().HaveCount(KnownCloses.Length);
        series.Take(14).Should().AllSatisfy(value => value.Should().BeNull());
        series[^1].Should().Be(singleValue);
    }
}