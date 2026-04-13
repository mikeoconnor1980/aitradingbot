applyTo: ".agent-context/3-develop/build/changes/20260412-adaptive-risk-drawdown-changes.md"
currentAgent: "None"
agentStartedAt: "2026-04-12T20:21:55Z"
status: "implemented"
lastUpdated: "2026-04-12T21:30:24Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: Adaptive Risk (Drawdown-Adjusted)

## Overview

Implement drawdown-based adaptive risk scaling with per-strategy HWM tracking, configurable drawdown tiers with scaling factors, an independent drawdown circuit breaker, backtest support, and dashboard display.

## PBI Details

**PBI ID:** Draft (P3)
**Depends On:** PBI #1 (R-Based Position Sizing)

As a **trader**, I want **the system to automatically reduce my risk percentage during drawdowns** so that **losses are contained beyond the natural anti-martingale effect, and trading halts before catastrophic loss**.

### Acceptance Criteria

- Given configured risk = 1% and account is in 7% drawdown from HWM, When a new trade signal is generated, Then the effective risk used for sizing is 0.75% (scaling factor 0.75)
- Given configured risk = 1% and account is in 12% drawdown from HWM, When a new trade signal is generated, Then the effective risk used for sizing is 0.50% (scaling factor 0.50)
- Given account is in 16% drawdown (above halt threshold), When a new entry signal is generated, Then the signal is blocked and dashboard shows drawdown CB active
- Given drawdown CB is active and equity recovers to 14% drawdown (below halt threshold), When the next equity refresh occurs, Then the drawdown CB auto-resets and trading resumes at the 0.50 scaling tier
- Given daily-loss CB is tripped but drawdown is only 3%, When a signal is generated, Then the signal is blocked by the daily-loss CB (drawdown CB is not active — they operate independently)
- Given a strategy's HWM is $10,000 and current equity is $10,500, When equity is refreshed, Then HWM updates to $10,500
- Given the application restarts, When the strategy resumes, Then the persisted HWM is loaded from the database and drawdown calculation continues correctly
- Given a backtest runs with adaptive risk enabled and the equity curve drops into the halt tier, When signals are generated during the halt period, Then those signals are skipped in the backtest results
- Given the user configures custom drawdown tiers, When tiers are not in ascending threshold order, Then a validation error is returned

## Objectives

- Add configurable drawdown tiers with scaling factors to `RiskLimitsConfig`
- Track per-strategy equity High-Water Mark (HWM) with database persistence
- Implement drawdown circuit breaker (independent from daily-loss CB) that halts entries when scaling = 0.0
- Apply drawdown scaling factor as overlay on `PositionSizeResolver` output
- Mirror drawdown logic identically in backtest engine
- Display drawdown state (%, active tier, CB status) on the dashboard

### Discovery References

- `.agent-context/0-knowledge/33-risk-management-and-trade-sizing.md` — full adaptive risk tier specification
- `.agent-context/0-knowledge/14-strategy-runtime-model.md` — IRiskEngine contract, UpdatePortfolioState
- `.agent-context/0-knowledge/15-grid-controller.md` — PositionSizeResolver call site
- `.agent-context/0-knowledge/18-backtesting-architecture.md` — BacktestRiskEngine, PassThroughRiskEngine

### Project Patterns

- `src/TradingApp.Application/Trading/Services/LiveRiskEngine.cs` — daily-loss circuit breaker pattern (model for drawdown CB)
- `src/TradingApp.Application/Abstractions/Services/IRiskEngine.cs` — interface with default-body methods for backward compat
- `src/TradingApp.Application/Trading/Services/PositionSizeResolver.cs` — static resolver, 2 call sites in GridController/SignalController
- `src/TradingApp.Application/Scheduling/StrategyScheduler.cs` — equity flow, UpdatePortfolioState call
- `src/TradingApp.Application/Trading/Models/MarketContext.cs` — mutable context carrying AccountEquity
- `src/TradingApp.Application/StrategyAuthoring/Models/RiskLimitsConfig.cs` — system-wide risk config record
- `src/TradingApp.Persistence/TradingAppDbContext.cs` — inline fluent entity config in OnModelCreating
- `src/TradingApp.Api/Controllers/RiskController.cs` — direct injection (no MediatR), risk endpoint pattern
- `frontend/trading-ui/src/app/features/dashboard/account-summary/portfolio-heat-indicator/` — tiered threshold indicator
- `tests/TradingApp.Application.Tests/Trading/Services/LiveRiskEngineTests.cs` — CB test pattern

