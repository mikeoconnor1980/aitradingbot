<!-- markdownlint-disable-file -->

# Task Details: Backtest Debug/Audit Log

## Phase 3: Pipeline Integration

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — sealed classes, async/await, CancellationToken propagation
- `.github/instructions/testing.instructions.md` — MSTest, FluentAssertions, Moq; integration test pattern from RealBacktestRunnerTests
- `.agent-context/0-knowledge/18-backtesting-architecture.md` — replay engine phases, shared pipeline
- `.agent-context/0-knowledge/15-grid-controller.md` — grid lifecycle state machine, signal emission
- `.agent-context/0-knowledge/14-strategy-runtime-model.md` — StrategyScheduler pipeline: context → evaluate → grid → risk → execute

## Design References

- **Audit collector injection pattern**: `StrategyScheduler` currently takes 6 constructor dependencies. The `IBacktestAuditCollector` becomes the 7th with a default of `NullBacktestAuditCollector.Instance`. Existing callers (live mode, tests) need no changes.
- **Order event capture**: `BacktestPositionManager` has access to `BacktestExecutionContextAccessor` which provides the `SimulatedExecutionEngine`. Before calling `CancelAllOrdersAsync`, enumerate open orders via a new `GetOpenOrders()` method on the engine, then log cancellation events.
- **Grid cycle tracking**: `BacktestRunner` already detects closed grid cycles via `TryCountClosedGridCycle`. Extend this to build `GridCycleEntry` from accumulated data.

### Task 3.1: Update StrategyScheduler to accept and invoke IBacktestAuditCollector {#task-31-update-strategyscheduler-to-accept-and-invoke-ibacktestauditcollector}

Add `IBacktestAuditCollector` as an optional constructor parameter (defaulting to `NullBacktestAuditCollector.Instance`). After the strategy evaluation and grid processing in `HandleCandleClosedAsync`, invoke `LogCandleEvaluation` with the full evaluation state.

- **Complexity**: High
- **Risk Factors**: Modifying the shared scheduler — must not break live trading path; default null collector ensures backward compatibility
- **Files**:
  - `src/TradingApp.Application/Scheduling/StrategyScheduler.cs` — modification
- **Success**:
  - Constructor accepts optional `IBacktestAuditCollector` parameter
  - After evaluation, `LogCandleEvaluation` is called with candle data, indicator snapshot, SetupDetected, grid state, position state, and signals emitted
  - Existing caller code compiles without changes (default parameter)
  - Live mode uses `NullBacktestAuditCollector` (zero overhead)
- **Dependencies**: Phase 1 (all tasks)

#### Implementation Details

```csharp
// src/TradingApp.Application/Scheduling/StrategyScheduler.cs — modification

// Add to field declarations:
    private readonly IBacktestAuditCollector _auditCollector;

// Update constructor — add optional parameter:
    public StrategyScheduler(
        IMarketContextBuilder contextBuilder,
        IStrategyEngine strategyEngine,
        IGridController gridController,
        IRiskEngine riskEngine,
        IPositionManager positionManager,
        string strategyConfigJson,
        string triggerTimeframe = "15m",
        IBacktestAuditCollector? auditCollector = null)
    {
        // ... existing assignments ...
        _auditCollector = auditCollector ?? NullBacktestAuditCollector.Instance;
    }
```

Update `HandleCandleClosedAsync` — add audit logging after evaluation and signal processing:

**Note**: The code below shows the FULL method body for context, but this is a MODIFICATION to the existing method. Only add the `_auditCollector` field, update the constructor with the optional parameter, and insert the `LogCandleEvaluation` call between grid processing and the `signals.Count == 0` check. Do not replace the entire method.

