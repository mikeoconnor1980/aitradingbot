# Proof Of Concept Epics

**Purpose:** Epics for the smallest build that proves the strategy and runtime architecture are worth continuing.  
**Scope:** Epics only. No PBIs yet.  
**Status:** Proposed  
**Date:** 2026-03-22

---

## Goal

The Proof Of Concept should answer three questions:

1. Can the strategy be replayed deterministically?
2. Can the runtime operate correctly on live market data without risking capital?
3. Is the strategy/runtime combination promising enough to justify a live-capable V1?

The POC should stop short of broad product scope and stop short of full live trading.

---

## Epic 1 — Solution Foundation and Shared Contracts

Establish the .NET solution structure, domain boundaries, and shared contracts used by replay and paper-trading modes.

Includes:

- solution and project structure
- domain entities and shared interfaces
- Signal contract model
- engine boundaries for strategy, risk, position, execution, and scheduling

Outcome:

- the core pipeline can be built once and reused consistently across test modes

---

## Epic 2 — Historical Data and Market Context

Implement the historical market-data layer needed for deterministic strategy replay.

Includes:

- 4H, 1H, and 15m candle ingestion
- local historical storage
- market-context and indicator preparation
- repeatable loading for backtest ranges

Outcome:

- the strategy has a consistent data foundation for historical testing

---

## Epic 3 — Deterministic Backtester

Implement replay-based backtesting using the shared runtime and simulated execution.

Includes:

- replay clock
- backtest runner
- simulated execution engine
- fees and slippage assumptions
- run metrics and result storage

Outcome:

- the strategy can be evaluated honestly before any live order path is introduced

---

## Epic 4 — Grid Strategy and Runtime State Model

Implement the initial GridStrategy, GridController, GridPlanner, and runtime state transitions needed for deterministic behaviour.

Includes:

- GridStrategy plugin
- GridController lifecycle
- grid planning and state transitions
- signal generation through approved contracts
- configuration loading and validation

Outcome:

- the initial strategy can run coherently in both replay and live-compatible runtime modes

---

## Epic 5 — Live Market Data, CandleClock, and Paper Trading

Run the live scheduling and strategy runtime against real market data with simulated execution.

Includes:

- Hyperliquid market-data integration
- CandleClock
- StrategyScheduler
- MarketStateStore
- paper-trading mode
- execution checkpoints

Outcome:

- the system proves deterministic runtime behaviour on live market conditions without risking capital

---

## Epic 6 — POC Evidence and Internal Observability

Capture enough runtime evidence to evaluate the POC without building the full product surface.

Includes:

- backtest run output and comparison data
- paper-trading run logs
- strategy state snapshots or equivalent runtime evidence
- lightweight internal visibility into signals, fills, and run outcomes

Outcome:

- the team can inspect what happened, diagnose failures, and decide whether to promote the project into V1

---

## POC Exit Criteria

The POC is complete when:

- the strategy replays deterministically over historical data
- the runtime executes once per closed candle in paper mode
- duplicate execution is prevented in normal restart scenarios
- enough evidence exists to judge strategy viability and runtime correctness
- the team can decide whether to proceed to V1 live-readiness work
