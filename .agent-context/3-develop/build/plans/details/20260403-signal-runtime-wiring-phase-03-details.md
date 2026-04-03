<!-- markdownlint-disable-file -->

# Task Details: F6.75 — Signal Runtime Wiring

## Phase 3: Backtest Signal Execution and Trade Pairing

## Standards and Knowledge References

- **csharp.instructions.md**: `sealed` classes, `_camelCase` private fields, factory methods
- **testing.instructions.md**: MSTest, FluentAssertions v6, Moq, `Given_When_Then` naming
- **18-backtesting-architecture.md**: `BacktestRunner` phases, `SimulatedExecutionEngine`, parity principle
- **16-signal-contracts.md**: `TradingSignal` shape, string-based `SignalType`
- **14-strategy-runtime-model.md**: Shared pipeline for live and backtest

### Task 3.1: Add `TradeType.SignalEntry` enum value {#task-31-add-tradetypesignalentry}

Add a new `SignalEntry` value to the `TradeType` enum so signal-mode entry fills can be distinguished from grid fills in trade pairing and metrics.

- **Complexity**: Low
- **Risk Factors**: Enum addition is additive; existing `switch` statements that don't handle it will fall through to `default` — verify no `default: throw` patterns exist
- **Files**:
  - `src/TradingApp.Application/Trading/Models/TradeType.cs` — modification
- **Success**:
  - `TradeType.SignalEntry` exists and compiles
  - No existing code breaks (all `switch` statements handle the new value gracefully or have no `default: throw`)
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Application/Trading/Models/TradeType.cs — modification
namespace TradingApp.Application.Trading.Models;

public enum TradeType
{
    GridFill,
    TakeProfit,
    HedgeOpen,
    HedgeClose,
    SignalEntry
}
```

##### Pattern References

- `src/TradingApp.Application/Trading/Models/TradeType.cs` — existing enum with 4 values

### Task 3.2: Add `OpenPosition` signal handling in `BacktestPositionManager` {#task-32-add-openposition-signal-handling}

Add a new `case "OpenPosition"` branch in `BacktestPositionManager.ExecuteSignalsAsync` that places a market buy order using `TradeType.SignalEntry`. This mirrors the `DeployGrid` handler but places a single market order instead of a grid of limit orders.

- **Complexity**: Medium
- **Risk Factors**: Must use `TradeType.SignalEntry` (not `GridFill`) so trade pairing works correctly; must handle `TakeProfit` signals for signal-mode exits (already handled by existing `PlaceTakeProfitAsync`)
- **Files**:
  - `src/TradingApp.Application/Trading/Services/BacktestPositionManager.cs` — modification
- **Success**:
  - `OpenPosition` signal places a market order via `SimulatedExecutionEngine` with `TradeType.SignalEntry`
  - Existing `DeployGrid`, `TakeProfit`, `CancelGrid` handling unchanged
- **Dependencies**:
  - Task 3.1 (`TradeType.SignalEntry`)

#### Implementation Details

```csharp
// src/TradingApp.Application/Trading/Services/BacktestPositionManager.cs — modification

// Add new case in ExecuteSignalsAsync switch statement:
        foreach (var signal in approvedSignals)
        {
            switch (signal.SignalType)
            {
                case "DeployGrid":
                    await DeployGridAsync(executionEngine, signal, cancellationToken);
                    break;

                case "TakeProfit":
                    await PlaceTakeProfitAsync(executionEngine, signal, cancellationToken);
                    break;

                case "CancelGrid":
                    await CancelOpenOrdersAsync(
                        executionEngine,
                        signal.Symbol,
                        CancellationReason.ManualCancel,
                        GetGridCycleId(signal.Parameters),
                        cancellationToken);
                    break;

                case "OpenPosition":
                    await OpenSignalPositionAsync(executionEngine, signal, cancellationToken);
                    break;
            }
        }

