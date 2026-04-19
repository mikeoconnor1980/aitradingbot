using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;
using TradePilot.Application.MarketData.Queries;

namespace TradePilot.Application.Tests.MarketData.Queries;

[TestClass]
public sealed class GetMarketInfoQueryHandlerTests
{
    [TestMethod]
    public async Task GivenMappedAsset_WhenHandle_ThenUsesExchangeMetadataProvider()
    {
        var expected = new MarketInfoDto { Asset = "BTC", MidPrice = 100m };
        var provider = new Mock<IExchangeMarketMetadataProvider>();
        provider.SetupGet(p => p.Exchange).Returns(Exchange.Hyperliquid);
        provider.Setup(p => p.GetMarketInfoAsync(
                It.Is<TradingPair>(pair => pair.Canonical == "BTC/USD:PERP"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var mapper = new Mock<IExchangeSymbolMapper>();
        mapper.SetupGet(m => m.Exchange).Returns(Exchange.Hyperliquid);
        mapper.Setup(m => m.FromExchangeSymbol("BTC-PERP"))
            .Returns(TradingPair.Create("BTC", "USD", AssetType.Perp));

        var sut = new GetMarketInfoQueryHandler([provider.Object], [mapper.Object]);

        var result = await sut.Handle(new GetMarketInfoQuery("BTC-PERP"), CancellationToken.None);

        result.Should().BeSameAs(expected);
    }

    [TestMethod]
    public async Task GivenMissingAsset_WhenHandle_ThenThrowsNotFound()
    {
        var provider = new Mock<IExchangeMarketMetadataProvider>();
        provider.SetupGet(p => p.Exchange).Returns(Exchange.Hyperliquid);
        provider.Setup(p => p.GetMarketInfoAsync(It.IsAny<TradingPair>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MarketInfoDto?)null);

        var mapper = new Mock<IExchangeSymbolMapper>();
        mapper.SetupGet(m => m.Exchange).Returns(Exchange.Hyperliquid);
        mapper.Setup(m => m.FromExchangeSymbol("BTC-PERP"))
            .Returns(TradingPair.Create("BTC", "USD", AssetType.Perp));

        var sut = new GetMarketInfoQueryHandler([provider.Object], [mapper.Object]);

        var act = () => sut.Handle(new GetMarketInfoQuery("BTC-PERP"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}