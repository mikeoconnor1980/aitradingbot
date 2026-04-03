applyTo: ".agent-context/3-develop/build/changes/20260403-signal-runtime-wiring-changes.md"
currentAgent: "Plan Implementer"
agentStartedAt: "2026-04-03T16:02:11Z"
status: "completed"
lastUpdated: "2026-04-03T16:35:41Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: F6.75 — Signal Runtime Wiring + Execution Path

## Overview

Wire signal-mode strategies into the shared scheduler/backtest pipeline so RSI entry conditions are evaluated with a populated indicator context and can produce executable trade signals, separate from the grid-only `DeployGrid` flow.

## PBI Details

**PBI ID:** Draft — F6.75
**Status:** Draft
**Risk Level:** High
**Depends On:** F6 (UI: RSI Condition + Signal Mode), F6.5 (Extract Indicator Calculators into Standalone Project)

### User Story

> As a **trader**, I want **signal-mode strategies to evaluate and execute through the live and backtest engine** so that **an RSI strategy that passes its conditions can actually open trades instead of remaining UI-only configuration**.

### Acceptance Criteria

- [ ] **Given** a signal strategy with one RSI condition, **When** `StrategyScheduler` builds `MarketContext`, **Then** it passes extracted indicator requirements into the market-context builder
- [ ] **Given** a signal strategy requiring RSI(14), **When** sufficient candle history exists, **Then** `IndicatorContext` contains RSI(14) values for the evaluator
- [ ] **Given** a signal strategy with valid indicator context, **When** the condition passes, **Then** strategy evaluation returns `SetupDetected = true`
- [ ] **Given** a signal strategy with valid indicator context, **When** the condition fails, **Then** strategy evaluation returns `SetupDetected = false` with a meaningful reason
- [ ] **Given** a passing signal strategy, **When** the execution pipeline processes the approved signal, **Then** it emits and executes a signal-mode trade intent rather than `DeployGrid`
- [ ] **Given** a signal strategy, **When** the runtime pipeline executes it, **Then** no grid configuration is required to open a position
- [ ] **Given** a signal strategy backtest where RSI conditions are met, **When** the backtest completes, **Then** at least one trade is recorded
- [ ] **Given** a signal strategy backtest where RSI conditions are not met, **When** the backtest completes, **Then** no trades are recorded and the debug output explains why
- [ ] **Given** an existing grid strategy, **When** backtests and scheduler flows are run after this change, **Then** the existing grid execution behavior remains unchanged
- [ ] **Given** signal-mode evaluation debug output, **When** a condition fails because data is missing or thresholds are not met, **Then** the recorded reason is available to developers and users

## Objectives

- Fix `StrategyScheduler` to extract indicator requirements from signal-mode strategies and pass them to the 4-arg `IMarketContextBuilder.Build` overload
- Create `ISignalController` + `SignalController` to handle post-evaluation execution for signal-mode strategies (emit `OpenPosition` / `TakeProfit` signals)
- Add `OpenPosition` signal handling in `BacktestPositionManager` with `TradeType.SignalEntry`
- Update `BacktestRunner` trade pairing (`RecordFill`, `IsCompatibleExit`) to support `SignalEntry → TakeProfit` pairs
- Maintain full grid-mode regression safety — no changes to `GridController` internals

### Discovery References

**Root Cause Analysis:**
1. `StrategyScheduler.HandleCandleClosedAsync` (line 67) calls the 3-arg `Build` overload → `IndicatorContext` is always `null`
2. `ConditionEvaluator.Evaluate` short-circuits with `"Indicator context not available."` when `IndicatorContext` is `null`
3. `GridController.ProcessAsync` throws `InvalidOperationException` when `config.Grid` is `null` (signal mode) and `SetupDetected = true`
4. `BacktestPositionManager.ExecuteSignalsAsync` only handles `DeployGrid`, `TakeProfit`, `CancelGrid` — no `OpenPosition` signal type exists

**Key Finding:** `IndicatorExtractor.Extract()` and `BacktestMarketContextBuilder.BuildIndicatorContext()` already work correctly — they are simply never connected in the scheduler pipeline.

### Project Patterns