```csharp
// src/TradingApp.Application/Scheduling/StrategyScheduler.cs — modification to HandleCandleClosedAsync
    public async Task HandleCandleClosedAsync(
        CandleClosedEvent evt,
        Candle? latestOneHourCandle,
        Candle? latestFourHourCandle,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evt);

        if (!string.Equals(evt.Timeframe, _triggerTimeframe, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var context = _contextBuilder.Build(
            evt.Candle,
            latestOneHourCandle,
            latestFourHourCandle);

        var evaluation = await _strategyEngine.EvaluateAsync(
            context,
            _strategyConfigJson,
            cancellationToken);

        var signals = await _gridController.ProcessAsync(
            evaluation,
            context,
            _gridState,
            _positionState,
            _strategyConfigJson,
            cancellationToken);

        // Log the per-candle evaluation via audit collector
        _auditCollector.LogCandleEvaluation(new CandleEvaluationEntry
        {
            TimestampUtc = evt.Candle.Timestamp,
            Open = evt.Candle.Open,
            High = evt.Candle.High,
            Low = evt.Candle.Low,
            Close = evt.Candle.Close,
            Volume = evt.Candle.Volume,
            IsWarmup = false,
            EmaFast = context.Indicators?.EmaFast ?? 0m,
            EmaSlow = context.Indicators?.EmaSlow ?? 0m,
            EmaTrend = context.Indicators?.EmaTrend ?? 0m,
            Rsi = context.Indicators?.Rsi ?? 0m,
            Atr = context.Indicators?.Atr ?? 0m,
            SetupDetected = evaluation.SetupDetected,
            GridLifecycleState = _gridState.Lifecycle.ToString(),
            PositionSize = _positionState.Size,
            PositionAvgEntry = _positionState.AverageEntryPrice,
            SignalsEmitted = signals.Select(s => s.SignalType).ToList(),
            GridCycleId = _gridState.GridCycleId
        });

        if (signals.Count == 0)
        {
            return;
        }

        var approvedSignals = await _riskEngine.ValidateAsync(signals, cancellationToken);
        if (approvedSignals.Count == 0)
        {
            return;
        }

        await _positionManager.ExecuteSignalsAsync(approvedSignals, cancellationToken);
    }
```

Add the required using:

```csharp
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Backtesting.Services;
```

##### Pattern References

- `src/TradingApp.Application/Scheduling/StrategyScheduler.cs` — existing constructor pattern with optional `triggerTimeframe` parameter
- `src/TradingApp.Application/Trading/Models/IndicatorSnapshot.cs` — indicator fields mapped to CandleEvaluationEntry

---

### Task 3.2: Update BacktestPositionManager to log order events {#task-32-update-backtestpositionmanager-to-log-order-events}

Inject `IBacktestAuditCollector` into `BacktestPositionManager`. Log `Placed` events when orders are submitted. Before cancellation calls, enumerate open orders and log `Cancelled` events with appropriate reason codes. Also add a `GetOpenOrders()` method to `SimulatedExecutionEngine` to enable pre-cancellation enumeration.

- **Complexity**: High
- **Risk Factors**: Must correctly infer cancellation reasons from signal context (DeployGrid → GridRedeployed, TakeProfit with reason "stop_loss" → StopLossTriggered)
- **Files**:
  - `src/TradingApp.Application/Trading/Services/BacktestPositionManager.cs` — modification
  - `src/TradingApp.Application/Backtesting/Services/SimulatedExecutionEngine.cs` — modification (add GetOpenOrders)
- **Success**:
  - Order `Placed` events logged for every `PlaceOrderAsync` call
  - Order `Cancelled` events logged before every `CancelAllOrdersAsync` with correct reason codes
  - Order `Filled` events logged for fills (captured in BacktestRunner, Task 3.3)
  - `SimulatedExecutionEngine.GetOpenOrders()` returns current open orders
- **Dependencies**: Phase 1 (all tasks)

#### Implementation Details

Add `GetOpenOrders` to `SimulatedExecutionEngine`:

```csharp
// src/TradingApp.Application/Backtesting/Services/SimulatedExecutionEngine.cs — modification
// Add new public method:

    public IReadOnlyList<SimulatedOrder> GetOpenOrders() => _openOrders.ToList();
```

