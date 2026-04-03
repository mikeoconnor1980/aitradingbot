using TradingApp.Indicators;

namespace TradingApp.Indicators.Tests;

[TestClass]
public sealed class BollingerBandsCalculatorTests
{
    private static readonly decimal[] KnownCloses =
    [
        86.16m, 89.09m, 88.78m, 90.32m, 89.07m,
        91.15m, 89.44m, 89.18m, 86.93m, 87.68m,
        86.96m, 89.43m, 89.32m, 88.72m, 87.45m,
        87.26m, 89.50m, 87.90m, 89.13m, 90.70m
    ];

    [TestMethod]
    public void GivenInsufficientData_WhenCalculate_ThenReturnsNull()
    {
        var closes = KnownCloses.Take(10).ToList();

        var result = BollingerBandsCalculator.Calculate(closes, 20);

        result.Should().BeNull();
    }

    [TestMethod]
    public void GivenKnownDataset_WhenCalculate_ThenMatchesExpectedValues()
    {
        var result = BollingerBandsCalculator.Calculate(KnownCloses, 20);

        result.Should().NotBeNull();
        result!.Upper.Should().BeApproximately(91.291910730023m, 0.000000001m);
        result.Middle.Should().BeApproximately(88.708500000000m, 0.000000001m);
        result.Lower.Should().BeApproximately(86.125089269977m, 0.000000001m);
    }

    [TestMethod]
    public void GivenKnownDataset_WhenCalculate_ThenMiddleEqualsSma()
    {
        var expectedSma = KnownCloses.Average();

        var result = BollingerBandsCalculator.Calculate(KnownCloses, 20);

        result.Should().NotBeNull();
        result!.Middle.Should().BeApproximately(expectedSma, 0.000000001m);
    }

    [TestMethod]
    public void GivenKnownDataset_WhenCalculate_ThenBandsAreSymmetric()
    {
        var result = BollingerBandsCalculator.Calculate(KnownCloses, 20);

        result.Should().NotBeNull();
        var upperDiff = result!.Upper - result.Middle;
        var lowerDiff = result.Middle - result.Lower;
        upperDiff.Should().BeApproximately(lowerDiff, 0.000000001m);
    }

    [TestMethod]
    public void GivenKnownDataset_WhenCalculate_ThenUpperIsAboveMiddle()
    {
        var result = BollingerBandsCalculator.Calculate(KnownCloses, 20);

        result.Should().NotBeNull();
        result!.Upper.Should().BeGreaterThan(result.Middle);
        result.Lower.Should().BeLessThan(result.Middle);
    }

    [TestMethod]
    public void GivenCustomMultiplier_WhenCalculate_ThenBandsAreWider()
    {
        var standard = BollingerBandsCalculator.Calculate(KnownCloses, 20, 2m);
        var wide = BollingerBandsCalculator.Calculate(KnownCloses, 20, 3m);

        standard.Should().NotBeNull();
        wide.Should().NotBeNull();
        (wide!.Upper - wide.Lower).Should().BeGreaterThan(standard!.Upper - standard.Lower);
    }

    [TestMethod]
    public void GivenFlatPrices_WhenCalculate_ThenBandsConverge()
    {
        var flat = Enumerable.Repeat(100m, 20).ToList();

        var result = BollingerBandsCalculator.Calculate(flat, 20);

        result.Should().NotBeNull();
        result!.Upper.Should().Be(result.Middle);
        result.Lower.Should().Be(result.Middle);
    }

    [TestMethod]
    public void GivenKnownDataset_WhenCalculateSeries_ThenLastValueMatchesCalculate()
    {
        var series = BollingerBandsCalculator.CalculateSeries(KnownCloses, 20);
        var singleValue = BollingerBandsCalculator.Calculate(KnownCloses, 20);

        series.Should().HaveCount(KnownCloses.Length);
        series.Take(19).Should().AllSatisfy(value => value.Should().BeNull());
        series[^1].Should().BeEquivalentTo(singleValue);
    }
}