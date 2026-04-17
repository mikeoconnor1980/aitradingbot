---
applyTo: ".agent-context/3-develop/build/changes/20260327-backtest-replay-engine-changes.md"
currentAgent: "None"
agentStartedAt: "2026-03-28T14:38:03Z"
status: "complete"
lastUpdated: "2026-03-28T15:33:20Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: F3 — Backtest Replay Engine

## Overview

Build the backtest replay engine that reads historical candle data from the local database and replays it sequentially through the live trading pipeline using a simulated execution engine, producing performance metrics (PnL, drawdown, win rate) and a full trade log.

## PBI Details

**PBI ID:** Draft
**PBI File:** `.agent-context/3-develop/backlog/draft/backtesting/F3-backtest-replay-engine.md`
**Status:** Draft
**Depends On:** F1 (Candle Data Persistence — assumed complete), core trading pipeline interfaces (defined in this plan)

### User Story

> As an **Operator**, I want to **run a backtest over a specified date range with a given strategy configuration** so that **I can measure how the grid strategy would have performed historically before risking capital**.

### Acceptance Criteria

- [ ] Given candle data exists for BTC 15m, 1h, and 4h in the database, When a backtest is triggered for a date range, Then candles are replayed in ascending time order through the full pipeline
- [ ] Given grid limit buy orders are placed by the strategy, When the candle low ≤ order price, Then the order fills in the SimulatedExecutionEngine
- [ ] Given take profit orders are placed, When the candle high ≥ TP price, Then the order fills in the SimulatedExecutionEngine
- [ ] Given a hedge trigger condition, When the candle close falls below the breakdown threshold, Then the hedge signal activates
- [ ] Given maker fee = 0.01% and taker fee = 0.035%, When a simulated fill occurs, Then the appropriate fee is deducted from PnL
- [ ] Given slippage = 0.05%, When a fill occurs, Then the fill price is adjusted away from the order price by the slippage percentage
- [ ] Given a backtest completes, Then the result includes: total trades, winning trades, losing trades, win rate, total PnL, max drawdown (absolute and %), average trade PnL, average hold time, hedges opened, total fees paid, grid cycles, final equity
- [ ] Given a backtest completes, Then the result includes a per-tick equity time-series and a complete ordered trade log
- [ ] Given the same inputs, When the backtest is run twice, Then the results are identical (deterministic)
- [ ] Given no candle data exists for the requested range, When the backtest is triggered, Then an error is returned indicating insufficient data
- [ ] Given 1h or 4h candle data is missing for the requested range, When the backtest is triggered, Then the runner fails fast with an error identifying which timeframe is missing
- [ ] Given an indicator warmup of 200 candles is required, When the backtest starts, Then the first 200 candles feed indicators only and no signals are generated
- [ ] Given insufficient candle data before the start date for indicator warmup, When the backtest is triggered, Then an error is returned indicating insufficient warmup data
- [ ] Given a grid completes (TP hit) mid-backtest, When conditions remain valid, Then the strategy re-deploys a new grid and the backtest continues
- [ ] Given multiple orders could fill on the same candle, When both a buy and TP qualify, Then the buy fills first
- [ ] Given initial capital of $10,000 in config, When the backtest runs, Then equity tracking starts at $10,000 and all metrics reference this starting capital
- [ ] Given 15m candles at time T, When the MarketContext is built, Then it includes the latest closed 1h and 4h candles at or before T

## Objectives

