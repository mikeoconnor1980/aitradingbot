applyTo: ".agent-context/3-develop/build/changes/20260403-indicator-infra-condition-evaluator-rsi-changes.md"
status: "in-progress"
lastUpdated: "2026-04-03T12:07:41Z"
currentAgent: "Plan Implementer"
agentStartedAt: "2026-04-03T11:48:44Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: F5 — Indicator Infrastructure & Condition Evaluator (RSI)

## Overview

Build the indicator calculation infrastructure with configurable periods, the condition evaluator engine with handler pattern, the RSI condition handler, and strategy engine routing by `strategyMode` (grid vs signal).

## PBI Details

**PBI ID:** Draft — F5  
**Status:** Draft  
**Risk Level:** High  
**Depends On:** F1 (Schema — complete)

### User Story

> As a **trader**, I want the **engine to evaluate RSI conditions against market data** so that **the system only enters trades when RSI meets my defined threshold**.

### Acceptance Criteria

- [ ] **Given** `strategyMode = "signal"` with RSI condition `operator = "lt", value = 40`, **When** RSI(14) = 35, **Then** `SetupDetected = true`
- [ ] **Given** RSI condition `operator = "lt", value = 40`, **When** RSI(14) = 45, **Then** `SetupDetected = false`
- [ ] **Given** RSI condition `operator = "cross_above", value = 30`, **When** previous RSI = 28 and current RSI = 32, **Then** `SetupDetected = true`
- [ ] **Given** `entryLogic = "all"` with RSI (passes) and an unknown type (warning), **When** evaluated, **Then** `SetupDetected = true` (unknown types don't block)
- [ ] **Given** `entryLogic = "any"` with RSI (fails) and no other conditions, **Then** `SetupDetected = false`
- [ ] **Given** one disabled RSI condition and entry logic `all`, **Then** `SetupDetected = false` (no enabled conditions)
- [ ] **Given** `strategyMode = "grid"`, **When** evaluated, **Then** existing `GridStrategyEngine` logic runs (unchanged)
- [ ] **Given** a strategy requiring RSI(14), **When** `IMarketContextBuilder.Build()` is called, **Then** `IndicatorContext.GetRsi(14)` returns a value
- [ ] **Given** existing grid strategy tests, **When** run, **Then** all pass (grid path untouched)

## Objectives

- Introduce `IndicatorContext` model with dynamic keyed indicator lookup (`GetRsi(period)`, `GetEma(period)`)
- Extract indicator requirements from `StrategyConfig` via `IndicatorExtractor`
- Modify `IMarketContextBuilder` to accept config-driven indicator requirements
- Build `IConditionEvaluator` orchestrator with `IConditionHandler` handler pattern
- Implement `RsiConditionHandler` — the first concrete condition handler
- Create `CompositeStrategyEngine` to route by `StrategyMode` (grid → `GridStrategyEngine`, signal → condition evaluator)
- Remove `SIGNAL_MODE_NOT_SUPPORTED` info message from `CrossFieldValidator`

### Discovery References

All key source files were read in full during discovery. The existing `BacktestMarketContextBuilder` already has parameterized `CalculateRsi(int period)` and `CalculateEma(int period)` methods — they just aren't driven from config. The `EntryConditionConfig` → `RsiParams` type hierarchy is fully built from F1 (schema PBI). The pipeline flows through `StrategyScheduler` which calls `IStrategyEngine.EvaluateAsync` then `IGridController.ProcessAsync`.

### Project Patterns

- `src/TradingApp.Application/Trading/Services/BacktestMarketContextBuilder.cs` — Indicator calculation with parameterized CalculateRsi/CalculateEma methods
- `src/TradingApp.Application/Trading/Services/GridStrategyEngine.cs` — Current IStrategyEngine impl, grid-only evaluation
- `src/TradingApp.Application/Scheduling/StrategyScheduler.cs` — Pipeline orchestrator calling contextBuilder → strategyEngine → gridController
- `src/TradingApp.Application/Trading/Models/MarketContext.cs` — Context with IndicatorSnapshot
- `src/TradingApp.Application/Trading/Models/IndicatorSnapshot.cs` — Fixed 5-field indicator model
- `src/TradingApp.Application/Trading/Models/StrategyEvaluation.cs` — SetupDetected + Reason result
- `src/TradingApp.Application/StrategyAuthoring/Models/StrategyConfig.cs` — Full config with StrategyMode, EntryConditions, EntryLogic
- `src/TradingApp.Application/StrategyAuthoring/Models/RsiParams.cs` — Period, Operator, Value
- `src/TradingApp.Application/StrategyAuthoring/Models/EntryConditionType.cs` — Unknown/Rsi/PriceVsEma/Macd
- `src/TradingApp.Application/StrategyAuthoring/Validation/CrossFieldValidator.cs` — SIGNAL_MODE_NOT_SUPPORTED info
- `src/TradingApp.Api/Program.cs` — Flat DI registrations
- `tests/TradingApp.Application.Tests/Trading/Services/GridControllerTests.cs` — GridController tests with private static Create* helpers
- `tests/TradingApp.Application.Tests/Scheduling/StrategySchedulerTests.cs` — StrategyScheduler tests with mocked IStrategyEngine
- `tests/TradingApp.Application.Tests/Backtesting/Services/RealBacktestRunnerTests.cs` — Integration tests using real GridStrategyEngine

### [x] Phase 1: Indicator Infrastructure

**Complexity**: Medium | **Risk**: Medium

- [x] Task 1.1: Create `IndicatorContext` model
  - Details: .agent-context/3-develop/build/plans/details/20260403-indicator-infra-condition-evaluator-rsi-phase-01-details.md#task-11-create-indicatorcontext-model

- [x] Task 1.2: Create `IndicatorRequirement` model and `IndicatorExtractor` utility
  - Details: .agent-context/3-develop/build/plans/details/20260403-indicator-infra-condition-evaluator-rsi-phase-01-details.md#task-12-create-indicatorrequirement-and-indicatorextractor

- [x] Task 1.3: Modify `IMarketContextBuilder` and `BacktestMarketContextBuilder`
  - Details: .agent-context/3-develop/build/plans/details/20260403-indicator-infra-condition-evaluator-rsi-phase-01-details.md#task-13-modify-imarketcontextbuilder-and-backtestmarketcontextbuilder

- [x] Task 1.4: Add `IndicatorContext` to `MarketContext`
  - Details: .agent-context/3-develop/build/plans/details/20260403-indicator-infra-condition-evaluator-rsi-phase-01-details.md#task-14-add-indicatorcontext-to-marketcontext

- [x] Task 1.5: Write unit tests for Phase 1
  - Details: .agent-context/3-develop/build/plans/details/20260403-indicator-infra-condition-evaluator-rsi-phase-01-details.md#task-15-write-unit-tests-for-phase-1

- [x] Task 1.6: Build and run tests
  - Details: .agent-context/3-develop/build/plans/details/20260403-indicator-infra-condition-evaluator-rsi-phase-01-details.md#task-16-build-and-run-tests

### [x] Phase 2: Condition Evaluator Engine & RSI Handler

**Complexity**: High | **Risk**: Medium

- [x] Task 2.1: Create condition evaluation models
  - Details: .agent-context/3-develop/build/plans/details/20260403-indicator-infra-condition-evaluator-rsi-phase-02-details.md#task-21-create-condition-evaluation-models

- [x] Task 2.2: Create `IConditionHandler` interface and `RsiConditionHandler`
  - Details: .agent-context/3-develop/build/plans/details/20260403-indicator-infra-condition-evaluator-rsi-phase-02-details.md#task-22-create-iconditionhandler-and-rsiconditionhandler

- [x] Task 2.3: Create `IConditionEvaluator` and `ConditionEvaluator`
  - Details: .agent-context/3-develop/build/plans/details/20260403-indicator-infra-condition-evaluator-rsi-phase-02-details.md#task-23-create-iconditionevaluator-and-conditionevaluator

- [x] Task 2.4: Write unit tests for Phase 2
  - Details: .agent-context/3-develop/build/plans/details/20260403-indicator-infra-condition-evaluator-rsi-phase-02-details.md#task-24-write-unit-tests-for-phase-2

- [x] Task 2.5: Build and run tests
  - Details: .agent-context/3-develop/build/plans/details/20260403-indicator-infra-condition-evaluator-rsi-phase-02-details.md#task-25-build-and-run-tests

### [ ] Phase 3: Strategy Engine Routing & Integration

**Complexity**: Medium | **Risk**: Medium

- [x] Task 3.1: Create `CompositeStrategyEngine`
  - Details: .agent-context/3-develop/build/plans/details/20260403-indicator-infra-condition-evaluator-rsi-phase-03-details.md#task-31-create-compositestrategyengine

- [x] Task 3.2: Update DI registrations
  - Details: .agent-context/3-develop/build/plans/details/20260403-indicator-infra-condition-evaluator-rsi-phase-03-details.md#task-32-update-di-registrations

- [x] Task 3.3: Remove `SIGNAL_MODE_NOT_SUPPORTED` info message
  - Details: .agent-context/3-develop/build/plans/details/20260403-indicator-infra-condition-evaluator-rsi-phase-03-details.md#task-33-remove-signal-mode-not-supported-info-message

- [x] Task 3.4: Write unit and integration tests for Phase 3
  - Details: .agent-context/3-develop/build/plans/details/20260403-indicator-infra-condition-evaluator-rsi-phase-03-details.md#task-34-write-unit-and-integration-tests

- [ ] Task 3.5: Build and run all tests
  - Details: .agent-context/3-develop/build/plans/details/20260403-indicator-infra-condition-evaluator-rsi-phase-03-details.md#task-35-build-and-run-all-tests

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: Indicator Infrastructure | Medium | Medium |
| Phase 2: Condition Evaluator Engine & RSI Handler | High | Medium |
| Phase 3: Strategy Engine Routing & Integration | Medium | Medium |
| **Total** | **Medium-High** | **Medium** |

### Scoping Notes

- Grid path (`StrategyMode.Grid`) remains entirely unchanged — all existing grid tests must pass
- `IndicatorSnapshot` is preserved; `IndicatorContext` is added alongside it as a nullable property on `MarketContext`
- `IMarketContextBuilder.Build()` gets an optional overload accepting `IReadOnlyList<IndicatorRequirement>?` — the original 3-parameter signature stays for backward compat
- Cross detection (`cross_above`/`cross_below`) requires both current and previous candle indicator values stored in `IndicatorContext`
- Unknown condition types produce a warning log, do not block evaluation (forward compatibility)
- `StrategyScheduler` is NOT modified in this PBI — the `CompositeStrategyEngine` routes internally, and the scheduler continues to call `IStrategyEngine.EvaluateAsync` as before

## Dependencies

- F1 (Extensible Strategy Schema) — **complete**, provides `StrategyConfig`, `EntryConditionConfig`, `RsiParams`, `StrategyMode`, `EntryLogic`
- No external library dependencies — all indicator math uses existing private methods in `BacktestMarketContextBuilder`

## Success Criteria

- All 9 acceptance criteria pass via unit tests
- All existing grid strategy tests pass without modification
- `RealBacktestRunnerTests` pass without modification
- A strategy with `strategyMode = "signal"` and an RSI entry condition can be evaluated by the engine end-to-end
- Adding a new condition type requires only: new handler class + DI registration — no evaluator changes

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-04-03T09:52:35Z | 2026-04-03T09:58:00Z |
| Plan Reviewer | plan-reviewed | 2026-04-03T11:42:36Z | 2026-04-03T11:44:00Z |
| Plan Implementer | in-progress | 2026-04-03T11:48:44Z | - |