### [x] Phase 1: Configuration & Domain Model

**Complexity**: Low | **Risk**: Low

- [x] Task 1.1: Create DrawdownTier record
  - Details: .agent-context/3-develop/build/plans/details/20260412-adaptive-risk-drawdown-phase-01-details.md#task-11-create-drawdowntier-record

- [x] Task 1.2: Add DrawdownTiers to RiskLimitsConfig
  - Details: .agent-context/3-develop/build/plans/details/20260412-adaptive-risk-drawdown-phase-01-details.md#task-12-add-drawdowntiers-to-risklimitsconfig

- [x] Task 1.3: Create RiskLimitsConfigValidator
  - Details: .agent-context/3-develop/build/plans/details/20260412-adaptive-risk-drawdown-phase-01-details.md#task-13-create-risklimitsconfigvalidator

- [x] Task 1.4: Add HighWaterMarkUsd to Strategy entity and database
  - Details: .agent-context/3-develop/build/plans/details/20260412-adaptive-risk-drawdown-phase-01-details.md#task-14-add-highwatermarkusd-to-strategy-entity-and-database

- [x] Task 1.5: Update appsettings.json with default DrawdownTiers
  - Details: .agent-context/3-develop/build/plans/details/20260412-adaptive-risk-drawdown-phase-01-details.md#task-15-update-appsettingsjson-with-default-drawdowntiers

- [x] Task 1.6: Unit tests for Phase 1
  - Details: .agent-context/3-develop/build/plans/details/20260412-adaptive-risk-drawdown-phase-01-details.md#task-16-unit-tests-for-phase-1

- [x] Task 1.7: Run architecture tests
  - Details: .agent-context/3-develop/build/plans/details/20260412-adaptive-risk-drawdown-phase-01-details.md#task-17-run-architecture-tests

### [x] Phase 2: Drawdown Tracking & Risk Engine Integration

**Complexity**: High | **Risk**: Medium

- [x] Task 2.1: Create DrawdownEvaluator static utility
  - Details: .agent-context/3-develop/build/plans/details/20260412-adaptive-risk-drawdown-phase-02-details.md#task-21-create-drawdownevaluator-static-utility

- [x] Task 2.2: Extend IRiskEngine with drawdown state
  - Details: .agent-context/3-develop/build/plans/details/20260412-adaptive-risk-drawdown-phase-02-details.md#task-22-extend-iriskengine-with-drawdown-state

- [x] Task 2.3: Add drawdown CB to LiveRiskEngine.ValidateAsync
  - Details: .agent-context/3-develop/build/plans/details/20260412-adaptive-risk-drawdown-phase-02-details.md#task-23-add-drawdown-cb-to-liveriskenginevalidateasync

- [x] Task 2.4: Add DrawdownScalingFactor to MarketContext
  - Details: .agent-context/3-develop/build/plans/details/20260412-adaptive-risk-drawdown-phase-02-details.md#task-24-add-drawdownscalingfactor-to-marketcontext

- [x] Task 2.5: Wire drawdown evaluation into StrategyScheduler
  - Details: .agent-context/3-develop/build/plans/details/20260412-adaptive-risk-drawdown-phase-02-details.md#task-25-wire-drawdown-evaluation-into-strategyscheduler

- [x] Task 2.6: Apply scaling factor at PositionSizeResolver call sites
  - Details: .agent-context/3-develop/build/plans/details/20260412-adaptive-risk-drawdown-phase-02-details.md#task-26-apply-scaling-factor-at-positionsizeresolver-call-sites

- [x] Task 2.7: Persist HWM changes via strategy repository
  - Details: .agent-context/3-develop/build/plans/details/20260412-adaptive-risk-drawdown-phase-02-details.md#task-27-persist-hwm-changes-via-strategy-repository

- [x] Task 2.8: Unit tests for Phase 2
  - Details: .agent-context/3-develop/build/plans/details/20260412-adaptive-risk-drawdown-phase-02-details.md#task-28-unit-tests-for-phase-2

- [x] Task 2.9: Run architecture tests
  - Details: .agent-context/3-develop/build/plans/details/20260412-adaptive-risk-drawdown-phase-02-details.md#task-29-run-architecture-tests

### [x] Phase 3: Backtest Support

**Complexity**: Medium | **Risk**: Medium

- [x] Task 3.1: Add drawdown tracking to BacktestRiskEngine
  - Details: .agent-context/3-develop/build/plans/details/20260412-adaptive-risk-drawdown-phase-03-details.md#task-31-add-drawdown-tracking-to-backtestriskengine

