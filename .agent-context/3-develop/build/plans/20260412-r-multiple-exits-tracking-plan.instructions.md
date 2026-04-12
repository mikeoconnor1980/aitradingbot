---
applyTo: ".agent-context/3-develop/build/changes/20260412-r-multiple-exits-tracking-changes.md"
currentAgent: "None"
agentStartedAt: "2026-04-12T13:17:32Z"
status: "plan-reviewed"
lastUpdated: "2026-04-12T13:23:04Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: R-Multiple Exit Types & Trade Tracking

## Overview

Express take-profit targets as multiples of R (1R, 2R, 3R) instead of arbitrary percentages, record R-multiple metrics for every closed trade when using `RiskBased` mode (InitialR, RMultipleResult, MFE, MAE), and compute aggregate metrics (expectancy, profit factor, SQN) in backtest results.

## PBI Details

**PBI ID:** Draft
**Priority:** P2
**Depends On:** P1 R-Based Position Sizing (completed)
**Knowledge Source:** `33-risk-management-and-trade-sizing.md`

### User Story

> As a **trader**, I want to **set take-profit targets as multiples of my risk (R) and track R-multiple results for every trade** so that **I maintain a consistent reward-to-risk ratio and can evaluate my system's statistical edge over time**.

### Acceptance Criteria

- [ ] **Given** a long trade with R = $100, entry at $50,000, SL at 2% ($49,000), and R-multiple TP = 2R, **When** the TP trigger is placed, **Then** TP price = $52,000 (4% above entry)
- [ ] **Given** a short trade with R = $100, entry at $50,000, SL at 2% ($51,000), and R-multiple TP = 3R, **When** the TP trigger is placed, **Then** TP price = $47,000 (6% below entry)
- [ ] **Given** a trade closes with PnL = $250 and InitialR = $100, **When** metrics are recorded, **Then** RMultipleResult = 2.5
- [ ] **Given** a trade closes with PnL = -$100 and InitialR = $100, **When** metrics are recorded, **Then** RMultipleResult = -1.0
- [ ] **Given** a trade where price reached +3R before closing at +1.5R, **When** MFE/MAE are checked, **Then** MFE = 3.0R and MAE = 0 (never went negative)
- [ ] **Given** 10 closed R-tracked trades with R-multiples [2.1, -1.0, 1.5, -1.0, 3.0, -0.8, 2.0, -1.0, 1.8, -1.0], **When** aggregate metrics are calculated, **Then** expectancy ≈ 0.56R, win rate = 50%, profit factor ≈ 2.17
- [ ] **Given** a `PercentWallet` backtest, **When** results are displayed, **Then** R-multiple metrics section is not shown
- [ ] **Given** a `RiskBased` backtest, **When** results are displayed, **Then** R-multiple histogram and metrics are shown
- [ ] **Given** an R-multiple TP target < 1.0, **When** the strategy is saved, **Then** a warning is shown
- [ ] **Given** an R-multiple TP target < 0, **When** the strategy is validated, **Then** it is rejected

## Objectives

- Add `RMultiple` exit type for take-profit with validation
- Calculate TP price from R-multiple × stop-loss distance
- Capture InitialR (dollar risk) at trade entry in backtests
- Track per-trade MFE/MAE in R multiples during backtests
- Compute aggregate R-multiple metrics (expectancy, profit factor, SQN)
- Display R-multiple metrics and distribution in the frontend

### Discovery References

- `.agent-context/0-knowledge/33-risk-management-and-trade-sizing.md` — R-based sizing philosophy, R-multiple targets
- `.agent-context/0-knowledge/16-signal-contracts.md` — TakeProfit signal payload
- `.agent-context/0-knowledge/15-grid-controller.md` — Grid lifecycle, TP calculation
- `.agent-context/0-knowledge/18-backtesting-architecture.md` — Backtest pipeline, metrics

### Project Patterns

