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
- updating GridState when fills occur
- emitting trading signals
- resetting grid after completion

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
    string strategyConfigJson,
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

---

# Inputs

`IGridController.ProcessAsync` receives:

| Parameter | Type | Description |
|-----------|------|-------------|
| `evaluation` | `StrategyEvaluation` | Contains `SetupDetected` bool and optional `Reason` |
| `context` | `MarketContext` | Trigger candle + HTF candles + `IndicatorSnapshot` |
| `gridState` | `GridState` | Current lifecycle, cycle ID, fill counts |
| `positionState` | `PositionState` | Symbol, size, entry price, unrealised PnL |
| `strategyConfigJson` | `string` | Configuration forwarded from `StrategyScheduler` |

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

The controller does not directly manage orders.

---

# Benefits

Centralising grid logic in the GridController provides:

- simpler strategy code
- safer lifecycle transitions
- easier debugging
- improved restart recovery