---
applyTo: ".agent-context/3-develop/build/changes/20260412-volatility-scaled-atr-initial-stop-changes.md"
currentAgent: "None"
agentStartedAt: "2026-04-13T10:59:29Z"
status: "implemented"
lastUpdated: "2026-04-13T11:27:07Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: Volatility-Scaled Initial Stop Loss (ATR-Based)

## Overview

Add `AtrInitial` exit rule type that sets the initial stop-loss distance using ATR × multiplier, locked at entry candle close. When combined with R-based position sizing, this keeps dollar risk (R) constant while adjusting position size inversely with volatility.

## PBI Details

**PBI ID:** Draft P3
**Depends On:** PBI #1 (R-Based Position Sizing)

As a **trader**, I want **my initial stop-loss distance to adapt to current market volatility using ATR** so that **I'm not stopped out prematurely in volatile conditions and my position sizes adjust automatically to maintain consistent dollar risk**.

### Acceptance Criteria

- **Given** `StopLoss.Type = AtrInitial`, ATR(14) = $500, entry price = $50,000, multiplier = 2.0, **When** a long entry is placed, **Then** initial SL price = $49,000 (entry − $1,000)
- **Given** `PositionSizeType = RiskBased`, risk = 1% of $10,000 equity (R = $100), and ATR-derived SL distance = 2% of entry, **When** position size is calculated, **Then** notional = $100 / 0.02 = $5,000
- **Given** ATR doubles from $500 to $1,000 (high volatility period), **When** a new entry is calculated, **Then** SL distance doubles to $2,000 and position size halves to $2,500
- **Given** `PositionSizeType = PercentWallet` and `StopLoss.Type = AtrInitial`, **When** an entry is placed, **Then** the SL price is set by ATR but position size uses PercentWallet logic (no R-based sizing)
- **Given** insufficient candle history for ATR(14), **When** an entry signal fires, **Then** SL falls back to `FixedPercent` using `Value` and a warning is logged
- **Given** `StopLoss.Type = AtrInitial` and a trailing stop is also configured, **When** the trailing stop tightens past the initial ATR SL, **Then** the tighter trailing SL is used
- **Given** ATR = $500 at the entry candle, **When** ATR changes to $800 on the next candle, **Then** the initial SL remains at the original ATR-derived price (locked at entry)
- **Given** the optimizer has `AtrMultiplierRange: { Min: 1.0, Max: 3.0, Step: 0.5 }`, **When** a sweep runs, **Then** strategy configs are generated with AtrMultiplier values 1.0, 1.5, 2.0, 2.5, 3.0

## Objectives

- Add `AtrInitial` variant to `ExitRuleType` enum and extend `ExitRuleConfig` with `AtrPeriod`
- Implement ATR-based initial stop-loss distance calculation locked at entry
- Integrate with R-based position sizing through `StopLossDistanceResolver`
- Add exit evaluation branches in `GridController` and `SignalController` using locked `AtrAtEntry` state
- Handle exchange-native trigger orders (initial placement, skip update for locked stops)
- Add optimizer support for sweeping `AtrMultiplier` values with `AtrInitial` stop type
- Implement fallback to `FixedPercent` when ATR is unavailable
- Add comprehensive unit tests for all scenarios

### Discovery References

- `ExitRuleType` enum currently has: `FixedPercent`, `SwingLow`, `AtrTrailing`, `RMultiple`
- `ExitRuleConfig` already has `AtrMultiplier` field (reusable) — needs new `AtrPeriod` field
- `StopLossDistanceResolver.Resolve` has switch-expression with `AtrTrailing` case — new `AtrInitial` case is mathematically identical
- `GridController.EvaluateExitConditions` has `isAtrTrailing` and `isFixedStopLoss` branches — needs new `isAtrInitial` branch
- `SignalController.EvaluateExitConditions` mirrors `GridController` — same changes needed
- `TriggerOrderManager.CalculateStopLossPrice` has `AtrTrailing` branch using candle high — `AtrInitial` must use entry price
- `TriggerOrderManager.UpdateProtectionOrdersAsync` must skip SL update for `AtrInitial` (locked stop)
- `GridState` needs `AtrAtEntry` field to capture ATR at deployment time
- `ParameterBounds` needs `AtrMultiplierOptions` and `StopLossType` fields
- `StrategyConfigGenerator.GenerateExitConfig` is hardcoded to `FixedPercent` — needs conditional `AtrInitial` support
- `BusinessRuleValidator` validates `AtrMultiplier` only for `AtrTrailing` — must also validate for `AtrInitial`
- ATR period is hardcoded to 14 in both context builders via `IncrementalAtr(14)` — `AtrPeriod` field is added to config for future use but actual pipeline continues using period 14

### Project Patterns

