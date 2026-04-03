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
}