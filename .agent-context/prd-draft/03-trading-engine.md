# PRD: Trading Engine

**Status:** Draft  
**Priority:** High (highest leverage work post-POC — core pipeline everything else depends on)  
**Date:** 2026-04-01  
**Depends on:** PRD-02 (Strategy Input Pipeline) provides compiled runtime plans consumed by this engine  
**Depended on by:** PRD-04 (Backtesting & Simulation) executes strategies through this engine  

---

## 1. Background & Context

### Problem Statement

The POC validated exchange connectivity (Hyperliquid REST, WebSocket, EIP-712 signing). The next step is to build the core trading pipeline that executes strategies — the engine that sits between strategy definitions and order execution.

### Current State

- Exchange integration proven via POC (see approved PRD: `hyperliquid-poc-prd.md`)
- Domain entities partially exist but are incomplete
- No strategy execution pipeline, risk engine, or grid controller exists yet
- The strategy input pipeline (PRD-02) will produce canonical JSON and compiled runtime plans — this engine consumes them

### Opportunity

Building the trading engine as a standalone, testable pipeline enables:
- Deterministic strategy execution on confirmed candle closes
- Shared pipeline between backtest, paper trading, and live execution
- Strong risk enforcement on every signal before execution
- The GridController state machine to be exercised and proven before capital is at risk

---

## 2. Goals & Objectives

### Business Goals

| ID | Goal | Success Metric |
|----|------|---------------|
| BG-1 | Build a reliable, deterministic trading pipeline | All pipeline components unit-tested; identical inputs produce identical outputs |
| BG-2 | Enforce risk controls on every trade signal | No signal reaches execution without RiskEngine approval |
| BG-3 | Enable strategy parameter iteration with evidence | Pipeline can be exercised via backtest (PRD-04) before live capital is at risk |

### User Goals

| ID | Goal | Description |
|----|------|-------------|
| UG-1 | Strategy executes faithfully | The compiled strategy plan is executed exactly as defined — no silent parameter changes |
| UG-2 | Risk limits are always enforced | Max exposure, daily loss limits, and cooldowns cannot be bypassed by strategy logic |
| UG-3 | Grid lifecycle is predictable | The state machine transitions are well-defined and recoverable after restarts |

### Non-Goals

| ID | Non-Goal | Rationale |
|----|----------|-----------|
| NG-1 | Multiple strategy types | Only `GridStrategy` is implemented. Pipeline supports future plugins but doesn't deliver them. |
| NG-2 | Multi-tenant execution | Single hardcoded identity for v1. Multi-tenant fan-out comes later. |
| NG-3 | Live order placement | This PRD builds the pipeline. Live execution depends on paper trading validation (PRD-04). |
| NG-4 | Strategy authoring or UI | Covered by PRD-02 (Strategy Input Pipeline). |

---

## 3. Scope

### Domain Model

Domain entities with `static Create` factory methods, validation guards, and private setters:

- `Strategy` — user-created strategy instance
- `StrategyConfig` — canonical JSON configuration (new versioned schema from PRD-02)
- `GridState` — grid lifecycle state (cycle ID, fill counts, lifecycle enum)
- `Signal` — persisted trading signal with type, payload, status
- `Order` — placed order record
- `Position` — open/closed position tracking
- `Fill` — individual fill record

### Core Interfaces

| Interface | Responsibility |
|-----------|---------------|
| `ITradingStrategy` | Strategy plugin contract — evaluates market context, returns `StrategyEvaluation` |
| `IStrategyEngine` | Orchestrates strategy evaluation given market context and compiled plan |
| `IGridController` | Grid lifecycle state machine — emits trading signals based on evaluation + state |
| `IRiskEngine` | Validates all signals against risk constraints before execution |
| `IPositionManager` | Ensures position consistency — no duplicate grids, correct sizing, order reconciliation |
| `IExecutionEngine` | Submits orders to exchange (live) or simulated engine (backtest/paper) |

### GridController + GridPlanner

**Lifecycle state machine:**

```
Inactive → Deploying → PartiallyFilled → FullyFilled → Closing → Closed
                ↑                                                    │
                └────────────────────────────────────────────────────┘
                                  (new cycle)
```

- `GridPlanner` calculates grid levels, order sizes, projected average entry, take profit level
- `GridController` manages lifecycle transitions and emits signals:
  - `DeployGrid`, `CancelGrid`, `TakeProfit`, `OpenHedge`, `AdjustHedge`, `CloseHedge`, `FlattenPosition`, `Cooldown`
- Partial fills: remaining buy levels stay open, average entry updates dynamically, TP recalculated on candle close
- Full fill: single limit take-profit order placed for whole position

### Signal Contracts

| Signal | Category | Key Payload |
|--------|----------|-------------|
| `DeployGrid` | Grid | symbol, gridPlan, reason |
| `CancelGrid` | Grid | symbol, reason |
| `TakeProfit` | Position | symbol, targetPrice, reason |
| `FlattenPosition` | Position | symbol, reason |
| `OpenHedge` | Hedge | symbol, percent, reason |
| `AdjustHedge` | Hedge | symbol, newPercent, reason |
| `CloseHedge` | Hedge | symbol, reason |
| `PauseStrategy` | Risk | symbol, reason |
| `Cooldown` | Risk | symbol, durationMinutes |