- `src/TradingApp.Application/StrategyAuthoring/Models/ExitRuleType.cs` - Enum to extend with `AtrInitial`
- `src/TradingApp.Application/StrategyAuthoring/Models/ExitRuleConfig.cs` - Config record, add `AtrPeriod` field
- `src/TradingApp.Application/Trading/Services/StopLossDistanceResolver.cs` - SL% resolver, add `AtrInitial` case
- `src/TradingApp.Application/Trading/Services/GridController.cs` - Grid exit evaluation, add `AtrInitial` branch
- `src/TradingApp.Application/Trading/Services/SignalController.cs` - Signal exit evaluation, add `AtrInitial` branch
- `src/TradingApp.Application/Trading/Services/TriggerOrderManager.cs` - Exchange trigger orders, add `AtrInitial` case
- `src/TradingApp.Application/Trading/Models/GridState.cs` - Grid state, add `AtrAtEntry` field
- `src/TradingApp.Application/StrategyAuthoring/Validation/BusinessRuleValidator.cs` - Validation, extend for `AtrInitial`
- `src/TradingApp.Application/Optimization/Models/ParameterBounds.cs` - Optimizer bounds, add ATR fields
- `src/TradingApp.Application/Optimization/Services/StrategyConfigGenerator.cs` - Config generator, add `AtrInitial` support
- `tests/TradingApp.Application.Tests/Trading/Services/TriggerOrderManagerTests.cs` - Test pattern for SL calculations
- `tests/TradingApp.Application.Tests/Trading/Services/PositionSizeResolverTests.cs` - Test pattern for sizing
- `tests/TradingApp.Application.Tests/Trading/Services/GridControllerTests.cs` - Test pattern for grid lifecycle

### [x] Phase 1: Domain Model, Configuration & Validation

**Complexity**: Low | **Risk**: Low

- [x] Task 1.1: Add `AtrInitial` to `ExitRuleType` enum
  - Details: .agent-context/3-develop/build/plans/details/20260412-volatility-scaled-atr-initial-stop-phase-01-details.md#task-11-add-atrinitial-to-exitruletype-enum

- [x] Task 1.2: Add `AtrPeriod` field to `ExitRuleConfig`
  - Details: .agent-context/3-develop/build/plans/details/20260412-volatility-scaled-atr-initial-stop-phase-01-details.md#task-12-add-atrperiod-field-to-exitruleconfig

- [x] Task 1.3: Add `AtrInitial` validation to `BusinessRuleValidator`
  - Details: .agent-context/3-develop/build/plans/details/20260412-volatility-scaled-atr-initial-stop-phase-01-details.md#task-13-add-atrinitial-validation-to-businessrulevalidator

- [x] Task 1.4: Add `AtrAtEntry` field to `GridState`
  - Details: .agent-context/3-develop/build/plans/details/20260412-volatility-scaled-atr-initial-stop-phase-01-details.md#task-14-add-atratentry-field-to-gridstate

- [x] Task 1.5: Unit tests for validation rules
  - Details: .agent-context/3-develop/build/plans/details/20260412-volatility-scaled-atr-initial-stop-phase-01-details.md#task-15-unit-tests-for-validation-rules

- [x] Task 1.6: Build and run architecture tests
  - Details: .agent-context/3-develop/build/plans/details/20260412-volatility-scaled-atr-initial-stop-phase-01-details.md#task-16-build-and-run-architecture-tests

### [x] Phase 2: SL Distance Resolution & Exit Evaluation

**Complexity**: Medium | **Risk**: Medium

- [x] Task 2.1: Add `AtrInitial` case to `StopLossDistanceResolver`
  - Details: .agent-context/3-develop/build/plans/details/20260412-volatility-scaled-atr-initial-stop-phase-02-details.md#task-21-add-atrinitial-case-to-stoplossdistanceresolver

- [x] Task 2.2: Capture `AtrAtEntry` in `GridController` at grid deployment
  - Details: .agent-context/3-develop/build/plans/details/20260412-volatility-scaled-atr-initial-stop-phase-02-details.md#task-22-capture-atratentry-in-gridcontroller-at-grid-deployment

- [x] Task 2.3: Add `AtrInitial` exit evaluation branch in `GridController`
  - Details: .agent-context/3-develop/build/plans/details/20260412-volatility-scaled-atr-initial-stop-phase-02-details.md#task-23-add-atrinitial-exit-evaluation-branch-in-gridcontroller

- [x] Task 2.4: Add `AtrInitial` exit evaluation branch in `SignalController`
  - Details: .agent-context/3-develop/build/plans/details/20260412-volatility-scaled-atr-initial-stop-phase-02-details.md#task-24-add-atrinitial-exit-evaluation-branch-in-signalcontroller

- [x] Task 2.5: Fix `isFixedStopLoss` guard to exclude `AtrInitial`
  - Details: .agent-context/3-develop/build/plans/details/20260412-volatility-scaled-atr-initial-stop-phase-02-details.md#task-25-fix-isfixedstoploss-guard-to-exclude-atrinitial

- [x] Task 2.6: Unit tests for SL distance resolution and exit evaluation
  - Details: .agent-context/3-develop/build/plans/details/20260412-volatility-scaled-atr-initial-stop-phase-02-details.md#task-26-unit-tests-for-sl-distance-resolution-and-exit-evaluation

