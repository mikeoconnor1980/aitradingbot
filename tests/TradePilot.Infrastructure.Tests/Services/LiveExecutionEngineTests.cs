using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradePilot.Application.Abstractions.Configuration;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Enums;
using TradePilot.Infrastructure.Hyperliquid.Models;
using TradePilot.Infrastructure.Services;

namespace TradePilot.Infrastructure.Tests.Services;

[TestClass]
public sealed class LiveExecutionEngineTests
{
    private Mock<IHyperliquidRestClient> _restClient = null!;
    private Mock<IHyperliquidSigner> _signer = null!;
    private Mock<INonceProvider> _nonceProvider = null!;
    private IOptions<HyperliquidOptions> _options = null!;
    private Mock<ILogger<LiveExecutionEngine>> _logger = null!;
    private LiveExecutionEngine _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _restClient = new Mock<IHyperliquidRestClient>();
        _signer = new Mock<IHyperliquidSigner>();
        _nonceProvider = new Mock<INonceProvider>();
        _logger = new Mock<ILogger<LiveExecutionEngine>>();
        _options = Options.Create(new HyperliquidOptions
        {
            BaseUrl = "https://api.hyperliquid-testnet.xyz",
            Network = "testnet"
        });

        _signer.Setup(s => s.SignHash(It.IsAny<byte[]>()))
            .Returns(("0xR", "0xS", 27));

        _nonceProvider.Setup(n => n.GetNextNonce())
            .Returns(100L);

