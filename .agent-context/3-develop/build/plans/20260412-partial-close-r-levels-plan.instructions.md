---
applyTo: ".agent-context/3-develop/build/changes/20260412-partial-close-r-levels-changes.md"
currentAgent: "Plan Reviewer"
agentStartedAt: "2026-04-12T21:54:12Z"
status: "plan-in-review"
lastUpdated: "2026-04-12T21:54:12Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: Partial Close at R-Levels

## Overview

Scale out of winning signal-based positions at configurable R-multiple milestones, locking in profit in tranches while letting the remaining position run. Each tranche is placed as a separate TP trigger order on Hyperliquid with a partial size. Existing SL/trailing stop config continues to manage the remaining position.

## PBI Details

**PBI ID:** Draft  
**Status:** Draft  
**Priority:** P3  
**Depends On:** P2 R-Multiple Exit Types & Trade Tracking

### User Story

> As a **trader**, I want to **automatically take partial profits at configurable R-level milestones** so that **I lock in gains progressively while giving the remainder of the position room to capture larger moves**.

### Acceptance Criteria

- [ ] **Given** a long position of 100 units with R = $100, entry at $50,000, SL at 2%, and partial closes [25% at 1R, 25% at 2R, 50% at 3R], **When** trigger orders are placed, **Then** 3 TP triggers: 25 units at $51,000 (1R), 25 units at $52,000 (2R), 50 units at $53,000 (3R)
- [ ] **Given** the 1R tranche fills (25 units closed at $51,000), **When** the position is updated, **Then** remaining position = 75 units and SL trigger order size is updated to 75 units
- [ ] **Given** the 2R tranche fills (25 more units closed), **When** the position is updated, **Then** remaining position = 50 units
- [ ] **Given** the final tranche fills at 3R, **When** the trade closes completely, **Then** total R-multiple = (25×1R + 25×2R + 50×3R) / 100 = 2.25R
- [ ] **Given** price reverses after the 1R partial and SL fires at the original SL level, **When** the remaining 75 units close at -1R, **Then** total result = +0.25R (1R partial) - 0.75R (SL on remainder) = -0.50R (better than -1.0R without partials)
- [ ] **Given** `PositionSizeType = PercentWallet`, **When** exit config is loaded, **Then** partial close UI is hidden
- [ ] **Given** partial close tranches summing to 110%, **When** the user tries to save, **Then** validation error: "Partial close percentages must not exceed 100%"
- [ ] **Given** a backtest with partial closes config, **When** candle high crosses the 1R level, **Then** the 1R tranche is simulated as filled
- [ ] **Given** `IncludePartialCloses = true` in the optimizer, **When** candidates are generated, **Then** some use partial close tranches and some use no partial closes (full TP/trailing only)

## Objectives

- Add `PartialCloses` list to `ExitConfig` with per-tranche `AtRMultiple` and `ClosePercent`
- Place multiple exchange-native TP trigger orders (one per tranche) on Hyperliquid
- Simulate partial closes in backtesting with correct R-multiple tracking
- Add optimizer support for generating candidates with/without partial closes
- Add frontend tranche editor in strategy builder exit config section

### Discovery References

- Knowledge file `33-risk-management-and-trade-sizing.md` explicitly defines partial-close at R-levels concept with example tranches (25%@1R, 25%@2R, 50%@3R+trail)
- `ExitConfig` is stored as JSON blob in `Strategy.ConfigJson` — no DB migration needed for new fields
- `TriggerOrderManager.CalculateTakeProfitPrice` already handles `ExitRuleType.RMultiple` price calculation
- `BacktestRunner.CloseCompatibleTrades` already supports partial fill matching (FIFO)
- `SimulatedExecutionEngine.PlaceTriggerOrderAsync` accepts explicit `size` parameter — partial triggers structurally supported
- Hyperliquid accepts multiple reduce-only triggers for the same asset simultaneously

### Project Patterns

- `src/TradePilot.Application/StrategyAuthoring/Models/ExitConfig.cs` — Current single-rule exit config (extend with PartialCloses list)
- `src/TradePilot.Application/StrategyAuthoring/Models/ExitRuleConfig.cs` — Exit rule structure
- `src/TradePilot.Application/Trading/Services/TriggerOrderManager.cs` — TP/SL trigger lifecycle including R-multiple price calc
- `src/TradePilot.Application/Trading/Models/ProtectionOrderState.cs` — Single TP/SL order tracking (extend for multiple TPs)
- `src/TradePilot.Application/Trading/Services/SignalController.cs` — Signal-mode exit evaluation (extend for partial TP signals)
- `src/TradePilot.Application/Trading/Services/LivePositionManager.cs` — Signal → order execution
- `src/TradePilot.Application/Trading/Services/FillProcessor.cs` — Fill fan-out and lifecycle transitions
- `src/TradePilot.Application/Backtesting/Services/SimulatedExecutionEngine.cs` — Trigger fill simulation
- `src/TradePilot.Application/Backtesting/Services/BacktestRunner.cs` — R-metric tracking, CloseCompatibleTrades
- `src/TradePilot.Application/Optimization/Models/ParameterBounds.cs` — Optimizer parameter bounds
- `src/TradePilot.Application/Optimization/Services/StrategyConfigGenerator.cs` — Candidate generation
- `frontend/trading-ui/src/app/features/strategy-builder/components/exit-rules-card/` — Exit config UI component
- `frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts` — Frontend strategy models
- `frontend/trading-ui/src/app/features/strategy-builder/components/entry-conditions-card/` — FormArray add/remove pattern reference
- `tests/TradePilot.Application.Tests/StrategyAuthoring/Validation/BusinessRuleValidatorTests.cs` — Exit config validation tests
- `tests/TradePilot.Application.Tests/Backtesting/Services/SimulatedExecutionEngineTests.cs` — Trigger fill simulation tests
- `tests/TradePilot.Application.Tests/Backtesting/Services/BacktestRunnerTests.cs` — R-multiple computation tests

