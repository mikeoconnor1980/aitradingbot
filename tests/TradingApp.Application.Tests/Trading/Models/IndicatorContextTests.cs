using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.Tests.Trading.Models;

[TestClass]
public sealed class IndicatorContextTests
{
    [TestMethod]
    public void GivenRsiSet_WhenGetRsi_ThenReturnsValue()
    {
        var context = new IndicatorContext();
        context.SetRsi(14, 35m, 28m);

        context.GetRsi(14).Should().Be(35m);
        context.GetPreviousRsi(14).Should().Be(28m);
    }

    [TestMethod]
    public void GivenRsiNotSet_WhenGetRsi_ThenReturnsNull()
    {
        var context = new IndicatorContext();

        context.GetRsi(20).Should().BeNull();
    }

    [TestMethod]
    public void GivenDifferentPeriods_WhenGetRsi_ThenReturnsCorrectValue()
    {
        var context = new IndicatorContext();
        context.SetRsi(14, 35m);
        context.SetRsi(21, 50m);

        context.GetRsi(14).Should().Be(35m);
        context.GetRsi(21).Should().Be(50m);
    }

    [TestMethod]
    public void GivenNoPreviousValue_WhenGetPreviousRsi_ThenReturnsNull()
    {
        var context = new IndicatorContext();
        context.SetRsi(14, 35m);

        context.GetPreviousRsi(14).Should().BeNull();
    }

    [TestMethod]
    public void GivenSmaSet_WhenGetSma_ThenReturnsValue()
    {
        var context = new IndicatorContext();
        context.SetSma(20, 42000m, 41900m);

        context.GetSma(20).Should().Be(42000m);
        context.GetPreviousSma(20).Should().Be(41900m);
    }

    [TestMethod]
    public void GivenSmaNotSet_WhenGetSma_ThenReturnsNull()
    {
        var context = new IndicatorContext();

        context.GetSma(20).Should().BeNull();
    }

    [TestMethod]
    public void GivenMacdSet_WhenGetMacd_ThenReturnsLineSignalAndHistogramValues()
    {
        var context = new IndicatorContext();
        context.SetMacd(12, 26, 9, 1.25m, 0.95m, 0.30m, 1.10m, 0.90m, 0.20m);

        context.GetMacd(12, 26, 9).Should().Be(1.25m);
        context.GetPreviousMacd(12, 26, 9).Should().Be(1.10m);
        context.GetMacdSignal(12, 26, 9).Should().Be(0.95m);
        context.GetPreviousMacdSignal(12, 26, 9).Should().Be(0.90m);
        context.GetMacdHistogram(12, 26, 9).Should().Be(0.30m);
        context.GetPreviousMacdHistogram(12, 26, 9).Should().Be(0.20m);
    }

    [TestMethod]
    public void GivenMacdSignalAndHistogramNotSet_WhenGetMacdSignalAndHistogram_ThenReturnsNull()
    {
        var context = new IndicatorContext();

        context.GetMacdSignal(12, 26, 9).Should().BeNull();
        context.GetPreviousMacdSignal(12, 26, 9).Should().BeNull();
        context.GetMacdHistogram(12, 26, 9).Should().BeNull();
        context.GetPreviousMacdHistogram(12, 26, 9).Should().BeNull();
    }
}