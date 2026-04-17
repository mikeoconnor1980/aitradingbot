---
applyTo: ".agent-context/3-develop/build/changes/20260403-macd-condition-handler-ui-changes.md"
currentAgent: "None"
agentStartedAt: "2026-04-03T20:09:55Z"
status: "complete"
lastUpdated: "2026-04-03T20:43:17Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: F8 — MACD Condition Handler + UI Card

## Overview

Add MACD as a new entry condition type with a backend handler (6 operators), a polymorphic UI card, and the "MACD Cross" template — completing the third and final Phase 1 template.

## PBI Details

**PBI ID:** Draft
**Status:** Draft
**Depends On:** F5 (Condition Evaluator + Indicator Infra), F6 (Signal Mode UI), F6.5 (Extract Indicator Calculators — MACD calculation), F7 (EMA Trend Filter Handler — establishes multi-condition-type UI pattern)

### Summary

> As a **trader**, I want to **use MACD-based entry conditions** so that **I can build momentum crossover strategies evaluated by the engine**.

MACD is one of the most widely used momentum indicators. Adding it as an entry condition unlocks the "MACD Cross" template — the third and final Phase 1 template — completing the initial strategy-builder feature set.

### Acceptance Criteria

- [ ] **Given** `macd` condition with `operator = "cross_above_signal"`, **When** MACD line crosses above signal line on current candle (previous: line < signal; current: line >= signal), **Then** condition passes with descriptive reason
- [ ] **Given** `macd` condition with `operator = "cross_below_signal"`, **When** MACD line crosses below signal line on current candle, **Then** condition passes
- [ ] **Given** `macd` condition with `operator = "above_zero"`, **When** MACD line > 0, **Then** condition passes
- [ ] **Given** `macd` condition with `operator = "below_zero"`, **When** MACD line < 0, **Then** condition passes
- [ ] **Given** `macd` condition with `operator = "histogram_rising"`, **When** current histogram > previous histogram, **Then** condition passes
- [ ] **Given** `macd` condition with `operator = "histogram_falling"`, **When** current histogram < previous histogram, **Then** condition passes
- [ ] **Given** MACD data not available in `IndicatorContext`, **When** handler evaluates, **Then** condition returns `Passed = false` with reason (fail closed)
- [ ] **Given** "MACD Cross" template, **When** selected, **Then** form pre-populates MACD condition (12/26/9 cross_above_signal) + exits (TP 2%, SL 1.5%)
- [ ] **Given** MACD condition in UI, **When** `fastPeriod >= slowPeriod`, **Then** validation error shown inline
- [ ] **Given** MACD condition in UI, **When** `fastPeriod` set to 0 or 51, **Then** validation error shown with bounds [2, 50]
- [ ] **Given** one MACD condition already exists, **When** user clicks "Add MACD", **Then** button is disabled
- [ ] **Given** both RSI and MACD conditions with entry logic = "all", **When** evaluated, **Then** both must pass for entry signal
- [ ] **Given** no MACD handler registered, **When** `macd` condition evaluated by `ConditionEvaluator`, **Then** existing unknown-handler behaviour applies (safety net)
- [ ] **Given** existing strategy with RSI conditions, **When** user saves after adding MACD, **Then** RSI conditions are unchanged

## Objectives

- Implement `MacdConditionHandler` with 6 MACD operators following the established `IConditionHandler` pattern
- Enhance `BusinessRuleValidator` with max-1-per-type, period range, and fast < slow cross-field validation for MACD
- Create polymorphic `MacdConditionItemComponent` following the RSI/PriceVsEma card pattern
- Add "MACD Cross" template to `STRATEGY_TEMPLATES` with correct signal-mode configuration
- Update all 4 `_isSignalTemplate()` sites to recognise `"macd_cross"`

### Discovery References

Backend infrastructure already scaffolded by F5/F6.5:
- `EntryConditionType.Macd` enum value exists
- `MacdParams` record with `FastPeriod`, `SlowPeriod`, `SignalPeriod`, `Operator` exists
- JSON serialization (`EntryConditionConfigConverter`, `EntryConditionParamsConverter`) already handles `"macd"` → `MacdParams`
- `IndicatorExtractor` already extracts MACD requirements from conditions
- `BacktestMarketContextBuilder` already computes and stores MACD in `IndicatorContext`
- `IndicatorContext` has full MACD API: `GetMacd/GetPreviousMacd/GetMacdSignal/GetPreviousMacdSignal/GetMacdHistogram/GetPreviousMacdHistogram`
- Frontend `EntryConditionType` union already includes `"macd"`

### Project Patterns

