# Signal Contracts

This document defines the signal types emitted by the trading system.

Signals represent strategy intent. They do not execute trades directly.

Signals pass through:

StrategyEngine → RiskEngine → PositionManager → ExecutionEngine

---

# Signal Categories

Grid signals
Position signals
Hedge signals
Risk signals

---

# Grid Signals

DeployGrid

Payload:

symbol
gridPlan
reason

---

CancelGrid

Payload:

symbol
reason

---

# Position Signals

TakeProfit

Payload:

symbol
targetPrice
reason

---

FlattenPosition

Payload:

symbol
reason

---

# Hedge Signals

OpenHedge

Payload:

symbol
percent
reason

---

AdjustHedge

Payload:

symbol
newPercent
reason

---

CloseHedge

Payload:

symbol
reason

---

# Risk Signals

PauseStrategy

Payload:

symbol
reason

---

Cooldown

Payload:

symbol
durationMinutes

---

# Signal Lifecycle

Signals move through several states:

Generated
Validated
Approved
Executed

Signals should be persisted in the database for audit and analysis.

---

# Signal Storage Example

Signals table fields:

Id
StrategyId
SignalType
Symbol
PayloadJson
Status
CreatedAt