# Grid Controller

The GridController orchestrates the lifecycle of a grid strategy.

Instead of placing grid logic inside the trading strategy, the GridController manages:

- grid planning
- lifecycle transitions
- signal generation
- hedge decisions
- take‑profit logic

The trading strategy only decides whether a valid grid setup exists.

The GridController handles everything that happens afterwards.

---

# Responsibilities

GridController is responsible for:

- creating grid plans using GridPlanner
- managing GridLifecycle transitions
- emitting trading signals
- resetting grid after completion

Execution fills update `GridState.FilledLevels` and move the runtime into
`PartiallyFilled` or `FullyFilled`. The controller then decides whether the cycle
stays active, transitions to `Closing`, or waits for the next candle.

It acts as the central brain of the grid system.

---

# Interface

`IGridController` (`src/TradingApp.Application/Abstractions/Services/IGridController.cs`):

```csharp
Task<IReadOnlyList<TradingSignal>> ProcessAsync(
    StrategyEvaluation evaluation,
    MarketContext context,
    GridState gridState,
    PositionState positionState,
    IStrategyConfig strategyConfig,
    CancellationToken cancellationToken = default);
```

Key model files:

| Model | File |
|-------|------|
| `GridLifecycle` (enum) | `src/TradingApp.Application/Trading/Models/GridLifecycle.cs` |
| `GridState` | `src/TradingApp.Application/Trading/Models/GridState.cs` |

`GridState` tracks `InitialRDollars` (nullable decimal) — the one-R dollar risk captured at grid deployment time when using `RiskBased` sizing. Cleared when grids enter `Closing` or `Closed` states to prevent stale values leaking into subsequent cycles.

`GridState` also tracks `AtrAtEntry` (nullable decimal) — the ATR value captured at grid deployment time when using `AtrInitial` stop-loss type. Used to compute a fixed stop-loss distance anchored to entry price. Cleared when grids enter `Closing` or `Closed` states, following the same lifecycle as `InitialRDollars`.

Note: Signals are currently emitted as `TradingSignal` with a `string SignalType` (e.g. `"DeployGrid"`).
Typed signal classes are planned — see [Signal Contracts](16-signal-contracts.md).

---

# Architecture Position

Pipeline:

MarketData
→ Indicators
→ Strategy
→ GridController
→ Signals
→ RiskEngine
→ PositionManager
→ ExecutionEngine
→ Exchange

---

# Lifecycle Ownership

The GridController manages the grid lifecycle state machine:

Inactive
Planning
Deploying
Active
PartiallyFilled
FullyFilled
Closing
Closed

The controller determines which transitions are allowed.

Operational flow with the current backtest/live runtime:

- `Inactive` or `Closed` -> `Deploying` when a fresh setup is detected
- `Deploying` -> `PartiallyFilled` / `FullyFilled` when execution reports fills
- `PartiallyFilled` -> stays active while remaining ladder levels are still working
- `PartiallyFilled` -> `Closing` only when candle-close take profit or stop loss triggers
- `FullyFilled` -> `Closing` when the controller places the full-position take-profit order
- `Closing` -> `Closed` when the exit order fills

---

# Inputs

`IGridController.ProcessAsync` receives:

| Parameter | Type | Description |
|-----------|------|-------------|
| `evaluation` | `StrategyEvaluation` | Contains `SetupDetected` bool and optional `Reason` |
| `context` | `MarketContext` | Trigger candle + HTF candles + `IndicatorSnapshot` |
| `gridState` | `GridState` | Current lifecycle, cycle ID, fill counts |
| `positionState` | `PositionState` | Symbol, size, entry price, unrealised PnL |
| `strategyConfig` | `IStrategyConfig` | Typed strategy config forwarded from `StrategyScheduler` |

---

# Outputs

The controller emits high‑level trading signals such as:

DeployGrid
CancelGrid
TakeProfit
OpenHedge
AdjustHedge
CloseHedge
FlattenPosition
Cooldown

These signals are then validated by the RiskEngine.

---

# Position Sizing for RiskBased Mode

When `StrategyConfig.Risk.PositionSizeType == RiskBased`, the controller resolves stop-loss distance and computes R-based notional:

1. **Stop-Loss Distance Resolution** via `StopLossDistanceResolver.Resolve()`:
   - `FixedPercent` → uses `StopLoss.Value` directly
   - `AtrTrailing` → computes `(ATR × multiplier) / anchorPrice × 100`; ATR is recalculated every candle (trailing stop)
   - `AtrInitial` → captures ATR at entry time (`GridState.AtrAtEntry`) and computes `(lockedATR × multiplier) / entryPrice × 100` for the entire position lifecycle; does not trail. Falls back to `StopLoss.Value` (fixed percent) when ATR is unavailable at entry
   - Fallback → `GridConfig.BreakdownThreshold` (grid-only)

   **Key difference:** `AtrInitial` locks the stop distance at entry time (fixed stop price). `AtrTrailing` adapts dynamically every candle close. See [31-atr-calculation.md](31-atr-calculation.md) for behavioral details and TriggerOrderManager implications.

2. **Total Notional**: `R = equity × riskPerTradePercent / 100`; `notional = R / (SL% / 100)`

3. **Per-Level Notional**: `notionalUsd = notional / gridLevels`
   (For PercentWallet/FixedNotional, the resolver output is used directly as `notionalUsd`.)
   This value is emitted in the `DeployGrid` signal under the key `"notionalUsd"`.

4. **Safety**: If `notionalUsd ≤ 0` (unresolvable SL distance), no signal is emitted.

Key files:
- `src/TradingApp.Application/Trading/Services/StopLossDistanceResolver.cs`
- `src/TradingApp.Application/Trading/Services/PositionSizeResolver.cs`

### Leverage Calculation in DeployGrid Signal

When `AutoLeverage = true` and mode = `RiskBased`, the controller computes leverage before emitting `DeployGrid`:

- Auto-leverage: `LeverageCalculator.CalculateLeverage(stopLossPercent, maxLeverage)`
- Manual fallback: `Math.Max(1, (int)Math.Floor(config.Risk.Leverage))`
- `isIsolated = true` for all RiskBased mode trades

The `DeployGrid` signal includes `["leverage"]` and `["isIsolated"]` parameters. `LivePositionManager` extracts these and calls `IExecutionEngine.SetLeverageAsync()` before placing grid orders.

File: `src/TradingApp.Application/Trading/Services/GridController.cs` → `DeployNewGridAsync`

---

# Interaction with GridPlanner

GridPlanner calculates:

- grid levels
- order sizes
- projected average entry
- take profit level

The controller requests a plan when a valid setup is detected.

---

# Interaction with PositionManager

PositionManager ensures:

- no duplicate grids
- hedge consistency
- correct position sizing
- order reconciliation

For partial fills, the controller does not immediately replace the ladder with a
persistent sell order. Remaining buy levels stay open, average entry can continue to
improve, and the controller checks candle close against the dynamic take-profit level
computed from the latest average entry. Once the cycle is fully filled, the controller
reverts to the standard closing flow and the PositionManager places a single limit
take-profit order for the whole position.

The controller does not directly manage orders.

---

# Benefits

Centralising grid logic in the GridController provides:

- simpler strategy code
- safer lifecycle transitions
- easier debugging
- improved restart recovery