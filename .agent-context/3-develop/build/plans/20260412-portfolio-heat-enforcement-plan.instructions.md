---
applyTo: ".agent-context/3-develop/build/changes/20260412-portfolio-heat-enforcement-changes.md"
currentAgent: "None"
agentStartedAt: "2026-04-12T13:29:21Z"
status: "implemented"
lastUpdated: "2026-04-12T15:23:42Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: Portfolio Heat Enforcement

## Overview

Enforce a maximum portfolio-wide risk exposure (portfolio heat) to prevent catastrophic correlated drawdowns across simultaneous open positions. Heat is the sum of R (risk in USD) across ALL open positions. Includes risk engine enforcement, a new API endpoint, dashboard display, and backtest support.

## PBI Details

**PBI**: pbi-draft-p2-portfolio-heat-enforcement.md
**Priority**: P2
**Depends On**: P1 R-Based Position Sizing (completed)

### User Story

> As a **trader**, I want **the system to block new entries when my total open risk exceeds a configured threshold** so that **correlated positions can't cause a catastrophic account drawdown if they all fail simultaneously**.

### Acceptance Criteria

- **Given** `MaxPortfolioHeatPercent = 6` and equity = $10,000, and 5 open `RiskBased` positions each with R = $100 (heat = 5%), **When** a new entry arrives with R = $100, **Then** the entry is allowed (5% + 1% = 6% ≤ 6%)
- **Given** the same scenario with 6 positions already open (heat = 6%), **When** a new entry arrives with R = $100, **Then** the entry is blocked (6% + 1% = 7% > 6%)
- **Given** a `PercentWallet` position with $2,000 notional and 3% SL, **When** heat is calculated, **Then** its estimated R = $2,000 × 0.03 = $60
- **Given** heat = 6% and one position closes, **When** a new entry arrives, **Then** the entry is allowed (heat dropped below limit)
- **Given** `MaxPortfolioHeatPercent = 0` (disabled), **When** any entry arrives, **Then** the heat check is skipped
- **Given** a TakeProfit signal, **When** heat is at the limit, **Then** the signal passes (risk-reducing)
- **Given** the dashboard loads, **When** there are 3 open positions with R = $100 each and equity = $10,000, **Then** the heat gauge shows 3% with a green indicator
- **Given** a backtest with heat enforcement enabled, **When** a 7th position would exceed the 6% limit, **Then** the entry is blocked and reported in results

## Objectives

- Add `MaxPortfolioHeatPercent` configuration to `RiskLimitsConfig`
- Implement portfolio heat calculation from position risk data
- Enforce heat limits in `LiveRiskEngine` for live trading
- Create `GET /api/risk/portfolio-heat` endpoint for dashboard consumption
- Display heat indicator on the dashboard with green/amber/red thresholds
- Enforce heat limits in backtesting with blocked-signal reporting

### Discovery References

- `33-risk-management-and-trade-sizing.md` — R-based sizing, portfolio heat formula, default 6% cap
- `16-signal-contracts.md` — Signal types, risk-reducing bypass list
- `30-worker-execution-pipeline.md` — Pipeline flow: signals → risk engine → execution
- `14-strategy-runtime-model.md` — IRiskEngine role in the pipeline

### Project Patterns

- `src/TradingApp.Application/Trading/Services/LiveRiskEngine.cs` — Risk enforcement pattern (checks + state tracking)
- `src/TradingApp.Application/StrategyAuthoring/Models/RiskLimitsConfig.cs` — Configuration class to extend
- `src/TradingApp.Application/Trading/Services/PositionSizeResolver.cs` — R calculation logic
- `src/TradingApp.Application/Trading/Models/TradingSignal.cs` — Signal parameter structure
- `src/TradingApp.Api/Controllers/AccountController.cs` — Direct service injection controller pattern
- `src/TradingApp.Application/MarketData/Models/PositionDto.cs` — Exchange position fields (SL, margin, entry)
- `tests/TradingApp.Application.Tests/Trading/Services/LiveRiskEngineTests.cs` — Risk engine test pattern
- `tests/TradingApp.Api.Tests/Controllers/AccountControllerTests.cs` — Controller test with WebApplicationFactory
- `frontend/trading-ui/src/app/features/dashboard/account-summary/margin-ratio-indicator/` — Threshold indicator pattern (TS + HTML + SCSS)
- `frontend/trading-ui/src/app/core/services/hyperliquid-api.service.ts` — API service GET pattern

### [x] Phase 1: Configuration + Heat Calculation Core

**Complexity**: Low | **Risk**: Low

- [x] Task 1.1: Add `MaxPortfolioHeatPercent` to `RiskLimitsConfig`
  - Details: .agent-context/3-develop/build/plans/details/20260412-portfolio-heat-enforcement-phase-01-details.md#task-11-add-maxportfolioheatpercent-to-risklimitsconfig