- `src/TradingApp.Application/Scheduling/StrategyScheduler.cs` — candle-close pipeline orchestrator, calls `IMarketContextBuilder.Build`, `IStrategyEngine.EvaluateAsync`, `IGridController.ProcessAsync`
- `src/TradingApp.Application/Abstractions/Services/IMarketContextBuilder.cs` — has 3-arg and 4-arg `Build` overloads; 4-arg accepts `IReadOnlyList<IndicatorRequirement>?`
- `src/TradingApp.Application/Trading/Services/BacktestMarketContextBuilder.cs` — implements both overloads; 4-arg populates `IndicatorContext` from requirements
- `src/TradingApp.Application/StrategyAuthoring/Services/IndicatorExtractor.cs` — static class, extracts `IndicatorRequirement[]` from `StrategyConfig.EntryConditions`
- `src/TradingApp.Application/StrategyAuthoring/Services/ConditionEvaluator.cs` — dispatches conditions to handlers; guard on `IndicatorContext is null`
- `src/TradingApp.Application/Trading/Services/CompositeStrategyEngine.cs` — routes `StrategyMode.Signal` → `ConditionEvaluator`, `Grid` → `GridStrategyEngine`
- `src/TradingApp.Application/Trading/Services/GridController.cs` — grid lifecycle state machine, `DeployGrid` / `TakeProfit` / `CancelGrid` signals
- `src/TradingApp.Application/Trading/Services/BacktestPositionManager.cs` — handles `DeployGrid`, `TakeProfit`, `CancelGrid`; places `OrderRequest` via `SimulatedExecutionEngine`
- `src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs` — creates `StrategyScheduler`, wires `CandleClock`, manages trade log and `RecordFill` pairing
- `src/TradingApp.Application/Trading/Models/TradeType.cs` — enum: `GridFill`, `TakeProfit`, `HedgeOpen`, `HedgeClose`
- `src/TradingApp.Application/Abstractions/Services/IGridController.cs` — `ProcessAsync` contract returning `IReadOnlyList<TradingSignal>`
- `tests/TradingApp.Application.Tests/Scheduling/StrategySchedulerTests.cs` — pipeline wiring tests, currently grid-mode only

### [x] Phase 1: Indicator Context Wiring in StrategyScheduler

**Complexity**: Medium | **Risk**: Low

- [x] Task 1.1: Update `StrategyScheduler.HandleCandleClosedAsync` to extract indicator requirements and call 4-arg `Build`
  - Details: .agent-context/3-develop/build/plans/details/20260403-signal-runtime-wiring-phase-01-details.md#task-11-update-strategyschedulehandlecandleclosedasync

- [x] Task 1.2: Add signal-mode scheduler tests proving indicator requirements are passed to market-context builder
  - Details: .agent-context/3-develop/build/plans/details/20260403-signal-runtime-wiring-phase-01-details.md#task-12-add-signal-mode-scheduler-tests

- [x] Task 1.3: Run all existing scheduler and strategy tests to verify no grid-mode regression
  - Details: .agent-context/3-develop/build/plans/details/20260403-signal-runtime-wiring-phase-01-details.md#task-13-run-regression-tests

### [x] Phase 2: Signal Controller and Execution Branch

**Complexity**: High | **Risk**: Medium

- [x] Task 2.1: Create `ISignalController` interface in `Abstractions/Services/`
  - Details: .agent-context/3-develop/build/plans/details/20260403-signal-runtime-wiring-phase-02-details.md#task-21-create-isignalcontroller-interface

- [x] Task 2.2: Create `SignalController` implementation that emits `OpenPosition` and `TakeProfit` signals
  - Details: .agent-context/3-develop/build/plans/details/20260403-signal-runtime-wiring-phase-02-details.md#task-22-create-signalcontroller-implementation

- [x] Task 2.3: Update `StrategyScheduler` to accept optional `ISignalController` and branch on `StrategyMode`
  - Details: .agent-context/3-develop/build/plans/details/20260403-signal-runtime-wiring-phase-02-details.md#task-23-update-strategyschedule-to-branch-on-strategymode

- [x] Task 2.4: Register `ISignalController` in DI (`Program.cs`)
  - Details: .agent-context/3-develop/build/plans/details/20260403-signal-runtime-wiring-phase-02-details.md#task-24-register-isignalcontroller-in-di

- [x] Task 2.5: Add `SignalController` unit tests
  - Details: .agent-context/3-develop/build/plans/details/20260403-signal-runtime-wiring-phase-02-details.md#task-25-add-signalcontroller-unit-tests

- [x] Task 2.6: Add scheduler signal-mode branching tests
  - Details: .agent-context/3-develop/build/plans/details/20260403-signal-runtime-wiring-phase-02-details.md#task-26-add-scheduler-signal-mode-branching-tests

- [x] Task 2.7: Run all tests to verify no regression
  - Details: .agent-context/3-develop/build/plans/details/20260403-signal-runtime-wiring-phase-02-details.md#task-27-run-all-tests