Update `BacktestPositionManager` — inject audit collector:

**Important prerequisite**: Add a `CurrentTimestampUtc` property to `BacktestExecutionContextAccessor` (currently only has `CurrentExecutionEngine`). The `BacktestRunner` must set this to the current candle's `Timestamp` before each candle iteration. This ensures order events use simulated time instead of wall-clock time.

```csharp
// src/TradingApp.Application/Backtesting/BacktestExecutionContextAccessor.cs — modification
// Add property:
    private readonly AsyncLocal<long> _currentTimestampUtc = new();

    public long CurrentTimestampUtc
    {
        get => _currentTimestampUtc.Value;
        set => _currentTimestampUtc.Value = value;
    }
```

```csharp
// src/TradingApp.Application/Trading/Services/BacktestPositionManager.cs — modification

public sealed class BacktestPositionManager : IPositionManager
{
    private readonly BacktestExecutionContextAccessor _executionContextAccessor;
    private readonly IBacktestAuditCollector _auditCollector;

    public BacktestPositionManager(
        BacktestExecutionContextAccessor executionContextAccessor,
        IBacktestAuditCollector? auditCollector = null)
    {
        _executionContextAccessor = executionContextAccessor ?? throw new ArgumentNullException(nameof(executionContextAccessor));
        _auditCollector = auditCollector ?? NullBacktestAuditCollector.Instance;
    }
    // ... existing code ...
```

Update `DeployGridAsync` to log cancellations and placements:

```csharp
// src/TradingApp.Application/Trading/Services/BacktestPositionManager.cs — modification to DeployGridAsync

    private async Task DeployGridAsync(
        Backtesting.Services.SimulatedExecutionEngine executionEngine,
        TradingSignal signal,
        CancellationToken cancellationToken)
    {
        // Log cancellations before cancelling
        var openOrders = executionEngine.GetOpenOrders();
        foreach (var order in openOrders)
        {
            _auditCollector.LogOrderEvent(new OrderEventEntry
            {
                // Use simulated time from execution context, NOT DateTimeOffset.UtcNow
                // The current candle timestamp must be available via BacktestExecutionContextAccessor.
                // Add a CurrentTimestampUtc property to BacktestExecutionContextAccessor and set it
                // in BacktestRunner before each candle iteration.
                TimestampUtc = _executionContextAccessor.CurrentTimestampUtc,
                EventType = OrderEventType.Cancelled,
                OrderId = order.OrderId,
                Side = order.Side.ToString(),
                OrderType = order.OrderType.ToString(),
                Price = order.Price,
                Size = order.Size,
                CancellationReason = CancellationReason.GridRedeployed,
                GridCycleId = signal.Parameters?.TryGetValue("gridCycleId", out var cycleId) == true
                    ? cycleId?.ToString() ?? "unknown"
                    : "unknown"
            });
        }

        await executionEngine.CancelAllOrdersAsync(signal.Symbol, cancellationToken);

        var anchorPrice = GetDecimal(signal.Parameters, "anchorPrice");
        var gridLevels = GetInt(signal.Parameters, "gridLevels");
        var gridSpacingPercent = Math.Abs(GetDecimal(signal.Parameters, "gridSpacingPercent"));
        var notionalPerLevel = Math.Abs(GetDecimal(signal.Parameters, "notionalPerLevel"));

        for (var level = 1; level <= gridLevels; level++)
        {
            var price = anchorPrice * (1m - ((gridSpacingPercent / 100m) * level));
            if (price <= 0m) continue;

            var size = decimal.Round(notionalPerLevel / price, 8, MidpointRounding.AwayFromZero);
            if (size <= 0m) continue;

            var orderId = await executionEngine.PlaceOrderAsync(
                new OrderRequest
                {
                    Symbol = signal.Symbol,
                    Side = OrderSide.Buy,
                    OrderType = OrderType.Limit,
                    Price = price,
                    Size = size,
                    TradeType = TradeType.GridFill
                },
                cancellationToken);

            // Log the placement — use simulated time, not wall-clock time
            _auditCollector.LogOrderEvent(new OrderEventEntry
            {
                TimestampUtc = _executionContextAccessor.CurrentTimestampUtc,
                EventType = OrderEventType.Placed,
                OrderId = orderId,
                Side = "Buy",
                OrderType = "Limit",
                Price = price,
                Size = size,
                GridCycleId = signal.Parameters?.TryGetValue("gridCycleId", out var placedCycleId) == true
                    ? placedCycleId?.ToString() ?? "unknown"
                    : "unknown"
            });
        }
    }
```