- [x] Task 1.2: Create `PortfolioHeatEntry` model and `PortfolioHeatResult` model
  - Details: .agent-context/3-develop/build/plans/details/20260412-portfolio-heat-enforcement-phase-01-details.md#task-12-create-portfolioheatentry-and-portfolioheatresult-models

- [x] Task 1.3: Create `PortfolioHeatCalculator` static class
  - Details: .agent-context/3-develop/build/plans/details/20260412-portfolio-heat-enforcement-phase-01-details.md#task-13-create-portfolioheatcalculator-static-class

- [x] Task 1.4: Update `appsettings.json` with `RiskLimits` section
  - Details: .agent-context/3-develop/build/plans/details/20260412-portfolio-heat-enforcement-phase-01-details.md#task-14-update-appssettingsjson-with-risklimits-section

- [x] Task 1.5: Unit tests for `PortfolioHeatCalculator`
  - Details: .agent-context/3-develop/build/plans/details/20260412-portfolio-heat-enforcement-phase-01-details.md#task-15-unit-tests-for-portfolioheatcalculator

- [x] Task 1.6: Build verification
  - Details: .agent-context/3-develop/build/plans/details/20260412-portfolio-heat-enforcement-phase-01-details.md#task-16-build-verification

### [x] Phase 2: LiveRiskEngine Heat Enforcement

**Complexity**: High | **Risk**: Medium

- [x] Task 2.1: Add position/equity tracking methods to `IRiskEngine`
  - Details: .agent-context/3-develop/build/plans/details/20260412-portfolio-heat-enforcement-phase-02-details.md#task-21-add-positionequity-tracking-methods-to-iriskengine

- [x] Task 2.2: Implement heat tracking state in `LiveRiskEngine`
  - Details: .agent-context/3-develop/build/plans/details/20260412-portfolio-heat-enforcement-phase-02-details.md#task-22-implement-heat-tracking-state-in-liveriskengine

- [x] Task 2.3: Add `CheckPortfolioHeat` to `ValidateAsync` pipeline
  - Details: .agent-context/3-develop/build/plans/details/20260412-portfolio-heat-enforcement-phase-02-details.md#task-23-add-checkportfolioheat-to-validateasync-pipeline

- [x] Task 2.4: Add `estimatedRiskUsd` to signal parameters in `GridController`
  - Details: .agent-context/3-develop/build/plans/details/20260412-portfolio-heat-enforcement-phase-02-details.md#task-24-add-estimatedriskusd-to-signal-parameters-in-gridcontroller

- [x] Task 2.5: Wire `StrategyScheduler` to call `UpdatePortfolioState`
  - Details: .agent-context/3-develop/build/plans/details/20260412-portfolio-heat-enforcement-phase-02-details.md#task-25-wire-strategyscheduler-to-call-updateportfoliostate

- [x] Task 2.6: Wire `FillProcessor` to call `RecordPositionClosed`
  - Details: .agent-context/3-develop/build/plans/details/20260412-portfolio-heat-enforcement-phase-02-details.md#task-26-wire-fillprocessor-to-call-recordpositionclosed

- [x] Task 2.7: Unit tests for heat enforcement in `LiveRiskEngineTests`
  - Details: .agent-context/3-develop/build/plans/details/20260412-portfolio-heat-enforcement-phase-02-details.md#task-27-unit-tests-for-heat-enforcement

- [x] Task 2.8: Build + existing test verification
  - Details: .agent-context/3-develop/build/plans/details/20260412-portfolio-heat-enforcement-phase-02-details.md#task-28-build-and-existing-test-verification

### [x] Phase 3: API Endpoint

**Complexity**: Medium | **Risk**: Low

- [x] Task 3.1: Create `PortfolioHeatResponse` DTO
  - Details: .agent-context/3-develop/build/plans/details/20260412-portfolio-heat-enforcement-phase-03-details.md#task-31-create-portfolioheatresponse-dto

- [x] Task 3.2: Create `RiskController` with `GET /api/risk/portfolio-heat`
  - Details: .agent-context/3-develop/build/plans/details/20260412-portfolio-heat-enforcement-phase-03-details.md#task-32-create-riskcontroller

- [x] Task 3.3: Register `RiskLimitsConfig` in API `Program.cs`
  - Details: .agent-context/3-develop/build/plans/details/20260412-portfolio-heat-enforcement-phase-03-details.md#task-33-register-risklimitsconfig-in-api-programcs

- [x] Task 3.4: Controller integration tests
  - Details: .agent-context/3-develop/build/plans/details/20260412-portfolio-heat-enforcement-phase-03-details.md#task-34-controller-integration-tests

