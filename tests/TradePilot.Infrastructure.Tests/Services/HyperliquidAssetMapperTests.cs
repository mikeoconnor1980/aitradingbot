using TradePilot.Infrastructure.Hyperliquid;

namespace TradePilot.Infrastructure.Tests.Services;

[TestClass]
public sealed class HyperliquidAssetMapperTests
{
    [TestMethod]
    [DataRow("BTC-PERP", "BTC")]
    [DataRow("BTCUSDT", "BTC")]
    [DataRow("BTC-USD", "BTC")]
    [DataRow("BTC/USDC", "BTC")]
    [DataRow("BTC.P", "BTC")]
    [DataRow("ETH", "ETH")]
    [DataRow("1000PEPEUSDT", "1000PEPE")]
    public void GivenMarketSymbol_WhenToCoin_ThenReturnsHyperliquidCoin(string market, string expected)
    {
        var result = HyperliquidAssetMapper.ToCoin(market);

        result.Should().Be(expected);
    }

    [TestMethod]
    [DataRow("BTC", "BTC-PERP")]
    [DataRow("ETH", "ETH-PERP")]
    [DataRow("1000PEPE", "1000PEPE-PERP")]
    public void GivenCoin_WhenToDisplayName_ThenReturnsExpectedDisplaySymbol(string coin, string expected)
    {
        var result = HyperliquidAssetMapper.ToDisplayName(coin);

        result.Should().Be(expected);
    }

    [TestMethod]
    public void GivenSupportedCoins_WhenRetrieved_ThenQuickPickSubsetReturned()
    {
        var result = HyperliquidAssetMapper.GetSupportedCoins();

        result.Should().NotContain("IBTC");
        result.Should().Contain("BTC");
    }

    [TestMethod]
    [DataRow("BTC")]
    [DataRow("ETH")]
    [DataRow("WIF")]
    [DataRow("PEPE")]
    [DataRow("XYZ:TSLA")]
    [DataRow("CASH:USA500")]
    public void GivenValidCoinName_WhenIsValidCoin_ThenReturnsTrue(string coin)
    {
        var result = HyperliquidAssetMapper.IsValidCoin(coin);

        result.Should().BeTrue();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(":")]
    [DataRow("BTC::USD")]
    [DataRow("BTC-PERP")]
    public void GivenInvalidCoinName_WhenIsValidCoin_ThenReturnsFalse(string? coin)
    {
        var result = HyperliquidAssetMapper.IsValidCoin(coin!);

        result.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("1m")]
    [DataRow("3m")]
    [DataRow("5m")]
    [DataRow("15m")]
    [DataRow("30m")]
    [DataRow("1h")]
    [DataRow("2h")]
    [DataRow("4h")]
    [DataRow("8h")]
    [DataRow("12h")]
    [DataRow("1d")]
    [DataRow("1w")]
    [DataRow("1M")]
    public void GivenSupportedTimeframe_WhenIsValidTimeframe_ThenReturnsTrue(string timeframe)
    {
        var result = HyperliquidAssetMapper.IsValidTimeframe(timeframe);

        result.Should().BeTrue();
    }

    [TestMethod]
    public void GivenAllHyperliquidTimeframes_WhenRetrieved_ThenReturnsFullSupportedSet()
    {
        var result = HyperliquidAssetMapper.GetSupportedTimeframes();

        result.Should().HaveCount(13);
    }
}