### [x] Phase 3: Backtest Signal Execution and Trade Pairing

**Complexity**: High | **Risk**: Medium

- [x] Task 3.1: Add `TradeType.SignalEntry` enum value
  - Details: .agent-context/3-develop/build/plans/details/20260403-signal-runtime-wiring-phase-03-details.md#task-31-add-tradetypesignalentry

- [x] Task 3.2: Add `OpenPosition` signal handling in `BacktestPositionManager`
  - Details: .agent-context/3-develop/build/plans/details/20260403-signal-runtime-wiring-phase-03-details.md#task-32-add-openposition-signal-handling

- [x] Task 3.3: Update `BacktestRunner.RecordFill` and `IsCompatibleExit` for `SignalEntry → TakeProfit` pairing
  - Details: .agent-context/3-develop/build/plans/details/20260403-signal-runtime-wiring-phase-03-details.md#task-33-update-recordfill-and-iscompatibleexit

- [x] Task 3.4: Update `BacktestRunner` to pass `ISignalController` into `StrategyScheduler`
  - Details: .agent-context/3-develop/build/plans/details/20260403-signal-runtime-wiring-phase-03-details.md#task-34-update-backtestrunner-to-pass-isignalcontroller

- [x] Task 3.5: Add `BacktestPositionManager` tests for `OpenPosition` signal handling
  - Details: .agent-context/3-develop/build/plans/details/20260403-signal-runtime-wiring-phase-03-details.md#task-35-add-backtestpositionmanager-openposition-tests

- [x] Task 3.6: Add end-to-end backtest test for signal-mode strategy
  - Details: .agent-context/3-develop/build/plans/details/20260403-signal-runtime-wiring-phase-03-details.md#task-36-add-e2e-backtest-signal-mode-test

- [x] Task 3.7: Run all tests including full backtest suite to verify no regression
  - Details: .agent-context/3-develop/build/plans/details/20260403-signal-runtime-wiring-phase-03-details.md#task-37-run-full-test-suite

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: Indicator Context Wiring | Medium | Low |
| Phase 2: Signal Controller + Execution Branch | High | Medium |
| Phase 3: Backtest Signal Execution + Trade Pairing | High | Medium |
| **Total** | **High** | **Medium** |

### Scoping Notes

- `PriceVsEmaConditionHandler` is intentionally out of scope — only `RsiConditionHandler` is registered; other condition types will skip silently with a warning (existing behavior)
- `BacktestConfig.Intervals` validation still requires 1h/4h intervals even for signal-mode — relaxing this is deferred to avoid scope creep
- `CandleEvaluationEntry` audit fields (`GridLifecycleState`, `GridCycleId`) will appear as `"Inactive"` / `null` for signal-mode — cosmetic, acceptable for now
- The `BacktestsController.MapStrategyConfig` hardcoding `StrategyMode.Grid` for legacy REST path is out of scope — signal-mode backtests use the DB-saved config
- No new API endpoints are added — signal-mode strategies backtest through the existing `POST /api/backtests` endpoint using the saved `StrategyConfig`
- `OpenPosition` is a new signal type not yet documented in `16-signal-contracts.md` — knowledge doc update deferred to a follow-up task

## Dependencies

- F6 (UI: RSI Condition + Signal Mode) — must be merged for signal-mode `StrategyConfig` to exist in the database
- F6.5 (Extract Indicator Calculators) — must be merged for `TradingApp.Indicators` project to be available to `BacktestMarketContextBuilder`
- `IndicatorExtractor.Extract()` — already implemented and tested
- `BacktestMarketContextBuilder.Build(candle, 1h, 4h, requirements)` — already implemented and tested
- `CompositeStrategyEngine` — already routes `StrategyMode.Signal` → `ConditionEvaluator`

## Success Criteria

- Signal-mode strategies with RSI conditions evaluate successfully with populated `IndicatorContext` in both live scheduler and backtest paths
- Signal-mode strategies can open and close positions via `OpenPosition` / `TakeProfit` signals without requiring `GridConfig`
- Backtest of a signal strategy with passing RSI conditions records at least one trade
- All existing grid-mode tests pass after updating test setup to supply the new `ISignalController` constructor parameter
- Pipeline ordering is maintained: context → evaluate → signal-controller → risk → position-manager

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-04-03T14:47:31Z | 2026-04-03T15:10:00Z |
| Plan Reviewer | plan-reviewed | 2026-04-03T15:52:59Z | 2026-04-03T15:57:44Z |
| Plan Implementer | completed | 2026-04-03T16:02:11Z | 2026-04-03T16:35:41Z |