- [x] Task 3.5: Build + test verification
  - Details: .agent-context/3-develop/build/plans/details/20260412-portfolio-heat-enforcement-phase-03-details.md#task-35-build-and-test-verification

### [x] Phase 4: Frontend Dashboard

**Complexity**: Medium | **Risk**: Low

- [x] Task 4.1: Create `PortfolioHeatDto` TypeScript interface
  - Details: .agent-context/3-develop/build/plans/details/20260412-portfolio-heat-enforcement-phase-04-details.md#task-41-create-portfolioheatdto-typescript-interface

- [x] Task 4.2: Add `getPortfolioHeat()` to `HyperliquidApiService`
  - Details: .agent-context/3-develop/build/plans/details/20260412-portfolio-heat-enforcement-phase-04-details.md#task-42-add-getportfolioheat-to-hyperliquidapiservice

- [x] Task 4.3: Create `PortfolioHeatIndicatorComponent`
  - Details: .agent-context/3-develop/build/plans/details/20260412-portfolio-heat-enforcement-phase-04-details.md#task-43-create-portfolioheatindicatorcomponent

- [x] Task 4.4: Integrate into `AccountSummaryComponent`
  - Details: .agent-context/3-develop/build/plans/details/20260412-portfolio-heat-enforcement-phase-04-details.md#task-44-integrate-into-accountsummarycomponent

- [x] Task 4.5: Frontend build + lint verification
  - Details: .agent-context/3-develop/build/plans/details/20260412-portfolio-heat-enforcement-phase-04-details.md#task-45-frontend-build-and-lint-verification

### [x] Phase 5: Backtest Heat Enforcement

**Complexity**: High | **Risk**: Medium

- [x] Task 5.1: Create `BacktestRiskEngine` with heat checking
  - Details: .agent-context/3-develop/build/plans/details/20260412-portfolio-heat-enforcement-phase-05-details.md#task-51-create-backtestriskengine

- [x] Task 5.2: Register `BacktestRiskEngine` for backtest runs
  - Details: .agent-context/3-develop/build/plans/details/20260412-portfolio-heat-enforcement-phase-05-details.md#task-52-register-backtestriskengine-for-backtest-runs

- [x] Task 5.3: Add `HeatBlockedSignalCount` to `BacktestResult`
  - Details: .agent-context/3-develop/build/plans/details/20260412-portfolio-heat-enforcement-phase-05-details.md#task-53-add-heatblockedsignalcount-to-backtestresult

- [x] Task 5.4: Unit tests for backtest heat enforcement
  - Details: .agent-context/3-develop/build/plans/details/20260412-portfolio-heat-enforcement-phase-05-details.md#task-54-unit-tests-for-backtest-heat-enforcement

- [x] Task 5.5: Build + test verification
  - Details: .agent-context/3-develop/build/plans/details/20260412-portfolio-heat-enforcement-phase-05-details.md#task-55-build-and-test-verification

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: Configuration + Heat Calculation Core | Low | Low |
| Phase 2: LiveRiskEngine Heat Enforcement | High | Medium |
| Phase 3: API Endpoint | Medium | Low |
| Phase 4: Frontend Dashboard | Medium | Low |
| Phase 5: Backtest Heat Enforcement | High | Medium |
| **Total** | **Medium-High** | **Medium** |

### Scoping Notes

- LiveRiskEngine is a singleton shared across strategy schedulers — heat tracking aggregates across all strategies naturally
- API endpoint computes heat independently from exchange positions (not from engine state) — this is correct since the API uses PassThroughRiskEngine
- Backtest heat enforcement requires a new `BacktestRiskEngine` class since `PassThroughRiskEngine` has no validation logic
- Partial position closes (e.g., TakeProfit taking partial profits) may briefly undercount heat until the next signal cycle — acceptable for POC
- `estimatedRiskUsd` is added to signal parameters by `GridController` to enable R tracking without threading R through order/fill lifecycle
- Heat indicator frontend component directly follows the established `MarginRatioIndicatorComponent` pattern

## Dependencies

- .NET / C# (existing)
- Angular + Angular Material (existing)
- Hyperliquid exchange API for live position/equity data (existing `IHyperliquidAccountService`)
- MSTest + Moq + FluentAssertions ≤ v6 (existing test stack)

## Success Criteria

- All 8 acceptance criteria from the PBI pass
- Heat is enforced in live trading — new entries blocked when heat exceeds configured limit
- Risk-reducing signals always pass regardless of heat level
- API endpoint returns accurate heat data from exchange positions
- Dashboard displays heat percentage with green/amber/red colour coding
- Backtest reports blocked signals due to heat limit
- All existing tests continue to pass
- Frontend builds and lints without errors

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-04-12T07:00:00Z | 2026-04-12T13:06:32Z |
| Plan Implementer | implemented | 2026-04-12T13:29:21Z | 2026-04-12T15:23:42Z |