- `src/TradingApp.Application/StrategyAuthoring/Models/ExitRuleType.cs` — Enum to extend with `RMultiple`
- `src/TradingApp.Application/StrategyAuthoring/Models/ExitRuleConfig.cs` — Config record, `Value` field reused for R target
- `src/TradingApp.Application/Trading/Services/TriggerOrderManager.cs` — `CalculateTakeProfitPrice` to extend
- `src/TradingApp.Application/Trading/Services/GridController.cs` — Inline TP evaluation to extend
- `src/TradingApp.Application/Trading/Services/PositionSizeResolver.cs` — R computation (equity × riskPercent / 100)
- `src/TradingApp.Application/Trading/Services/StopLossDistanceResolver.cs` — Resolves SL distance %
- `src/TradingApp.Application/Trading/Models/GridState.cs` — Add InitialR for per-cycle tracking
- `src/TradingApp.Application/Backtesting/Models/BacktestTrade.cs` — Add R tracking fields
- `src/TradingApp.Application/Backtesting/Models/BacktestResult.cs` — Add aggregate R metrics
- `src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs` — RecordFill/CloseCompatibleTrades for R threading
- `src/TradingApp.Application/Backtesting/Services/BacktestMetricsCalculator.cs` — Compute R aggregate metrics
- `src/TradingApp.Application/Backtesting/Services/SimulatedExecutionEngine.cs` — UpdateUnrealisedPnl (MFE/MAE hook)
- `src/TradingApp.Application/Backtesting/Models/SimulatedPosition.cs` — Position lifecycle
- `src/TradingApp.Domain/Entities/BacktestRun.cs` — Entity, migration for R aggregate columns
- `src/TradingApp.Application/Backtesting/BacktestRunResponseMapper.cs` — Map new fields
- `src/TradingApp.Application/StrategyAuthoring/Validation/BusinessRuleValidator.cs` — R-multiple validation rules
- `src/TradingApp.Application/StrategyAuthoring/Validation/CrossFieldValidator.cs` — Cross-field: RMultiple TP requires RiskBased + SL
- `frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts` — TS types
- `frontend/trading-ui/src/app/core/models/backtest.model.ts` — Backtest TS types
- `frontend/trading-ui/src/app/features/strategy-builder/components/exit-rules-card/` — Exit rules form
- `frontend/trading-ui/src/app/features/backtesting/backtest-result/` — KPI cards
- `frontend/trading-ui/src/app/features/backtesting/trade-log-table/` — Trade table

### [ ] Phase 1: Domain Models & Validation

**Complexity**: Low | **Risk**: Low

- [ ] Task 1.1: Add `RMultiple` to `ExitRuleType` enum
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-01-details.md#task-11-add-rmultiple-to-exitruletype-enum

- [ ] Task 1.2: Add R-multiple validation rules to `BusinessRuleValidator`
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-01-details.md#task-12-add-r-multiple-validation-rules

- [ ] Task 1.3: Add cross-field validation for RMultiple TP
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-01-details.md#task-13-add-cross-field-validation-for-rmultiple-tp

- [ ] Task 1.4: Unit tests for validation rules
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-01-details.md#task-14-unit-tests-for-validation-rules

- [ ] Task 1.5: Build and verify
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-01-details.md#task-15-build-and-verify

### [ ] Phase 2: R-Multiple TP Price Calculation

**Complexity**: Medium | **Risk**: Low

- [ ] Task 2.1: Extend `TriggerOrderManager.CalculateTakeProfitPrice` for RMultiple
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-02-details.md#task-21-extend-calculatetakeprofitprice-for-rmultiple

- [ ] Task 2.2: Update GridController inline TP calculation for RMultiple
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-02-details.md#task-22-update-gridcontroller-inline-tp-calculation

- [ ] Task 2.3: Unit tests for R-multiple TP price calculation
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-02-details.md#task-23-unit-tests-for-r-multiple-tp-price-calculation

- [ ] Task 2.4: Build and verify
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-02-details.md#task-24-build-and-verify

### [ ] Phase 3: Per-Trade R Tracking & MFE/MAE

**Complexity**: High | **Risk**: Medium

- [ ] Task 3.1: Add InitialR to GridState
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-03-details.md#task-31-add-initialr-to-gridstate

- [ ] Task 3.2: Capture InitialR during grid deployment in GridController
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-03-details.md#task-32-capture-initialr-during-grid-deployment

- [ ] Task 3.3: Add R tracking fields to BacktestTrade
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-03-details.md#task-33-add-r-tracking-fields-to-backtesttrade

- [ ] Task 3.4: Thread InitialR through RecordFill
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-03-details.md#task-34-thread-initialr-through-recordfill

- [ ] Task 3.5: Add per-trade MFE/MAE tracking in BacktestRunner
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-03-details.md#task-35-add-per-trade-mfemae-tracking

- [ ] Task 3.6: Compute RMultipleResult and MFE/MAE at trade close
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-03-details.md#task-36-compute-rmultipleresult-and-mfemae-at-trade-close

- [ ] Task 3.7: Unit tests for R tracking and MFE/MAE
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-03-details.md#task-37-unit-tests-for-r-tracking-and-mfemae

- [ ] Task 3.8: Build and verify
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-03-details.md#task-38-build-and-verify

### [ ] Phase 4: Aggregate R Metrics & API

**Complexity**: Medium | **Risk**: Low

- [ ] Task 4.1: Add R-multiple aggregate fields to BacktestResult
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-04-details.md#task-41-add-r-multiple-aggregate-fields-to-backtestresult

- [ ] Task 4.2: Extend BacktestMetricsCalculator with R-multiple calculations
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-04-details.md#task-42-extend-backtestmetricscalculator

