using TradePilot.Indicators.Incremental;

namespace TradePilot.Indicators.Tests;

[TestClass]
public sealed class IncrementalRsiTests
{
    private static readonly decimal[] Closes =
        [44.34m, 44.09m, 43.61m, 44.33m, 44.83m, 45.10m, 45.42m, 45.84m, 46.08m, 45.89m,
         46.03m, 45.61m, 46.28m, 46.28m, 46.00m, 46.03m, 46.41m, 46.22m, 45.64m, 46.21m,
         46.25m, 45.71m, 46.45m, 45.78m, 45.35m, 44.03m, 44.18m, 44.22m, 44.57m, 43.42m];

    [TestMethod]
    public void GivenCloses_WhenAddedIncrementally_ThenMatchesStaticCalculation()
    {
        var period = 14;
        var incremental = new IncrementalRsi(period);
        var staticResult = RsiCalculator.Calculate(Closes.ToList(), period);

        foreach (var close in Closes)
        {
            incremental.Add(close);
        }

        incremental.Current.Should().Be(staticResult,
            because: $"incremental RSI({period}) should match static for {Closes.Length} values");
    }

    [TestMethod]
    public void GivenInsufficientData_WhenAdded_ThenCurrentIsNull()
    {
        var rsi = new IncrementalRsi(14);
        for (var i = 0; i < 14; i++)
        {
            rsi.Add(Closes[i]);
        }

        rsi.Current.Should().BeNull("need period+1 values to produce first RSI");
    }

    [TestMethod]
    public void GivenPartialData_WhenAddedIncrementally_ThenMatchesStaticAtEachStep()
    {
        var period = 14;
        var incremental = new IncrementalRsi(period);

        for (var count = 1; count <= Closes.Length; count++)
        {
            incremental.Add(Closes[count - 1]);
            var staticResult = RsiCalculator.Calculate(Closes.Take(count).ToList(), period);
            incremental.Current.Should().Be(staticResult,
                because: $"incremental RSI({period}) should match static after {count} values");
        }
    }

    [TestMethod]
    public void GivenDifferentPeriods_WhenAddedIncrementally_ThenEachMatchesStatic()
    {
        foreach (var period in new[] { 7, 14, 21 })
        {
            var incremental = new IncrementalRsi(period);
            var staticResult = RsiCalculator.Calculate(Closes.ToList(), period);

            foreach (var close in Closes)
            {
                incremental.Add(close);
            }

            incremental.Current.Should().Be(staticResult,
                because: $"incremental RSI({period}) should match static");
        }
    }
}