Note: `PlaceOrderAsync` already returns `Task<string>` (confirmed from `IExecutionEngine` interface), so the returned order ID can be used directly for logging.

Similarly update `PlaceTakeProfitAsync` with cancellation logging (reason: determine from `signal.Reason` — if it contains "stop_loss" use `StopLossTriggered`, otherwise `PositionOpened`) and placement logging.

Also need to pass the `GridCycleId` through the signal parameters. The `GridController` generates the `GridCycleId` in `GridState` when deploying — it should be included in the `DeployGrid` signal parameters. If not already present, update `GridController.ProcessAsync` to include `gridCycleId` in the signal parameters dictionary.

##### Pattern References

- `src/TradingApp.Application/Trading/Services/BacktestPositionManager.cs` — existing signal routing pattern
- `src/TradingApp.Application/Backtesting/Services/SimulatedExecutionEngine.cs` — `_openOrders` list access

---

### Task 3.3: Update BacktestRunner to wire collector and log grid cycles {#task-33-update-backtestrunner-to-wire-collector-and-log-grid-cycles}

Update `BacktestRunner.RunAsync` to:
1. Create the appropriate `IBacktestAuditCollector` based on `config.EnableAuditLog`
2. Pass it to the `StrategyScheduler` constructor
3. Log warmup candle entries (with `IsWarmup = true`)
4. Log `Filled` order events from fills returned by `executionEngine.ProcessCandle`
5. Build `GridCycleEntry` when `TryCountClosedGridCycle` returns true
6. Return the collector data alongside the `BacktestResult`

- **Complexity**: High
- **Risk Factors**: Multiple integration points in the main loop; must track accumulated grid cycle data (deploy time, anchor price, levels) to build entries at cycle close
- **Files**:
  - `src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs` — modification
  - `src/TradingApp.Application/Backtesting/Models/BacktestResult.cs` — modification (add audit data)
  - `src/TradingApp.Application/Backtesting/Services/IBacktestRunner.cs` — check if interface needs updating
- **Success**:
  - Audit collector is created/wired correctly based on `EnableAuditLog`
  - Warmup candles produce `IsWarmup = true` entries with indicator values
  - Fills produce `Filled` order events
  - Closed grid cycles produce `GridCycleEntry` records
  - `BacktestResult` includes audit data (nullable)
- **Dependencies**: Task 3.1, Task 3.2, Phase 1, Phase 2

#### Implementation Details

Update `BacktestResult` to carry audit data:

```csharp
// src/TradingApp.Application/Backtesting/Models/BacktestResult.cs — modification
// Add nullable properties for audit data (non-required, defaulting to null):

    public IReadOnlyList<CandleEvaluationEntry>? CandleEvaluationLog { get; init; }
    public IReadOnlyList<OrderEventEntry>? OrderEventLog { get; init; }
    public IReadOnlyList<GridCycleEntry>? GridCycleLog { get; init; }
```

**Note:** `BacktestResult` is a `sealed class`, not a `record`. The `with` expression is NOT available. These new properties must NOT use the `required` keyword — they default to null. After `metricsCalculator.Calculate(...)` returns, construct the final result by updating `BacktestMetricsCalculator.Calculate()` to accept and forward the audit data, or by creating a new `BacktestResult` instance that copies all computed fields and adds the audit data. The simplest approach: add the 3 properties to the object initializer inside `BacktestMetricsCalculator.Calculate()` (passing them as parameters), or construct a new result after calculation.

