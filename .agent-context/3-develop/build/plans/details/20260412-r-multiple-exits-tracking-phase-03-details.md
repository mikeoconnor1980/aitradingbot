<!-- markdownlint-disable-file -->

# Task Details: R-Multiple Exit Types & Trade Tracking

## Phase 3: Per-Trade R Tracking & MFE/MAE

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — sealed classes, PascalCase
- `.github/instructions/testing.instructions.md` — MSTest, FluentAssertions, Given_When_Then
- `.agent-context/0-knowledge/33-risk-management-and-trade-sizing.md` — R = equity × riskPerTradePercent / 100, MFE/MAE in R multiples

## Design References

**InitialR Capture Flow:**
1. `PositionSizeResolver.CalculateRiskBased` computes `R = equity × riskPercent / 100` (currently discarded)
2. Extend this to also return R alongside the notional
3. Store InitialR on `GridState` during grid deployment
4. Thread InitialR into `BacktestTrade` at entry time

**MFE/MAE Tracking Flow:**
1. Add `Dictionary<string, TradeExcursionTracker>` in `BacktestRunner.RunCoreAsync` scope
2. Each candle, for each open trade: compute unrealised P&L at candle High and Low
3. Update best/worst excursion per trade
4. At trade close: convert to R multiples → MFE = bestPnl / InitialR, MAE = worstPnl / InitialR

### Task 3.1: Add InitialR to GridState {#task-31-add-initialr-to-gridstate}

Add `InitialRDollars` property to `GridState` to track the dollar risk per grid cycle.

- **Complexity**: Low
- **Risk Factors**: None — additive property
- **Files**:
  - `src/TradingApp.Application/Trading/Models/GridState.cs` — add property
- **Success**:
  - `GridState.InitialRDollars` is available for assignment during grid deployment
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Application/Trading/Models/GridState.cs — modification
// Add after CandlesSinceEntry property:

/// <summary>
/// Dollar risk (1R) for the current grid cycle.
/// Set during RiskBased position sizing at grid deployment.
/// Null for non-RiskBased sizing modes.
/// </summary>
public decimal? InitialRDollars { get; set; }
```

##### Pattern References

- `src/TradingApp.Application/Trading/Models/GridState.cs` — existing property pattern (`TrailingStopHighWatermark`)

### Task 3.2: Capture InitialR during grid deployment in GridController {#task-32-capture-initialr-during-grid-deployment}

Compute and store `InitialRDollars` on `GridState` during grid deployment when using `RiskBased` sizing. Also extend `PositionSizeResolver` to expose the R value.

- **Complexity**: Medium
- **Risk Factors**: Must not change `ResolveNotional` return value for non-RiskBased callers
- **Files**:
  - `src/TradingApp.Application/Trading/Services/PositionSizeResolver.cs` — add `ResolveInitialR` method
  - `src/TradingApp.Application/Trading/Services/GridController.cs` — set InitialRDollars on GridState
- **Success**:
  - `PositionSizeResolver.ResolveInitialR` returns R for RiskBased, null otherwise
  - GridState.InitialRDollars is set at grid deployment time
  - GridState.InitialRDollars is reset to null when position closes
- **Dependencies**: Task 3.1

#### Implementation Details

```csharp
// src/TradingApp.Application/Trading/Services/PositionSizeResolver.cs — modification
// Add new public method:

public static decimal? ResolveInitialR(RiskConfig risk, decimal accountEquity)
{
    if (risk.PositionSizeType != PositionSizeType.RiskBased)
    {
        return null;
    }

    if (!risk.RiskPerTradePercent.HasValue || risk.RiskPerTradePercent.Value <= 0m)
    {
        return null;
    }

    return Math.Max(0m, accountEquity) * (risk.RiskPerTradePercent.Value / 100m);
}
```

```csharp
// src/TradingApp.Application/Trading/Services/GridController.cs — modification
// In ProcessAsync, after computing positionSize and before emitting DeployGrid signal:

