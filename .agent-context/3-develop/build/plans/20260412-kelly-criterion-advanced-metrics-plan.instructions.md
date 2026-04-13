---
applyTo: ".agent-context/3-develop/build/changes/20260412-kelly-criterion-advanced-metrics-changes.md"
currentAgent: "None"
agentStartedAt: "2026-04-12T22:30:00Z"
status: "complete"
lastUpdated: "2026-04-12T22:45:00Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: Kelly Criterion & Advanced Backtest Metrics

## Overview

Add Kelly-optimal risk percentage, Half-Kelly, and Win/Loss R-Ratio to backtest results. Enhance the frontend to display SQN quality labels, Kelly comparison against configured risk, and low sample size warnings.

## PBI Details

**PBI ID:** Draft — P3
**Depends On:** PBI #7 (R-Multiple Exits & Trade Tracking) — ✅ Already Implemented

### Scope Adjustment

P2 (R-Multiple Exits & Trade Tracking) was implemented on 2026-04-12 and already delivered:
- `Expectancy`, `ProfitFactor`, `Sqn` — persisted to `BacktestRun`, computed in `BacktestMetricsCalculator`, returned in `BacktestRunResponse`
- `AvgWinR`, `AvgLossR`, `RWinRate`, `RDistribution` — computed and returned (not persisted)
- Frontend R-Multiple Metrics section with cards for all above

**True P3 scope** (net-new work):
- Backend: `KellyPercent`, `HalfKellyPercent`, `WinLossRRatio` calculation, persistence, and API response
- Backend: Add `ProfitFactor` and `Sqn` to `BacktestSummaryDto` list view
- Frontend: Kelly/Half-Kelly comparison display, SQN quality label, low sample size warning, advisory label
- Unit tests for Kelly calculation including edge cases

### Design Decision: ProfitFactor

The PBI draft specifies ProfitFactor "from raw PnL, not R-multiples." However, P2 already implements ProfitFactor from R-multiples (sum of positive R / |sum of negative R|). Since Kelly%, SQN, and Expectancy are only meaningful with RiskBased sizing (where R-multiples exist), keeping a single R-based ProfitFactor is consistent. Adding a separate raw-PnL-based ProfitFactor would create confusion. This plan uses the existing R-based ProfitFactor.

### Acceptance Criteria

- [x] ~~Given a backtest with RiskBased sizing and 50 trades, when viewing results, then Expectancy, ProfitFactor, and SQN are displayed~~ (already done in P2)
- [ ] Given a backtest with 60% win rate, avg winning R = 2.0, avg losing R = 1.0, when backtest completes, then Kelly% = 40% and Half-Kelly = 20%
- [ ] Given the configured riskPerTradePercent = 1%, when viewing results with Kelly% = 20%, then the UI shows "Your risk: 1% | Kelly suggests: 20% | Half-Kelly: 10%"
- [ ] Given a backtest with PercentWallet sizing, when viewing results, then Kelly%, Half-Kelly, and Win/Loss R-Ratio show "—" (null, since R-multiples are not tracked)
- [ ] Given a backtest with only 15 trades, when advanced metrics are displayed, then a warning "Low sample size (15 trades) — metrics may be unreliable" is shown
- [ ] Given a backtest with SQN = 3.2, when viewing results, then SQN is displayed as "3.2 — Excellent"
- [ ] Given a backtest with all losing trades, when Kelly% is calculated, then Kelly% = negative (no edge)
- [ ] Given a backtest where no losing trades exist, when Profit Factor and Kelly are calculated, then they display as "∞" / null appropriately
- [ ] Given the backtest completes, when the BacktestRun entity is saved, then KellyPercent, HalfKellyPercent, and WinLossRRatio are persisted

## Objectives

- Calculate Kelly%, Half-Kelly, and WinLossRRatio in BacktestMetricsCalculator
- Persist new metrics to BacktestRun entity via EF Core migration
- Enhance frontend R-Multiple Metrics section with Kelly comparison, SQN labels, and warnings
- Add ProfitFactor and Sqn to the backtest list view summary

