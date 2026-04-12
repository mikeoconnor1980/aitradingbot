using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Backtesting.Services;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Entities;
using TradingApp.Domain.Enums;
using TradingApp.Domain.Trading;

namespace TradingApp.Application.Tests.Backtesting.Services;

[TestClass]
public sealed class SimulatedExecutionEngineTests
{
    private FeeModel _feeModel = default!;
    private SimulatedExecutionEngine _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _feeModel = new FeeModel
        {
            MakerFeeRate = 0.0001m,
            TakerFeeRate = 0.00035m,
            SlippageRate = 0m
        };

        _sut = new SimulatedExecutionEngine(_feeModel);
    }

    [TestMethod]
    public async Task GivenOrdersPlaced_WhenPlaceOrderAsync_ThenGeneratesUniqueSequentialOrderIds()
    {
        var firstOrderId = await _sut.PlaceOrderAsync(CreateOrderRequest(OrderSide.Buy, OrderType.Limit, 100m, 1m));
        var secondOrderId = await _sut.PlaceOrderAsync(CreateOrderRequest(OrderSide.Buy, OrderType.Limit, 95m, 1m));

        firstOrderId.Should().Be("SIM-000001");
        secondOrderId.Should().Be("SIM-000002");
    }

    [TestMethod]
    public async Task GivenLimitBuyOrder_WhenCandleLowAtOrBelowPrice_ThenFillsOrder()
    {
        var orderId = await _sut.PlaceOrderAsync(CreateOrderRequest(OrderSide.Buy, OrderType.Limit, 100m, 1m));
        var candle = CreateCandle(open: 102m, high: 105m, low: 99m, close: 101m);

        var fills = _sut.ProcessCandle(candle);

        fills.Should().HaveCount(1);
        fills[0].OrderId.Should().Be(orderId);
        fills[0].FillPrice.Should().Be(100m);
        fills[0].Fee.Should().Be(0.01m);
        fills[0].IsMaker.Should().BeTrue();
        _sut.GetOpenOrders().Should().BeEmpty();
        _sut.GetAllFills().Should().ContainSingle();

        var position = _sut.GetPosition();
        position.Symbol.Should().Be("BTC");
        position.Size.Should().Be(1m);
        position.AverageEntryPrice.Should().Be(100m);
        position.RealisedPnL.Should().Be(-0.01m);
        position.UnrealisedPnL.Should().Be(1m);
    }

    [TestMethod]
    public async Task GivenTakeProfitOrder_WhenCandleHighAtOrAbovePrice_ThenFillsOrderAndTracksNetPnl()
    {
        await _sut.PlaceOrderAsync(CreateOrderRequest(OrderSide.Buy, OrderType.Limit, 100m, 1m, TradeType.GridFill));
        _sut.ProcessCandle(CreateCandle(open: 101m, high: 102m, low: 99m, close: 100m, timestampUtc: 1_000));
        var takeProfitOrderId = await _sut.PlaceOrderAsync(CreateOrderRequest(OrderSide.Sell, OrderType.Limit, 110m, 1m, TradeType.TakeProfit));

        var fills = _sut.ProcessCandle(CreateCandle(open: 108m, high: 112m, low: 107m, close: 111m, timestampUtc: 2_000));

        fills.Should().HaveCount(1);
        fills[0].OrderId.Should().Be(takeProfitOrderId);
        fills[0].FillPrice.Should().Be(110m);
        fills[0].Fee.Should().Be(0.011m);

        var position = _sut.GetPosition();
        position.Size.Should().Be(0m);
        position.AverageEntryPrice.Should().Be(0m);
        position.UnrealisedPnL.Should().Be(0m);
        position.RealisedPnL.Should().Be(9.979m);
    }

    [TestMethod]
    public async Task GivenMarketHedgeOrder_WhenProcessCandle_ThenFillsAtCloseWithTakerFeeAndSlippage()
    {
        var feeModel = new FeeModel
        {
            MakerFeeRate = 0.0001m,
            TakerFeeRate = 0.00035m,
            SlippageRate = 0.0005m
        };
        _sut = new SimulatedExecutionEngine(feeModel);

        var orderId = await _sut.PlaceOrderAsync(CreateOrderRequest(OrderSide.Sell, OrderType.Market, 0m, 2m, TradeType.HedgeOpen));

        var fills = _sut.ProcessCandle(CreateCandle(open: 92m, high: 94m, low: 88m, close: 90m));

        fills.Should().HaveCount(1);
        fills[0].OrderId.Should().Be(orderId);
        fills[0].FillPrice.Should().Be(89.955m);
        fills[0].Fee.Should().Be(0.0629685m);
        fills[0].IsMaker.Should().BeFalse();

        var position = _sut.GetPosition();
        position.Size.Should().Be(-2m);
        position.AverageEntryPrice.Should().Be(89.955m);
        position.RealisedPnL.Should().Be(-0.0629685m);
        position.UnrealisedPnL.Should().Be(-0.09m);
    }

    [TestMethod]
    public async Task GivenOrderPriceNotReached_WhenProcessCandle_ThenDoesNotFill()
    {
        await _sut.PlaceOrderAsync(CreateOrderRequest(OrderSide.Buy, OrderType.Limit, 100m, 1m));

        var fills = _sut.ProcessCandle(CreateCandle(open: 103m, high: 106m, low: 101m, close: 104m));

        fills.Should().BeEmpty();
        _sut.GetOpenOrders().Should().ContainSingle();
        _sut.GetAllFills().Should().BeEmpty();
        _sut.GetPosition().IsOpen.Should().BeFalse();
    }

    [TestMethod]
    public async Task GivenBuyAndTakeProfitQualifyOnSameCandle_WhenProcessing_ThenBuyFillsBeforeTakeProfit()
    {
        var takeProfitOrderId = await _sut.PlaceOrderAsync(CreateOrderRequest(OrderSide.Sell, OrderType.Limit, 110m, 1m, TradeType.TakeProfit));
        var buyOrderId = await _sut.PlaceOrderAsync(CreateOrderRequest(OrderSide.Buy, OrderType.Limit, 100m, 1m, TradeType.GridFill));

        var fills = _sut.ProcessCandle(CreateCandle(open: 105m, high: 112m, low: 99m, close: 108m));

        fills.Should().HaveCount(2);
        fills[0].OrderId.Should().Be(buyOrderId);
        fills[1].OrderId.Should().Be(takeProfitOrderId);
        _sut.GetPosition().Size.Should().Be(0m);
        _sut.GetPosition().RealisedPnL.Should().Be(9.979m);
    }

    [TestMethod]
    public async Task GivenCancelledOrder_WhenProcessCandle_ThenDoesNotFill()
    {
        var orderId = await _sut.PlaceOrderAsync(CreateOrderRequest(OrderSide.Buy, OrderType.Limit, 100m, 1m));
        await _sut.CancelOrderAsync(orderId);

        var fills = _sut.ProcessCandle(CreateCandle(open: 102m, high: 103m, low: 99m, close: 100m));

        fills.Should().BeEmpty();
        _sut.GetOpenOrders().Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenOpenOrdersForMultipleSymbols_WhenCancelAllOrdersAsync_ThenRemovesOnlyMatchingSymbolOrders()
    {
        await _sut.PlaceOrderAsync(CreateOrderRequest(OrderSide.Buy, OrderType.Limit, 100m, 1m, symbol: "BTC"));
        await _sut.PlaceOrderAsync(CreateOrderRequest(OrderSide.Buy, OrderType.Limit, 90m, 1m, symbol: "ETH"));

        await _sut.CancelAllOrdersAsync("BTC");

        _sut.GetOpenOrders().Should().ContainSingle();
        _sut.GetOpenOrders()[0].Symbol.Should().Be("ETH");
    }

    [TestMethod]
    public async Task GivenShortPosition_WhenMarketBuyClosesIt_ThenTracksShortPnlAndFees()
    {
        await _sut.PlaceOrderAsync(CreateOrderRequest(OrderSide.Sell, OrderType.Market, 0m, 2m, TradeType.HedgeOpen));
        _sut.ProcessCandle(CreateCandle(open: 92m, high: 94m, low: 88m, close: 90m, timestampUtc: 1_000));
        await _sut.PlaceOrderAsync(CreateOrderRequest(OrderSide.Buy, OrderType.Market, 0m, 2m, TradeType.HedgeClose));

        var fills = _sut.ProcessCandle(CreateCandle(open: 86m, high: 88m, low: 84m, close: 85m, timestampUtc: 2_000));

        fills.Should().HaveCount(1);
        _sut.GetPosition().Size.Should().Be(0m);
        _sut.GetPosition().AverageEntryPrice.Should().Be(0m);
        _sut.GetPosition().RealisedPnL.Should().Be(9.8775m);
    }

    [TestMethod]
    public void GivenEmptyOrderBook_WhenProcessCandle_ThenReturnsNoFills()
    {
        var fills = _sut.ProcessCandle(CreateCandle(open: 100m, high: 101m, low: 99m, close: 100m));

        fills.Should().BeEmpty();
        _sut.GetAllFills().Should().BeEmpty();
        _sut.GetPosition().IsOpen.Should().BeFalse();
    }

    [TestMethod]
    public async Task GivenSetLeverage_WhenCalled_ThenRecordsLeverageForAsset()
    {
        await _sut.SetLeverageAsync("BTC", 33, isIsolated: true);

        _sut.LeverageByAsset.Should().ContainKey("BTC");
        _sut.LeverageByAsset["BTC"].Leverage.Should().Be(33);
        _sut.LeverageByAsset["BTC"].IsIsolated.Should().BeTrue();
    }

    [TestMethod]
    public async Task GivenLeveragedLongPosition_WhenEntryFills_ThenTracksMarginAndLiquidationPrice()
    {
        _sut.SetMaxLeverage("BTC", 50);
        await _sut.SetLeverageAsync("BTC", 33, isIsolated: true);
        await _sut.PlaceOrderAsync(CreateOrderRequest(OrderSide.Buy, OrderType.Limit, 50000m, 1m));

        _sut.ProcessCandle(CreateCandle(open: 50100m, high: 50200m, low: 49900m, close: 50000m));

        var position = _sut.GetPosition();
        position.MarginUsed.Should().BeApproximately(1515.15151515m, 0.00000001m);
        position.Leverage.Should().Be(33);
        position.LiquidationPrice.Should().BeApproximately(48984.84848485m, 0.00000001m);
    }

    [TestMethod]
    public async Task GivenLongPositionWithLeverage_WhenStopLossCrossesBeforeLiquidation_ThenClosesAtStopLoss()
    {
        _sut.SetMaxLeverage("BTC", 50);
        await _sut.SetLeverageAsync("BTC", 33, isIsolated: true);
        await _sut.PlaceOrderAsync(CreateOrderRequest(OrderSide.Buy, OrderType.Limit, 50000m, 1m));
        _sut.ProcessCandle(CreateCandle(open: 50100m, high: 50200m, low: 49900m, close: 50000m, timestampUtc: 1_000));
        await _sut.PlaceTriggerOrderAsync("BTC", "sell", 1m, 49000m, "sl");

        var fills = _sut.ProcessCandle(CreateCandle(open: 50000m, high: 50050m, low: 49000m, close: 49200m, timestampUtc: 2_000));

        fills.Should().ContainSingle();
        fills[0].CloseReason.Should().Be(CancellationReason.StopLossTriggered);
        fills[0].FillPrice.Should().Be(49000m);
        _sut.GetPosition().IsOpen.Should().BeFalse();
        _sut.GetPosition().LiquidationPrice.Should().Be(0m);
        _sut.GetOpenOrders().Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenLongPositionWithLeverage_WhenPriceGapsThroughStopLossToLiquidation_ThenForceClosedAtLiquidationPrice()
    {
        _sut.SetMaxLeverage("BTC", 50);
        await _sut.SetLeverageAsync("BTC", 33, isIsolated: true);
        await _sut.PlaceOrderAsync(CreateOrderRequest(OrderSide.Buy, OrderType.Limit, 50000m, 1m));
        _sut.ProcessCandle(CreateCandle(open: 50100m, high: 50200m, low: 49900m, close: 50000m, timestampUtc: 1_000));
        await _sut.PlaceTriggerOrderAsync("BTC", "sell", 1m, 49000m, "sl");

        var fills = _sut.ProcessCandle(CreateCandle(open: 48990m, high: 48995m, low: 48900m, close: 48920m, timestampUtc: 2_000));

        fills.Should().ContainSingle();
        fills[0].CloseReason.Should().Be(CancellationReason.LiquidationTriggered);
        fills[0].FillPrice.Should().BeApproximately(48984.84848485m, 0.00000001m);
        fills[0].IsMaker.Should().BeFalse();
        _sut.GetPosition().IsOpen.Should().BeFalse();
    }

    [TestMethod]
    public async Task GivenPositionWithLeverageOne_WhenPriceGapsBeyondStopLoss_ThenDoesNotLiquidate()
    {
        _sut.SetMaxLeverage("BTC", 50);
        await _sut.SetLeverageAsync("BTC", 1, isIsolated: true);
        await _sut.PlaceOrderAsync(CreateOrderRequest(OrderSide.Buy, OrderType.Limit, 50000m, 1m));
        _sut.ProcessCandle(CreateCandle(open: 50100m, high: 50200m, low: 49900m, close: 50000m, timestampUtc: 1_000));
        await _sut.PlaceTriggerOrderAsync("BTC", "sell", 1m, 49000m, "sl");

        var fills = _sut.ProcessCandle(CreateCandle(open: 48900m, high: 48950m, low: 47000m, close: 47500m, timestampUtc: 2_000));

        fills.Should().ContainSingle();
        fills[0].CloseReason.Should().Be(CancellationReason.StopLossTriggered);
        _sut.GetAllFills().Should().NotContain(fill => fill.CloseReason == CancellationReason.LiquidationTriggered);
    }

    [TestMethod]
    public async Task GivenShortPositionWithLeverage_WhenPriceGapsThroughStopLoss_ThenLiquidatedAtHighPrice()
    {
        _sut.SetMaxLeverage("BTC", 20);
        await _sut.SetLeverageAsync("BTC", 20, isIsolated: true);
        await _sut.PlaceOrderAsync(CreateOrderRequest(OrderSide.Sell, OrderType.Market, 0m, 1m, TradeType.HedgeOpen));
        _sut.ProcessCandle(CreateCandle(open: 50000m, high: 50050m, low: 49950m, close: 50000m, timestampUtc: 1_000));
        await _sut.PlaceTriggerOrderAsync("BTC", "buy", 1m, 51000m, "sl");

        var fills = _sut.ProcessCandle(CreateCandle(open: 51050m, high: 51300m, low: 51040m, close: 51200m, timestampUtc: 2_000));

        fills.Should().ContainSingle();
        fills[0].CloseReason.Should().Be(CancellationReason.LiquidationTriggered);
        fills[0].FillPrice.Should().Be(51250m);
        _sut.GetPosition().IsOpen.Should().BeFalse();
    }

    private static Candle CreateCandle(
        decimal open,
        decimal high,
        decimal low,
        decimal close,
        string symbol = "BTC",
        long timestampUtc = 1_000)
    {
        return Candle.Create(
            symbol,
            "15m",
            timestampUtc,
            open,
            high,
            low,
            close,
            1000m,
            10);
    }

    private static OrderRequest CreateOrderRequest(
        OrderSide side,
        OrderType orderType,
        decimal price,
        decimal size,
        TradeType tradeType = TradeType.GridFill,
        string symbol = "BTC")
    {
        return new OrderRequest
        {
            Symbol = symbol,
            Side = side,
            OrderType = orderType,
            Price = price,
            Size = size,
            TradeType = tradeType
        };
    }
}