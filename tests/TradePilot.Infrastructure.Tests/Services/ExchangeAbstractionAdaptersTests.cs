using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;
using TradePilot.Domain.ValueObjects;
using TradePilot.Infrastructure.Binance;
using TradePilot.Infrastructure.Hyperliquid;

namespace TradePilot.Infrastructure.Tests.Services;

[TestClass]
public sealed class ExchangeAbstractionAdaptersTests
{
    [TestMethod]
    public void GivenHyperliquidSymbolMapper_WhenRoundTripped_ThenCanonicalPerpIsPreserved()
    {
        IExchangeSymbolMapper mapper = new HyperliquidAssetMapper();
        var pair = TradingPair.Create("BTC", "USD", AssetType.Perp);

        var exchangeSymbol = mapper.ToExchangeSymbol(pair);
        var roundTrip = mapper.FromExchangeSymbol(exchangeSymbol);

        exchangeSymbol.Should().Be("BTC");
        roundTrip.Canonical.Should().Be("BTC/USD:PERP");
    }

    [TestMethod]
    public void GivenBinanceSymbolMapper_WhenRoundTripped_ThenCanonicalPerpIsPreserved()
    {
        IExchangeSymbolMapper mapper = new BinanceAssetMapper();
        var pair = TradingPair.Create("ETH", "USD", AssetType.Perp);

        var exchangeSymbol = mapper.ToExchangeSymbol(pair);
        var roundTrip = mapper.FromExchangeSymbol(exchangeSymbol);

        exchangeSymbol.Should().Be("ETHUSDT");
        roundTrip.Canonical.Should().Be("ETH/USD:PERP");
    }

    [TestMethod]
    public void GivenHyperliquidCapabilities_WhenSpotPairChecked_ThenUnsupported()
    {
        IExchangeCapabilities capabilities = new HyperliquidCapabilities();
        var spotPair = TradingPair.Create("BTC", "USD", AssetType.Spot);

        capabilities.Supports(spotPair).Should().BeFalse();
        capabilities.CapabilitySet.SupportedProductTypes.Should().Contain(AssetType.Perp);
    }

    [TestMethod]
    public async Task GivenHyperliquidMetadataProvider_WhenMetaContainsLeverage_ThenReturnsValue()
    {
        var restClient = new Mock<IHyperliquidRestClient>();
        restClient.Setup(client => client.GetMarketInfoAsync("BTC", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarketInfoDto { Asset = "BTC", MidPrice = 1m });
        restClient.Setup(client => client.PostInfoAsync<System.Text.Json.JsonElement>(
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>("""{"universe":[{"name":"BTC","maxLeverage":50}]}"""));

        var provider = new HyperliquidMarketMetadataProvider(restClient.Object);

        var leverage = await provider.GetMaxLeverageAsync(TradingPair.Create("BTC", "USD", AssetType.Perp));

        leverage.Should().Be(50);
    }

    [TestMethod]
    public async Task GivenHyperliquidAccountAdapter_WhenPairProvided_ThenBaseAssetIsPassedThrough()
    {
        var accountService = new Mock<IHyperliquidAccountService>();
        accountService.Setup(service => service.GetRecentFillsAsync("BTC", "wallet", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FillEventDto>());

        var adapter = new HyperliquidAccountAdapter(accountService.Object);

        _ = await adapter.GetRecentFillsAsync(TradingPair.Create("BTC", "USD", AssetType.Perp), "wallet", CancellationToken.None);

        accountService.Verify(service => service.GetRecentFillsAsync("BTC", "wallet", It.IsAny<CancellationToken>()), Times.Once);
    }
}