<!-- markdownlint-disable-file -->

# Task Details: F3 — Backtest Replay Engine

## Phase 2: SimulatedExecutionEngine

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — `sealed` classes, `_camelCase` private fields, `async/await`, `CancellationToken`
- `.github/instructions/testing.instructions.md` — MSTest, Moq, FluentAssertions v6, `Given_When_Then` naming, `[TestInitialize]`
- `.agent-context/0-knowledge/18-backtesting-architecture.md` — Fill logic: limit buy at low ≤ price, TP at high ≥ price, hedge at close < breakdown; fee model; slippage
- `.agent-context/3-develop/backlog/draft/backtesting/F3-backtest-replay-engine.md` — Fill priority: buy orders first, then TP; all-or-nothing fills; configurable fees/slippage

## Design References

- Fill PnL formula: `Fill PnL = (Exit Price - Entry Price) × Size × Direction`
- Fee formula: `Fee = Fill Size × Fill Price × Fee Rate`
- Net PnL: `Fill PnL - Entry Fee - Exit Fee`
- Slippage adjusts fill price away from order price by configured percentage
- On same candle: buy fills processed before TP fills (conservative ordering)

---

### Task 2.1: Create SimulatedExecutionEngine with order management {#task-21-create-simulatedexecutionengine-with-order-management}

Create the `SimulatedExecutionEngine` class implementing `IExecutionEngine`. Handle order placement (add to in-memory order book), cancellation, and position tracking.

- **Complexity**: High
- **Risk Factors**: Must correctly maintain order book and position state; order ID generation; thread-safety not required (sequential backtest)
- **Files**:
  - `src/TradingApp.Application/Backtesting/Services/SimulatedExecutionEngine.cs` — new file
- **Success**:
  - Implements `IExecutionEngine` (PlaceOrderAsync, CancelOrderAsync, CancelAllOrdersAsync)
  - Maintains in-memory order book and position state
  - Generates unique order IDs
  - Supports order cancellation
- **Dependencies**: Phase 1 (IExecutionEngine, OrderRequest, SimulatedOrder, SimulatedFill, SimulatedPosition, FeeModel)

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/Services/SimulatedExecutionEngine.cs — new file
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Backtesting.Services;

/// <summary>
/// Simulated execution engine for backtesting. Maintains an in-memory order book
/// and position state. Fills orders based on candle OHLC data.
/// </summary>
public sealed class SimulatedExecutionEngine : IExecutionEngine
{
    private readonly FeeModel _feeModel;
    private readonly List<SimulatedOrder> _openOrders = new();
    private readonly List<SimulatedFill> _allFills = new();
    private readonly SimulatedPosition _position = new();
    private int _orderCounter;

    public SimulatedExecutionEngine(FeeModel feeModel)
    {
        _feeModel = feeModel ?? throw new ArgumentNullException(nameof(feeModel));
    }

    // --- IExecutionEngine ---

    public Task<string> PlaceOrderAsync(OrderRequest order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        var orderId = $"SIM-{++_orderCounter:D6}";

        _openOrders.Add(new SimulatedOrder
        {
            OrderId = orderId,
            Symbol = order.Symbol,
            Side = order.Side,
            OrderType = order.OrderType,
            Price = order.Price,
            Size = order.Size,
            TradeType = order.TradeType,
            PlacedAtUtc = 0 // Set by caller if needed; ProcessCandle uses candle timestamp
        });

        return Task.FromResult(orderId);
    }