- `src/TradePilot.Application/StrategyAuthoring/Services/RsiConditionHandler.cs` — Reference handler implementation (no logger, operator switch, Fail helper)
- `src/TradePilot.Application/StrategyAuthoring/Services/PriceVsEmaConditionHandler.cs` — Handler with ILogger, cross detection pattern
- `src/TradePilot.Application/StrategyAuthoring/Services/IConditionHandler.cs` — Interface: `ConditionType` + `Evaluate`
- `src/TradePilot.Application/StrategyAuthoring/Models/MacdParams.cs` — Existing params record
- `src/TradePilot.Application/Trading/Models/IndicatorContext.cs` — MACD getters (current + previous for line, signal, histogram)
- `src/TradePilot.Application/StrategyAuthoring/Validation/BusinessRuleValidator.cs` — Existing MACD_PERIODS_INVALID rule
- `tests/TradePilot.Application.Tests/StrategyAuthoring/Services/RsiConditionHandlerTests.cs` — Test pattern for condition handlers
- `frontend/trading-ui/src/app/features/strategy-builder/components/rsi-condition-item/` — Reference UI condition card
- `frontend/trading-ui/src/app/features/strategy-builder/services/condition-factory.service.ts` — FormGroup factory pattern
- `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.ts` — Condition params mapping
- `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.ts` — Client-side validation
- `frontend/trading-ui/src/app/features/strategy-builder/enums/price-vs-ema-operator.enum.ts` — Operator enum file pattern
- `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts` — Template application + _addLoadedCondition

### [x] Phase 1: Backend — MacdConditionHandler + Validation + Tests

**Complexity**: Medium | **Risk**: Low

- [x] Task 1.1: Create `MacdConditionHandler` implementing `IConditionHandler`
  - Details: .agent-context/3-develop/build/plans/details/20260403-macd-condition-handler-ui-phase-01-details.md#task-11-create-macdconditionhandler

- [x] Task 1.2: Enhance `BusinessRuleValidator` with MACD-specific validation rules
  - Details: .agent-context/3-develop/build/plans/details/20260403-macd-condition-handler-ui-phase-01-details.md#task-12-enhance-businessrulevalidator

- [x] Task 1.3: Register `MacdConditionHandler` in DI (`Program.cs`)
  - Details: .agent-context/3-develop/build/plans/details/20260403-macd-condition-handler-ui-phase-01-details.md#task-13-register-macdconditionhandler-in-di

- [x] Task 1.4: Create `MacdConditionHandlerTests`
  - Details: .agent-context/3-develop/build/plans/details/20260403-macd-condition-handler-ui-phase-01-details.md#task-14-create-macdconditionhandlertests

- [x] Task 1.5: Add `BusinessRuleValidatorTests` for MACD enhancements
  - Details: .agent-context/3-develop/build/plans/details/20260403-macd-condition-handler-ui-phase-01-details.md#task-15-add-businessrulevalidatortests-for-macd

- [x] Task 1.6: Run all backend tests and architecture tests
  - Details: .agent-context/3-develop/build/plans/details/20260403-macd-condition-handler-ui-phase-01-details.md#task-16-run-all-backend-tests

### [x] Phase 2: Frontend — Models, Services & Validation

**Complexity**: Medium | **Risk**: Low

- [x] Task 2.1: Update `MacdOperator` type and add `MACD Cross` template to `strategy.model.ts`
  - Details: .agent-context/3-develop/build/plans/details/20260403-macd-condition-handler-ui-phase-02-details.md#task-21-update-macdoperator-type-and-add-macd-cross-template

- [x] Task 2.2: Create `macd-operator.enum.ts` operator enum file
  - Details: .agent-context/3-develop/build/plans/details/20260403-macd-condition-handler-ui-phase-02-details.md#task-22-create-macd-operator-enum-file

- [x] Task 2.3: Add `createMacdCondition()` to `ConditionFactoryService`
  - Details: .agent-context/3-develop/build/plans/details/20260403-macd-condition-handler-ui-phase-02-details.md#task-23-add-createmacdcondition-to-conditionfactoryservice

- [x] Task 2.4: Add MACD branch to `StrategyMapperService`
  - Details: .agent-context/3-develop/build/plans/details/20260403-macd-condition-handler-ui-phase-02-details.md#task-24-add-macd-branch-to-strategymapperservice

- [x] Task 2.5: Add MACD validation to `StrategyValidationService`
  - Details: .agent-context/3-develop/build/plans/details/20260403-macd-condition-handler-ui-phase-02-details.md#task-25-add-macd-validation-to-strategyvalidationservice