// ... existing code ...
gridState.GridCycleId = Guid.NewGuid().ToString("N");
gridState.Lifecycle = GridLifecycle.Deploying;
gridState.TotalLevels = gridLevels;
gridState.FilledLevels = 0;
gridState.InitialRDollars = PositionSizeResolver.ResolveInitialR(config.Risk, context.AccountEquity);
// ... existing DeployGrid signal emission ...
```

```csharp
// Also in EvaluateExitConditions, when grid closes (lifecycle → Closing), reset InitialR:
// Already handled — GridState is reused per cycle, and InitialRDollars is overwritten at each deployment.
// But ensure it's nulled when position closes:
// In the TP and SL exit paths that set gridState.Lifecycle = GridLifecycle.Closing:
gridState.InitialRDollars = null;
```

##### Pattern References

- `src/TradingApp.Application/Trading/Services/PositionSizeResolver.cs` — existing `CalculateRiskBased` R formula
- `src/TradingApp.Application/Trading/Services/GridController.cs` — grid deployment section (lines 162-180)

### Task 3.3: Add R tracking fields to BacktestTrade {#task-33-add-r-tracking-fields-to-backtesttrade}

Add nullable R-tracking properties to `BacktestTrade`. These are serialized in the `TradesJson` blob — no DB migration needed.

- **Complexity**: Low
- **Risk Factors**: None — nullable properties, backward-compatible JSON deserialization
- **Files**:
  - `src/TradingApp.Application/Backtesting/Models/BacktestTrade.cs` — add new properties
- **Success**:
  - `InitialRDollars`, `RMultipleResult`, `MFE`, `MAE` are available on `BacktestTrade`
  - Existing backtests with no R data deserialize cleanly (fields are null)
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/Models/BacktestTrade.cs — modification
// Add after ExitReason property:

/// <summary>Dollar risk (1R) at trade entry. Null for non-RiskBased sizing.</summary>
public decimal? InitialRDollars { get; init; }

/// <summary>Realised return expressed as a multiple of R (PnL / InitialR). Null if InitialR is not tracked.</summary>
public decimal? RMultipleResult { get; init; }

/// <summary>Maximum favourable excursion in R multiples (best unrealised profit / InitialR). Always >= 0.</summary>
public decimal? MFE { get; init; }

/// <summary>Maximum adverse excursion in R multiples (worst unrealised loss / InitialR). Always <= 0.</summary>
public decimal? MAE { get; init; }
```

##### Pattern References

- `src/TradingApp.Application/Backtesting/Models/BacktestTrade.cs` — existing property pattern

### Task 3.4: Thread InitialR through RecordFill {#task-34-thread-initialr-through-recordfill}

Update `BacktestRunner.RecordFill` to read `InitialRDollars` from `GridState` and set it on new `BacktestTrade` entries.

- **Complexity**: Medium
- **Risk Factors**: Must handle signal mode (no grid state)
- **Files**:
  - `src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs` — update `RecordFill`, `AppendOpenTrade`, `CloseCompatibleTrades`
- **Success**:
  - New entry trades have `InitialRDollars` set from GridState (when RiskBased)
  - Closed trades carry forward `InitialRDollars` from their open counterpart
- **Dependencies**: Tasks 3.1, 3.2, 3.3

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs — modification
// In RecordFill, when creating entry trades:

if (fill.TradeType is TradeType.GridFill or TradeType.HedgeOpen or TradeType.SignalEntry)
{
    tradeLog.Add(new BacktestTrade
    {
        TradeId = fill.OrderId,
        GridCycleId = gridCycleId,
        EntryTimeUtc = fill.FillTimeUtc,
        EntryPrice = fill.FillPrice,
        // ... existing fields ...
        InitialRDollars = gridState.InitialRDollars,
    });
    return;
}

// In CloseCompatibleTrades, when creating paired (closed) trades:
var pairedTrade = new BacktestTrade
{
    // ... existing fields ...
    InitialRDollars = openTrade.InitialRDollars,
    // RMultipleResult, MFE, MAE set in Task 3.6
};