### Discovery References

- `BacktestMetricsCalculator.CalculateRMetrics()` already computes Expectancy, ProfitFactor, Sqn, AvgWinR, AvgLossR, RWinRate — Kelly extends this method
- `BacktestRunResponseMapper.ComputeRMetrics()` is a duplicate of the calculator — Kelly must be added to both
- `BacktestRun.MarkCompleted()` and `BacktestRun.Create()` accept optional metric params — same pattern for Kelly
- `BacktestProcessorService` passes `result.*` to `MarkCompleted()` — same pattern for Kelly
- Frontend `backtest-result.component.html` has existing "R-Multiple Metrics" section — Kelly extends this

### Project Patterns

- `src/TradingApp.Application/Backtesting/Services/BacktestMetricsCalculator.cs` — Calculator with RMetricsSummary private class
- `src/TradingApp.Application/Backtesting/BacktestRunResponseMapper.cs` — Mapper with duplicate ComputeRMetrics and entity ?? rMetrics fallback
- `src/TradingApp.Domain/Entities/BacktestRun.cs` — Entity with MarkCompleted() and Create() factory methods
- `src/TradingApp.Application/Backtesting/Models/BacktestResult.cs` — Internal result DTO
- `src/TradingApp.Application/Backtesting/Models/BacktestRunResponse.cs` — API response model
- `src/TradingApp.Api/Models/BacktestSummaryDto.cs` — List view DTO
- `src/TradingApp.Application/Backtesting/Models/BacktestRunSummary.cs` — Query handler result model
- `src/TradingApp.Persistence/Repositories/BacktestRunRepository.cs` — GetPagedSummariesCoreAsync projection
- `src/TradingApp.Persistence/TradingAppDbContext.cs` — EF entity config with HasConversion<double?>()
- `src/TradingApp.Api/Services/BacktestProcessorService.cs` — Calls MarkCompleted() with result values
- `tests/TradingApp.Application.Tests/Backtesting/Services/BacktestMetricsCalculatorTests.cs` — Test class with CreateRTrackedTrades helper
- `frontend/trading-ui/src/app/core/models/backtest.model.ts` — BacktestResult interface
- `frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.ts` — Result display component
- `frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.html` — R-Multiple Metrics section template

### [x] Phase 1: Backend — Kelly Calculation, Persistence & Tests

**Complexity**: Medium | **Risk**: Low

- [x] Task 1.1: Add KellyPercent, HalfKellyPercent, WinLossRRatio to BacktestMetricsCalculator
  - Details: .agent-context/3-develop/build/plans/details/20260412-kelly-criterion-advanced-metrics-phase-01-details.md#task-11-add-kelly-to-backtest-metrics-calculator

- [x] Task 1.2: Add KellyPercent, HalfKellyPercent, WinLossRRatio to BacktestRunResponseMapper
  - Details: .agent-context/3-develop/build/plans/details/20260412-kelly-criterion-advanced-metrics-phase-01-details.md#task-12-add-kelly-to-backtest-run-response-mapper

- [x] Task 1.3: Add fields to BacktestResult and BacktestRunResponse
  - Details: .agent-context/3-develop/build/plans/details/20260412-kelly-criterion-advanced-metrics-phase-01-details.md#task-13-add-fields-to-backtest-result-and-backtest-run-response

- [x] Task 1.4: Add properties to BacktestRun entity and update MarkCompleted/Create
  - Details: .agent-context/3-develop/build/plans/details/20260412-kelly-criterion-advanced-metrics-phase-01-details.md#task-14-add-properties-to-backtest-run-entity

- [x] Task 1.5: Add EF Core configuration and migration
  - Details: .agent-context/3-develop/build/plans/details/20260412-kelly-criterion-advanced-metrics-phase-01-details.md#task-15-add-ef-core-configuration-and-migration

- [x] Task 1.6: Wire Kelly metrics through BacktestProcessorService
  - Details: .agent-context/3-develop/build/plans/details/20260412-kelly-criterion-advanced-metrics-phase-01-details.md#task-16-wire-kelly-metrics-through-backtest-processor-service

