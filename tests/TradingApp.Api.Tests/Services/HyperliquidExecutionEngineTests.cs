using Microsoft.Extensions.Logging;
using TradingApp.Api.Models;
using TradingApp.Api.Services;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Enums;

namespace TradingApp.Api.Tests.Services;

[TestClass]
public sealed class HyperliquidExecutionEngineTests
{
    private Mock<IHyperliquidOrderService> _orderServiceMock = default!;
    private HyperliquidExecutionEngine _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _orderServiceMock = new Mock<IHyperliquidOrderService>();
        _sut = new HyperliquidExecutionEngine(
            _orderServiceMock.Object,
            Mock.Of<ILogger<HyperliquidExecutionEngine>>());
    }

    [TestMethod]
    public async Task GivenLimitBuyOrder_WhenPlaceOrderAsync_ThenMapsCorrectlyAndReturnsOrderId()
    {
        var expectedOrderId = "order-123";
        _orderServiceMock
            .Setup(s => s.PlaceOrderAsync(It.IsAny<PlaceOrderRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaceOrderResponse { Success = true, OrderId = expectedOrderId, Status = "filled" });

        var order = new OrderRequest
        {
            Symbol = "BTC-PERP",
            Side = OrderSide.Buy,
            OrderType = OrderType.Limit,
            Price = 50000m,
            Size = 0.1m,
            TradeType = TradeType.GridFill,
        };

        var orderId = await _sut.PlaceOrderAsync(order);

        orderId.Should().Be(expectedOrderId);

        _orderServiceMock.Verify(s => s.PlaceOrderAsync(
            It.Is<PlaceOrderRequest>(r =>
                r.Asset == "BTC-PERP" &&
                r.Side == "buy" &&
                r.OrderType == "limit" &&
                r.Price == 50000m &&
                r.Size == 0.1m),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenMarketSellOrder_WhenPlaceOrderAsync_ThenMapsWithNullPrice()
    {
        _orderServiceMock
            .Setup(s => s.PlaceOrderAsync(It.IsAny<PlaceOrderRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaceOrderResponse { Success = true, OrderId = "order-456", Status = "filled" });

        var order = new OrderRequest
        {
            Symbol = "ETH-PERP",
            Side = OrderSide.Sell,
            OrderType = OrderType.Market,
            Price = 3000m, // Should be ignored for market orders
            Size = 1.5m,
            TradeType = TradeType.TakeProfit,
        };

        await _sut.PlaceOrderAsync(order);

        _orderServiceMock.Verify(s => s.PlaceOrderAsync(
            It.Is<PlaceOrderRequest>(r =>
                r.Side == "sell" &&
                r.OrderType == "market" &&
                r.Price == null),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenRejectedOrder_WhenPlaceOrderAsync_ThenReturnsEmptyString()
    {
        _orderServiceMock
            .Setup(s => s.PlaceOrderAsync(It.IsAny<PlaceOrderRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaceOrderResponse { Success = false, Status = "rejected", Detail = "Insufficient margin" });

        var order = new OrderRequest
        {
            Symbol = "BTC-PERP",
            Side = OrderSide.Buy,
            OrderType = OrderType.Limit,
            Price = 50000m,
            Size = 0.1m,
            TradeType = TradeType.GridFill,
        };

        var orderId = await _sut.PlaceOrderAsync(order);

        orderId.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenPlacedOrder_WhenCancelOrderAsync_ThenDelegatesToOrderServiceWithCorrectAsset()
    {
        // First place an order to establish the asset mapping
        _orderServiceMock
            .Setup(s => s.PlaceOrderAsync(It.IsAny<PlaceOrderRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaceOrderResponse { Success = true, OrderId = "order-789" });

        var order = new OrderRequest
        {
            Symbol = "BTC-PERP",
            Side = OrderSide.Buy,
            OrderType = OrderType.Limit,
            Price = 50000m,
            Size = 0.1m,
            TradeType = TradeType.GridFill,
        };

        await _sut.PlaceOrderAsync(order);

        // Now cancel it
        await _sut.CancelOrderAsync("order-789");

        _orderServiceMock.Verify(
            s => s.CancelOrderAsync("order-789", "BTC-PERP", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenUnknownOrderId_WhenCancelOrderAsync_ThenDoesNotThrow()
    {
        // Cancelling an order that was never placed through this engine should not throw
        await _sut.CancelOrderAsync("unknown-order");

        _orderServiceMock.Verify(
            s => s.CancelOrderAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GivenSymbol_WhenCancelAllOrdersAsync_ThenDelegatesToOrderService()
    {
        await _sut.CancelAllOrdersAsync("BTC-PERP");

        _orderServiceMock.Verify(
            s => s.CancelAllOrdersAsync("BTC-PERP", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenMultipleOrders_WhenCancelAllOrdersAsync_ThenCleansUpOrderMap()
    {
        _orderServiceMock
            .Setup(s => s.PlaceOrderAsync(It.IsAny<PlaceOrderRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaceOrderResponse { Success = true, OrderId = "order-1" });

        await _sut.PlaceOrderAsync(new OrderRequest
        {
            Symbol = "BTC-PERP",
            Side = OrderSide.Buy,
            OrderType = OrderType.Limit,
            Price = 50000m,
            Size = 0.1m,
            TradeType = TradeType.GridFill,
        });

        // Cancel all for BTC-PERP
        await _sut.CancelAllOrdersAsync("BTC-PERP");

        // Now trying to cancel the specific order should NOT call the service
        // (order was already removed from the mapping)
        await _sut.CancelOrderAsync("order-1");

        _orderServiceMock.Verify(
            s => s.CancelOrderAsync("order-1", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GivenNullOrder_WhenPlaceOrderAsync_ThenThrowsArgumentNullException()
    {
        var act = () => _sut.PlaceOrderAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [TestMethod]
    public async Task GivenEmptyOrderId_WhenCancelOrderAsync_ThenThrowsArgumentException()
    {
        var act = () => _sut.CancelOrderAsync("");

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