// In remaining open split:
var remainingOpenTrade = new BacktestTrade
{
    // ... existing fields ...
    InitialRDollars = openTrade.InitialRDollars,
};

// AppendOpenTrade:
private static void AppendOpenTrade(
    List<BacktestTrade> tradeLog,
    SimulatedFill fill,
    string gridCycleId,
    decimal size,
    decimal fee,
    decimal? initialRDollars = null)
{
    tradeLog.Add(new BacktestTrade
    {
        // ... existing fields ...
        InitialRDollars = initialRDollars,
    });
}
```

##### Pattern References

- `src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs` — existing `RecordFill`, `CloseCompatibleTrades`, `AppendOpenTrade` methods

### Task 3.5: Add per-trade MFE/MAE tracking in BacktestRunner {#task-35-add-per-trade-mfemae-tracking}

Add a `TradeExcursionTracker` class and tracking dictionary to the backtest run loop. Update MFE/MAE for all open trades on each candle.

- **Complexity**: High
- **Risk Factors**: Performance impact with many open trades (negligible for grid with ~5-10 levels)
- **Files**:
  - `src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs` — add tracker, update per candle
- **Success**:
  - Open trades accumulate MFE (from candle High) and MAE (from candle Low)
  - Tracker entries are created when trades open and removed when closed
- **Dependencies**: Task 3.4

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs — modification
// Add nested class:

private sealed class TradeExcursionTracker
{
    public decimal BestPnL { get; set; }
    public decimal WorstPnL { get; set; }
}

// In RunCoreAsync, after tradeLog declaration:
var excursionTrackers = new Dictionary<string, TradeExcursionTracker>(StringComparer.Ordinal);

// After ProcessCandle fills are handled and before scheduler processes:
// Update MFE/MAE for all open trades
UpdateTradeExcursions(tradeLog, excursionTrackers, candle);

// Add method:
private static void UpdateTradeExcursions(
    List<BacktestTrade> tradeLog,
    Dictionary<string, TradeExcursionTracker> trackers,
    Candle candle)
{
    foreach (var trade in tradeLog)
    {
        if (trade.ExitTimeUtc is not null || !trade.InitialRDollars.HasValue)
        {
            continue;
        }

        if (!trackers.TryGetValue(trade.TradeId, out var tracker))
        {
            tracker = new TradeExcursionTracker();
            trackers[trade.TradeId] = tracker;
        }

        // For long trades: best at High, worst at Low
        // For short trades: best at Low, worst at High
        decimal bestPriceForTrade;
        decimal worstPriceForTrade;

        if (trade.Side == OrderSide.Buy)
        {
            bestPriceForTrade = (candle.High - trade.EntryPrice) * trade.Size;
            worstPriceForTrade = (candle.Low - trade.EntryPrice) * trade.Size;
        }
        else
        {
            bestPriceForTrade = (trade.EntryPrice - candle.Low) * trade.Size;
            worstPriceForTrade = (trade.EntryPrice - candle.High) * trade.Size;
        }

        tracker.BestPnL = Math.Max(tracker.BestPnL, bestPriceForTrade);
        tracker.WorstPnL = Math.Min(tracker.WorstPnL, worstPriceForTrade);
    }
}
```

##### Pattern References

- `src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs` — existing per-candle processing loop in `RunCoreAsync`

### Task 3.6: Compute RMultipleResult and MFE/MAE at trade close {#task-36-compute-rmultipleresult-and-mfemae-at-trade-close}

When trades close in `CloseCompatibleTrades`, compute `RMultipleResult`, `MFE`, and `MAE` from the excursion tracker and InitialR.

- **Complexity**: Medium
- **Risk Factors**: Must handle edge cases (InitialR = 0, tracker not found)
- **Files**:
  - `src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs` — update `CloseCompatibleTrades`
- **Success**:
  - Closed trades with InitialRDollars have RMultipleResult = PnL / InitialR
  - MFE and MAE are converted to R multiples
  - Non-RiskBased trades have null R fields
  - Tracker entries are removed after close
