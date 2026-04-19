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
    public void GivenSupportedCoins_WhenRetrieved_ThenIbctIsNotExposed()
    {
        var result = HyperliquidAssetMapper.GetSupportedCoins();

        result.Should().NotContain("IBTC");
        result.Should().Contain("BTC");
    }
}