Update `BacktestRunner.RunAsync` — collector creation and wiring:

```csharp
// src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs — modification

    // At the start of RunAsync, after validation:
    var auditCollector = config.EnableAuditLog
        ? new BacktestAuditCollector()
        : null;
    IBacktestAuditCollector collector = auditCollector ?? NullBacktestAuditCollector.Instance;

    // Pass collector to StrategyScheduler:
    var scheduler = new StrategyScheduler(
        _marketContextBuilder,
        _strategyEngine,
        _gridController,
        _riskEngine,
        _positionManager,
        config.StrategyConfigJson,
        auditCollector: collector);
```

Log warmup candle entries:

```csharp
    // In the warmup loop:
    for (var index = 0; index < replayData.WarmupEndIndex; index++)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var warmupCandle = replayData.Candles15m[index];
        _marketContextBuilder.UpdateIndicators(warmupCandle);

        if (auditCollector is not null)
        {
            // Build context to get indicator snapshot during warmup
            var warmupContext = _marketContextBuilder.Build(warmupCandle, null, null);
            auditCollector.LogCandleEvaluation(new CandleEvaluationEntry
            {
                TimestampUtc = warmupCandle.Timestamp,
                Open = warmupCandle.Open,
                High = warmupCandle.High,
                Low = warmupCandle.Low,
                Close = warmupCandle.Close,
                Volume = warmupCandle.Volume,
                IsWarmup = true,
                EmaFast = warmupContext.Indicators?.EmaFast ?? 0m,
                EmaSlow = warmupContext.Indicators?.EmaSlow ?? 0m,
                EmaTrend = warmupContext.Indicators?.EmaTrend ?? 0m,
                Rsi = warmupContext.Indicators?.Rsi ?? 0m,
                Atr = warmupContext.Indicators?.Atr ?? 0m,
                SetupDetected = false,
                GridLifecycleState = "Inactive",
                PositionSize = 0m,
                PositionAvgEntry = 0m,
                SignalsEmitted = [],
                GridCycleId = null
            });
        }
    }
```

Log `Filled` order events from fills:

```csharp
    // In the main candle loop, after processing fills:
    foreach (var fill in fills)
    {
        RecordFill(tradeLog, currentGridState, fill);

        collector.LogOrderEvent(new OrderEventEntry
        {
            TimestampUtc = fill.FillTimeUtc,
            EventType = OrderEventType.Filled,
            OrderId = fill.OrderId,
            Side = fill.Side.ToString(),
            OrderType = fill.IsMaker ? "Limit" : "Market",
            Price = fill.FillPrice,
            Size = fill.Size,
            FillPrice = fill.FillPrice,
            Fee = fill.Fee,
            IsMaker = fill.IsMaker,
            GridCycleId = currentGridState.GridCycleId ?? "default"
        });
    }
```

Track grid cycle data for building `GridCycleEntry`:

```csharp
    // Add tracking variables before the main loop:
    var activeCycleDeployTime = 0L;
    var activeCycleAnchorPrice = 0m;
    var activeCycleLevelPrices = new List<decimal>();
    var activeCycleLevelsPlaced = 0;
    var activeCycleTpPrice = 0m;
    var activeCycleSlPrice = 0m;

    // After TryCountClosedGridCycle returns true:
    if (TryCountClosedGridCycle(currentGridState, countedClosedCycles))
    {
        gridCycles++;

        if (auditCollector is not null)
        {
            // Compute cycle PnL from trades matching this cycle ID
            var cycleId = currentGridState.GridCycleId ?? "";
            var cycleTrades = tradeLog.Where(t => t.GridCycleId == cycleId).ToList();
            var cyclePnl = cycleTrades.Sum(t => t.PnL ?? 0m);
            var exitReason = cycleTrades.Any(t => t.TradeType == Trading.Models.TradeType.TakeProfit)
                ? "TakeProfit"
                : "StopLoss";

            auditCollector.LogGridCycleCompleted(new GridCycleEntry
            {
                GridCycleId = cycleId,
                DeployTimestampUtc = activeCycleDeployTime,
                AnchorPrice = activeCycleAnchorPrice,
                LevelsPlaced = activeCycleLevelsPlaced,
                LevelPrices = activeCycleLevelPrices.ToList(),
                LevelsFilled = currentGridState.FilledLevels,
                TakeProfitPrice = activeCycleTpPrice,
                StopLossPrice = activeCycleSlPrice,
                ExitReason = exitReason,
                CyclePnl = cyclePnl,
                CycleDurationMs = candle.Timestamp - activeCycleDeployTime,
                CloseTimestampUtc = candle.Timestamp
            });
        }
    }
```