- **Dependencies**: Tasks 3.4, 3.5

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs — modification
// Update CloseCompatibleTrades signature to accept excursionTrackers:

private static void CloseCompatibleTrades(
    List<BacktestTrade> tradeLog,
    IReadOnlyList<BacktestTrade> compatibleOpenTrades,
    SimulatedFill fill,
    string gridCycleId,
    Dictionary<string, TradeExcursionTracker> excursionTrackers)
{
    // ... existing loop logic ...
    
    // When creating pairedTrade:
    var initialR = openTrade.InitialRDollars;
    var pnl = CalculateTradePnl(openTrade.Side, openTrade.EntryPrice, fill.FillPrice, closedSize);
    
    decimal? rMultipleResult = null;
    decimal? mfe = null;
    decimal? mae = null;
    
    if (initialR.HasValue && initialR.Value > 0m)
    {
        rMultipleResult = Math.Round(pnl / initialR.Value, 4);
        
        if (excursionTrackers.TryGetValue(openTrade.TradeId, out var tracker))
        {
            mfe = Math.Round(tracker.BestPnL / initialR.Value, 4);
            mae = Math.Round(tracker.WorstPnL / initialR.Value, 4);
            excursionTrackers.Remove(openTrade.TradeId);
        }
    }

    var pairedTrade = new BacktestTrade
    {
        TradeId = openTrade.TradeId,
        GridCycleId = openTrade.GridCycleId,
        EntryTimeUtc = openTrade.EntryTimeUtc,
        EntryPrice = openTrade.EntryPrice,
        ExitTimeUtc = fill.FillTimeUtc,
        ExitPrice = fill.FillPrice,
        Side = openTrade.Side,
        Size = closedSize,
        PnL = pnl,
        Fees = openTrade.Fees + allocatedExitFee,
        TradeType = openTrade.TradeType,
        ExitReason = fill.CloseReason?.ToString(),
        InitialRDollars = openTrade.InitialRDollars,
        RMultipleResult = rMultipleResult,
        MFE = mfe,
        MAE = mae,
    };
    // ... rest of existing logic ...
}
```

Update `RecordFill` to pass `excursionTrackers` to `CloseCompatibleTrades`.

##### Pattern References

- `src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs` — existing `CloseCompatibleTrades` method

### Task 3.7: Unit tests for R tracking and MFE/MAE {#task-37-unit-tests-for-r-tracking-and-mfemae}

Write unit tests for:
- InitialR threading through RecordFill
- RMultipleResult computation at close
- MFE/MAE tracking across candles
- Non-RiskBased trades have null R fields

- **Complexity**: Medium
- **Risk Factors**: Complex test setup with multi-candle scenarios
- **Files**:
  - `tests/TradingApp.Application.Tests/Backtesting/Services/BacktestRunnerTests.cs` — add R-tracking tests
  - `tests/TradingApp.Application.Tests/Backtesting/Services/RealBacktestRunnerTests.cs` — add integration test with RiskBased + RMultiple TP
- **Success**:
  - PnL = $250, InitialR = $100 → RMultipleResult = 2.5
  - PnL = -$100, InitialR = $100 → RMultipleResult = -1.0
  - MFE/MAE reflect per-candle High/Low extremes in R multiples
  - All tests pass
- **Dependencies**: Tasks 3.4, 3.5, 3.6

### Task 3.8: Build and verify {#task-38-build-and-verify}

Build solution and run all tests.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None
- **Success**:
  - `dotnet build TradingApp.sln` succeeds
  - `dotnet test TradingApp.sln` — all tests pass
- **Dependencies**: Task 3.7

## Phase Success Criteria

- InitialR is captured at grid deployment for RiskBased mode
- BacktestTrade carries InitialRDollars, RMultipleResult, MFE, MAE
- MFE/MAE are updated per candle during the trade's lifetime
- RMultipleResult = PnL / InitialR at trade close
- Non-RiskBased trades have null R fields throughout
- All existing tests continue to pass
