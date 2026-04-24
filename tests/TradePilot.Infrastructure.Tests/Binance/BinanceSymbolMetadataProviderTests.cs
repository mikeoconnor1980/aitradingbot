using TradePilot.Application.Abstractions.Services;
using TradePilot.Infrastructure.Binance;

namespace TradePilot.Infrastructure.Tests.Binance;

[TestClass]
public sealed class BinanceSymbolMetadataProviderTests
{
    private Mock<IBinanceExchangeInfoCache> _cacheMock = null!;

    [TestInitialize]
    public void Setup()
    {
        _cacheMock = new Mock<IBinanceExchangeInfoCache>(MockBehavior.Strict);
    }

    [TestMethod]
    public async Task GivenCacheWithSymbols_WhenGetSupportedSymbolsAsync_ThenReturnsMappedMetadata()
    {
        _cacheMock
            .Setup(cache => cache.GetSupportedSymbolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, BinanceExchangeSymbolMetadata>
            {
                ["BTC"] = new("BTC", "BTCUSDT", 3, 1, 125),
            });

        var sut = new BinanceSymbolMetadataProvider(_cacheMock.Object);

        var result = await sut.GetSupportedSymbolsAsync();

        result.Should().ContainSingle();
        result[0].Should().BeEquivalentTo(new ExchangeSymbolMetadata("BTC", "BTCUSDT", 3, 1, 125));
    }

    [TestMethod]
    public async Task GivenKnownAsset_WhenGetSymbolAsync_ThenReturnsMappedMetadata()
    {
        _cacheMock
            .Setup(cache => cache.GetSymbolAsync("ETH", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BinanceExchangeSymbolMetadata("ETH", "ETHUSDT", 3, 2, 100));

        var sut = new BinanceSymbolMetadataProvider(_cacheMock.Object);

        var result = await sut.GetSymbolAsync("ETH");

        result.Should().BeEquivalentTo(new ExchangeSymbolMetadata("ETH", "ETHUSDT", 3, 2, 100));
    }
}