Note: The active cycle tracking variables (`activeCycleDeployTime`, `activeCycleAnchorPrice`, etc.) need to be populated when a `DeployGrid` signal is detected. This can be done by checking `currentGridState.Lifecycle` transitions or by reading signal parameters. The implementer should determine the most reliable approach — likely checking when `GridLifecycle` transitions from `Inactive`/`Closed` to `Deploying` and reading the `DeployGrid` signal parameters at that point.

Return audit data in the result:

```csharp
    // BacktestResult is a sealed class (NOT a record), so 'with' expressions are not available.
    // Instead, pass audit data as additional parameters to metricsCalculator.Calculate(),
    // or construct the result manually after calculation and copy all metric fields:
    var metrics = metricsCalculator.Calculate(
        tradeLog, equityTimeSeries, config.InitialCapital, gridCycles,
        Math.Max(0, replayData.Candles15m.Count - replayData.WarmupEndIndex));

    // Construct final result with audit data included:
    return new BacktestResult
    {
        TotalTrades = metrics.TotalTrades,
        WinningTrades = metrics.WinningTrades,
        LosingTrades = metrics.LosingTrades,
        WinRate = metrics.WinRate,
        TotalPnL = metrics.TotalPnL,
        MaxDrawdownAbsolute = metrics.MaxDrawdownAbsolute,
        MaxDrawdownPercent = metrics.MaxDrawdownPercent,
        AverageTradePnL = metrics.AverageTradePnL,
        AverageHoldTime = metrics.AverageHoldTime,
        HedgesOpened = metrics.HedgesOpened,
        TotalFeesPaid = metrics.TotalFeesPaid,
        GridCycles = metrics.GridCycles,
        CandlesReplayed = metrics.CandlesReplayed,
        FinalEquity = metrics.FinalEquity,
        EquityTimeSeries = metrics.EquityTimeSeries,
        TradeLog = metrics.TradeLog,
        CandleEvaluationLog = auditCollector?.CandleEvaluations,
        OrderEventLog = auditCollector?.OrderEvents,
        GridCycleLog = auditCollector?.GridCycles
    };
```

Alternatively, add `CandleEvaluationLog`, `OrderEventLog`, `GridCycleLog` parameters to `BacktestMetricsCalculator.Calculate()` and set them in the existing object initializer.

##### Pattern References

- `src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs` — existing main loop, `RecordFill` pattern, `TryCountClosedGridCycle`
- `src/TradingApp.Application/Trading/Models/GridState.cs` — `GridCycleId`, `FilledLevels`, `Lifecycle`

---

### Task 3.4: Update BacktestProcessorService to persist debug data {#task-34-update-backtestprocessorservice-to-persist-debug-data}

Update `BacktestProcessorService.ProcessJobAsync` to serialize and pass debug data to `MarkCompleted`. Update `BuildConfig` to include `EnableAuditLog`.

- **Complexity**: Medium
- **Risk Factors**: Must correctly pass `EnableAuditLog` from entity to config and audit data from result to entity
- **Files**:
  - `src/TradingApp.Api/Services/BacktestProcessorService.cs` — modification
- **Success**:
  - `BuildConfig` includes `EnableAuditLog = run.AuditLogEnabled`
  - `MarkCompleted` call includes serialized debug JSON (or null when disabled)
  - Debug data persisted to database via repository update