// Add new private method after DeployGridAsync:
    private async Task OpenSignalPositionAsync(
        SimulatedExecutionEngine executionEngine,
        TradingSignal signal,
        CancellationToken cancellationToken)
    {
        var entryPrice = GetDecimal(signal.Parameters, "entryPrice");
        var size = Math.Abs(GetDecimal(signal.Parameters, "size"));

        if (size <= 0m)
        {
            return;
        }

        await PlaceAndLogOrderAsync(
            executionEngine,
            new OrderRequest
            {
                Symbol = signal.Symbol,
                Side = OrderSide.Buy,
                OrderType = OrderType.Market,
                Price = entryPrice,
                Size = size,
                TradeType = TradeType.SignalEntry,
                GridCycleId = "signal"
            },
            "signal",
            cancellationToken);
    }
```

##### Pattern References

- `src/TradingApp.Application/Trading/Services/BacktestPositionManager.cs` — `DeployGridAsync` method (market order placement pattern), `PlaceAndLogOrderAsync` helper, `GetDecimal` / `GetString` parameter accessors
- `src/TradingApp.Application/Trading/Models/OrderRequest.cs` — `OrderRequest` shape

### Task 3.3: Update `BacktestRunner.RecordFill` and `IsCompatibleExit` for `SignalEntry` to `TakeProfit` pairing {#task-33-update-recordfill-and-iscompatibleexit}

Update the `RecordFill` and `IsCompatibleExit` methods in `BacktestRunner` to handle `TradeType.SignalEntry` fills as entry trades (same as `GridFill`) and pair them with `TakeProfit` exits. Also update `ApplyGridFillState` to handle `SignalEntry` without modifying grid lifecycle.

- **Complexity**: Medium
- **Risk Factors**: `RecordFill` uses `is TradeType.GridFill or TradeType.HedgeOpen` pattern for entries — must add `SignalEntry`. `IsCompatibleExit` must pair `SignalEntry` with `TakeProfit`. `ApplyGridFillState` must not crash on the new enum value.
- **Files**:
  - `src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs` — modification (3 methods)
- **Success**:
  - `SignalEntry` fills are recorded as open trades in the trade log
  - `TakeProfit` fills correctly close `SignalEntry` trades (FIFO pairing)
  - Grid lifecycle state is not affected by `SignalEntry` fills
- **Dependencies**:
  - Task 3.1 (`TradeType.SignalEntry`)

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs — modification

// 1. Update RecordFill — add SignalEntry to the entry-trade guard:
// BEFORE:
//     if (fill.TradeType is TradeType.GridFill or TradeType.HedgeOpen)
// AFTER:
        if (fill.TradeType is TradeType.GridFill or TradeType.HedgeOpen or TradeType.SignalEntry)

// 2. Update IsCompatibleExit — add SignalEntry → TakeProfit pairing:
// BEFORE:
//     return exitTradeType switch
//     {
//         TradeType.TakeProfit => openTrade.TradeType == TradeType.GridFill,
//         TradeType.HedgeClose => openTrade.TradeType == TradeType.HedgeOpen,
//         _ => false
//     };
// AFTER:
        return exitTradeType switch
        {
            TradeType.TakeProfit => openTrade.TradeType is TradeType.GridFill or TradeType.SignalEntry,
            TradeType.HedgeClose => openTrade.TradeType == TradeType.HedgeOpen,
            _ => false
        };

// 3. Update ApplyGridFillState — add no-op case for SignalEntry:
// Add before the closing brace of the switch:
            case TradeType.SignalEntry:
                // Signal-mode entries do not affect grid lifecycle state.
                break;
```

##### Pattern References

- `src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs` — `RecordFill` (line ~310), `IsCompatibleExit` (line ~450), `ApplyGridFillState` (line ~465)

### Task 3.4: Update `BacktestRunner` to pass `ISignalController` into `StrategyScheduler` {#task-34-update-backtestrunner-to-pass-isignalcontroller}

Update `BacktestRunner` to accept `ISignalController` via constructor injection and pass it to the `StrategyScheduler` it creates. This enables signal-mode strategies to execute through the signal controller during backtests.

