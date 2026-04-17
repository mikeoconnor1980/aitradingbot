using TradePilot.Indicators;

namespace TradePilot.Indicators.Tests;

[TestClass]
public sealed class AtrCalculatorTests
{
    private static readonly (decimal High, decimal Low, decimal Close)[] KnownBars =
    [
        (48.70m, 47.79m, 48.16m),
        (48.72m, 48.14m, 48.61m),
        (48.90m, 48.39m, 48.75m),
        (48.87m, 48.37m, 48.63m),
        (48.82m, 48.24m, 48.74m),
        (49.05m, 48.64m, 49.03m),
        (49.20m, 48.94m, 49.07m),
        (49.35m, 48.86m, 49.32m),
        (49.92m, 49.50m, 49.91m),
        (50.19m, 49.87m, 50.13m),
        (50.12m, 49.20m, 49.53m),
        (49.66m, 48.90m, 49.50m),
        (49.88m, 49.43m, 49.75m),
        (50.19m, 49.73m, 50.03m),
        (50.36m, 49.26m, 50.31m),
        (50.57m, 50.09m, 50.52m)
    ];

    [TestMethod]
    public void GivenInsufficientData_WhenCalculate_ThenReturnsNull()
    {
        var bars = KnownBars.Take(5).ToList();

        var result = AtrCalculator.Calculate(bars, 14);

        result.Should().BeNull();
    }

    [TestMethod]
    public void GivenKnownDataset_WhenCalculateAtr14_ThenMatchesExpectedValue()
    {
        var result = AtrCalculator.Calculate(KnownBars, 14);

        result.Should().NotBeNull();
        result!.Value.Should().BeApproximately(0.561581632653m, 0.000000001m);
    }

    [TestMethod]
    public void GivenEmptyBars_WhenCalculate_ThenReturnsNull()
    {
        var result = AtrCalculator.Calculate(Array.Empty<(decimal High, decimal Low, decimal Close)>(), 14);

        result.Should().BeNull();
    }

    [TestMethod]
    public void GivenMinimalBars_WhenCalculate_ThenReturnsInitialTrueRangeAverage()
    {
        var bars = KnownBars.Take(15).ToList();

        var result = AtrCalculator.Calculate(bars, 14);

        result.Should().NotBeNull();
        result!.Value.Should().BeApproximately(0.567857142857m, 0.000000001m);
    }

    [TestMethod]
    public void GivenKnownDataset_WhenCalculateSeries_ThenLastValueMatchesCalculate()
    {
        var series = AtrCalculator.CalculateSeries(KnownBars, 14);
        var singleValue = AtrCalculator.Calculate(KnownBars, 14);

        series.Should().HaveCount(KnownBars.Length);
        series.Take(14).Should().AllSatisfy(value => value.Should().BeNull());
        series[^1].Should().Be(singleValue);
    }
}