    public Task CancelOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        _openOrders.RemoveAll(o => o.OrderId == orderId);
        return Task.CompletedTask;
    }

    public Task CancelAllOrdersAsync(string symbol, CancellationToken cancellationToken = default)
    {
        _openOrders.RemoveAll(o => o.Symbol == symbol);
        return Task.CompletedTask;
    }

    // --- Backtest-specific methods ---

    /// <summary>
    /// Evaluate all open orders against the given candle's OHLC.
    /// Returns fills that occurred on this candle.
    /// Buy orders are processed before TP orders (fill priority).
    /// </summary>
    public IReadOnlyList<SimulatedFill> ProcessCandle(Candle candle)
    {
        // Implementation in Task 2.2
        throw new NotImplementedException();
    }

    public IReadOnlyList<SimulatedOrder> GetOpenOrders() => _openOrders.AsReadOnly();

    public SimulatedPosition GetPosition() => _position;

    public IReadOnlyList<SimulatedFill> GetAllFills() => _allFills.AsReadOnly();
}
```

##### Pattern References

- `src/TradingApp.Application/Abstractions/Services/IHyperliquidRestClient.cs` — Interface pattern; `SimulatedExecutionEngine` implements `IExecutionEngine` the same way `HyperliquidRestClient` implements `IHyperliquidRestClient`
- `src/TradingApp.Infrastructure/Services/NonceProvider.cs` — Counter-based ID generation pattern (Interlocked vs simple increment — sequential backtest doesn't need Interlocked)

---

### Task 2.2: Implement ProcessCandle fill simulation logic {#task-22-implement-processcandle-fill-simulation-logic}

Implement the `ProcessCandle` method with fill simulation rules: limit buy fills, take profit fills, hedge fills, fee/slippage calculation, and fill priority ordering.

- **Complexity**: High
- **Risk Factors**: Fill priority ordering must be correct (buy before TP); slippage direction matters (against trader); position tracking must handle both long and short sides
- **Files**:
  - `src/TradingApp.Application/Backtesting/Services/SimulatedExecutionEngine.cs` — modification (replace `ProcessCandle` stub)
- **Success**:
  - Limit buy fills when `candle.Low ≤ order.Price`
  - Take profit fills when `candle.High ≥ order.Price`
  - Hedge (market) fills at `candle.Close` (± slippage)
  - Fees calculated using `FeeModel`
  - Slippage applied correctly (buy: price increases; sell: price decreases)
  - Buy orders fill before TP orders on same candle
  - Filled orders removed from order book
  - Position state updated after fills
- **Dependencies**: Task 2.1

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/Services/SimulatedExecutionEngine.cs — modification
// Replace the ProcessCandle stub with the full implementation:

public IReadOnlyList<SimulatedFill> ProcessCandle(Candle candle)
{
    var candleFills = new List<SimulatedFill>();
    var filledOrderIds = new List<string>();

    // Sort: buy orders first, then sell/TP orders (fill priority)
    var orderedOrders = _openOrders
        .OrderBy(o => o.Side == OrderSide.Buy ? 0 : 1)
        .ToList();

    foreach (var order in orderedOrders)
    {
        if (order.Symbol != candle.Symbol)
            continue;

        var fill = TryFillOrder(order, candle);
        if (fill is not null)
        {
            candleFills.Add(fill);
            filledOrderIds.Add(order.OrderId);
            UpdatePosition(fill);
        }
    }

    // Remove filled orders from the book
    _openOrders.RemoveAll(o => filledOrderIds.Contains(o.OrderId));
    _allFills.AddRange(candleFills);

    return candleFills;
}

private SimulatedFill? TryFillOrder(SimulatedOrder order, Candle candle)
{
    return order switch
    {
        // Limit buy: fills when candle low ≤ order price
        { Side: OrderSide.Buy, OrderType: OrderType.Limit } when candle.Low <= order.Price
            => CreateFill(order, candle.Timestamp, order.Price, isMaker: true),

        // Take profit (limit sell): fills when candle high ≥ order price
        { Side: OrderSide.Sell, OrderType: OrderType.Limit } when candle.High >= order.Price
            => CreateFill(order, candle.Timestamp, order.Price, isMaker: true),

        // Hedge / market order: fills at candle close
        { OrderType: OrderType.Market }
            => CreateFill(order, candle.Timestamp, candle.Close, isMaker: false),

        _ => null
    };
}

private SimulatedFill CreateFill(SimulatedOrder order, long fillTimeUtc, decimal basePrice, bool isMaker)
{
    var fillPrice = _feeModel.ApplySlippage(basePrice, order.Side);
    var fee = _feeModel.CalculateFee(order.Size, fillPrice, isMaker);

    return new SimulatedFill
    {
        OrderId = order.OrderId,
        FillTimeUtc = fillTimeUtc,
        FillPrice = fillPrice,
        Symbol = order.Symbol,
        Side = order.Side,
        Size = order.Size,
        Fee = fee,
        TradeType = order.TradeType,
        IsMaker = isMaker
    };
}

private void UpdatePosition(SimulatedFill fill)
{
    if (fill.Side == OrderSide.Buy)
    {
        // Buying: increase position or reduce short
        if (_position.Size >= 0)
        {
            // Adding to long position
            var totalCost = (_position.AverageEntryPrice * _position.Size) + (fill.FillPrice * fill.Size);
            _position.Size += fill.Size;
            _position.AverageEntryPrice = _position.Size > 0 ? totalCost / _position.Size : 0;
        }
        else
        {
            // Reducing short position
            var closedSize = Math.Min(fill.Size, Math.Abs(_position.Size));
            var pnl = (fill.FillPrice - _position.AverageEntryPrice) * closedSize * -1; // short: profit when price drops
            _position.RealisedPnL += pnl;
            _position.Size += fill.Size;
            if (_position.Size > 0)
            {
                _position.AverageEntryPrice = fill.FillPrice;
            }
        }
    }
    else // Sell
    {
        if (_position.Size <= 0)
        {
            // Adding to short position
            var totalCost = (Math.Abs(_position.AverageEntryPrice * _position.Size)) + (fill.FillPrice * fill.Size);
            _position.Size -= fill.Size;
            _position.AverageEntryPrice = _position.Size < 0 ? totalCost / Math.Abs(_position.Size) : 0;
        }
        else
        {
            // Reducing long position
            var closedSize = Math.Min(fill.Size, _position.Size);
            var pnl = (fill.FillPrice - _position.AverageEntryPrice) * closedSize;
            _position.RealisedPnL += pnl;
            _position.Size -= fill.Size;
            if (_position.Size < 0)
            {
                _position.AverageEntryPrice = fill.FillPrice;
            }
        }
    }

    _position.Symbol = fill.Symbol;
}
```

