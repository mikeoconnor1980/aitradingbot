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

GridController receives:

StrategyConfig
MarketSnapshot
IndicatorSnapshot
GridState
PositionState
SetupDetected flag

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