- **Complexity**: Medium
- **Risk Factors**: Constructor change to `BacktestRunner` requires updating DI registration and test setup
- **Files**:
  - `src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs` — modification
  - `src/TradingApp.Api/Program.cs` — modification (if `BacktestRunner` is DI-registered)
  - `tests/TradingApp.Application.Tests/Backtesting/Services/BacktestRunnerTests.cs` — modification (update `Setup()` to add `Mock<ISignalController>` and pass to constructor)
- **Important**: Existing `BacktestRunnerTests.Setup()` constructs `BacktestRunner` with 7 parameters. Adding the required `ISignalController` parameter will break compilation. The test setup must be updated to: (1) add a `Mock<ISignalController>` field, (2) pass `.Object` as the 8th constructor parameter, (3) update the `IMarketContextBuilder` mock to also handle the 4-arg `Build` overload (since Phase 1 changed the scheduler to always call 4-arg).
- **Success**:
  - `BacktestRunner` passes `ISignalController` to `StrategyScheduler` constructor
  - Signal-mode backtests route through `SignalController` instead of `GridController`
- **Dependencies**:
  - Phase 2 completed (ISignalController, SignalController, StrategyScheduler updated constructor)

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs — modification

// Add field:
    private readonly ISignalController _signalController;

// Add constructor parameter (after _positionManager):
    public BacktestRunner(
        ICandleRepository candleRepository,
        IMarketContextBuilder marketContextBuilder,
        IStrategyEngine strategyEngine,
        IGridController gridController,
        IRiskEngine riskEngine,
        IPositionManager positionManager,
        BacktestExecutionContextAccessor executionContextAccessor,
        ISignalController signalController)
    {
        // ... existing null checks ...
        _signalController = signalController ?? throw new ArgumentNullException(nameof(signalController));
    }

// Update StrategyScheduler construction in RunAsync:
// BEFORE:
//     var scheduler = new StrategyScheduler(
//         _marketContextBuilder,
//         _strategyEngine,
//         _gridController,
//         _riskEngine,
//         positionManager,
//         config.Strategy,
//         auditCollector: collector);
// AFTER:
        var scheduler = new StrategyScheduler(
            _marketContextBuilder,
            _strategyEngine,
            _gridController,
            _riskEngine,
            positionManager,
            config.Strategy,
            auditCollector: collector,
            signalController: _signalController);