### [ ] Phase 1: Domain Models, Validation & Serialization

**Complexity**: Medium | **Risk**: Low

- [ ] Task 1.1: Create `PartialCloseLevel` record and extend `ExitConfig`
  - Details: .agent-context/3-develop/build/plans/details/20260412-partial-close-r-levels-phase-01-details.md#task-11-create-partialcloselevel-record-and-extend-exitconfig

- [ ] Task 1.2: Add validation rules for partial close configuration
  - Details: .agent-context/3-develop/build/plans/details/20260412-partial-close-r-levels-phase-01-details.md#task-12-add-validation-rules-for-partial-close-configuration

- [ ] Task 1.3: Add unit tests for partial close validation
  - Details: .agent-context/3-develop/build/plans/details/20260412-partial-close-r-levels-phase-01-details.md#task-13-add-unit-tests-for-partial-close-validation

- [ ] Task 1.4: Verify JSON serialization backward compatibility
  - Details: .agent-context/3-develop/build/plans/details/20260412-partial-close-r-levels-phase-01-details.md#task-14-verify-json-serialization-backward-compatibility

- [ ] Task 1.5: Run architecture tests
  - Details: .agent-context/3-develop/build/plans/details/20260412-partial-close-r-levels-phase-01-details.md#task-15-run-architecture-tests

### [ ] Phase 2: Backtest Simulation

**Complexity**: High | **Risk**: Medium

- [ ] Task 2.1: Extend `SimulatedExecutionEngine` to process multiple TP triggers per candle
  - Details: .agent-context/3-develop/build/plans/details/20260412-partial-close-r-levels-phase-02-details.md#task-21-extend-simulatedexecutionengine-for-multiple-tp-triggers

- [ ] Task 2.2: Extend `BacktestPositionManager` to place partial TP triggers
  - Details: .agent-context/3-develop/build/plans/details/20260412-partial-close-r-levels-phase-02-details.md#task-22-extend-backtestpositionmanager-to-place-partial-tp-triggers

- [ ] Task 2.3: Extend `BacktestRunner` R-metric tracking for partial closes
  - Details: .agent-context/3-develop/build/plans/details/20260412-partial-close-r-levels-phase-02-details.md#task-23-extend-backtestrunner-r-metric-tracking-for-partial-closes

- [ ] Task 2.4: Add unit tests for backtest partial close simulation
  - Details: .agent-context/3-develop/build/plans/details/20260412-partial-close-r-levels-phase-02-details.md#task-24-add-unit-tests-for-backtest-partial-close-simulation

- [ ] Task 2.5: Run architecture tests
  - Details: .agent-context/3-develop/build/plans/details/20260412-partial-close-r-levels-phase-02-details.md#task-25-run-architecture-tests

### [ ] Phase 3: Live Execution

**Complexity**: High | **Risk**: High

- [ ] Task 3.1: Extend `ProtectionOrderState` for multiple TP trigger orders
  - Details: .agent-context/3-develop/build/plans/details/20260412-partial-close-r-levels-phase-03-details.md#task-31-extend-protectionorderstate-for-multiple-tp-triggers

- [ ] Task 3.2: Extend `TriggerOrderManager` to place partial TP triggers
  - Details: .agent-context/3-develop/build/plans/details/20260412-partial-close-r-levels-phase-03-details.md#task-32-extend-triggerordermanager-for-partial-tp-triggers

- [ ] Task 3.3: Extend `FillProcessor` for partial TP fill handling
  - Details: .agent-context/3-develop/build/plans/details/20260412-partial-close-r-levels-phase-03-details.md#task-33-extend-fillprocessor-for-partial-tp-fills

- [ ] Task 3.4: Update `TradingSession` fill callback for SL size adjustment
  - Details: .agent-context/3-develop/build/plans/details/20260412-partial-close-r-levels-phase-03-details.md#task-34-update-tradingsession-fill-callback-for-sl-size-adjustment