- **Dependencies**: Task 2.1, Task 2.4, Task 3.3

#### Implementation Details

```csharp
// src/TradingApp.Api/Services/BacktestProcessorService.cs — modification to ProcessJobAsync

    // After: var result = await runner.RunAsync(config, OnProgress, stoppingToken);

    backtestRun.MarkCompleted(
        // ... existing parameters ...
        tradesJson: BacktestRunResponseMapper.SerializeTrades(result.TradeLog),
        equityTimeSeriesJson: BacktestRunResponseMapper.SerializeEquityTimeSeries(result.EquityTimeSeries),
        candleLogJson: result.CandleEvaluationLog is not null
            ? BacktestRunResponseMapper.SerializeCandleLog(result.CandleEvaluationLog)
            : null,
        orderEventLogJson: result.OrderEventLog is not null
            ? BacktestRunResponseMapper.SerializeOrderEventLog(result.OrderEventLog)
            : null,
        gridCycleLogJson: result.GridCycleLog is not null
            ? BacktestRunResponseMapper.SerializeGridCycleLog(result.GridCycleLog)
            : null);
```

```csharp
// src/TradingApp.Api/Services/BacktestProcessorService.cs — modification to BuildConfig

    private static BacktestConfig BuildConfig(BacktestRun run)
    {
        // ... existing deserialization ...
        return new BacktestConfig
        {
            // ... existing properties ...
            StrategyConfigJson = run.StrategyConfigJson,
            EnableAuditLog = run.AuditLogEnabled,
        };
    }
```

##### Pattern References

- `src/TradingApp.Api/Services/BacktestProcessorService.cs` — existing `MarkCompleted` call with `BacktestRunResponseMapper.SerializeTrades`

---

### Task 3.5: Add GridCycleId and HasAuditLog to response models {#task-35-add-gridcycleid-and-hasauditlog-to-response-models}

Add `GridCycleId` to `BacktestTradeResponse` (prerequisite for UI). Add `HasAuditLog` boolean to `BacktestRunResponse`. Update mapper.

- **Complexity**: Low
- **Risk Factors**: None — additive changes to DTOs
- **Files**:
  - `src/TradingApp.Application/Backtesting/Models/BacktestTradeResponse.cs` — modification
  - `src/TradingApp.Application/Backtesting/Models/BacktestRunResponse.cs` — modification
  - `src/TradingApp.Application/Backtesting/BacktestRunResponseMapper.cs` — modification
- **Success**:
  - `BacktestTradeResponse.GridCycleId` populated from `BacktestTrade.GridCycleId`
  - `BacktestRunResponse.HasAuditLog` true when entity has non-null debug data
  - Frontend receives both new fields
- **Dependencies**: Phase 2

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/Models/BacktestTradeResponse.cs — modification
// Add after existing TradeType property:

    public required string GridCycleId { get; init; }
```

```csharp
// src/TradingApp.Application/Backtesting/Models/BacktestRunResponse.cs — modification
// Add after existing CreatedAt property:

    public required bool HasAuditLog { get; init; }
```

```csharp
// src/TradingApp.Application/Backtesting/BacktestRunResponseMapper.cs — modification to MapTrades

    private static IReadOnlyList<BacktestTradeResponse> MapTrades(IReadOnlyList<BacktestTrade> trades)
    {
        return trades
            .Select(trade => new BacktestTradeResponse
            {
                // ... existing mappings ...
                TradeType = trade.TradeType.ToString(),
                GridCycleId = trade.GridCycleId
            })
            .ToList();
    }
```

```csharp
// src/TradingApp.Application/Backtesting/BacktestRunResponseMapper.cs — modification to ToResponse

    return new BacktestRunResponse
    {
        // ... existing mappings ...
        CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(entity.CreatedAtUtc).UtcDateTime,
        HasAuditLog = entity.CandleLogJson is not null
    };