- [ ] Task 4.3: Add R-multiple columns to BacktestRun entity, migration, and update callers
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-04-details.md#task-43-add-r-multiple-columns-to-backtestrun-entity

- [ ] Task 4.4: Update BacktestRunResponse and BacktestTradeResponse DTOs
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-04-details.md#task-44-update-response-dtos

- [ ] Task 4.5: Update BacktestRunResponseMapper
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-04-details.md#task-45-update-backtestrunresponsemapper

- [ ] Task 4.6: Unit tests for aggregate metrics
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-04-details.md#task-46-unit-tests-for-aggregate-metrics

- [ ] Task 4.7: Build and verify
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-04-details.md#task-47-build-and-verify

### [ ] Phase 5: Frontend — Strategy Configuration

**Complexity**: Low | **Risk**: Low

- [ ] Task 5.1: Add `r_multiple` to TypeScript ExitRuleType
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-05-details.md#task-51-add-r_multiple-to-typescript-exitruletype

- [ ] Task 5.2: Enable R-multiple option in exit-rules-card
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-05-details.md#task-52-enable-r-multiple-option-in-exit-rules-card

- [ ] Task 5.3: Update strategy-mapper.service.ts
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-05-details.md#task-53-update-strategy-mapper

- [ ] Task 5.4: Add sub-1R warning
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-05-details.md#task-54-add-sub-1r-warning

- [ ] Task 5.5: Frontend build and lint
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-05-details.md#task-55-frontend-build-and-lint

### [ ] Phase 6: Frontend — Backtest Results Display

**Complexity**: Medium | **Risk**: Low

- [ ] Task 6.1: Update backtest TypeScript models
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-06-details.md#task-61-update-backtest-typescript-models

- [ ] Task 6.2: Add R-metric KPI cards to backtest-result component
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-06-details.md#task-62-add-r-metric-kpi-cards

- [ ] Task 6.3: Add R columns to trade-log-table
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-06-details.md#task-63-add-r-columns-to-trade-log-table

- [ ] Task 6.4: Add R-distribution histogram component
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-06-details.md#task-64-add-r-distribution-histogram-component

- [ ] Task 6.5: Conditional display based on RiskBased mode
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-06-details.md#task-65-conditional-display-based-on-riskbased-mode

- [ ] Task 6.6: Frontend build and lint
  - Details: .agent-context/3-develop/build/plans/details/20260412-r-multiple-exits-tracking-phase-06-details.md#task-66-frontend-build-and-lint

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: Domain Models & Validation | Low | Low |
| Phase 2: R-Multiple TP Price Calculation | Medium | Low |
| Phase 3: Per-Trade R Tracking & MFE/MAE | High | Medium |
| Phase 4: Aggregate R Metrics & API | Medium | Low |
| Phase 5: Frontend — Strategy Configuration | Low | Low |
| Phase 6: Frontend — Backtest Results Display | Medium | Low |
| **Overall** | **Medium** | **Low** |

### Scoping Notes

- `ExitRuleConfig.Value` is reused for the R-multiple target (e.g., 2.0 = 2R) — no new field needed on the config record
- R-multiple TP requires a stop-loss to compute the distance; this is enforced by cross-field validation
- BacktestTrade is stored as JSON blob (`TradesJson`) — new nullable fields are backward-compatible, no migration needed for trade data
- BacktestRun entity columns need a migration for aggregate R metrics (Expectancy, ProfitFactor, SQN)
- MFE/MAE tracking is per-trade using candle High/Low, tracked via a `Dictionary<string, TradeExcursionTracker>` in BacktestRunner
- Live MFE/MAE tracking (per price update) is deferred — this plan covers backtest MFE/MAE only
- R-distribution histogram uses simple CSS bar chart (lightweight-charts HistogramSeries is time-keyed, unsuitable for value-keyed distributions)

## Dependencies

- P1 R-Based Position Sizing (completed) — `PositionSizeType.RiskBased`, `RiskPerTradePercent`, `PositionSizeResolver.CalculateRiskBased`
- `StopLossDistanceResolver` — resolves SL % from exit config
- `lightweight-charts` — already installed for equity chart (histogram not used for R-distribution)

## Success Criteria

- All 10 PBI acceptance criteria pass
- R-multiple TP prices calculated correctly for long and short trades
- InitialR captured at entry, RMultipleResult computed at close
- MFE/MAE tracked per trade during backtest candle replay
- Aggregate metrics (Expectancy, Profit Factor, SQN) displayed for RiskBased backtests
- R-distribution histogram shown in backtest results
- Non-RiskBased backtests show no R-metric section
- All existing tests continue to pass
- Frontend builds and lints without errors

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-04-12T12:57:01Z | 2026-04-12T13:16:46Z |
| Plan Reviewer | plan-reviewed | 2026-04-12T13:17:32Z | 2026-04-12T13:23:04Z |
