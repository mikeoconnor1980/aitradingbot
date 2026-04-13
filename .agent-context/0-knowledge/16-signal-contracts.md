# Signal Contracts

This document defines the signal types emitted by the trading system.

Signals represent strategy intent. They do not execute trades directly.

Signals pass through:

StrategyEngine → RiskEngine → PositionManager → ExecutionEngine

---

# TradingSignal Class

The current implementation uses a single flexible model rather than typed signal classes:

```csharp
// src/TradingApp.Application/Trading/Models/TradingSignal.cs
public sealed class TradingSignal
{
    public required string SignalType { get; init; }    // e.g. "DeployGrid", "TakeProfit"
    public required string Symbol { get; init; }
    public string? Reason { get; init; }
    public IReadOnlyDictionary<string, object>? Parameters { get; init; }
}
```

`SignalType` string values correspond to the signal types listed below (e.g. `"DeployGrid"`, `"OpenHedge"`). Typed C# signal classes with strongly-typed payloads are a planned future step.

---

# Signal Categories

Grid signals
Position signals
Hedge signals
Risk signals

---

# Grid Signals

DeployGrid

Entry signal to activate a grid strategy. Emitted once per grid lifecycle when setup is detected.

Payload:

| Parameter | Type | Description |
|-----------|------|-------------|
| `symbol` | string | Trading pair (e.g. `"BTC-PERP"`) |
| `gridPlan` | object | Grid configuration (levels, spacing, notional, entryMode) |
| `estimatedRiskUsd` | decimal | Estimated R (risk in USD) across all grid levels; used for portfolio heat validation and tracking |
| `reason` | string | Human-readable setup reason |
| `autoLeverage` (optional) | int | Derived leverage for `RiskBased` mode with `autoLeverage = true` |

Rejection: blocked if `currentPortfolioHeat + estimatedRiskUsd > equity × maxPortfolioHeatPercent / 100`.

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