- [x] Task 2.7: Build and run tests
  - Details: .agent-context/3-develop/build/plans/details/20260412-volatility-scaled-atr-initial-stop-phase-02-details.md#task-27-build-and-run-tests

### [x] Phase 3: Trigger Order Management

**Complexity**: Medium | **Risk**: Low

- [x] Task 3.1: Add `AtrInitial` case to `CalculateStopLossPrice`
  - Details: .agent-context/3-develop/build/plans/details/20260412-volatility-scaled-atr-initial-stop-phase-03-details.md#task-31-add-atrinitial-case-to-calculatestoplossprice

- [x] Task 3.2: Skip SL update for `AtrInitial` in `UpdateProtectionOrdersAsync`
  - Details: .agent-context/3-develop/build/plans/details/20260412-volatility-scaled-atr-initial-stop-phase-03-details.md#task-32-skip-sl-update-for-atrinitial-in-updateprotectionordersasync

- [x] Task 3.3: Unit tests for trigger order management
  - Details: .agent-context/3-develop/build/plans/details/20260412-volatility-scaled-atr-initial-stop-phase-03-details.md#task-33-unit-tests-for-trigger-order-management

- [x] Task 3.4: Build and run tests
  - Details: .agent-context/3-develop/build/plans/details/20260412-volatility-scaled-atr-initial-stop-phase-03-details.md#task-34-build-and-run-tests

### [x] Phase 4: Optimizer Support

**Complexity**: Medium | **Risk**: Low

- [x] Task 4.1: Add ATR fields to `ParameterBounds`
  - Details: .agent-context/3-develop/build/plans/details/20260412-volatility-scaled-atr-initial-stop-phase-04-details.md#task-41-add-atr-fields-to-parameterbounds

- [x] Task 4.2: Extend `StrategyConfigGenerator.GenerateExitConfig` for `AtrInitial`
  - Details: .agent-context/3-develop/build/plans/details/20260412-volatility-scaled-atr-initial-stop-phase-04-details.md#task-42-extend-strategyconfiggeneratorgenerateexitconfig-for-atrinitial

- [x] Task 4.3: Unit tests for optimizer support
  - Details: .agent-context/3-develop/build/plans/details/20260412-volatility-scaled-atr-initial-stop-phase-04-details.md#task-43-unit-tests-for-optimizer-support

- [x] Task 4.4: Build and run all tests
  - Details: .agent-context/3-develop/build/plans/details/20260412-volatility-scaled-atr-initial-stop-phase-04-details.md#task-44-build-and-run-all-tests

## Scoping Summary

| Phase | Complexity | Risk |
|-------|----------|------|
| Phase 1: Domain Model, Configuration & Validation | Low | Low |
| Phase 2: SL Distance Resolution & Exit Evaluation | Medium | Medium |
| Phase 3: Trigger Order Management | Medium | Low |
| Phase 4: Optimizer Support | Medium | Low |
| **Total** | **Medium** | **Low** |

### Scoping Notes

- `AtrPeriod` is added to `ExitRuleConfig` for configuration completeness and optimizer sweeping, but the actual ATR calculation pipeline (`IncrementalAtr(14)`) remains hardcoded to period 14. Making the ATR period dynamically configurable in the indicator pipeline is a separate concern.
- `AtrAtEntry` is in-memory only in `GridState`. On worker restart, the ATR stop price cannot be recomputed from state. The `TriggerOrderManager` exchange trigger order remains in place, so the stop is still enforced. Full persistence to DB is deferred as a follow-up.
- The `AtrInitial` + trailing stop combo is handled implicitly: both `GridController` and `SignalController` evaluate `isAtrTrailing` and `isAtrInitial` independently. If `isAtrTrailing` tightens past the `AtrInitial` stop, the trailing stop triggers first.
- `StrategyInterpreterPrompt` (LLM config generation) awareness of `atr_initial` is deferred — not in scope for this PBI.

## Dependencies

- PBI #1 (R-Based Position Sizing) must be implemented — `PositionSizeResolver`, `StopLossDistanceResolver`, `RiskBased` mode, `InitialRDollars` all exist
- No new NuGet packages required
- No database migrations required (in-memory state only)
- No frontend changes required
- No DevOps/infrastructure changes required

## Success Criteria

- All 8 acceptance criteria pass via unit tests
- `AtrInitial` stop-loss distance inversely scales position size with volatility when `RiskBased` sizing is active
- ATR value is locked at entry — does not change on subsequent candles
- Fallback to `FixedPercent` when ATR is unavailable
- Exchange trigger orders are placed at initial ATR stop price and NOT updated on subsequent candles
- Optimizer can sweep `AtrMultiplier` values and generate `AtrInitial` configs
- All existing tests continue to pass

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|----------|
| Implementation Planner | planned | 2026-04-12T21:13:27Z | 2026-04-12T21:26:09Z |
| Plan Reviewer | plan-reviewed | 2026-04-12T21:30:00Z | 2026-04-12T21:35:00Z |
| Plan Implementer | implemented | 2026-04-13T10:59:29Z | 2026-04-13T11:27:07Z |