        // Seed asset index cache via meta endpoint
        var metaJson = JsonSerializer.Deserialize<JsonElement>(
            """{"universe":[{"name":"BTC","maxLeverage":50},{"name":"ETH","maxLeverage":25},{"name":"SOL","maxLeverage":20}]}""");
        _restClient.Setup(r => r.PostInfoAsync<JsonElement>(
                It.Is<object>(o => IsInfoRequestType(o, "meta")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(metaJson);

        var spotMetaJson = JsonSerializer.Deserialize<JsonElement>(
            """{"tokens":[{"name":"USDC","index":0,"szDecimals":8,"weiDecimals":8},{"name":"BTC","index":69,"szDecimals":0,"weiDecimals":5},{"name":"IBTC","index":499,"szDecimals":2,"weiDecimals":8}],"universe":[{"tokens":[69,0],"name":"@50","index":50,"isCanonical":false},{"tokens":[499,0],"name":"@51","index":51,"isCanonical":false}]}""");
        _restClient.Setup(r => r.PostInfoAsync<JsonElement>(
                It.Is<object>(o => IsInfoRequestType(o, "spotMeta")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(spotMetaJson);

        _sut = new LiveExecutionEngine(
            _restClient.Object,
            _signer.Object,
            _nonceProvider.Object,
            _options,
            _logger.Object);
    }

    [TestMethod]
    public async Task GivenLimitOrder_WhenPlaceOrderAsync_ThenSignsAndSubmits()
    {
        // Arrange
        var exchangeResponse = BuildSuccessResponse(12345L);
        _restClient.Setup(r => r.PostExchangeAsync<HyperliquidExchangeResponse>(
                It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(exchangeResponse);

        var order = new OrderRequest
        {
            Symbol = "BTC-PERP",
            Side = OrderSide.Buy,
            OrderType = OrderType.Limit,
            Price = 50000m,
            Size = 0.01m,
            TradeType = TradeType.GridFill
        };

        // Act
        var orderId = await _sut.PlaceOrderAsync(order);

        // Assert
        orderId.Should().Be("12345");
        _signer.Verify(s => s.SignHash(It.IsAny<byte[]>()), Times.Once);
        _restClient.Verify(r => r.PostExchangeAsync<HyperliquidExchangeResponse>(
            It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GivenMarketOrder_WhenPlaceOrderAsync_ThenResolvesMidPrice()
    {
        // Arrange
        var marketInfo = new TradePilot.Application.MarketData.Models.MarketInfoDto
        {
            Asset = "BTC-PERP",
            MidPrice = 50000m,
            MarkPrice = 50000m,
            IndexPrice = 50000m,
            FundingRate = 0.0001m,
            Volume24h = 1000000m,
            OpenInterest = 500000m,
            PriceChange24hPercent = 1.5m
        };
        _restClient.Setup(r => r.GetMarketInfoAsync("BTC-PERP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(marketInfo);

        var exchangeResponse = BuildSuccessResponse(99999L);
        _restClient.Setup(r => r.PostExchangeAsync<HyperliquidExchangeResponse>(
                It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(exchangeResponse);

        var order = new OrderRequest
        {
            Symbol = "BTC-PERP",
            Side = OrderSide.Buy,
            OrderType = OrderType.Market,
            Price = 0m,
            Size = 0.01m,
            TradeType = TradeType.SignalEntry
        };

        // Act
        var orderId = await _sut.PlaceOrderAsync(order);

        // Assert
        orderId.Should().Be("99999");
        _restClient.Verify(r => r.GetMarketInfoAsync("BTC-PERP", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GivenReduceOnlyOrder_WhenPlaceOrderAsync_ThenSetsReduceOnlyFlag()
    {
        var exchangeResponse = BuildSuccessResponse(45678L);
        var marketInfo = new TradePilot.Application.MarketData.Models.MarketInfoDto
        {
            Asset = "BTC-PERP",
            MidPrice = 50000m,
            MarkPrice = 50000m,
            IndexPrice = 50000m,
            FundingRate = 0.0001m,
            Volume24h = 1000000m,
            OpenInterest = 500000m,
            PriceChange24hPercent = 1.5m
        };

        _restClient.Setup(r => r.GetMarketInfoAsync("BTC-PERP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(marketInfo);

        _restClient.Setup(r => r.PostExchangeAsync<HyperliquidExchangeResponse>(
                It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(exchangeResponse);

        var order = new OrderRequest
        {
            Symbol = "BTC-PERP",
            Side = OrderSide.Sell,
            OrderType = OrderType.Market,
            Price = 0m,
            Size = 0.02m,
            TradeType = TradeType.Manual,
            ReduceOnly = true,
        };

        var orderId = await _sut.PlaceOrderAsync(order);

        orderId.Should().Be("45678");
        _restClient.Verify(r => r.PostExchangeAsync<HyperliquidExchangeResponse>(
            It.Is<object>(payload => PayloadHasReduceOnly(payload)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GivenFractionalSpotMarketOrder_WhenPlaceOrderAsync_ThenUsesSpotPairIndexWithoutPerpMidPriceLookup()
    {
        var exchangeResponse = BuildSuccessResponse(54321L);
        _restClient.Setup(r => r.PostExchangeAsync<HyperliquidExchangeResponse>(
                It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(exchangeResponse);

        var order = new OrderRequest
        {
            Symbol = "IBTC-USD",
            AssetType = AssetType.Spot,
            Side = OrderSide.Buy,
            OrderType = OrderType.Market,
            Price = 5m,
            Size = 2.27865m,
            TradeType = TradeType.DcaBuy
        };

        var orderId = await _sut.PlaceOrderAsync(order);

        orderId.Should().Be("54321");
        _restClient.Verify(r => r.GetMarketInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _restClient.Verify(r => r.PostExchangeAsync<HyperliquidExchangeResponse>(
            It.Is<object>(payload => PayloadHasOrderAsset(payload, 10051) && PayloadHasOrderSize(payload, "2.27")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GivenLowPricedFractionalSpotMarketOrder_WhenPlaceOrderAsync_ThenNormalizesPriceToSpotPrecision()
    {
        var exchangeResponse = BuildSuccessResponse(98765L);
        _restClient.Setup(r => r.PostExchangeAsync<HyperliquidExchangeResponse>(
                It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(exchangeResponse);

        var order = new OrderRequest
        {
            Symbol = "IBTC-USD",
            AssetType = AssetType.Spot,
            Side = OrderSide.Buy,
            OrderType = OrderType.Market,
            Price = 0.002266m,
            Size = 6640.1m,
            TradeType = TradeType.DcaBuy
        };

        var orderId = await _sut.PlaceOrderAsync(order);

        orderId.Should().Be("98765");
        _restClient.Verify(r => r.PostExchangeAsync<HyperliquidExchangeResponse>(
            It.Is<object>(payload =>
                PayloadHasOrderAsset(payload, 10051)
                && PayloadHasOrderSize(payload, "6640.1")
                && PayloadHasOrderPrice(payload, "0.002379")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GivenRejectedFractionalSpotOrder_WhenPlaceOrderAsync_ThenLogsExchangeDetail()
    {
        var rejected = new HyperliquidExchangeResponse
        {
            Status = "ok",
            Response = new HyperliquidExchangeResponseData
            {
                Type = "order",
                Data = new HyperliquidOrderResponseData
                {
                    Statuses =
                    [
                        new HyperliquidOrderStatus
                        {
                            Error = "Invalid size precision"
                        }
                    ]
                }
            }
        };

        _restClient.Setup(r => r.PostExchangeAsync<HyperliquidExchangeResponse>(
                It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rejected);

        var order = new OrderRequest
        {
            Symbol = "IBTC-USD",
            AssetType = AssetType.Spot,
            Side = OrderSide.Buy,
            OrderType = OrderType.Market,
            Price = 5m,
            Size = 2.27865m,
            TradeType = TradeType.DcaBuy
        };

        var orderId = await _sut.PlaceOrderAsync(order);

        orderId.Should().BeEmpty();
        VerifyLogged(LogLevel.Warning, "Invalid size precision");
    }

    [TestMethod]
    public async Task GivenSpotOrderBelowMinimumNotional_WhenPlaceOrderAsync_ThenRejectsBeforeExchangeCall()
    {
        var order = new OrderRequest
        {
            Symbol = "IBTC-USD",
            AssetType = AssetType.Spot,
            Side = OrderSide.Buy,
            OrderType = OrderType.Market,
            Price = 5m,
            Size = 1.23865m,
            TradeType = TradeType.DcaBuy
        };

        var orderId = await _sut.PlaceOrderAsync(order);

        orderId.Should().BeEmpty();
        _restClient.Verify(r => r.PostExchangeAsync<HyperliquidExchangeResponse>(
            It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyLogged(LogLevel.Warning, "Spot order below minimum notional");
    }

    [TestMethod]
    public async Task GivenSpotOrderBelowTradingPrecision_WhenPlaceOrderAsync_ThenRejectsBeforeExchangeCall()
    {
        var order = new OrderRequest
        {
            Symbol = "BTC-USD",
            AssetType = AssetType.Spot,
            Side = OrderSide.Buy,
            OrderType = OrderType.Market,
            Price = 76146m,
            Size = 0.00039398m,
            TradeType = TradeType.DcaBuy
        };

        var orderId = await _sut.PlaceOrderAsync(order);

        orderId.Should().BeEmpty();
        _restClient.Verify(r => r.PostExchangeAsync<HyperliquidExchangeResponse>(
            It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyLogged(LogLevel.Warning, "Order size rounded down to zero");
    }

    [TestMethod]
    public async Task GivenRejectedOrder_WhenPlaceOrderAsync_ThenReturnsEmpty()
    {
        // Arrange
        var rejected = new HyperliquidExchangeResponse
        {
            Status = "error",
            Response = null
        };
        _restClient.Setup(r => r.PostExchangeAsync<HyperliquidExchangeResponse>(
                It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rejected);

        var order = new OrderRequest
        {
            Symbol = "BTC-PERP",
            Side = OrderSide.Buy,
            OrderType = OrderType.Limit,
            Price = 50000m,
            Size = 0.01m,
            TradeType = TradeType.GridFill
        };

        // Act
        var orderId = await _sut.PlaceOrderAsync(order);

        // Assert
        orderId.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenPlacedOrder_WhenCancelOrderAsync_ThenSubmitsCancellation()
    {
        // Arrange: Place an order first to populate asset mapping
        var exchangeResponse = BuildSuccessResponse(777L);
        _restClient.Setup(r => r.PostExchangeAsync<HyperliquidExchangeResponse>(
                It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(exchangeResponse);

        var order = new OrderRequest
        {
            Symbol = "BTC-PERP",
            Side = OrderSide.Buy,
            OrderType = OrderType.Limit,
            Price = 50000m,
            Size = 0.01m,
            TradeType = TradeType.GridFill
        };
        await _sut.PlaceOrderAsync(order);

        // Act
        await _sut.CancelOrderAsync("777");

        // Assert — two exchange calls: one for placement, one for cancellation
        _restClient.Verify(r => r.PostExchangeAsync<HyperliquidExchangeResponse>(
            It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [TestMethod]
    public async Task GivenUnknownOrderId_WhenCancelOrderAsync_ThenDoesNothing()
    {
        // Act
        await _sut.CancelOrderAsync("unknown-999");

        // Assert — no exchange call
        _restClient.Verify(r => r.PostExchangeAsync<HyperliquidExchangeResponse>(
            It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task GivenPlacedOrders_WhenCancelAllOrdersAsync_ThenSubmitsCancellation()
    {
        // Arrange
        var exchangeResponse = BuildSuccessResponse(111L);
        _restClient.Setup(r => r.PostExchangeAsync<HyperliquidExchangeResponse>(
                It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(exchangeResponse);

        var order = new OrderRequest
        {
            Symbol = "BTC-PERP",
            Side = OrderSide.Buy,
            OrderType = OrderType.Limit,
            Price = 50000m,
            Size = 0.01m,
            TradeType = TradeType.GridFill
        };
        await _sut.PlaceOrderAsync(order);

        // Act
        await _sut.CancelAllOrdersAsync("BTC-PERP");

        // Assert — two exchange calls: one placement, one cancel-all
        _restClient.Verify(r => r.PostExchangeAsync<HyperliquidExchangeResponse>(
            It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [TestMethod]
    public async Task GivenNullOrder_WhenPlaceOrderAsync_ThenThrowsArgumentNull()
    {
        // Act
        Func<Task> act = () => _sut.PlaceOrderAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [TestMethod]
    public async Task GivenNetworkError_WhenPlaceOrderAsync_ThenReturnsEmpty()
    {
        // Arrange
        _restClient.Setup(r => r.PostExchangeAsync<HyperliquidExchangeResponse>(
                It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var order = new OrderRequest
        {
            Symbol = "BTC-PERP",
            Side = OrderSide.Buy,
            OrderType = OrderType.Limit,
            Price = 50000m,
            Size = 0.01m,
            TradeType = TradeType.GridFill
        };

        // Act
        var orderId = await _sut.PlaceOrderAsync(order);

        // Assert
        orderId.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenAssetWith50xMax_WhenSetLeverageAt33x_ThenSendsUpdateLeverageAction()
    {
        // Arrange
        var exchangeResponse = JsonSerializer.Deserialize<JsonElement>("""{"status":"ok"}""");
        _restClient.Setup(r => r.PostExchangeAsync<JsonElement>(
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(exchangeResponse);

        // Act
        await _sut.SetLeverageAsync("BTC", 33, isIsolated: true);

        // Assert
        _signer.Verify(s => s.SignHash(It.IsAny<byte[]>()), Times.Once);
        _restClient.Verify(r => r.PostExchangeAsync<JsonElement>(
            It.Is<object>(payload => PayloadHasLeverage(payload, assetIndex: 0, leverage: 33, isCross: false, nonce: 100L)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GivenLeverageExceedsMax_WhenSetLeverageAsync_ThenClampsToMaxAndLogsWarning()
    {
        // Arrange
        var metaJson = JsonSerializer.Deserialize<JsonElement>(
            """{"universe":[{"name":"BTC","maxLeverage":50},{"name":"ETH","maxLeverage":25}]}""");
        _restClient.Setup(r => r.PostInfoAsync<JsonElement>(
                It.Is<object>(o => o.ToString()!.Contains("meta")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(metaJson);
        var exchangeResponse = JsonSerializer.Deserialize<JsonElement>("""{"status":"ok"}""");
        _restClient.Setup(r => r.PostExchangeAsync<JsonElement>(
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(exchangeResponse);

        // Act
        await _sut.SetLeverageAsync("ETH", 60, isIsolated: true);

        // Assert
        _restClient.Verify(r => r.PostExchangeAsync<JsonElement>(
            It.Is<object>(payload => PayloadHasLeverage(payload, assetIndex: 1, leverage: 25, isCross: false, nonce: 100L)),
            It.IsAny<CancellationToken>()), Times.Once);
        VerifyLogged(LogLevel.Warning, "Leverage 60x exceeds max 25x for ETH. Clamping to 25x.");
    }

    private static HyperliquidExchangeResponse BuildSuccessResponse(long oid)
    {
        return new HyperliquidExchangeResponse
        {
            Status = "ok",
            Response = new HyperliquidExchangeResponseData
            {
                Type = "order",
                Data = new HyperliquidOrderResponseData
                {
                    Statuses =
                    [
                        new HyperliquidOrderStatus
                        {
                            Resting = new HyperliquidRestingOrder { Oid = oid }
                        }
                    ]
                }
            }
        };
    }

    private static bool PayloadHasLeverage(object payload, int assetIndex, int leverage, bool isCross, long nonce)
    {
        var json = JsonSerializer.SerializeToElement(payload);
        var action = json.GetProperty("action");

        return json.GetProperty("nonce").GetInt64() == nonce
            && action.GetProperty("type").GetString() == "updateLeverage"
            && action.GetProperty("asset").GetInt32() == assetIndex
            && action.GetProperty("isCross").GetBoolean() == isCross
            && action.GetProperty("leverage").GetInt32() == leverage;
    }

    private static bool PayloadHasOrderAsset(object payload, int assetIndex)
    {
        var json = JsonSerializer.SerializeToElement(payload);
        var action = json.GetProperty("action");
        var orders = action.GetProperty("orders");

        return orders.GetArrayLength() > 0
            && orders[0].GetProperty("a").GetInt32() == assetIndex;
    }

    private static bool PayloadHasOrderSize(object payload, string expectedSize)
    {
        var json = JsonSerializer.SerializeToElement(payload);
        var orders = json.GetProperty("action").GetProperty("orders");

        return orders.GetArrayLength() > 0
            && string.Equals(orders[0].GetProperty("s").GetString(), expectedSize, StringComparison.Ordinal);
    }

    private static bool PayloadHasOrderPrice(object payload, string expectedPrice)
    {
        var json = JsonSerializer.SerializeToElement(payload);
        var orders = json.GetProperty("action").GetProperty("orders");

        return orders.GetArrayLength() > 0
            && string.Equals(orders[0].GetProperty("p").GetString(), expectedPrice, StringComparison.Ordinal);
    }

    private static bool PayloadHasReduceOnly(object payload)
    {
        var json = JsonSerializer.SerializeToElement(payload);
        var orders = json.GetProperty("action").GetProperty("orders");

        return orders.GetArrayLength() > 0 && orders[0].GetProperty("r").GetBoolean();
    }

    private static bool IsInfoRequestType(object request, string type)
    {
        var json = JsonSerializer.SerializeToElement(request);
        return json.TryGetProperty("type", out var typeElement)
            && string.Equals(typeElement.GetString(), type, StringComparison.OrdinalIgnoreCase);
    }

    private void VerifyLogged(LogLevel level, string message)
    {
        _logger.Verify(
            logger => logger.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(message, StringComparison.Ordinal)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
