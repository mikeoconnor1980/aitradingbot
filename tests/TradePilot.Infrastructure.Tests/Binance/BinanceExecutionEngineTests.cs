using Microsoft.Extensions.Logging;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Trading.Models;
using TradePilot.Infrastructure.Binance;

namespace TradePilot.Infrastructure.Tests.Binance;

[TestClass]
public sealed class BinanceExecutionEngineTests
{
    private Mock<IBinanceFuturesAuthClient> _authClientMock = null!;
    private Mock<IBinanceExchangeInfoCache> _exchangeInfoCacheMock = null!;
    private BinanceExecutionEngine _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _authClientMock = new Mock<IBinanceFuturesAuthClient>(MockBehavior.Strict);
        _exchangeInfoCacheMock = new Mock<IBinanceExchangeInfoCache>(MockBehavior.Strict);
        _sut = new BinanceExecutionEngine(
            _authClientMock.Object,
            _exchangeInfoCacheMock.Object,
            Mock.Of<ILogger<BinanceExecutionEngine>>());
    }

    [TestMethod]
    public async Task GivenLimitOrderWithExcessPrecision_WhenPlaceOrderAsync_ThenNormalizesBeforeSubmission()
    {
        SetupExchangeMetadata("BTC", sizeDecimals: 3, priceDecimals: 2);

        BinancePlaceOrderRequest? capturedRequest = null;
        _authClientMock
            .Setup(client => client.PlaceOrderAsync(It.IsAny<BinancePlaceOrderRequest>(), It.IsAny<CancellationToken>()))
            .Callback<BinancePlaceOrderRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new BinancePlaceOrderResult { OrderId = 12345L, Status = "NEW" });

        var orderId = await _sut.PlaceOrderAsync(CreateLimitOrder(size: 0.1239m, price: 67890.129m));

        orderId.Should().Be("12345");
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Symbol.Should().Be("BTCUSDT");
        capturedRequest.Quantity.Should().Be(0.123m);
        capturedRequest.Price.Should().Be(67890.12m);
        capturedRequest.TimeInForce.Should().Be("GTC");
    }

    [TestMethod]
    public async Task GivenMissingExchangeMetadata_WhenPlaceOrderAsync_ThenThrowsDomainException()
    {
        _exchangeInfoCacheMock
            .Setup(cache => cache.GetSymbolAsync("BTC", It.IsAny<CancellationToken>()))
            .ReturnsAsync((BinanceExchangeSymbolMetadata?)null);

        var act = () => _sut.PlaceOrderAsync(CreateLimitOrder());

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*No exchange metadata found*");
        _authClientMock.Verify(
            client => client.PlaceOrderAsync(It.IsAny<BinancePlaceOrderRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GivenOrderThatNormalizesToZero_WhenPlaceOrderAsync_ThenThrowsDomainException()
    {
        SetupExchangeMetadata("BTC", sizeDecimals: 3, priceDecimals: 2);

        var act = () => _sut.PlaceOrderAsync(CreateLimitOrder(size: 0.0009m));

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*normalizes to zero*");
        _authClientMock.Verify(
            client => client.PlaceOrderAsync(It.IsAny<BinancePlaceOrderRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GivenTriggerOrderWithExcessPrecision_WhenPlaceTriggerOrderAsync_ThenNormalizesBeforeSubmission()
    {
        SetupExchangeMetadata("BTC", sizeDecimals: 1, priceDecimals: 2);

        BinancePlaceOrderRequest? capturedRequest = null;
        _authClientMock
            .Setup(client => client.PlaceOrderAsync(It.IsAny<BinancePlaceOrderRequest>(), It.IsAny<CancellationToken>()))
            .Callback<BinancePlaceOrderRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new BinancePlaceOrderResult { OrderId = 444L, Status = "NEW" });

        var orderId = await _sut.PlaceTriggerOrderAsync("BTC-PERP", "sell", 1.29m, 1234.567m, "sl");

        orderId.Should().Be("444");
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Quantity.Should().Be(1.2m);
        capturedRequest.StopPrice.Should().Be(1234.56m);
        capturedRequest.Type.Should().Be("STOP_MARKET");
        capturedRequest.ReduceOnly.Should().BeTrue();
    }

    [TestMethod]
    public async Task GivenUnknownOrderId_WhenCancelOrderAsync_ThenThrowsDomainException()
    {
        var act = () => _sut.CancelOrderAsync("missing-order");

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*asset mapping not found*");
    }

    [TestMethod]
    public async Task GivenTrackedOrderId_WhenCancelOrderAsync_ThenUsesTrackedAssetMapping()
    {
        SetupExchangeMetadata("BTC", sizeDecimals: 3, priceDecimals: 2);
        _authClientMock
            .Setup(client => client.PlaceOrderAsync(It.IsAny<BinancePlaceOrderRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BinancePlaceOrderResult { OrderId = 123L, Status = "NEW" });
        _authClientMock
            .Setup(client => client.CancelOrderAsync("BTCUSDT", 123L, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.PlaceOrderAsync(CreateLimitOrder());
        await _sut.CancelOrderAsync("123");

        _authClientMock.Verify(
            client => client.CancelOrderAsync("BTCUSDT", 123L, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenReplacementPlaceFailsOnce_WhenModifyTriggerOrderAsync_ThenRetriesAndTracksRecoveryOrder()
    {
        SetupExchangeMetadata("BTC", sizeDecimals: 3, priceDecimals: 2);
        _authClientMock
            .Setup(client => client.CancelOrderAsync("BTCUSDT", 111L, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _authClientMock
            .Setup(client => client.CancelOrderAsync("BTCUSDT", 222L, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _authClientMock
            .SetupSequence(client => client.PlaceOrderAsync(It.IsAny<BinancePlaceOrderRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("temporary failure"))
            .ReturnsAsync(new BinancePlaceOrderResult { OrderId = 222L, Status = "NEW" });

        await _sut.ModifyTriggerOrderAsync("111", "BTC-PERP", "sell", 12345.678m, 0.1239m, "tp");
        await _sut.CancelOrderAsync("222");

        _authClientMock.Verify(
            client => client.PlaceOrderAsync(
                It.Is<BinancePlaceOrderRequest>(request =>
                    request.Symbol == "BTCUSDT"
                    && request.Quantity == 0.123m
                    && request.StopPrice == 12345.67m
                    && request.Type == "TAKE_PROFIT_MARKET"),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        _authClientMock.Verify(
            client => client.CancelOrderAsync("BTCUSDT", 111L, It.IsAny<CancellationToken>()),
            Times.Once);
        _authClientMock.Verify(
            client => client.CancelOrderAsync("BTCUSDT", 222L, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenReplacementPlaceFailsTwice_WhenModifyTriggerOrderAsync_ThenThrowsDomainException()
    {
        SetupExchangeMetadata("BTC", sizeDecimals: 3, priceDecimals: 2);
        _authClientMock
            .Setup(client => client.CancelOrderAsync("BTCUSDT", 111L, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _authClientMock
            .SetupSequence(client => client.PlaceOrderAsync(It.IsAny<BinancePlaceOrderRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("first placement failure"))
            .ThrowsAsync(new InvalidOperationException("second placement failure"));

        var act = () => _sut.ModifyTriggerOrderAsync("111", "BTC-PERP", "sell", 12345.678m, 0.1239m, "sl");

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*replacement failed twice*no sl protection*");
        _authClientMock.Verify(
            client => client.PlaceOrderAsync(It.IsAny<BinancePlaceOrderRequest>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [TestMethod]
    public async Task GivenIsolatedLeverageRequest_WhenSetLeverageAsync_ThenSetsMarginTypeBeforeLeverage()
    {
        List<string> sequence = [];

        _authClientMock
            .Setup(client => client.SetMarginTypeAsync("BTCUSDT", true, It.IsAny<CancellationToken>()))
            .Callback<string, bool, CancellationToken>((_, _, _) => sequence.Add("marginType"))
            .Returns(Task.CompletedTask);
        _authClientMock
            .Setup(client => client.SetLeverageAsync("BTCUSDT", 20, It.IsAny<CancellationToken>()))
            .Callback<string, int, CancellationToken>((_, _, _) => sequence.Add("leverage"))
            .Returns(Task.CompletedTask);

        await _sut.SetLeverageAsync("BTC-PERP", 20, isIsolated: true);

        sequence.Should().Equal("marginType", "leverage");
    }

    private void SetupExchangeMetadata(string asset, int sizeDecimals, int priceDecimals)
    {
        _exchangeInfoCacheMock
            .Setup(cache => cache.GetSymbolAsync(asset, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BinanceExchangeSymbolMetadata(asset, $"{asset}USDT", sizeDecimals, priceDecimals, 125));
    }

    private static OrderRequest CreateLimitOrder(decimal size = 0.1234m, decimal price = 50000.12m)
    {
        return new OrderRequest
        {
            Symbol = "BTC-PERP",
            Side = OrderSide.Buy,
            OrderType = OrderType.Limit,
            Price = price,
            Size = size,
            TradeType = TradeType.GridFill,
        };
    }
}