```

##### Pattern References

- `src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs` — existing constructor pattern (line ~28), `StrategyScheduler` construction (line ~70)
- `src/TradingApp.Api/Program.cs` — DI registration pattern

### Task 3.5: Add `BacktestPositionManager` tests for `OpenPosition` signal handling {#task-35-add-backtestpositionmanager-openposition-tests}

Add tests to verify that `BacktestPositionManager` correctly handles `OpenPosition` signals by placing market orders with `TradeType.SignalEntry`.

- **Complexity**: Medium
- **Risk Factors**: Need to mock `SimulatedExecutionEngine` via `BacktestExecutionContextAccessor` — follow existing test patterns
- **Files**:
  - `tests/TradingApp.Application.Tests/Trading/Services/BacktestPositionManagerTests.cs` — new or modification (check if exists)
- **Success**:
  - Test verifies `OpenPosition` signal places a market buy order with `TradeType.SignalEntry`
  - Test verifies `OpenPosition` signal with zero size does not place an order
  - Test verifies existing `DeployGrid` handling is unchanged
- **Dependencies**:
  - Task 3.2 (OpenPosition handler in BacktestPositionManager)

#### Implementation Details

```csharp
// tests/TradingApp.Application.Tests/Trading/Services/BacktestPositionManagerTests.cs — new file or modification

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
    public async Task GivenOpenPositionSignal_WhenExecuteSignals_ThenMarketOrderPlacedWithSignalEntry()
    {
        // Arrange
        var executionEngine = new SimulatedExecutionEngine(new FeeModel { MakerFee = 0.0002m, TakerFee = 0.0005m });
        _contextAccessor.CurrentExecutionEngine = executionEngine;
        _contextAccessor.CurrentTimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var signal = new TradingSignal
        {
            SignalType = "OpenPosition",
            Symbol = "BTC-USD",
            Reason = "RSI below 30.",
            Parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["entryPrice"] = 50000m,
                ["size"] = 0.02m,
                ["orderType"] = "Market",
            }
        };

        // Act
        await _sut.ExecuteSignalsAsync([signal]);

        // Assert — market orders are immediately filled by SimulatedExecutionEngine,
        // so verify via the execution engine's fill log or order history.
        // The implementing agent should check the available assertion surface on
        // SimulatedExecutionEngine (e.g., GetFills(), GetPosition(), or internal order list)
        // and assert that exactly one fill with TradeType.SignalEntry was recorded.
    }

    [TestMethod]
    public async Task GivenOpenPositionSignalWithZeroSize_WhenExecuteSignals_ThenNoOrderPlaced()
    {
        // Arrange
        var executionEngine = new SimulatedExecutionEngine(new FeeModel { MakerFee = 0.0002m, TakerFee = 0.0005m });
        _contextAccessor.CurrentExecutionEngine = executionEngine;
        _contextAccessor.CurrentTimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var signal = new TradingSignal
        {
            SignalType = "OpenPosition",
            Symbol = "BTC-USD",
            Reason = "RSI below 30.",
            Parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["entryPrice"] = 50000m,
                ["size"] = 0m,
                ["orderType"] = "Market",
            }
        };

        // Act
        await _sut.ExecuteSignalsAsync([signal]);

        // Assert
        var openOrders = executionEngine.GetOpenOrders();
        openOrders.Should().BeEmpty();
    }
}
```

##### Pattern References

- `tests/TradingApp.Application.Tests/Backtesting/Services/BacktestRunnerTests.cs` — `BacktestExecutionContextAccessor` usage, `SimulatedExecutionEngine` construction with `FeeModel`
- `src/TradingApp.Application/Trading/Services/BacktestPositionManager.cs` — `ExecuteSignalsAsync` method

### Task 3.6: Add end-to-end backtest test for signal-mode strategy {#task-36-add-e2e-backtest-signal-mode-test}

Add a backtest test that verifies a signal-mode strategy with RSI conditions can open and close trades when conditions are met. This is the integration-level test that proves the full pipeline works end-to-end.

- **Complexity**: High
- **Risk Factors**: Requires mocking `ICandleRepository` with sufficient candle history for RSI(14) warmup; must verify trade log contains at least one completed trade
- **Files**:
  - `tests/TradingApp.Application.Tests/Backtesting/Services/BacktestRunnerTests.cs` — modification
- **Success**:
  - Signal-mode backtest with passing RSI conditions records at least one `SignalEntry` trade
  - Signal-mode backtest with non-passing RSI conditions records zero trades
  - Grid-mode backtest regression: existing tests still pass
- **Dependencies**:
  - All Phase 3 tasks (3.1-3.4) completed and passing

#### Implementation Details

```csharp
// tests/TradingApp.Application.Tests/Backtesting/Services/BacktestRunnerTests.cs — modification
// Add new test methods:

    [TestMethod]
    public async Task GivenSignalModeStrategyWithPassingRsi_WhenRunAsync_ThenTradesRecorded()
    {
        // Arrange
        var signalConfig = new StrategyConfig
        {
            SchemaVersion = 1,
            StrategyMode = StrategyMode.Signal,
            StrategyName = "RSI Signal Test",
            Market = "BTC-USD",
            Timeframe = "15m",
            Direction = Direction.Long,
            EntryLogic = EntryLogic.All,
            EntryConditions =
            [
                new EntryConditionConfig
                {
                    Id = "rsi-entry",
                    Type = EntryConditionType.Rsi,
                    Enabled = true,
                    Params = new RsiParams { Period = 14, Operator = "lt", Threshold = 30m },
                }
            ],
            Exit = new ExitConfig
            {
                TakeProfit = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.FixedPercent, Value = 2m },
                StopLoss = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.FixedPercent, Value = 5m },
            },
            Risk = new RiskConfig { PositionSizeValue = 1000m, Leverage = 1m, MaxOpenTrades = 1 },
        };

        var backtestConfig = CreateBacktestConfig(signalConfig);

        // Set up candle data with a price decline sufficient to trigger RSI < 30
        // then a recovery to trigger take profit
        SetupCandleDataForSignalMode();

        // Act
        var result = await _sut.RunAsync(backtestConfig);

        // Assert
        result.TradeLog.Should().NotBeEmpty("signal-mode strategy should open trades when RSI conditions are met");
        result.TradeLog.Should().Contain(trade => trade.TradeType == TradeType.SignalEntry);
    }

    [TestMethod]
    public async Task GivenSignalModeStrategyWithNonPassingRsi_WhenRunAsync_ThenNoTradesRecorded()
    {
        // Arrange — RSI threshold set very low (5) so conditions never pass with normal price action
        var signalConfig = new StrategyConfig
        {
            SchemaVersion = 1,
            StrategyMode = StrategyMode.Signal,
            StrategyName = "RSI Signal Test - No Trigger",
            Market = "BTC-USD",
            Timeframe = "15m",
            Direction = Direction.Long,
            EntryLogic = EntryLogic.All,
            EntryConditions =
            [
                new EntryConditionConfig
                {
                    Id = "rsi-entry",
                    Type = EntryConditionType.Rsi,
                    Enabled = true,
                    Params = new RsiParams { Period = 14, Operator = "lt", Threshold = 5m },
                }
            ],
            Exit = new ExitConfig
            {
                TakeProfit = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.FixedPercent, Value = 2m },
                StopLoss = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.FixedPercent, Value = 5m },
            },
            Risk = new RiskConfig { PositionSizeValue = 1000m, Leverage = 1m, MaxOpenTrades = 1 },
        };

        var backtestConfig = CreateBacktestConfig(signalConfig);
        SetupCandleDataForSignalMode();

        // Act
        var result = await _sut.RunAsync(backtestConfig);

        // Assert
        result.TradeLog.Should().BeEmpty("RSI should never drop below 5 with normal price data");
    }
