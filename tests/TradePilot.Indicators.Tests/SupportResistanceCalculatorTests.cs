using TradePilot.Indicators;

namespace TradePilot.Indicators.Tests;

[TestClass]
public sealed class SupportResistanceCalculatorTests
{
    [TestMethod]
    public void GivenInsufficientData_WhenCalculate_ThenReturnsNull()
    {
        var bars = CreateBars([100m, 101m, 102m], [99m, 100m, 101m], [100.5m, 100.5m, 101.5m]);

        var result = SupportResistanceCalculator.Calculate(bars, lookback: 50, strength: 3);

        result.Should().BeNull();
    }

    [TestMethod]
    public void GivenSwingLowBelowPrice_WhenCalculate_ThenReturnsSupportLevel()
    {
        // Create a V-shaped pattern: prices drop then rise, creating a swing low at the bottom
        var highs = new[] { 110m, 108m, 106m, 104m, 102m, 100m, 102m, 104m, 106m, 108m, 110m };
        var lows = new[] { 108m, 106m, 104m, 102m, 100m, 98m, 100m, 102m, 104m, 106m, 108m };
        var closes = new[] { 109m, 107m, 105m, 103m, 101m, 99m, 101m, 103m, 105m, 107m, 109m };
        var bars = CreateBars(highs, lows, closes);

        var result = SupportResistanceCalculator.Calculate(bars, lookback: 20, strength: 2);

        result.Should().NotBeNull();
        result!.Support.Should().Be(98m);
    }

    [TestMethod]
    public void GivenSwingHighAbovePrice_WhenCalculate_ThenReturnsResistanceLevel()
    {
        // Create an inverted V: prices rise then fall, creating a swing high at the top
        var highs = new[] { 100m, 102m, 104m, 106m, 108m, 110m, 108m, 106m, 104m, 102m, 100m };
        var lows = new[] { 98m, 100m, 102m, 104m, 106m, 108m, 106m, 104m, 102m, 100m, 98m };
        var closes = new[] { 99m, 101m, 103m, 105m, 107m, 109m, 107m, 105m, 103m, 101m, 99m };
        var bars = CreateBars(highs, lows, closes);

        var result = SupportResistanceCalculator.Calculate(bars, lookback: 20, strength: 2);

        result.Should().NotBeNull();
        result!.Resistance.Should().Be(110m);
    }

    [TestMethod]
    public void GivenMultipleSwingLows_WhenCalculate_ThenReturnsNearestSupport()
    {
        // Two V-shaped dips: first deeper at 90, second shallower at 95, current price at 100
        var highs = new decimal[] { 105, 103, 100, 95, 93, 95, 100, 103, 105, 103, 100, 98, 100, 103, 105 };
        var lows = new decimal[]  { 103, 100, 97, 93, 90, 93, 97, 100, 103, 100, 97, 95, 97, 100, 103 };
        var closes = new decimal[] { 104, 101, 98, 94, 91, 94, 98, 101, 104, 101, 98, 96, 98, 101, 104 };
        var bars = CreateBars(highs, lows, closes);

        var result = SupportResistanceCalculator.Calculate(bars, lookback: 20, strength: 2);

        result.Should().NotBeNull();
        // Nearest support below current price (104) should be 95, not 90
        result!.Support.Should().Be(95m);
    }

    [TestMethod]
    public void GivenNoSwingPoints_WhenCalculate_ThenReturnsNullLevels()
    {
        // Monotonically rising - no swing lows or highs
        var count = 15;
        var highs = Enumerable.Range(0, count).Select(i => 102m + i).ToArray();
        var lows = Enumerable.Range(0, count).Select(i => 100m + i).ToArray();
        var closes = Enumerable.Range(0, count).Select(i => 101m + i).ToArray();
        var bars = CreateBars(highs, lows, closes);

        var result = SupportResistanceCalculator.Calculate(bars, lookback: 20, strength: 2);

        result.Should().NotBeNull();
        result!.Support.Should().BeNull();
        result!.Resistance.Should().BeNull();
    }

    [TestMethod]
    public void GivenZeroLookback_WhenCalculate_ThenThrows()
    {
        var bars = CreateBars([100m], [99m], [99.5m]);

        var act = () => SupportResistanceCalculator.Calculate(bars, lookback: 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    public void GivenZeroStrength_WhenCalculate_ThenThrows()
    {
        var bars = CreateBars([100m], [99m], [99.5m]);

        var act = () => SupportResistanceCalculator.Calculate(bars, lookback: 50, strength: 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    public void GivenHigherStrength_WhenCalculate_ThenFindsStrongerLevelsOnly()
    {
        // V-shape but with strength=4 requiring more candles on each side
        var highs = new decimal[] { 110, 108, 106, 104, 102, 100, 102, 104, 106, 108, 110 };
        var lows = new decimal[]  { 108, 106, 104, 102, 100, 98, 100, 102, 104, 106, 108 };
        var closes = new decimal[] { 109, 107, 105, 103, 101, 99, 101, 103, 105, 107, 109 };
        var bars = CreateBars(highs, lows, closes);

        var result = SupportResistanceCalculator.Calculate(bars, lookback: 20, strength: 4);

        result.Should().NotBeNull();
        // With strength=4, the swing low at index 5 has exactly 5 bars on each side,
        // so 4 on left and 4 on right from the valid range should work
        result!.Support.Should().Be(98m);
    }

    private static IReadOnlyList<(decimal High, decimal Low, decimal Close)> CreateBars(
        decimal[] highs,
        decimal[] lows,
        decimal[] closes)
    {
        return highs.Select((high, index) => (high, lows[index], closes[index])).ToList();
    }
}
