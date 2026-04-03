using TradingApp.Application.Backtesting;
using TradingApp.Application.Backtesting.Services;
using TradingApp.Application.Trading.Models;
using TradingApp.Application.Trading.Services;
using TradingApp.Domain.Entities;
using TradingApp.Domain.Enums;
using TradingApp.Domain.Trading;

namespace TradingApp.Application.Tests.Trading.Services;

[TestClass]
public sealed class BacktestPositionManagerTests
{
    private BacktestExecutionContextAccessor _contextAccessor = default!;
    private BacktestPositionManager _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _contextAccessor = new BacktestExecutionContextAccessor();
        _sut = new BacktestPositionManager(_contextAccessor);
    }

    [TestMethod]
    public async Task GivenOpenPositionSignal_WhenExecuteSignalsAsync_ThenPlacesSignalEntryMarketOrder()
    {
        var executionEngine = CreateExecutionEngine();
        _contextAccessor.CurrentExecutionEngine = executionEngine;
        _contextAccessor.CurrentTimestampUtc = 1_000;

        await _sut.ExecuteSignalsAsync(
        [
            new TradingSignal
            {
                SignalType = "OpenPosition",
                Symbol = "BTC",
                Reason = "RSI below threshold.",
                Parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["entryPrice"] = 50_000m,
                    ["size"] = 0.02m,
                    ["orderType"] = OrderType.Market.ToString(),
                    ["gridCycleId"] = "signal"
                }
            }
        ]);

        var openOrders = executionEngine.GetOpenOrders();
        openOrders.Should().ContainSingle();
        openOrders[0].TradeType.Should().Be(TradeType.SignalEntry);
        openOrders[0].Side.Should().Be(OrderSide.Buy);
        openOrders[0].OrderType.Should().Be(OrderType.Market);
        openOrders[0].GridCycleId.Should().Be("signal");

        var fills = executionEngine.ProcessCandle(CreateCandle(close: 50_250m, timestampUtc: 2_000));

        fills.Should().ContainSingle();
        fills[0].TradeType.Should().Be(TradeType.SignalEntry);
        executionEngine.GetPosition().Size.Should().Be(0.02m);
    }

    [TestMethod]
    public async Task GivenOpenPositionSignalWithZeroSize_WhenExecuteSignalsAsync_ThenDoesNotPlaceOrder()
    {
        var executionEngine = CreateExecutionEngine();
        _contextAccessor.CurrentExecutionEngine = executionEngine;
        _contextAccessor.CurrentTimestampUtc = 1_000;

        await _sut.ExecuteSignalsAsync(
        [
            new TradingSignal
            {
                SignalType = "OpenPosition",
                Symbol = "BTC",
                Reason = "RSI below threshold.",
                Parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["entryPrice"] = 50_000m,
                    ["size"] = 0m,
                    ["orderType"] = OrderType.Market.ToString(),
                    ["gridCycleId"] = "signal"
                }
            }
        ]);

        executionEngine.GetOpenOrders().Should().BeEmpty();
        executionEngine.GetAllFills().Should().BeEmpty();
    }

    private static SimulatedExecutionEngine CreateExecutionEngine()
    {
        return new SimulatedExecutionEngine(new FeeModel
        {
            MakerFeeRate = 0.0001m,
            TakerFeeRate = 0.00035m,
            SlippageRate = 0m,
        });
    }

    private static Candle CreateCandle(decimal close, long timestampUtc)
    {
        return Candle.Create(
            "Binance",
            "BTC",
            "15m",
            timestampUtc,
            close,
            close + 100m,
            Math.Max(0m, close - 100m),
            close,
            1_000m,
            10);
    }
}