##### Pattern References

- `.agent-context/3-develop/backlog/draft/backtesting/F3-backtest-replay-engine.md` — Fill rules table: Limit Buy at `Low ≤ Price`, TP at `High ≥ Price`, Hedge at `Close < Breakdown`; fee model; fill priority
- `.agent-context/0-knowledge/18-backtesting-architecture.md` — Fill logic description

---

### Task 2.3: Write SimulatedExecutionEngine unit tests {#task-23-write-simulatedexecutionengine-unit-tests}

Write comprehensive unit tests covering all fill scenarios, fee calculation, slippage, fill priority, and edge cases.

- **Complexity**: Medium
- **Risk Factors**: Must cover all fill paths and edge cases; position tracking correctness
- **Files**:
  - `tests/TradingApp.Application.Tests/Backtesting/Services/SimulatedExecutionEngineTests.cs` — new file
- **Success**:
  - Tests cover: limit buy fill, TP fill, hedge fill, no fill (price not reached), fee calculation, slippage, fill priority (buy before TP), order cancellation, position tracking, empty order book
  - All tests pass
- **Dependencies**: Tasks 2.1, 2.2

#### Implementation Details

```csharp
// tests/TradingApp.Application.Tests/Backtesting/Services/SimulatedExecutionEngineTests.cs — new file
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Backtesting.Services;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Tests.Backtesting.Services;

[TestClass]
public sealed class SimulatedExecutionEngineTests
{
    private SimulatedExecutionEngine _sut = default!;
    private FeeModel _feeModel = default!;

    [TestInitialize]
    public void Setup()
    {
        _feeModel = new FeeModel
        {
            MakerFeeRate = 0.0001m,  // 0.01%
            TakerFeeRate = 0.00035m, // 0.035%
            SlippageRate = 0m
        };
        _sut = new SimulatedExecutionEngine(_feeModel);
    }

    // --- Limit Buy Fill ---

    [TestMethod]
    public async Task GivenLimitBuyOrder_WhenCandleLowAtOrBelowPrice_ThenFillsOrder()
    {
        // Arrange
        var orderId = await _sut.PlaceOrderAsync(CreateOrderRequest(OrderSide.Buy, OrderType.Limit, 100m, 1m, TradeType.GridFill));
        var candle = CreateCandle(open: 102m, high: 105m, low: 99m, close: 101m); // low <= 100

        // Act
        var fills = _sut.ProcessCandle(candle);

        // Assert
        fills.Should().HaveCount(1);
        fills[0].OrderId.Should().Be(orderId);
        fills[0].FillPrice.Should().Be(100m);
        fills[0].Side.Should().Be(OrderSide.Buy);
    }

    [TestMethod]
    public async Task GivenLimitBuyOrder_WhenCandleLowAbovePrice_ThenDoesNotFill()
    {
        // Arrange
        await _sut.PlaceOrderAsync(CreateOrderRequest(OrderSide.Buy, OrderType.Limit, 100m, 1m, TradeType.GridFill));
        var candle = CreateCandle(open: 102m, high: 105m, low: 101m, close: 103m); // low > 100

        // Act
        var fills = _sut.ProcessCandle(candle);

        // Assert
        fills.Should().BeEmpty();
        _sut.GetOpenOrders().Should().HaveCount(1); // order still in book
    }

    // --- Take Profit Fill ---

    [TestMethod]
    public async Task GivenTakeProfitOrder_WhenCandleHighAtOrAbovePrice_ThenFillsOrder()
    {
        // Arrange
        await _sut.PlaceOrderAsync(CreateOrderRequest(OrderSide.Sell, OrderType.Limit, 110m, 1m, TradeType.TakeProfit));
        var candle = CreateCandle(open: 108m, high: 111m, low: 107m, close: 109m); // high >= 110

        // Act
        var fills = _sut.ProcessCandle(candle);

        // Assert
        fills.Should().HaveCount(1);
        fills[0].FillPrice.Should().Be(110m);
        fills[0].TradeType.Should().Be(TradeType.TakeProfit);
    }

    // --- Hedge (Market) Fill ---

    [TestMethod]
    public async Task GivenMarketOrder_WhenProcessCandle_ThenFillsAtCandleClose()
    {
        // Arrange
        await _sut.PlaceOrderAsync(CreateOrderRequest(OrderSide.Sell, OrderType.Market, 0m, 0.5m, TradeType.HedgeOpen));
        var candle = CreateCandle(open: 100m, high: 102m, low: 98m, close: 99m);

        // Act
        var fills = _sut.ProcessCandle(candle);

        // Assert
        fills.Should().HaveCount(1);
        fills[0].FillPrice.Should().Be(99m); // candle close
        fills[0].IsMaker.Should().BeFalse();
    }

    // --- Fee Calculation ---

    [TestMethod]
    public async Task GivenMakerFee_WhenLimitOrderFills_ThenMakerFeeApplied()
    {
        // Arrange: maker fee = 0.01% on 1 unit @ $100 = $0.01
        await _sut.PlaceOrderAsync(CreateOrderRequest(OrderSide.Buy, OrderType.Limit, 100m, 1m, TradeType.GridFill));
        var candle = CreateCandle(open: 102m, high: 105m, low: 99m, close: 101m);

        // Act
        var fills = _sut.ProcessCandle(candle);

        // Assert
        fills[0].Fee.Should().Be(0.01m); // 1 * 100 * 0.0001
        fills[0].IsMaker.Should().BeTrue();
    }

    // --- Slippage ---

    [TestMethod]
    public async Task GivenSlippage_WhenBuyFills_ThenPriceIncreasedBySlippage()
    {
        // Arrange: 0.05% slippage on buy @ 100 = 100.05
        var feeModelWithSlippage = new FeeModel { SlippageRate = 0.0005m };
        var engine = new SimulatedExecutionEngine(feeModelWithSlippage);
        await engine.PlaceOrderAsync(CreateOrderRequest(OrderSide.Buy, OrderType.Limit, 100m, 1m, TradeType.GridFill));
        var candle = CreateCandle(open: 102m, high: 105m, low: 99m, close: 101m);

        // Act
        var fills = engine.ProcessCandle(candle);

        // Assert
        fills[0].FillPrice.Should().Be(100.05m);
    }

    // --- Fill Priority ---

    [TestMethod]
    public async Task GivenBuyAndTpOnSameCandle_WhenProcessCandle_ThenBuyFillsFirst()
    {
        // Arrange: both buy at 99 and TP at 105 should fill on this candle
        await _sut.PlaceOrderAsync(CreateOrderRequest(OrderSide.Buy, OrderType.Limit, 99m, 1m, TradeType.GridFill));
        await _sut.PlaceOrderAsync(CreateOrderRequest(OrderSide.Sell, OrderType.Limit, 105m, 1m, TradeType.TakeProfit));
        var candle = CreateCandle(open: 100m, high: 106m, low: 98m, close: 103m);

        // Act
        var fills = _sut.ProcessCandle(candle);

        // Assert
        fills.Should().HaveCount(2);
        fills[0].Side.Should().Be(OrderSide.Buy);  // buy first
        fills[1].Side.Should().Be(OrderSide.Sell);  // TP second
    }

    // --- Order Cancellation ---

    [TestMethod]
    public async Task GivenOpenOrder_WhenCancelOrderAsync_ThenOrderRemovedFromBook()
    {
        // Arrange
        var orderId = await _sut.PlaceOrderAsync(CreateOrderRequest(OrderSide.Buy, OrderType.Limit, 100m, 1m, TradeType.GridFill));

        // Act
        await _sut.CancelOrderAsync(orderId);

        // Assert
        _sut.GetOpenOrders().Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenMultipleOrders_WhenCancelAllOrdersAsync_ThenAllOrdersForSymbolRemoved()
    {
        // Arrange
        await _sut.PlaceOrderAsync(CreateOrderRequest(OrderSide.Buy, OrderType.Limit, 100m, 1m, TradeType.GridFill));
        await _sut.PlaceOrderAsync(CreateOrderRequest(OrderSide.Buy, OrderType.Limit, 99m, 1m, TradeType.GridFill));

        // Act
        await _sut.CancelAllOrdersAsync("BTC");

        // Assert
        _sut.GetOpenOrders().Should().BeEmpty();
    }

    // --- Helpers ---

    private static OrderRequest CreateOrderRequest(OrderSide side, OrderType orderType, decimal price, decimal size, TradeType tradeType)
    {
        return new OrderRequest
        {
            Symbol = "BTC",
            Side = side,
            OrderType = orderType,
            Price = price,
            Size = size,
            TradeType = tradeType
        };
    }

    private static Candle CreateCandle(decimal open, decimal high, decimal low, decimal close, long timestamp = 1000)
    {
        // Adjust to match F1's Candle entity constructor/factory
        return new Candle
        {
            Symbol = "BTC",
            Interval = "15m",
            Timestamp = timestamp,
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Volume = 1000m
        };
    }
}
```

