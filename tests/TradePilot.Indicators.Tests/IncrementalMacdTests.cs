using TradePilot.Indicators.Incremental;

namespace TradePilot.Indicators.Tests;

[TestClass]
public sealed class IncrementalMacdTests
{
    private static readonly decimal[] Closes =
        [44.34m, 44.09m, 43.61m, 44.33m, 44.83m, 45.10m, 45.42m, 45.84m, 46.08m, 45.89m,
         46.03m, 45.61m, 46.28m, 46.28m, 46.00m, 46.03m, 46.41m, 46.22m, 45.64m, 46.21m,
         46.25m, 45.71m, 46.45m, 45.78m, 45.35m, 44.03m, 44.18m, 44.22m, 44.57m, 43.42m,
         42.66m, 43.13m, 43.50m, 43.65m, 43.56m, 43.74m, 44.07m, 43.61m, 44.33m, 44.83m,
         45.10m, 45.42m, 45.84m, 46.08m, 45.89m];

    [TestMethod]
    public void GivenCloses_WhenAddedIncrementally_ThenMatchesStaticCalculation()
    {
        var incremental = new IncrementalMacd(12, 26, 9);
        var staticResult = MacdCalculator.Calculate(Closes.ToList(), 12, 26, 9);

        foreach (var close in Closes)
        {
            incremental.Add(close);
        }

        staticResult.Should().NotBeNull();
        incremental.Line.Should().NotBeNull();
        incremental.Line!.Value.Should().BeApproximately(staticResult!.Line, 0.0000001m,
            because: "incremental MACD line should match static");
        incremental.Signal!.Value.Should().BeApproximately(staticResult.Signal, 0.0000001m,
            because: "incremental MACD signal should match static");
        incremental.Histogram!.Value.Should().BeApproximately(staticResult.Histogram, 0.0000001m,
            because: "incremental MACD histogram should match static");
    }

    [TestMethod]
    public void GivenInsufficientDataForSignal_WhenAdded_ThenSignalIsNull()
    {
        var macd = new IncrementalMacd(12, 26, 9);

        for (var i = 0; i < 26; i++)
        {
            macd.Add(Closes[i]);
        }

        macd.Line.Should().NotBeNull("slow period is met so MACD line should exist");
        macd.Signal.Should().BeNull("not enough MACD line values for signal EMA");
    }

    [TestMethod]
    public void GivenInsufficientDataForLine_WhenAdded_ThenLineIsNull()
    {
        var macd = new IncrementalMacd(12, 26, 9);

        for (var i = 0; i < 25; i++)
        {
            macd.Add(Closes[i]);
        }

        macd.Line.Should().BeNull("slow period not yet met");
    }
}
