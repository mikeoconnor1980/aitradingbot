using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using TradingApp.Api.Models;
using TradingApp.Api.Services;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.MarketData.Models;
using TradingApp.Infrastructure.Hyperliquid.Models;

namespace TradingApp.Api.Tests.Services;

[TestClass]
public sealed class HyperliquidOrderServiceTests
{
    private Mock<IHyperliquidRestClient> _restClientMock = default!;
    private Mock<IHyperliquidSigner> _signerMock = default!;
    private Mock<INonceProvider> _nonceProviderMock = default!;
    private Mock<IHyperliquidAccountService> _accountServiceMock = default!;
    private Mock<IHyperliquidAssetMetadataCache> _metadataCacheMock = default!;
    private Mock<ILogger<HyperliquidOrderService>> _loggerMock = default!;
    private IOptions<HyperliquidOptions> _options = default!;
    private HyperliquidOrderService _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _restClientMock = new Mock<IHyperliquidRestClient>();
        _signerMock = new Mock<IHyperliquidSigner>();
        _nonceProviderMock = new Mock<INonceProvider>();
        _accountServiceMock = new Mock<IHyperliquidAccountService>();
        _metadataCacheMock = new Mock<IHyperliquidAssetMetadataCache>();
        _loggerMock = new Mock<ILogger<HyperliquidOrderService>>();
        _options = Options.Create(new HyperliquidOptions
        {
            BaseUrl = "https://api.hyperliquid-testnet.xyz",
            WsBaseUrl = "wss://api.hyperliquid-testnet.xyz/ws",
            Network = "testnet",
        });

        _signerMock.Setup(s => s.WalletAddress).Returns("0xTestAddress");
        _signerMock
            .Setup(s => s.SignHash(It.IsAny<byte[]>()))
            .Returns(("0x" + new string('a', 64), "0x" + new string('b', 64), 27));
        _nonceProviderMock
            .Setup(n => n.GetNextNonce())
            .Returns(1716499200000L);

        _metadataCacheMock
            .Setup(m => m.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssetMetadata(3, 5, 40));