```

Note: The implementing agent will need to adapt the candle data setup helpers (`SetupCandleDataForSignalMode`, `CreateBacktestConfig`) to match the existing test patterns in `BacktestRunnerTests.cs`. The key requirement is generating enough candle history for RSI(14) warmup (~50 candles) with a price decline that produces RSI < 30.

##### Pattern References

- `tests/TradingApp.Application.Tests/Backtesting/Services/BacktestRunnerTests.cs` — existing backtest test structure, `BacktestConfig` construction, candle data setup via `ICandleRepository` mock

### Task 3.7: Run full test suite to verify no regression {#task-37-run-full-test-suite}

Run the complete test suite across all test projects to verify no regressions in grid-mode, backtest, or scheduling flows.

- **Complexity**: Low
- **Risk Factors**: None — verification step
- **Files**:
  - All test projects under `tests/`
- **Success**:
  - All existing tests pass without modification
  - All new Phase 3 tests pass
  - `dotnet test` exits with code 0
- **Dependencies**:
  - All Phase 3 tasks completed

## Phase Success Criteria

- `TradeType.SignalEntry` exists and is used for signal-mode position entries
- `BacktestPositionManager` handles `OpenPosition` signals with market orders using `TradeType.SignalEntry`
- `BacktestRunner` pairs `SignalEntry` entries with `TakeProfit` exits via `IsCompatibleExit`
- `BacktestRunner` passes `ISignalController` to `StrategyScheduler` for signal-mode backtest execution
- End-to-end backtest of a signal-mode RSI strategy opens and closes trades correctly
- All grid-mode backtest tests pass unchanged (no regression)