Signal lifecycle: `Generated → Validated → Approved → Executed`  
Signals persisted in database for audit and analysis.

### Market Context

- `MarketContextBuilder` builds `MarketContext` from candle data
- Indicators: EMA(20/50/200), RSI(14), VWAP
- `IndicatorSnapshot` captured per candle
- Deterministic: same candle input always produces same context
- Higher-timeframe candles (1H, 4H) resolved without look-ahead bias

### Scheduling

- `CandleClock` — detects confirmed candle closes, emits `CandleClosedEvent` exactly once
- `StrategyScheduler` — subscribes to `CandleClosedEvent`, builds shared `MarketContext`, fans out evaluation to subscribers
- Trigger timeframe: 15m for GridStrategy
- Identical components used in backtest and live — no separate scheduling for each mode

### Persistence

- SQLite + EF Core (POC phase)
- Strategy configs, grid state, signals, orders, positions, fills
- All data tenant-scoped by `UserId`
- Composite indexes for efficient queries

---

## 4. Technical Considerations

### Architecture Position

```
Compiled Runtime Plan (from PRD-02)
        ↓
   IStrategyEngine
        ↓
   GridStrategy (ITradingStrategy)
        ↓
   IGridController
        ↓
   Trading Signals
        ↓
   IRiskEngine
        ↓
   IPositionManager
        ↓
   IExecutionEngine
        ↓
   Exchange (live) / SimulatedEngine (backtest/paper)
```

### Project Location

All pipeline services live in `TradingApp.Application`:

```
src/TradingApp.Application/
├── Trading/
│   ├── Models/         (MarketContext, StrategyEvaluation, GridState, GridLifecycle,
│   │                    PositionState, TradingSignal, OrderRequest, TradeType)
│   ├── Services/       (GridStrategy, GridController, GridPlanner, RiskEngine,
│   │                    PositionManager, StrategyEngine, MarketContextBuilder)
│   └── Indicators/     (EMA, RSI, VWAP calculators)
├── Scheduling/
│   ├── CandleClock.cs
│   ├── StrategyScheduler.cs
│   └── Models/CandleClosedEvent.cs
└── Abstractions/Services/
    (ITradingStrategy, IStrategyEngine, IGridController, IRiskEngine,
     IPositionManager, IExecutionEngine, IMarketContextBuilder)
```

### Constraints

| Constraint | Detail |
|-----------|--------|
| **One symbol** | BTC perpetual only |
| **One strategy** | `GridStrategy` only |
| **One user** | Single hardcoded identity (multi-tenant later) |
| **Candle-based execution** | Confirmed closes only — no tick-level triggers |
| **No live orders** | Engine built and tested via backtest/paper (PRD-04) before live |

### Key Architecture Decisions

- Strategies execute only on confirmed candle closes (deterministic)
- All orders pass through RiskEngine — strategies never bypass risk checks
- Backtesting reuses the same pipeline components (not a separate system)
- Signal contracts define the boundary between strategy logic and execution
- `IExecutionEngine` is the only component that differs between backtest, paper, and live modes

---

## 5. Acceptance Criteria

- [ ] Domain model entities compile and persist to SQLite
- [ ] `GridController` state machine transitions are tested for all lifecycle states
- [ ] `GridPlanner` calculates correct grid levels, sizes, and TP for given inputs
- [ ] `MarketContextBuilder` produces deterministic `MarketContext` from candle inputs
- [ ] Indicator calculations (EMA, RSI, VWAP) produce correct values against known datasets
- [ ] `RiskEngine` blocks signals that violate risk constraints (max exposure, daily loss, cooldown)
- [ ] `RiskEngine` approves valid signals
- [ ] Signal lifecycle (Generated → Validated → Approved → Executed) is tracked and persisted
- [ ] `CandleClock` emits exactly one `CandleClosedEvent` per confirmed close (no duplicates)
- [ ] `StrategyScheduler` invokes the full pipeline on each trigger-timeframe candle close
- [ ] All pipeline components are injectable and testable in isolation

---

## 6. References

| Document | Path |
|----------|------|
| Trading Strategy | [01-trading-strategy.md](../../0-knowledge/01-trading-strategy.md) |
| Domain Model | [04-domain-model.md](../../0-knowledge/04-domain-model.md) |
| Strategy Runtime Model | [14-strategy-runtime-model.md](../../0-knowledge/14-strategy-runtime-model.md) |
| Grid Controller | [15-grid-controller.md](../../0-knowledge/15-grid-controller.md) |
| Signal Contracts | [16-signal-contracts.md](../../0-knowledge/16-signal-contracts.md) |
| Scheduling Architecture | [19-scheduling-architecture.md](../../0-knowledge/19-scheduling-architecture.md) |
| Architecture Decisions | [10-architecture-decisions.md](../../0-knowledge/10-architecture-decisions.md) |
| Strategy Input Pipeline (produces compiled plans) | [02-strategy-input-pipeline.md](02-strategy-input-pipeline.md) |
