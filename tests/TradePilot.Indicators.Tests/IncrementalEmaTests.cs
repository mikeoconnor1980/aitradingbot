using TradePilot.Indicators.Incremental;

namespace TradePilot.Indicators.Tests;

[TestClass]
public sealed class IncrementalEmaTests
{
    private static readonly decimal[] Closes =
        [44.34m, 44.09m, 43.61m, 44.33m, 44.83m, 45.10m, 45.42m, 45.84m, 46.08m, 45.89m,
         46.03m, 45.61m, 46.28m, 46.28m, 46.00m, 46.03m, 46.41m, 46.22m, 45.64m, 46.21m,
         46.25m, 45.71m, 46.45m, 45.78m, 45.35m, 44.03m, 44.18m, 44.22m, 44.57m, 43.42m];

    [TestMethod]
    public void GivenCloses_WhenAddedIncrementally_ThenMatchesStaticCalculation()
    {
        foreach (var period in new[] { 5, 10, 14, 20 })
        {
            var incremental = new IncrementalEma(period);
            var staticResult = EmaCalculator.Calculate(Closes.ToList(), period);

            foreach (var close in Closes)
            {
                incremental.Add(close);
            }

            incremental.Current.Should().Be(staticResult,
                because: $"incremental EMA({period}) should match static for {Closes.Length} values");
        }
    }

    [TestMethod]
    public void GivenInsufficientData_WhenAdded_ThenCurrentIsNull()
    {
        var ema = new IncrementalEma(10);
        for (var i = 0; i < 9; i++)
        {
            ema.Add(Closes[i]);
        }

        ema.Current.Should().BeNull();
    }

    [TestMethod]
    public void GivenExactlyPeriodValues_WhenAdded_ThenCurrentIsSma()
    {
        var period = 5;
        var ema = new IncrementalEma(period);

        for (var i = 0; i < period; i++)
        {
            ema.Add(Closes[i]);
        }

        var expectedSma = Closes.Take(period).Average();
        ema.Current.Should().Be(expectedSma);
    }

    [TestMethod]
    public void GivenPartialData_WhenAddedIncrementally_ThenMatchesStaticAtEachStep()
    {
        var period = 10;
        var incremental = new IncrementalEma(period);

        for (var count = 1; count <= Closes.Length; count++)
        {
            incremental.Add(Closes[count - 1]);
            var staticResult = EmaCalculator.Calculate(Closes.Take(count).ToList(), period);
            incremental.Current.Should().Be(staticResult,
                because: $"incremental EMA({period}) should match static after {count} values");
        }
    }
}
