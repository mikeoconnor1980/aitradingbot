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