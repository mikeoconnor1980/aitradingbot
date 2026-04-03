using TradingApp.Indicators;

namespace TradingApp.Indicators.Tests;

[TestClass]
public sealed class EmaCalculatorTests
{
    private static readonly decimal[] KnownCloses =
    [
        22m, 22.27m, 22.19m, 22.08m, 22.17m,
        22.18m, 22.13m, 22.23m, 22.43m, 22.24m
    ];

    [TestMethod]
    public void GivenInsufficientData_WhenCalculate_ThenReturnsNull()
    {
        var result = EmaCalculator.Calculate([10m, 20m], 5);

        result.Should().BeNull();
    }

    [TestMethod]
    public void GivenExactPeriodCount_WhenCalculate_ThenReturnsSmaSeed()
    {
        var closes = KnownCloses.Take(5).ToList();
        var expectedSma = closes.Average();

        var result = EmaCalculator.Calculate(closes, 5);

        result.Should().Be(expectedSma);
    }

    [TestMethod]
    public void GivenKnownDataset_WhenCalculateEma5_ThenMatchesExpectedValue()
    {
        var result = EmaCalculator.Calculate(KnownCloses, 5);

        result.Should().NotBeNull();
        result!.Value.Should().BeApproximately(22.2470m, 0.01m);
    }

    [TestMethod]
    public void GivenEmptyList_WhenCalculate_ThenReturnsNull()
    {
        var result = EmaCalculator.Calculate([], 5);

        result.Should().BeNull();
    }

    [TestMethod]
    public void GivenKnownDataset_WhenCalculateSeries_ThenFirstWarmupEntriesAreNull()
    {
        var series = EmaCalculator.CalculateSeries(KnownCloses, 5);

        series.Should().HaveCount(KnownCloses.Length);
        series.Take(4).Should().AllSatisfy(value => value.Should().BeNull());
        series[4].Should().NotBeNull();
    }

    [TestMethod]
    public void GivenKnownDataset_WhenCalculateSeries_ThenLastValueMatchesCalculate()
    {
        var series = EmaCalculator.CalculateSeries(KnownCloses, 5);
        var singleValue = EmaCalculator.Calculate(KnownCloses, 5);

        series[^1].Should().Be(singleValue);
    }
}