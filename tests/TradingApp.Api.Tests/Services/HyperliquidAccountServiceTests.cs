using System.Text.Json;
using Microsoft.Extensions.Logging;
using TradingApp.Api.Services;
using TradingApp.Application.Abstractions.Services;

namespace TradingApp.Api.Tests.Services;

[TestClass]
public sealed class HyperliquidAccountServiceTests
{
    private Mock<IHyperliquidRestClient> _restClientMock = null!;
    private Mock<IHyperliquidSigner> _signerMock = null!;
    private Mock<ILogger<HyperliquidAccountService>> _loggerMock = null!;
    private HyperliquidAccountService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _restClientMock = new Mock<IHyperliquidRestClient>();
        _signerMock = new Mock<IHyperliquidSigner>();
        _loggerMock = new Mock<ILogger<HyperliquidAccountService>>();

        _signerMock.SetupGet(s => s.WalletAddress).Returns("0xTestWallet");

        _sut = new HyperliquidAccountService(
            _restClientMock.Object,
            _signerMock.Object,
            _loggerMock.Object);
    }

    [TestMethod]
    public async Task GivenFundingRatesAvailable_WhenGetPositionsAsync_ThenMapsEnrichedFields()
    {
        _restClientMock
            .Setup(r => r.PostInfoAsync<JsonElement>(
                It.Is<object>(request => RequestHasType(request, "clearinghouseState")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ParseJson(
                """
                {
                  "assetPositions": [
                    {
                      "position": {
                        "coin": "BTC",
                        "szi": "0.0276",
                        "entryPx": "71464",
                        "markPx": "72000",
                        "unrealizedPnl": "14.79",
                        "returnOnEquity": "0.03755",
                        "liquidationPx": "65000",
                        "marginUsed": "393.82",
                        "leverage": { "value": "5", "type": "cross" }
                      }
                    }
                  ]
                }
                """));

        _restClientMock
            .Setup(r => r.PostInfoAsync<JsonElement>(
                It.Is<object>(request => RequestHasType(request, "metaAndAssetCtxs")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ParseJson(
                """
                [
                  {
                    "universe": [
                      { "name": "BTC" },
                      { "name": "ETH" }
                    ]
                  },
                  [
                    { "funding": "-0.0001" },
                    { "funding": "0.0002" }
                  ]
                ]
                """));

        _restClientMock
            .Setup(r => r.PostInfoAsync<JsonElement>(
                It.Is<object>(request => RequestHasType(request, "openOrders")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ParseJson(
                """
                [
                  {
                    "oid": 1001,
                    "coin": "BTC",
                    "side": "A",
                    "limitPx": "64000",
                    "sz": "0.0276",
                    "orderType": { "trigger": { "triggerPx": "64000", "isMarket": true, "tpsl": "sl" } },
                    "status": "open",
                    "reduceOnly": true
                  },
                  {
                    "oid": 1002,
                    "coin": "BTC",
                    "side": "A",
                    "limitPx": "76000",
                    "sz": "0.0276",
                    "orderType": { "trigger": { "triggerPx": "76000", "isMarket": true, "tpsl": "tp" } },
                    "status": "open",
                    "reduceOnly": true
                  }
                ]
                """));

        var result = await _sut.GetPositionsAsync();

        result.Should().ContainSingle();
        var position = result[0];
        position.Asset.Should().Be("BTC");
        position.MarkPrice.Should().Be(72000m);
        position.MarginUsed.Should().Be(393.82m);
        position.FundingRate.Should().Be(-0.0001m);
        position.Leverage.Should().Be(5);
        position.MarginMode.Should().Be("cross");
        position.UnrealisedPnlPercent.Should().Be(3.755m);
        position.StopLossPrice.Should().Be(64000m);
        position.StopLossOrderId.Should().Be("1001");
        position.TakeProfitPrice.Should().Be(76000m);
        position.TakeProfitOrderId.Should().Be("1002");
    }

    [TestMethod]
    public async Task GivenCrossMarginWithoutMarginUsed_WhenGetPositionsAsync_ThenCalculatesMarginFallback()
    {
        _restClientMock
            .Setup(r => r.PostInfoAsync<JsonElement>(
                It.Is<object>(request => RequestHasType(request, "clearinghouseState")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ParseJson(
                """
                {
                  "assetPositions": [
                    {
                      "position": {
                        "coin": "BTC",
                        "szi": "0.0276561797752809",
                        "entryPx": "71464",
                        "markPx": "71200",
                        "unrealizedPnl": "0",
                        "returnOnEquity": "0",
                        "liquidationPx": "65000",
                        "marginUsed": "0",
                        "leverage": { "value": "5", "type": "cross" }
                      }
                    }
                  ]
                }
                """));

        _restClientMock
            .Setup(r => r.PostInfoAsync<JsonElement>(
                It.Is<object>(request => RequestHasType(request, "metaAndAssetCtxs")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ParseJson("[{" + "\"universe\":[{\"name\":\"BTC\"}]} , [{\"funding\":\"0\"}]]"));

        _restClientMock
          .Setup(r => r.PostInfoAsync<JsonElement>(
            It.Is<object>(request => RequestHasType(request, "openOrders")),
            It.IsAny<CancellationToken>()))
          .ReturnsAsync(ParseJson("[]"));

        var result = await _sut.GetPositionsAsync();

        result.Should().ContainSingle();
        result[0].MarginUsed.Should().BeApproximately(393.82m, 0.01m);
    }

    [TestMethod]
    public async Task GivenFundingLookupFails_WhenGetPositionsAsync_ThenReturnsPositionsWithZeroFundingRate()
    {
        _restClientMock
            .Setup(r => r.PostInfoAsync<JsonElement>(
                It.Is<object>(request => RequestHasType(request, "clearinghouseState")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ParseJson(
                """
                {
                  "assetPositions": [
                    {
                      "position": {
                        "coin": "ETH",
                        "szi": "1.5",
                        "entryPx": "3000",
                        "markPx": "3050",
                        "unrealizedPnl": "75",
                        "returnOnEquity": "0.05",
                        "liquidationPx": "2500",
                        "marginUsed": "0",
                        "leverage": { "value": "10", "type": "cross" }
                      }
                    }
                  ]
                }
                """));

        _restClientMock
            .Setup(r => r.PostInfoAsync<JsonElement>(
                It.Is<object>(request => RequestHasType(request, "metaAndAssetCtxs")),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Funding unavailable"));

        _restClientMock
          .Setup(r => r.PostInfoAsync<JsonElement>(
            It.Is<object>(request => RequestHasType(request, "openOrders")),
            It.IsAny<CancellationToken>()))
          .ReturnsAsync(ParseJson("[]"));

        var result = await _sut.GetPositionsAsync();

        result.Should().ContainSingle();
        result[0].MarkPrice.Should().Be(3050m);
        result[0].FundingRate.Should().Be(0m);
        result[0].MarginUsed.Should().Be(457.5m);
    }

      [TestMethod]
      public async Task GivenTriggerOpenOrders_WhenGetOpenOrdersAsync_ThenMapsTriggerFields()
      {
        _restClientMock
          .Setup(r => r.PostInfoAsync<JsonElement>(
            It.Is<object>(request => RequestHasType(request, "openOrders")),
            It.IsAny<CancellationToken>()))
          .ReturnsAsync(ParseJson(
            """
            [
              {
              "oid": 2001,
              "coin": "ETH",
              "side": "B",
              "limitPx": "3200",
              "sz": "1.25",
              "orderType": { "trigger": { "triggerPx": "3200", "isMarket": true, "tpsl": "tp" } },
              "status": "open",
              "reduceOnly": true
              }
            ]
            """));

        var result = await _sut.GetOpenOrdersAsync();

        result.Should().ContainSingle();
        result[0].OrderType.Should().Be("trigger");
        result[0].TriggerPrice.Should().Be(3200m);
        result[0].TpslType.Should().Be("tp");
        result[0].IsReduceOnly.Should().BeTrue();
      }

    private static bool RequestHasType(object request, string type)
    {
        return JsonSerializer.Serialize(request).Contains($"\"type\":\"{type}\"", StringComparison.Ordinal);
    }

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}