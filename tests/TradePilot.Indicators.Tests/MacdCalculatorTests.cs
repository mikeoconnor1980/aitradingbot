using TradePilot.Indicators;

namespace TradePilot.Indicators.Tests;

[TestClass]
public sealed class MacdCalculatorTests
{
    private static readonly decimal[] KnownCloses = CreateTrendingCloses(40);

    [TestMethod]
    public void GivenInsufficientData_WhenCalculate_ThenReturnsNull()
    {
        var closes = KnownCloses.Take(33).ToList();

        var result = MacdCalculator.Calculate(closes);

        result.Should().BeNull();
    }

    [TestMethod]
    public void GivenKnownDataset_WhenCalculate_ThenMatchesExpectedValues()
    {
        var result = MacdCalculator.Calculate(KnownCloses);

        result.Should().NotBeNull();
        result!.Line.Should().BeApproximately(3.491923221999m, 0.000000001m);
        result.Signal.Should().BeApproximately(3.500991884646m, 0.000000001m);
        result.Histogram.Should().BeApproximately(-0.009068662647m, 0.000000001m);
    }

    [TestMethod]
    public void GivenKnownDataset_WhenCalculate_ThenHistogramEqualsLineMinusSignal()
    {
        var result = MacdCalculator.Calculate(KnownCloses);

        result.Should().NotBeNull();
        result!.Histogram.Should().Be(result.Line - result.Signal);
    }

    [TestMethod]
    public void GivenUptrend_WhenCalculate_ThenMacdLineIsPositive()
    {
        var result = MacdCalculator.Calculate(KnownCloses);

        result.Should().NotBeNull();
        result!.Line.Should().BeGreaterThan(0m);
    }

    [TestMethod]
    public void GivenCustomParameters_WhenCalculate_ThenReturnsResult()
    {
        var result = MacdCalculator.Calculate(KnownCloses, 8, 17, 9);

        result.Should().NotBeNull();
    }

    [TestMethod]
    public void GivenEmptyCloses_WhenCalculate_ThenReturnsNull()
    {
        var result = MacdCalculator.Calculate([]);

        result.Should().BeNull();
    }

    [TestMethod]
    public void GivenKnownDataset_WhenCalculateSeries_ThenLastValueMatchesCalculate()
    {
        var series = MacdCalculator.CalculateSeries(KnownCloses);
        var singleValue = MacdCalculator.Calculate(KnownCloses);

        series.Should().HaveCount(KnownCloses.Length);
        series.Take(33).Should().AllSatisfy(value => value.Should().BeNull());
        series[^1].Should().BeEquivalentTo(singleValue);
    }

    private static decimal[] CreateTrendingCloses(int count)
    {
        var closes = new decimal[count];
        var basePrice = 100m;

        for (var index = 0; index < count; index++)
        {
            closes[index] = basePrice + (index * 0.5m) + (index % 3 == 0 ? -0.2m : 0.1m);
        }

        return closes;
    }
}