- [x] Task 1.7: Add ProfitFactor and Sqn to BacktestSummaryDto and list view projection
  - Details: .agent-context/3-develop/build/plans/details/20260412-kelly-criterion-advanced-metrics-phase-01-details.md#task-17-add-profitfactor-and-sqn-to-backtest-summary-dto

- [x] Task 1.8: Unit tests for Kelly calculation
  - Details: .agent-context/3-develop/build/plans/details/20260412-kelly-criterion-advanced-metrics-phase-01-details.md#task-18-unit-tests-for-kelly-calculation

- [x] Task 1.9: Build solution and run all tests
  - Details: .agent-context/3-develop/build/plans/details/20260412-kelly-criterion-advanced-metrics-phase-01-details.md#task-19-build-solution-and-run-all-tests

### [x] Phase 2: Frontend — Advanced Metrics Display

**Complexity**: Medium | **Risk**: Low

- [x] Task 2.1: Add Kelly fields to BacktestResult TypeScript interface and BacktestSummary
  - Details: .agent-context/3-develop/build/plans/details/20260412-kelly-criterion-advanced-metrics-phase-02-details.md#task-21-add-kelly-fields-to-typescript-interfaces

- [x] Task 2.2: Add Kelly comparison, SQN label, low sample warning to backtest-result component
  - Details: .agent-context/3-develop/build/plans/details/20260412-kelly-criterion-advanced-metrics-phase-02-details.md#task-22-add-kelly-display-to-backtest-result-component

- [x] Task 2.3: Add ProfitFactor and Sqn columns to backtest list view
  - Details: .agent-context/3-develop/build/plans/details/20260412-kelly-criterion-advanced-metrics-phase-02-details.md#task-23-add-advanced-columns-to-backtest-list-view

- [x] Task 2.4: Frontend build and lint
  - Details: .agent-context/3-develop/build/plans/details/20260412-kelly-criterion-advanced-metrics-phase-02-details.md#task-24-frontend-build-and-lint

## Scoping Summary

| Phase | Complexity | Risk |
|-------|-----------|------|
| Phase 1: Backend — Kelly Calculation, Persistence & Tests | Medium | Low |
| Phase 2: Frontend — Advanced Metrics Display | Medium | Low |
| **Total** | **Medium** | **Low** |

### Scoping Notes

- P2 already delivered Expectancy, ProfitFactor, Sqn, AvgWinR, AvgLossR, RWinRate, RDistribution end-to-end — P3 scope is significantly reduced
- Kelly formula is straightforward: `Kelly% = W - (1-W)/R` where W = rWinRate/100, R = AvgWinR/|AvgLossR|
- WinLossRRatio is the Kelly denominator: `AvgWinR / |AvgLossR|`
- All patterns (entity, calculator, mapper, response, migration) are well-established from P2
- Frontend extends existing R-Multiple Metrics section — no new components needed

## Dependencies

- .NET / EF Core (existing)
- Angular / Angular Material (existing)
- PBI #7 (R-Multiple Exits & Trade Tracking) — ✅ Already implemented

## Success Criteria

- Kelly%, Half-Kelly, WinLossRRatio calculated correctly from R-multiple data
- New metrics persisted to database via EF Core migration
- API returns Kelly metrics in BacktestRunResponse and summary metrics in BacktestSummaryDto
- Frontend displays Kelly comparison, SQN quality label, and low sample warning
- All unit tests pass including Kelly edge cases (negative edge, all wins, all losses, < 2 trades)
- Frontend builds and lints cleanly

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|----------|
| Implementation Planner | planned | 2026-04-12T20:05:15Z | 2026-04-12T20:14:35Z |
| Plan Reviewer | plan-reviewed | 2026-04-12T20:17:35Z | 2026-04-12T20:22:57Z |
| 3-Develop: 2 Implementer | implemented | 2026-04-12T20:50:50Z | 2026-04-12T22:07:18Z |
| 3-Develop: 3 Reviewer | complete | 2026-04-12T22:30:00Z | 2026-04-12T22:45:00Z |