- Define pipeline interfaces (`IExecutionEngine`, `IStrategyEngine`, `IGridController`, `IRiskEngine`, `IPositionManager`, `IMarketContextBuilder`) as thin contracts for the trading pipeline
- Implement shared scheduling components (`CandleClock`, `StrategyScheduler`) from the scheduling architecture knowledge docs; CandleClock computes close time from `Timestamp + IntervalDurationMs` (F1's Candle entity only has `Timestamp`)
- Implement `SimulatedExecutionEngine` with in-memory order book, fill simulation (limit buy, take profit, hedge), configurable fees/slippage, and fill priority ordering
- Implement `CandleReplayEngine` with multi-timeframe alignment, warmup period handling, and lookahead prevention
- Implement `BacktestMetricsCalculator` to compute summary performance metrics from the trade log
- Implement `BacktestRunner` to orchestrate the full backtest pipeline with equity tracking, trade logging with entry/exit pairing, input validation, and multi-cycle grid redeployment

### Discovery References

- `.agent-context/0-knowledge/18-backtesting-architecture.md` — Authoritative backtest architecture: pipeline flow, `IExecutionEngine` interface segregation, fill logic, fee model, component names
- `.agent-context/0-knowledge/19-scheduling-architecture.md` — `CandleClock` and `StrategyScheduler` design with code samples, `CandleClosedEvent` model, duplicate execution prevention
- `.agent-context/0-knowledge/15-grid-controller.md` — Grid lifecycle state machine (Inactive → Closed), signal types, inputs/outputs
- `.agent-context/0-knowledge/16-signal-contracts.md` — Signal types: DeployGrid, CancelGrid, TakeProfit, OpenHedge, etc., signal lifecycle
- `.agent-context/0-knowledge/14-strategy-runtime-model.md` — `ITradingStrategy`, `StrategyRun`, execution loop per subscriber
- `.agent-context/0-knowledge/13-strategy-config-schema.md` — Strategy config JSON schema: trend, bias, entry, grid, exit, hedge, risk
- `.agent-context/0-knowledge/04-domain-model.md` — Domain entities including Candle, Backtest, BacktestResult, Signal, Order, Position
- `.agent-context/0-knowledge/10-architecture-decisions.md` — ADRs: EF Core (SQLite), MediatR, multi-tenancy (candles exempt)
- `.agent-context/3-develop/backlog/draft/backtesting/F1-candle-data-persistence.md` — `Candle` entity, `ICandleRepository` interface contract (prerequisite)

### Project Patterns

- `src/TradePilot.Application/MarketData/Queries/GetCandlesQuery.cs` — Canonical query + handler co-location pattern
- `src/TradePilot.Application/Abstractions/Services/IHyperliquidRestClient.cs` — Application-layer service interface pattern (model for `IExecutionEngine`)
- `src/TradePilot.Application/Abstractions/Configuration/HyperliquidOptions.cs` — `IOptions<T>` configuration pattern
- `src/TradePilot.Application/MarketData/Models/CandleDto.cs` — Existing `CandleDto` with `Timestamp` (unix ms), OHLCV decimals
- `src/TradePilot.Application/Abstractions/Commands/Command.cs` — CQRS base types
- `src/TradePilot.Application/Abstractions/Exceptions/DomainException.cs` — Domain exception pattern (maps to HTTP 400)
- `tests/TradePilot.Infrastructure.Tests/Services/HyperliquidSignerTests.cs` — Canonical unit test: sealed class, Given_When_Then naming, FluentAssertions
- `tests/TradePilot.Api.Tests/Services/HyperliquidOrderServiceTests.cs` — Service unit test with `[TestInitialize]`, Moq mocks
- `tests/TradePilot.Application.Tests/Usings.cs` — Global usings: FluentAssertions, MSTest, Moq

### [x] Phase 1: Foundation — Models, Interfaces, and Scheduling

**Complexity**: Medium | **Risk**: Low

- [x] Task 1.1: Create backtest models and DTOs (note: `TradeType` enum lives in `Trading/Models/` — see Task 1.2)
  - Details: .agent-context/3-develop/build/plans/details/20260327-backtest-replay-engine-phase-01-details.md#task-11-create-backtest-models-and-dtos

- [x] Task 1.2: Create trading pipeline models (includes `TradeType` enum)
  - Details: .agent-context/3-develop/build/plans/details/20260327-backtest-replay-engine-phase-01-details.md#task-12-create-trading-pipeline-models

- [x] Task 1.3: Create pipeline interfaces
  - Details: .agent-context/3-develop/build/plans/details/20260327-backtest-replay-engine-phase-01-details.md#task-13-create-pipeline-interfaces

- [x] Task 1.4: Implement CandleClock and CandleClosedEvent
  - Details: .agent-context/3-develop/build/plans/details/20260327-backtest-replay-engine-phase-01-details.md#task-14-implement-candleclock-and-candleclosedevent

- [x] Task 1.5: Write CandleClock unit tests
  - Details: .agent-context/3-develop/build/plans/details/20260327-backtest-replay-engine-phase-01-details.md#task-15-write-candleclock-unit-tests

- [x] Task 1.6: Verify solution builds and tests pass
  - Details: .agent-context/3-develop/build/plans/details/20260327-backtest-replay-engine-phase-01-details.md#task-16-verify-solution-builds-and-tests-pass

### [x] Phase 2: SimulatedExecutionEngine

**Complexity**: High | **Risk**: Medium

- [x] Task 2.1: Create SimulatedExecutionEngine with order management
  - Details: .agent-context/3-develop/build/plans/details/20260327-backtest-replay-engine-phase-02-details.md#task-21-create-simulatedexecutionengine-with-order-management

- [x] Task 2.2: Implement ProcessCandle fill simulation logic
  - Details: .agent-context/3-develop/build/plans/details/20260327-backtest-replay-engine-phase-02-details.md#task-22-implement-processcandle-fill-simulation-logic

- [x] Task 2.3: Write SimulatedExecutionEngine unit tests
  - Details: .agent-context/3-develop/build/plans/details/20260327-backtest-replay-engine-phase-02-details.md#task-23-write-simulatedexecutionengine-unit-tests

- [x] Task 2.4: Verify solution builds and all tests pass
  - Details: .agent-context/3-develop/build/plans/details/20260327-backtest-replay-engine-phase-02-details.md#task-24-verify-solution-builds-and-all-tests-pass

### [x] Phase 3: CandleReplayEngine and BacktestMetricsCalculator

**Complexity**: Medium | **Risk**: Medium

- [x] Task 3.1: Create CandleReplayEngine with multi-timeframe alignment
  - Details: .agent-context/3-develop/build/plans/details/20260327-backtest-replay-engine-phase-03-details.md#task-31-create-candlereplayengine-with-multi-timeframe-alignment

- [x] Task 3.2: Implement warmup period handling
  - Details: .agent-context/3-develop/build/plans/details/20260327-backtest-replay-engine-phase-03-details.md#task-32-implement-warmup-period-handling

- [x] Task 3.3: Create BacktestMetricsCalculator
  - Details: .agent-context/3-develop/build/plans/details/20260327-backtest-replay-engine-phase-03-details.md#task-33-create-backtestmetricscalculator

- [x] Task 3.4: Write CandleReplayEngine unit tests
  - Details: .agent-context/3-develop/build/plans/details/20260327-backtest-replay-engine-phase-03-details.md#task-34-write-candlereplayengine-unit-tests

- [x] Task 3.5: Write BacktestMetricsCalculator unit tests
  - Details: .agent-context/3-develop/build/plans/details/20260327-backtest-replay-engine-phase-03-details.md#task-35-write-backtestmetricscalculator-unit-tests

- [x] Task 3.6: Verify solution builds and all tests pass
  - Details: .agent-context/3-develop/build/plans/details/20260327-backtest-replay-engine-phase-03-details.md#task-36-verify-solution-builds-and-all-tests-pass

### [x] Phase 4: BacktestRunner Orchestrator

**Complexity**: High | **Risk**: Medium

- [x] Task 4.1: Create StrategyScheduler
  - Details: .agent-context/3-develop/build/plans/details/20260327-backtest-replay-engine-phase-04-details.md#task-41-create-strategyscheduler

- [x] Task 4.2: Create BacktestRunner implementing IBacktestRunner (includes trade entry/exit pairing for per-trade PnL)
  - Details: .agent-context/3-develop/build/plans/details/20260327-backtest-replay-engine-phase-04-details.md#task-42-create-backtestrunner-implementing-ibacktestrunner

- [x] Task 4.3: Implement input validation with fail-fast error handling
  - Details: .agent-context/3-develop/build/plans/details/20260327-backtest-replay-engine-phase-04-details.md#task-43-implement-input-validation-with-fail-fast-error-handling

- [x] Task 4.4: Write BacktestRunner unit tests
  - Details: .agent-context/3-develop/build/plans/details/20260327-backtest-replay-engine-phase-04-details.md#task-44-write-backtestrunner-unit-tests

- [x] Task 4.5: Write StrategyScheduler unit tests
  - Details: .agent-context/3-develop/build/plans/details/20260327-backtest-replay-engine-phase-04-details.md#task-45-write-strategyscheduler-unit-tests

- [x] Task 4.6: Verify full solution builds and all tests pass
  - Details: .agent-context/3-develop/build/plans/details/20260327-backtest-replay-engine-phase-04-details.md#task-46-verify-full-solution-builds-and-all-tests-pass

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: Foundation — Models, Interfaces, and Scheduling | Medium | Low |
| Phase 2: SimulatedExecutionEngine | High | Medium |
| Phase 3: CandleReplayEngine and BacktestMetricsCalculator | Medium | Medium |
| Phase 4: BacktestRunner Orchestrator | High | Medium |
| **Overall** | **High** | **Medium** |

### Scoping Notes

- F1 (Candle Data Persistence) is assumed complete — `Candle` entity in Domain, `ICandleRepository` in Application/Abstractions, EF Core implementation in Persistence
- Pipeline interfaces defined in this plan are intentionally thin; they will be expanded when the core trading pipeline PBIs are implemented
- CandleClock and StrategyScheduler are shared components used by both live and backtest modes
- LLM context is not included in v1 backtest (out of scope per PBI)
- Backtest result persistence to database is out of scope (handled by F4)
- No API endpoints are created (handled by F4)
- All backtest components live in `TradePilot.Application` — no Infrastructure layer changes needed
- The `SimulatedExecutionEngine` is an Application-layer component (pure in-memory logic, no external I/O)
- StrategyExecutionCheckpoint is not used in backtest mode (sequential replay has no restart concern)

## Dependencies

- F1 — Candle Data Persistence (`Candle` entity, `ICandleRepository`, `TradePilotDbContext`)
- .NET 9 / C# 13 (current project target)
- MediatR (existing — for future F4 command handler integration)
- MSTest + Moq + FluentAssertions v6 (existing test stack)

## Success Criteria

- All 4 phases build successfully with zero compiler errors
- All unit tests pass (CandleClock, SimulatedExecutionEngine, CandleReplayEngine, BacktestMetricsCalculator, BacktestRunner, StrategyScheduler)
- SimulatedExecutionEngine correctly fills limit buys, take profits, and hedges per PBI fill rules
- CandleReplayEngine correctly aligns multi-timeframe candles and handles warmup periods
- BacktestMetricsCalculator produces all required metrics (total trades, win rate, PnL, max drawdown, etc.)
- BacktestRunner orchestrates the full pipeline and produces deterministic results
- BacktestRunner fails fast with descriptive errors for missing data, missing timeframes, and insufficient warmup data

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-03-27T20:54:37Z | 2026-03-27T21:18:40Z |
| Plan Reviewer | plan-reviewed | 2026-03-27T21:20:00Z | 2026-03-27T21:32:00Z |
| 3-Develop: 2 Implementer | implemented | 2026-03-27T21:34:05Z | 2026-03-28T14:35:00Z |
| 3-Develop: 3 Reviewer | complete | 2026-03-28T14:38:03Z | 2026-03-28T15:33:20Z |
