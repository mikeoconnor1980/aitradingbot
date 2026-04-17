using TradePilot.Indicators.Incremental;

namespace TradePilot.Indicators.Tests;

[TestClass]
public sealed class IncrementalSmaTests
{
    private static readonly decimal[] Closes =
        [44.34m, 44.09m, 43.61m, 44.33m, 44.83m, 45.10m, 45.42m, 45.84m, 46.08m, 45.89m];

    [TestMethod]
    public void GivenCloses_WhenAddedIncrementally_ThenMatchesManualSma()
    {
        var period = 5;
        var sma = new IncrementalSma(period);

        foreach (var close in Closes)
        {
            sma.Add(close);
        }

        var expected = Closes.Skip(Closes.Length - period).Take(period).Average();
        sma.Current.Should().Be(expected);
    }

    [TestMethod]
    public void GivenExactlyPeriodValues_WhenAdded_ThenCurrentIsAverage()
    {
        var period = 5;
        var sma = new IncrementalSma(period);

        for (var i = 0; i < period; i++)
        {
            sma.Add(Closes[i]);
        }

        var expected = Closes.Take(period).Average();
        sma.Current.Should().Be(expected);
    }

    [TestMethod]
    public void GivenFewerThanPeriodValues_WhenAdded_ThenAveragesAvailableValues()
    {
        var sma = new IncrementalSma(10);
        sma.Add(10m);
        sma.Add(20m);
        sma.Add(30m);

        sma.Current.Should().Be(20m);
    }

    [TestMethod]
    public void GivenSlidingWindow_WhenOldValueDropped_ThenSumUpdatesCorrectly()
    {
        var sma = new IncrementalSma(3);
        sma.Add(10m);
        sma.Add(20m);
        sma.Add(30m);
        sma.Current.Should().Be(20m, because: "SMA(3) of [10,20,30] = 20");

        sma.Add(40m);
        sma.Current.Should().Be(30m, because: "SMA(3) of [20,30,40] = 30");

        sma.Add(50m);
        sma.Current.Should().Be(40m, because: "SMA(3) of [30,40,50] = 40");
    }
}
