using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Enums;
using TradingApp.Infrastructure.Hyperliquid.Models;
using TradingApp.Infrastructure.Services;

namespace TradingApp.Infrastructure.Tests.Services;

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
                It.Is<object>(o => o.ToString()!.Contains("meta")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(metaJson);

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
        var marketInfo = new TradingApp.Application.MarketData.Models.MarketInfoDto
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
