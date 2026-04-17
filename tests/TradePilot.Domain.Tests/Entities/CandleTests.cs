using TradePilot.Domain.Entities;

namespace TradePilot.Domain.Tests.Entities;

[TestClass]
public sealed class CandleTests
{
    [TestMethod]
    public void GivenValidInputs_WhenCreate_ThenReturnsCandle()
    {
        var candle = Candle.Create("BTC", "15m", 1710000000000, 67000m, 67500m, 66800m, 67200m, 1234.56m, 42);

        candle.Symbol.Should().Be("BTC");
        candle.Interval.Should().Be("15m");
        candle.Timestamp.Should().Be(1710000000000);
        candle.Source.Should().Be("Hyperliquid");
        candle.Open.Should().Be(67000m);
        candle.High.Should().Be(67500m);
        candle.Low.Should().Be(66800m);
        candle.Close.Should().Be(67200m);
        candle.Volume.Should().Be(1234.56m);
        candle.NumTrades.Should().Be(42);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    public void GivenInvalidSymbol_WhenCreate_ThenThrowsArgumentException(string? symbol)
    {
        var act = () => Candle.Create(symbol!, "15m", 1000, 100m, 105m, 95m, 102m, 50m, 10);

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    public void GivenInvalidInterval_WhenCreate_ThenThrowsArgumentException(string? interval)
    {
        var act = () => Candle.Create("BTC", interval!, 1000, 100m, 105m, 95m, 102m, 50m, 10);

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    public void GivenInvalidSource_WhenCreate_ThenThrowsArgumentException(string? source)
    {
        var act = () => Candle.Create("BTC", "15m", 1000, 100m, 105m, 95m, 102m, 50m, 10, source!);

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void GivenExplicitSource_WhenCreate_ThenSourceIsSet()
    {
        var candle = Candle.Create("BTC", "15m", 1710000000000, 67000m, 67500m, 66800m, 67200m, 1234.56m, 42, source: "Binance");

        candle.Source.Should().Be("Binance");
    }
}