- [ ] Task 3.5: Add unit tests for live partial close execution
  - Details: .agent-context/3-develop/build/plans/details/20260412-partial-close-r-levels-phase-03-details.md#task-35-add-unit-tests-for-live-partial-close-execution

- [ ] Task 3.6: Run architecture tests
  - Details: .agent-context/3-develop/build/plans/details/20260412-partial-close-r-levels-phase-03-details.md#task-36-run-architecture-tests

### [ ] Phase 4: Optimizer Integration

**Complexity**: Medium | **Risk**: Low

- [ ] Task 4.1: Add `IncludePartialCloses` to `ParameterBounds`
  - Details: .agent-context/3-develop/build/plans/details/20260412-partial-close-r-levels-phase-04-details.md#task-41-add-includepartialcloses-to-parameterbounds

- [ ] Task 4.2: Extend `StrategyConfigGenerator` for partial close candidates
  - Details: .agent-context/3-develop/build/plans/details/20260412-partial-close-r-levels-phase-04-details.md#task-42-extend-strategyconfiggenerator-for-partial-close-candidates

- [ ] Task 4.3: Add unit tests for optimizer partial close generation
  - Details: .agent-context/3-develop/build/plans/details/20260412-partial-close-r-levels-phase-04-details.md#task-43-add-unit-tests-for-optimizer-partial-close-generation

- [ ] Task 4.4: Run architecture tests
  - Details: .agent-context/3-develop/build/plans/details/20260412-partial-close-r-levels-phase-04-details.md#task-44-run-architecture-tests

### [ ] Phase 5: Frontend Tranche Editor

**Complexity**: Medium | **Risk**: Low

- [ ] Task 5.1: Add `PartialCloseTranche` model and extend `ExitConfig` interface
  - Details: .agent-context/3-develop/build/plans/details/20260412-partial-close-r-levels-phase-05-details.md#task-51-add-partialclosetranche-model-and-extend-exitconfig

- [ ] Task 5.2: Add tranche `FormArray` to strategy builder form and mapper
  - Details: .agent-context/3-develop/build/plans/details/20260412-partial-close-r-levels-phase-05-details.md#task-52-add-tranche-formarray-to-strategy-builder

- [ ] Task 5.3: Add tranche editor UI to exit-rules-card component
  - Details: .agent-context/3-develop/build/plans/details/20260412-partial-close-r-levels-phase-05-details.md#task-53-add-tranche-editor-ui-to-exit-rules-card

- [ ] Task 5.4: Add frontend validation for tranches
  - Details: .agent-context/3-develop/build/plans/details/20260412-partial-close-r-levels-phase-05-details.md#task-54-add-frontend-validation-for-tranches

- [ ] Task 5.5: Run frontend build and lint
  - Details: .agent-context/3-develop/build/plans/details/20260412-partial-close-r-levels-phase-05-details.md#task-55-run-frontend-build-and-lint

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: Domain Models, Validation & Serialization | Medium | Low |
| Phase 2: Backtest Simulation | High | Medium |
| Phase 3: Live Execution | High | High |
| Phase 4: Optimizer Integration | Medium | Low |
| Phase 5: Frontend Tranche Editor | Medium | Low |
| **Total** | **High** | **Medium** |

### Scoping Notes

- No DB migration needed — `ExitConfig` is stored as JSON in `Strategy.ConfigJson`; new fields deserialize as defaults for existing strategies
- Partial closes only apply when `PositionSizeType = RiskBased` and strategy mode is `Signal` (not Grid)
- No automatic breakeven SL move after first partial (SL stays as configured in ExitConfig.StopLoss) — per PBI scope
- Hyperliquid supports multiple reduce-only trigger orders for same asset simultaneously
- `BacktestRunner.CloseCompatibleTrades` already handles partial FIFO fill matching — minimal change needed
- Depends on P2 R-Multiple Exit Types & Trade Tracking being complete
- Phase 3 includes changes to `TradePilot.Worker` (`TradingSession` fill callback) in addition to `TradePilot.Application`

## Dependencies

- P2 R-Multiple Exit Types & Trade Tracking (prerequisite PBI)
- Hyperliquid exchange API (reduce-only trigger orders)
- `ExitRuleType.RMultiple` enum value (already exists)
- `TriggerOrderManager.CalculateTakeProfitPrice` R-multiple formula (already exists)

## Success Criteria

- All acceptance criteria pass
- Partial close tranches configurable via strategy builder UI
- Backtest correctly simulates partial fills at R-levels with accurate R-multiple tracking
- Live execution places multiple TP trigger orders and adjusts SL size after each fill
- Optimizer can generate candidates with/without partial closes
- Existing strategies without partial closes continue to work unchanged (backward compatible)
- All unit tests pass
- Frontend builds and lints clean

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-04-12T15:00:00Z | 2026-04-12T21:28:31Z |
| Plan Reviewer | plan-in-review | 2026-04-12T21:54:12Z | - |