        _sut = new HyperliquidOrderService(
            _restClientMock.Object,
            _signerMock.Object,
            _nonceProviderMock.Object,
                _accountServiceMock.Object,
            _metadataCacheMock.Object,
            _options,
            _loggerMock.Object);
    }

    [TestMethod]
    public async Task GivenValidLimitOrder_WhenPlaceOrderAsync_ThenReturnsSuccessWithOrderId()
    {
        var request = new PlaceOrderRequest
        {
            Asset = "BTC-PERP",
            Side = "buy",
            OrderType = "limit",
            Price = 65000m,
            Size = 0.001m,
        };

        _restClientMock
            .Setup(r => r.PostExchangeAsync<HyperliquidExchangeResponse>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HyperliquidExchangeResponse
            {
                Status = "ok",
                Response = new HyperliquidExchangeResponseData
                {
                    Type = "order",
                    Data = new HyperliquidOrderResponseData
                    {
                        Statuses =
                        [
                            new HyperliquidOrderStatus { Resting = new HyperliquidRestingOrder { Oid = 12345 } },
                        ],
                    },
                },
            });

        var result = await _sut.PlaceOrderAsync(request);

        result.Success.Should().BeTrue();
        result.OrderId.Should().Be("12345");
        result.Status.Should().Be("open");
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task GivenExchangeStatusError_WhenPlaceOrderAsync_ThenReturnsRejectedWithDetail()
    {
        var request = new PlaceOrderRequest
        {
            Asset = "BTC-PERP",
            Side = "buy",
            OrderType = "limit",
            Price = 65000m,
            Size = 0.001m,
        };

        _restClientMock
            .Setup(r => r.PostExchangeAsync<HyperliquidExchangeResponse>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HyperliquidExchangeResponse
            {
                Status = "ok",
                Response = new HyperliquidExchangeResponseData
                {
                    Type = "order",
                    Data = new HyperliquidOrderResponseData
                    {
                        Statuses = [new HyperliquidOrderStatus { Error = "Insufficient margin" }],
                    },
                },
            });

        var result = await _sut.PlaceOrderAsync(request);

        result.Success.Should().BeFalse();
        result.Status.Should().Be("rejected");
        result.Detail.Should().Be("Insufficient margin");
    }

    [TestMethod]
    public async Task GivenSuccessfulOrderAndRejectedStopLoss_WhenPlaceOrderAsync_ThenReturnsSuccessWithWarningDetail()
    {
        var request = new PlaceOrderRequest
        {
            Asset = "BTC-PERP",
            Side = "buy",
            OrderType = "limit",
            Price = 65000m,
            Size = 0.001m,
            StopLossPrice = 64000m,
        };

        _restClientMock
            .SetupSequence(r => r.PostExchangeAsync<HyperliquidExchangeResponse>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HyperliquidExchangeResponse
            {
                Status = "ok",
                Response = new HyperliquidExchangeResponseData
                {
                    Type = "order",
                    Data = new HyperliquidOrderResponseData
                    {
                        Statuses =
                        [
                            new HyperliquidOrderStatus { Resting = new HyperliquidRestingOrder { Oid = 12345 } },
                        ],
                    },
                },
            })
            .ReturnsAsync(new HyperliquidExchangeResponse
            {
                Status = "ok",
                Response = new HyperliquidExchangeResponseData
                {
                    Type = "order",
                    Data = new HyperliquidOrderResponseData
                    {
                        Statuses = [new HyperliquidOrderStatus { Error = "Insufficient margin" }],
                    },
                },
            });

        var result = await _sut.PlaceOrderAsync(request);

        result.Success.Should().BeTrue();
        result.OrderId.Should().Be("12345");
        result.Status.Should().Be("open");
        result.Detail.Should().Be("Stop loss trigger order failed: Insufficient margin");
    }

    [TestMethod]
    public async Task GivenSignatureRejection_WhenPlaceOrderAsync_ThenThrowsSigningException()
    {
        var request = new PlaceOrderRequest
        {
            Asset = "BTC-PERP",
            Side = "buy",
            OrderType = "limit",
            Price = 65000m,
            Size = 0.001m,
        };

        _restClientMock
            .Setup(r => r.PostExchangeAsync<HyperliquidExchangeResponse>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HyperliquidApiException(
                "signature rejected by exchange", 400, "validation_error"));

        var action = () => _sut.PlaceOrderAsync(request);

        await action.Should().ThrowAsync<SigningException>()
            .WithMessage("*Signature rejected*");
    }

    [TestMethod]
    public async Task GivenLimitOrderWithoutPrice_WhenPlaceOrderAsync_ThenThrowsDomainException()
    {
        var request = new PlaceOrderRequest
        {
            Asset = "BTC-PERP",
            Side = "buy",
            OrderType = "limit",
            Price = null,
            Size = 0.001m,
        };

        var action = () => _sut.PlaceOrderAsync(request);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("*Price*required*limit*");
    }

    [TestMethod]
    public async Task GivenTestSign_WhenTestSignAsync_ThenReturnsSignatureWithoutExchangeSubmission()
    {
        var result = await _sut.TestSignAsync();

        result.DomainSeparator.Should().StartWith("0x");
        result.TypeHash.Should().StartWith("0x");
        result.MessageHash.Should().StartWith("0x");
        result.Signature.V.Should().Be(27);
        result.Signature.R.Should().StartWith("0x");
        result.Signature.S.Should().StartWith("0x");

        _restClientMock.Verify(
            r => r.PostExchangeAsync<HyperliquidExchangeResponse>(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GivenValidOrderId_WhenCancelOrderAsync_ThenSubmitsCancelAction()
    {
        _restClientMock
            .Setup(r => r.PostExchangeAsync<JsonElement>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonDocument.Parse("""{ "status": "ok", "response": { "type": "cancel" } }""").RootElement.Clone());

        await _sut.CancelOrderAsync("12345", "BTC", CancellationToken.None);

        _restClientMock.Verify(
            r => r.PostExchangeAsync<JsonElement>(
                It.Is<object>(payload =>
                    JsonSerializer.Serialize(payload, (JsonSerializerOptions?)null).Contains("\"type\":\"cancel\"", StringComparison.OrdinalIgnoreCase) &&
                    JsonSerializer.Serialize(payload, (JsonSerializerOptions?)null).Contains("12345", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenInvalidOrderId_WhenCancelOrderAsync_ThenThrowsDomainException()
    {
        var action = () => _sut.CancelOrderAsync("not-a-number", "BTC", CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>();
    }

    [TestMethod]
    public async Task GivenOpenOrders_WhenCancelAllOrdersAsync_ThenCancelsMatchingAssetOrders()
    {
        _accountServiceMock
            .Setup(s => s.GetOpenOrdersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new OpenOrderDto { OrderId = "111", Asset = "BTC", Side = "Buy", Price = 60000m, Size = 0.01m },
                new OpenOrderDto { OrderId = "222", Asset = "BTC", Side = "Sell", Price = 70000m, Size = 0.02m },
                new OpenOrderDto { OrderId = "333", Asset = "ETH", Side = "Buy", Price = 3000m, Size = 1m },
            ]);

        _restClientMock
            .Setup(r => r.PostExchangeAsync<JsonElement>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonDocument.Parse("""{ "status": "ok", "response": { "type": "cancel" } }""").RootElement.Clone());

        await _sut.CancelAllOrdersAsync("BTC", CancellationToken.None);

        _restClientMock.Verify(
            r => r.PostExchangeAsync<JsonElement>(
                It.Is<object>(payload =>
                    JsonSerializer.Serialize(payload, (JsonSerializerOptions?)null).Contains("111", StringComparison.OrdinalIgnoreCase) &&
                    JsonSerializer.Serialize(payload, (JsonSerializerOptions?)null).Contains("222", StringComparison.OrdinalIgnoreCase) &&
                    !JsonSerializer.Serialize(payload, (JsonSerializerOptions?)null).Contains("333", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenNoOpenOrders_WhenCancelAllOrdersAsync_ThenDoesNotSubmitRequest()
    {
        _accountServiceMock
            .Setup(s => s.GetOpenOrdersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _sut.CancelAllOrdersAsync("BTC", CancellationToken.None);

        _restClientMock.Verify(
            r => r.PostExchangeAsync<HyperliquidExchangeResponse>(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GivenValidModifyParameters_WhenModifyOrderAsync_ThenSubmitsModifyActionWithWireDecimals()
    {
        _restClientMock
            .Setup(r => r.PostExchangeAsync<JsonElement>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonDocument.Parse("""{ "status": "ok", "response": { "type": "batchModifyOrders" } }""").RootElement.Clone());

        await _sut.ModifyOrderAsync("12345", "BTC", "Buy", 64500m, 0.002m, CancellationToken.None);

        _restClientMock.Verify(
            r => r.PostExchangeAsync<JsonElement>(
                It.Is<object>(payload =>
                    JsonSerializer.Serialize(payload, (JsonSerializerOptions?)null).Contains("batchModifyOrders", StringComparison.OrdinalIgnoreCase) &&
                    JsonSerializer.Serialize(payload, (JsonSerializerOptions?)null).Contains("64500", StringComparison.OrdinalIgnoreCase) &&
                    JsonSerializer.Serialize(payload, (JsonSerializerOptions?)null).Contains("0.002", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenValidTriggerRequest_WhenPlaceTriggerOrderAsync_ThenReturnsSuccessWithOrderId()
    {
        var request = new PlaceTriggerOrderRequest
        {
            Asset = "BTC",
            Side = "sell",
            Size = 0.1m,
            TriggerPrice = 64000m,
            TpslType = "sl",
        };

        _restClientMock
            .Setup(r => r.PostExchangeAsync<HyperliquidExchangeResponse>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HyperliquidExchangeResponse
            {
                Status = "ok",
                Response = new HyperliquidExchangeResponseData
                {
                    Type = "order",
                    Data = new HyperliquidOrderResponseData
                    {
                        Statuses =
                        [
                            new HyperliquidOrderStatus { Resting = new HyperliquidRestingOrder { Oid = 98765 } },
                        ],
                    },
                },
            });

        var result = await _sut.PlaceTriggerOrderAsync(request);

        result.Success.Should().BeTrue();
        result.OrderId.Should().Be("98765");
        result.Status.Should().Be("open");

        _restClientMock.Verify(
            r => r.PostExchangeAsync<HyperliquidExchangeResponse>(
                It.Is<object>(payload =>
                    JsonSerializer.Serialize(payload, (JsonSerializerOptions?)null).Contains("\"trigger\":{\"isMarket\":true,\"triggerPx\":\"64000\",\"tpsl\":\"sl\"}", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenUnknownAsset_WhenPlaceTriggerOrderAsync_ThenThrowsNotFoundException()
    {
        var request = new PlaceTriggerOrderRequest
        {
            Asset = "UNKNOWN",
            Side = "sell",
            Size = 0.1m,
            TriggerPrice = 64000m,
            TpslType = "sl",
        };

        _metadataCacheMock
            .Setup(m => m.GetAsync("UNKNOWN", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Asset", "UNKNOWN"));

        var action = () => _sut.PlaceTriggerOrderAsync(request);

        await action.Should().ThrowAsync<NotFoundException>();
    }

    [TestMethod]
    public async Task GivenExchangeError_WhenPlaceTriggerOrderAsync_ThenReturnsRejectedResponse()
    {
        var request = new PlaceTriggerOrderRequest
        {
            Asset = "BTC",
            Side = "sell",
            Size = 0.1m,
            TriggerPrice = 64000m,
            TpslType = "sl",
        };

        _restClientMock
            .Setup(r => r.PostExchangeAsync<HyperliquidExchangeResponse>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HyperliquidExchangeResponse
            {
                Status = "ok",
                Response = new HyperliquidExchangeResponseData
                {
                    Type = "order",
                    Data = new HyperliquidOrderResponseData
                    {
                        Statuses = [new HyperliquidOrderStatus { Error = "Trigger rejected" }],
                    },
                },
            });

        var result = await _sut.PlaceTriggerOrderAsync(request);

        result.Success.Should().BeFalse();
        result.Status.Should().Be("rejected");
        result.Detail.Should().Be("Trigger rejected");
    }

    [TestMethod]
    public async Task GivenValidTriggerModifyParameters_WhenModifyTriggerOrderAsync_ThenSubmitsTriggerModifyAction()
    {
        _restClientMock
            .Setup(r => r.PostExchangeAsync<JsonElement>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonDocument.Parse("""{ "status": "ok", "response": { "type": "batchModifyOrders" } }""").RootElement.Clone());

        await _sut.ModifyTriggerOrderAsync("12345", "BTC", "sell", 63500m, 0.003m, "sl", CancellationToken.None);

        _restClientMock.Verify(
            r => r.PostExchangeAsync<JsonElement>(
                It.Is<object>(payload =>
                    JsonSerializer.Serialize(payload, (JsonSerializerOptions?)null).Contains("batchModifyOrders", StringComparison.OrdinalIgnoreCase) &&
                    JsonSerializer.Serialize(payload, (JsonSerializerOptions?)null).Contains("63500", StringComparison.OrdinalIgnoreCase) &&
                    JsonSerializer.Serialize(payload, (JsonSerializerOptions?)null).Contains("\"trigger\":{\"isMarket\":true,\"triggerPx\":\"63500\",\"tpsl\":\"sl\"}", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenUnknownAsset_WhenModifyTriggerOrderAsync_ThenThrowsNotFoundException()
    {
        _metadataCacheMock
            .Setup(m => m.GetAsync("UNKNOWN", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Asset", "UNKNOWN"));

        var action = () => _sut.ModifyTriggerOrderAsync("12345", "UNKNOWN", "sell", 63500m, 0.003m, "sl", CancellationToken.None);

        await action.Should().ThrowAsync<NotFoundException>();
    }
}
