using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Infrastructure.Binance;

namespace TradingApp.Infrastructure.Tests.Services;

[TestClass]
public sealed class BinanceAssetMapperTests
{
    [TestMethod]
    [DataRow("BTC", "BTCUSDT")]
    [DataRow("ETH", "ETHUSDT")]
    [DataRow("SOL", "SOLUSDT")]
    [DataRow("DOGE", "DOGEUSDT")]
    [DataRow("AVAX", "AVAXUSDT")]
    [DataRow("ARB", "ARBUSDT")]
    [DataRow("LINK", "LINKUSDT")]
    [DataRow("OP", "OPUSDT")]
    [DataRow("BTC-USD", "BTCUSDT")]
    [DataRow("ETH-PERP", "ETHUSDT")]
    public void GivenValidSymbol_WhenToFuturesSymbol_ThenReturnsBinanceSymbol(string displaySymbol, string expected)
    {
        var result = BinanceAssetMapper.ToFuturesSymbol(displaySymbol);

        result.Should().Be(expected);
    }

    [TestMethod]
    public void GivenInvalidSymbol_WhenToFuturesSymbol_ThenThrowsDomainException()
    {
        var act = () => BinanceAssetMapper.ToFuturesSymbol("INVALID");

        act.Should().Throw<DomainException>();
    }

    [TestMethod]
    [DataRow("BTC", true)]
    [DataRow("btc", true)]
    [DataRow("BTC-USD", true)]
    [DataRow("btc-perp", true)]
    [DataRow("INVALID", false)]
    public void GivenSymbol_WhenIsValidSymbol_ThenReturnsExpectedResult(string symbol, bool expected)
    {
        var result = BinanceAssetMapper.IsValidSymbol(symbol);

        result.Should().Be(expected);
    }

    [TestMethod]
    [DataRow("BTC-USD", "BTC")]
    [DataRow("ETH-PERP", "ETH")]
    [DataRow("SOL-USDT", "SOL")]
    [DataRow("ARB", "ARB")]
    public void GivenDisplaySymbol_WhenNormalizeSymbol_ThenReturnsBinanceDisplaySymbol(string symbol, string expected)
    {
        var result = BinanceAssetMapper.NormalizeSymbol(symbol);

        result.Should().Be(expected);
    }

    [TestMethod]
    [DataRow("5m", 300_000L)]
    [DataRow("15m", 900_000L)]
    [DataRow("1h", 3_600_000L)]
    [DataRow("4h", 14_400_000L)]
    [DataRow("1d", 86_400_000L)]
    public void GivenValidInterval_WhenGetIntervalMs_ThenReturnsMilliseconds(string interval, long expected)
    {
        var result = BinanceAssetMapper.GetIntervalMs(interval);

        result.Should().Be(expected);
    }

    [TestMethod]
    public void GivenInvalidInterval_WhenGetIntervalMs_ThenThrowsDomainException()
    {
        var act = () => BinanceAssetMapper.GetIntervalMs("2h");

        act.Should().Throw<DomainException>();
    }

    [TestMethod]
    [DataRow("5m", true)]
    [DataRow("1d", true)]
    [DataRow("2h", false)]
    public void GivenInterval_WhenIsValidInterval_ThenReturnsExpectedResult(string interval, bool expected)
    {
        var result = BinanceAssetMapper.IsValidInterval(interval);

        result.Should().Be(expected);
    }
}