- [x] Task 3.2: Track drawdown-blocked signals in backtest metrics
  - Details: .agent-context/3-develop/build/plans/details/20260412-adaptive-risk-drawdown-phase-03-details.md#task-32-track-drawdown-blocked-signals-in-backtest-metrics

- [x] Task 3.3: Unit and integration tests for Phase 3
  - Details: .agent-context/3-develop/build/plans/details/20260412-adaptive-risk-drawdown-phase-03-details.md#task-33-unit-and-integration-tests-for-phase-3

- [x] Task 3.4: Run architecture tests
  - Details: .agent-context/3-develop/build/plans/details/20260412-adaptive-risk-drawdown-phase-03-details.md#task-34-run-architecture-tests

### [x] Phase 4: API Endpoint & Frontend Dashboard

**Complexity**: Medium | **Risk**: Low

- [x] Task 4.1: Create DrawdownStateResponse DTO and API endpoint
  - Details: .agent-context/3-develop/build/plans/details/20260412-adaptive-risk-drawdown-phase-04-details.md#task-41-create-drawdownstateresponse-dto-and-api-endpoint

- [x] Task 4.2: Create DrawdownState frontend model and API service method
  - Details: .agent-context/3-develop/build/plans/details/20260412-adaptive-risk-drawdown-phase-04-details.md#task-42-create-drawdownstate-frontend-model-and-api-service-method

- [x] Task 4.3: Create DrawdownIndicatorComponent
  - Details: .agent-context/3-develop/build/plans/details/20260412-adaptive-risk-drawdown-phase-04-details.md#task-43-create-drawdownindicatorcomponent

- [x] Task 4.4: Wire DrawdownIndicatorComponent into AccountSummary dashboard
  - Details: .agent-context/3-develop/build/plans/details/20260412-adaptive-risk-drawdown-phase-04-details.md#task-44-wire-drawdownindicatorcomponent-into-accountsummary-dashboard

- [x] Task 4.5: Frontend build and lint
  - Details: .agent-context/3-develop/build/plans/details/20260412-adaptive-risk-drawdown-phase-04-details.md#task-45-frontend-build-and-lint

## Scoping Summary

| Phase | Complexity | Risk |
|-------|----------|------|
| Phase 1: Configuration & Domain Model | Low | Low |
| Phase 2: Drawdown Tracking & Risk Engine Integration | High | Medium |
| Phase 3: Backtest Support | Medium | Medium |
| Phase 4: API Endpoint & Frontend Dashboard | Medium | Low |
| **Total** | **Medium-High** | **Medium** |

### Scoping Notes

- HWM stored as nullable decimal column on `Strategy` entity (simplest path — avoids new table/repository)
- `DrawdownEvaluator` is a stateless static utility — avoids singleton/scoped DI conflicts
- Drawdown CB state is derived (not persisted) — recalculated each candle cycle from equity vs HWM
- SQLite dev: `HighWaterMarkUsd` column added via `CreateMissingColumnsAsync` or manual `ALTER TABLE` shim in `EnsureCreated` flow
- `RiskLimitsConfig.DrawdownTiers` is system-wide (all strategies share tiers); per-strategy tier overrides deferred
- Frontend uses polling (not SignalR) for drawdown state — matches existing `PortfolioHeatIndicator` pattern

## Dependencies

- PBI #1 (R-Based Position Sizing) — `PositionSizeResolver` with `RiskBased` mode must exist
- MSTest, Moq, FluentAssertions (existing test framework)
- EF Core SQLite + SQL Server (existing persistence)
- Angular Material (existing frontend framework)

## Success Criteria

- All 9 acceptance criteria from the PBI pass
- Drawdown-scaled sizing is verified via unit tests at each tier boundary
- Drawdown CB trips at halt threshold and auto-resets on equity recovery
- Daily-loss CB and drawdown CB operate independently
- HWM persists across application restarts
- Backtest results reflect trades skipped due to drawdown CB
- Dashboard displays current drawdown %, active tier, and CB status
- All existing tests continue to pass (no regressions)
- Frontend builds and lints cleanly

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|----------|
| Implementation Planner | planned | 2026-04-12T20:05:15Z | 2026-04-12T20:17:15Z |
| Plan Reviewer | reviewed | 2026-04-12T20:17:44Z | 2026-04-12T20:21:55Z |
| 3-Develop: 2 Implementer | implemented | 2026-04-12T20:21:55Z | 2026-04-12T21:30:24Z |
