using TradingApp.Indicators.Incremental;

namespace TradingApp.Indicators.Tests;

[TestClass]
public sealed class IncrementalAtrTests
{
    private static readonly (decimal High, decimal Low, decimal Close)[] Bars =
        [(48.70m, 47.79m, 48.16m), (48.72m, 48.14m, 48.61m), (48.90m, 48.39m, 48.75m),
         (48.87m, 48.37m, 48.63m), (48.82m, 48.24m, 48.74m), (49.05m, 48.64m, 49.03m),
         (49.20m, 48.94m, 49.07m), (49.35m, 48.86m, 49.32m), (49.92m, 49.50m, 49.91m),
         (50.19m, 49.87m, 50.13m), (50.12m, 49.20m, 49.53m), (49.66m, 48.90m, 49.50m),
         (49.88m, 49.43m, 49.75m), (50.19m, 49.73m, 50.03m), (50.36m, 49.26m, 50.31m),
         (50.57m, 50.09m, 50.52m), (50.65m, 50.30m, 50.41m), (50.43m, 49.21m, 49.34m),
         (49.63m, 48.98m, 49.37m), (50.33m, 49.61m, 50.23m)];

    [TestMethod]
    public void GivenBars_WhenAddedIncrementally_ThenMatchesStaticCalculation()
    {
        var period = 14;
        var incremental = new IncrementalAtr(period);
        var staticResult = AtrCalculator.Calculate(Bars.ToList(), period);

        foreach (var bar in Bars)
        {
            incremental.Add(bar.High, bar.Low, bar.Close);
        }

        incremental.Current.Should().Be(staticResult,
            because: $"incremental ATR({period}) should match static for {Bars.Length} values");
    }

    [TestMethod]
    public void GivenInsufficientData_WhenAdded_ThenCurrentIsNull()
    {
        var atr = new IncrementalAtr(14);
        for (var i = 0; i < 14; i++)
        {
            atr.Add(Bars[i].High, Bars[i].Low, Bars[i].Close);
        }

        atr.Current.Should().BeNull("need period+1 bars to produce first ATR");
    }

    [TestMethod]
    public void GivenPartialData_WhenAddedIncrementally_ThenMatchesStaticAtEachStep()
    {
        var period = 14;
        var incremental = new IncrementalAtr(period);

        for (var count = 1; count <= Bars.Length; count++)
        {
            incremental.Add(Bars[count - 1].High, Bars[count - 1].Low, Bars[count - 1].Close);
            var staticResult = AtrCalculator.Calculate(Bars.Take(count).ToList(), period);
            incremental.Current.Should().Be(staticResult,
                because: $"incremental ATR({period}) should match static after {count} bars");
        }
    }
}
