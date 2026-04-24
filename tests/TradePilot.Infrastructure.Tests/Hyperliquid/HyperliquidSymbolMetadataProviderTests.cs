using System.Text.Json;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Infrastructure.Hyperliquid;

namespace TradePilot.Infrastructure.Tests.Hyperliquid;

[TestClass]
public sealed class HyperliquidSymbolMetadataProviderTests
{
    private Mock<IHyperliquidRestClient> _restClientMock = null!;

    [TestInitialize]
    public void Setup()
    {
        _restClientMock = new Mock<IHyperliquidRestClient>(MockBehavior.Strict);
    }

    [TestMethod]
    public async Task GivenMetaUniverse_WhenGetSupportedSymbolsAsync_ThenReturnsMappedMetadata()
    {
        _restClientMock
            .Setup(client => client.PostInfoAsync<JsonElement>(
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonDocument.Parse(
                """
                {
                  "universe": [
                    { "name": "BTC", "szDecimals": 5, "maxLeverage": 40 }
                  ]
                }
                """).RootElement.Clone());

        var sut = new HyperliquidSymbolMetadataProvider(_restClientMock.Object);

        var result = await sut.GetSupportedSymbolsAsync();

        result.Should().ContainSingle();
        result[0].Should().BeEquivalentTo(new ExchangeSymbolMetadata("BTC", "BTC", 5, 1, 40));
    }

    [TestMethod]
    public async Task GivenKnownAsset_WhenGetSymbolAsync_ThenReturnsMatchingMetadata()
    {
        _restClientMock
            .Setup(client => client.PostInfoAsync<JsonElement>(
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonDocument.Parse(
                """
                {
                  "universe": [
                    { "name": "ETH", "szDecimals": 4, "maxLeverage": 25 }
                  ]
                }
                """).RootElement.Clone());

        var sut = new HyperliquidSymbolMetadataProvider(_restClientMock.Object);

        var result = await sut.GetSymbolAsync("ETH");

        result.Should().BeEquivalentTo(new ExchangeSymbolMetadata("ETH", "ETH", 4, 2, 25));
    }
}