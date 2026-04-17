using TradePilot.Application.Trading.Services;

namespace TradePilot.Application.Tests.Trading.Services;

[TestClass]
public sealed class LeverageCalculatorTests
{
    [TestMethod]
    public void GivenTwoPercentSLAnd50xMax_WhenCalculated_ThenReturns33()
    {
        var result = LeverageCalculator.CalculateLeverage(2m, 50);

        result.Should().Be(33);
    }

    [TestMethod]
    public void GivenOnePercentSLAnd20xMax_WhenCalculated_ThenReturns20()
    {
        var result = LeverageCalculator.CalculateLeverage(1m, 20);

        result.Should().Be(20);
    }

    [TestMethod]
    public void GivenVeryLargeSL_WhenCalculated_ThenReturnsOne()
    {
        var result = LeverageCalculator.CalculateLeverage(50m, 50);

        result.Should().Be(1);
    }

    [TestMethod]
    public void GivenCalculatedExceedsMax_WhenClamped_ThenReturnsMax()
    {
        var result = LeverageCalculator.CalculateLeverage(0.5m, 20);

        result.Should().Be(20);
    }

    [TestMethod]
    public void GivenZeroMaxLeverage_WhenCalculated_ThenUsesFallback()
    {
        var result = LeverageCalculator.CalculateLeverage(2m, 0);

        result.Should().BeGreaterThan(0);
        result.Should().BeLessThanOrEqualTo(LeverageCalculator.FallbackMaxLeverage);
    }

    [TestMethod]
    public void GivenDeriveMaintenanceMarginRate_WhenMaxIs50_ThenReturnsOnePercent()
    {
        var rate = LeverageCalculator.DeriveMaintenanceMarginRate(50);

        rate.Should().Be(0.01m);
    }

    [TestMethod]
    public void GivenDeriveMaintenanceMarginRate_WhenMaxIs20_ThenReturnsTwoPointFivePercent()
    {
        var rate = LeverageCalculator.DeriveMaintenanceMarginRate(20);

        rate.Should().Be(0.025m);
    }
}