##### Pattern References

- `tests/TradingApp.Api.Tests/Services/HyperliquidOrderServiceTests.cs` — Service test with `[TestInitialize]`, mock setup, multiple test methods
- `tests/TradingApp.Infrastructure.Tests/Services/HyperliquidSignerTests.cs` — Pure unit test, Given_When_Then naming, FluentAssertions

---

### Task 2.4: Verify solution builds and all tests pass {#task-24-verify-solution-builds-and-all-tests-pass}

Build the full solution and run all tests.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification only)
- **Success**:
  - `dotnet build` succeeds
  - `dotnet test` passes all tests (Phase 1 + Phase 2)
  - No regressions
- **Dependencies**: All Phase 2 tasks

---

## Phase Success Criteria

- SimulatedExecutionEngine correctly fills limit buy orders when candle low ≤ order price
- SimulatedExecutionEngine correctly fills take profit orders when candle high ≥ TP price
- SimulatedExecutionEngine correctly fills hedge market orders at candle close
- Fees calculated correctly per FeeModel (maker 0.01%, taker 0.035%)
- Slippage applied correctly (buy: price up, sell: price down)
- Fill priority enforced (buy before TP on same candle)
- Order cancellation works (single and all-for-symbol)
- Position tracking updates correctly after fills
- All unit tests pass