- [x] Task 2.6: Update `_isSignalTemplate()` in all 4 locations
  - Details: .agent-context/3-develop/build/plans/details/20260403-macd-condition-handler-ui-phase-02-details.md#task-26-update-issignaltemplate-in-all-4-locations

- [x] Task 2.7: Add unit tests for MACD factory and mapper
  - Details: .agent-context/3-develop/build/plans/details/20260403-macd-condition-handler-ui-phase-02-details.md#task-27-add-unit-tests-for-macd-factory-and-mapper

- [x] Task 2.8: Run frontend build and lint
  - Details: .agent-context/3-develop/build/plans/details/20260403-macd-condition-handler-ui-phase-02-details.md#task-28-run-frontend-build-and-lint

### [x] Phase 3: Frontend — MACD Condition Card + Template Integration

**Complexity**: Medium | **Risk**: Low

- [x] Task 3.1: Create `MacdConditionItemComponent` (TS, HTML, SCSS)
  - Details: .agent-context/3-develop/build/plans/details/20260403-macd-condition-handler-ui-phase-03-details.md#task-31-create-macdconditionitemcomponent

- [x] Task 3.2: Update `EntryConditionsCardComponent` — add MACD dispatch, button, duplicate
  - Details: .agent-context/3-develop/build/plans/details/20260403-macd-condition-handler-ui-phase-03-details.md#task-32-update-entryconditionscardcomponent

- [x] Task 3.3: Update `strategy-builder-page.component.ts` — load MACD conditions + MACD Cross template
  - Details: .agent-context/3-develop/build/plans/details/20260403-macd-condition-handler-ui-phase-03-details.md#task-33-update-strategy-builder-page

- [x] Task 3.4: Run frontend build and lint
  - Details: .agent-context/3-develop/build/plans/details/20260403-macd-condition-handler-ui-phase-03-details.md#task-34-run-frontend-build-and-lint

## Scoping Summary

| Phase | Complexity | Risk |
|-------|-----------|------|
| Phase 1: Backend — MacdConditionHandler + Validation + Tests | Medium | Low |
| Phase 2: Frontend — Models, Services & Validation | Medium | Low |
| Phase 3: Frontend — MACD Condition Card + Template Integration | Medium | Low |
| **Total** | **Medium** | **Low** |

### Scoping Notes

- Backend MACD infrastructure (enum, params, serialization, extraction, indicator context) is already fully scaffolded from F5/F6.5 — only the handler, enhanced validation, and DI registration are needed
- Frontend `EntryConditionType` already includes `"macd"` — only interface, component, and service updates needed
- Frontend `MacdOperator` type exists with provisional values (`cross_above`, `cross_below`, `gt`, `lt`) from F5 — must be replaced with PBI operators (`cross_above_signal`, `cross_below_signal`, `above_zero`, `below_zero`, `histogram_rising`, `histogram_falling`)
- Frontend `MacdParams` interface and `EntryConditionConfig.params` union already include MACD — no structural additions needed
- All 6 operators map directly to existing `IndicatorContext` getters (no new API needed)
- `_isSignalTemplate()` is duplicated in 4 files — all must be updated for `"macd_cross"`
- Frontend validation service has a generic `period` check that will false-positive on MACD — must be excluded
- No database, migration, DevOps, or infrastructure changes required

## Dependencies

- F5 (Condition Evaluator + Indicator Infra) — completed
- F6 (Signal Mode UI) — completed
- F6.5 (Extract Indicator Calculators — MACD calculation) — completed
- F7 (EMA Trend Filter Handler) — completed

## Success Criteria

- All 6 MACD operators evaluate correctly with descriptive reasons
- Missing data returns `Passed = false` (fail closed)
- Unknown operator returns `Passed = false` with descriptive reason
- Max 1 MACD condition per strategy enforced by both backend validator and frontend UI
- BusinessRuleValidator validates MACD period ranges and fast < slow constraint
- MACD condition UI card renders with fast/slow/signal period inputs and operator dropdown
- "MACD Cross" template pre-populates correctly (12/26/9, cross_above_signal, TP 2%, SL 1.5%)
- Existing RSI and PriceVsEma conditions are not affected
- All backend tests pass (handler + validator + architecture)
- Frontend builds and lints cleanly

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-04-03T17:50:12Z | 2026-04-03T19:02:25Z |
| Plan Reviewer | plan-reviewed | 2026-04-03T19:05:54Z | 2026-04-03T19:14:12Z |
| Plan Implementer | implemented | 2026-04-03T19:45:18Z | 2026-04-03T20:03:38Z |
| Implementation Reviewer | complete | 2026-04-03T20:09:55Z | 2026-04-03T20:43:17Z |