```

##### Pattern References

- `src/TradingApp.Application/Backtesting/BacktestRunResponseMapper.cs` — existing `MapTrades` and `ToResponse` patterns

---

### Task 3.6: Integration tests for audit log capture {#task-36-integration-tests-for-audit-log-capture}

Extend `RealBacktestRunnerTests` to verify that audit data is captured during a full backtest run. Add `BacktestRunnerTests` tests for the audit-disabled path.

- **Complexity**: Medium
- **Risk Factors**: Integration test requires a run that produces at least one grid cycle
- **Files**:
  - `tests/TradingApp.Application.Tests/Backtesting/Services/RealBacktestRunnerTests.cs` — modification
  - `tests/TradingApp.Application.Tests/Backtesting/Services/BacktestRunnerTests.cs` — modification
- **Success**:
  - Integration test verifies: audit-enabled run produces non-empty candle evaluations, order events, and grid cycle entries
  - Integration test verifies: warmup entries have `IsWarmup = true`
  - Unit test verifies: audit-disabled run returns null audit data
  - All tests pass: `dotnet test tests/TradingApp.Application.Tests --filter "FullyQualifiedName~BacktestRunnerTests"`
- **Dependencies**: Tasks 3.1–3.5

#### Implementation Details

```csharp
// tests/TradingApp.Application.Tests/Backtesting/Services/RealBacktestRunnerTests.cs — modification
// Add new test method:

    [TestMethod]
    public async Task GivenAuditLogEnabled_WhenRunCompletes_ThenAuditDataIsCaptured()
    {
        // Use existence test setup with EnableAuditLog = true on config
        var config = CreateConfig() with { EnableAuditLog = true };

        var result = await _sut.RunAsync(config);

        result.CandleEvaluationLog.Should().NotBeNull();
        result.CandleEvaluationLog.Should().NotBeEmpty();
        result.CandleEvaluationLog!.Any(e => e.IsWarmup).Should().BeTrue("warmup entries should be present");
        result.CandleEvaluationLog!.Any(e => !e.IsWarmup).Should().BeTrue("evaluation entries should be present");
        result.OrderEventLog.Should().NotBeNull();
        result.GridCycleLog.Should().NotBeNull();
    }
```

```csharp
// tests/TradingApp.Application.Tests/Backtesting/Services/BacktestRunnerTests.cs — modification
// Add new test method:

    [TestMethod]
    public async Task GivenAuditLogDisabled_WhenRunCompletes_ThenAuditDataIsNull()
    {
        // Use existing setup with EnableAuditLog = false
        SetupCandles();
        var config = CreateConfig() with { EnableAuditLog = false };

        var result = await _sut.RunAsync(config);

        result.CandleEvaluationLog.Should().BeNull();
        result.OrderEventLog.Should().BeNull();
        result.GridCycleLog.Should().BeNull();
    }
```

Note: The `CreateConfig()` helper may need updating to support `EnableAuditLog`. If `BacktestConfig` or `BacktestResult` is not a record (and doesn't support `with`), use object initialization directly.

##### Pattern References

- `tests/TradingApp.Application.Tests/Backtesting/Services/RealBacktestRunnerTests.cs` — integration test pattern with real GridController and strategy engine
- `tests/TradingApp.Application.Tests/Backtesting/Services/BacktestRunnerTests.cs` — unit test pattern with mocked collaborators

## Phase Success Criteria

- `StrategyScheduler` logs per-candle evaluations via audit collector without breaking existing tests
- `BacktestPositionManager` logs order placement and cancellation events with correct reason codes
- `BacktestRunner` creates and wires the collector, logs warmup entries and fills, builds grid cycle entries
- `BacktestProcessorService` serializes and persists debug data
- `GridCycleId` appears in API trade responses
- `HasAuditLog` flag in backtest result response
- All existing tests pass: `dotnet test tests/TradingApp.Application.Tests` and `dotnet test tests/TradingApp.Api.Tests`
- New integration test verifies end-to-end audit capture
