---
applyTo: ".agent-context/3-develop/build/changes/20260405-strategy-optimizer-changes.md"
currentAgent: "Plan Implementer"
agentStartedAt: "2026-04-05T00:06:17Z"
status: "completed"
lastUpdated: "2026-04-05T00:31:21Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: The Optimizer — Signal Strategy Parameter Sweep

## Overview

Add a strategy optimizer feature that automatically sweeps through combinations of signal strategy parameters (entry conditions, indicator settings, SL/TP, leverage, position sizing), runs backtests for each combination in parallel, filters results by configurable fitness thresholds, ranks the top 10 by a composite fitness score, and persists each result with the full deployable `StrategyConfig` JSON. Users can promote any winning result directly into the strategy builder.

The optimizer operates exclusively on `StrategyMode.Signal` with `Direction.Long`. It tests all valid combinations of entry conditions (RSI, MACD, PriceVsEma — singles, pairs, and triples) across both `EntryLogic.All` and `EntryLogic.Any`, combined with parameter ranges for exit rules, risk, and optionally the trend filter.

## PBI Details

### User Story

> As a **trader**, I want to **run an automated parameter sweep across signal strategy configurations** so that **I can discover the top-performing strategies without manually testing hundreds of combinations**.

### Business Value

- Turns the backtesting engine into a strategy discovery tool
- Eliminates manual trial-and-error tuning
- Surfaces optimal parameter combinations that a human would never try
- "Promote to Strategy" workflow makes findings immediately actionable

### Acceptance Criteria

- [ ] **Given** a user navigates to the Optimizer tab, **When** the page loads, **Then** a configuration form is displayed with symbol, date range, initial capital, parameter bounds (SL min/max, TP min/max, Leverage min/max), sample size, and fitness thresholds
- [ ] **Given** valid configuration is entered, **When** the user clicks "Run Optimization", **Then** the system generates random parameter combinations and runs backtests in parallel, displaying a live progress indicator
- [ ] **Given** an optimization is running, **When** progress updates arrive via SignalR, **Then** a progress bar shows completed/total runs
- [ ] **Given** an optimization completes, **When** results are displayed, **Then** only results meeting ALL fitness thresholds are shown (default: WinRate ≥ 40%, TotalTrades ≥ 10, MaxDrawdown < 30% of capital)
- [ ] **Given** qualifying results exist, **When** ranked by fitness score, **Then** the top 10 are displayed in a table with rank, fitness score, signal combination description, and key metrics (PnL, WinRate, MaxDrawdown, Trades, Sharpe)
- [ ] **Given** a result row is expanded, **When** the user views details, **Then** the full strategy configuration is shown in human-readable form
- [ ] **Given** a user clicks "Create Strategy" on a result row, **When** the strategy builder opens, **Then** the form is pre-filled with the exact configuration from the optimization result
- [ ] **Given** fitness thresholds are configurable, **When** the user adjusts them, **Then** the thresholds are used as minimum requirements (Min Win Rate, Min Trades, Max Drawdown %)
- [ ] **Given** an optimization run completes, **When** the results are persisted, **Then** the top 10 are stored in the database and can be retrieved later from the optimization history list
- [ ] **Given** the user navigates to Optimizer tab, **When** previous runs exist, **Then** a history list shows prior optimization runs with timestamp, symbol, total combinations, and top result metrics

## Objectives

- Create `OptimizationRun` domain entity with top-result storage
- Build `SweepRunner` application service that generates parameter combos, runs backtests via `IBacktestRunner.RunAsync` in parallel, and ranks results
- Add `OptimizationsController` with endpoints to start, poll progress, get results
- Build Angular "Optimizer" tab with configure form, progress view, and results table
- Support "Promote to Strategy" via `StrategyConfig` JSON round-trip

### Discovery References

- **18-backtesting-architecture.md**: BacktestRunner.RunAsync is in-memory, reusable for sweep
- **IBacktestRunner**: Two overloads — `RunAsync(config, ct)` and `RunAsync(config, onProgress, ct)`
- **StrategyConfig**: Full signal config including `EntryConditions`, `TrendFilter`, `Exit`, `Risk`
- **BacktestResult**: Contains all metrics — TotalPnL, WinRate, MaxDrawdown, TotalTrades, etc.
- **CompositeStrategyEngine**: Routes `StrategyMode.Signal` to `EvaluateSignalMode`
- **ConditionEvaluator**: Dispatches to RSI/MACD/PriceVsEma handlers; supports `EntryLogic.All/Any`
- **BacktestProcessorService**: Existing background processing pattern with SignalR progress

### Project Patterns

- `src/TradePilot.Domain/Entities/BacktestRun.cs` — Domain entity with factory method `CreateQueued()`, private setters, mutation methods
- `src/TradePilot.Persistence/TradePilotDbContext.cs` — DbSet registration, SQLite model config
- `src/TradePilot.Persistence/Repositories/BacktestRunRepository.cs` — Repository pattern with EF Core
- `src/TradePilot.Application/Backtesting/Services/BacktestRunner.cs` — Direct `RunAsync` invocation (no queue needed for sweep)
- `src/TradePilot.Application/Backtesting/RunBacktestCommand.cs` — MediatR command pattern
- `src/TradePilot.Api/Services/BacktestProcessorService.cs` — BackgroundService with SignalR progress
- `src/TradePilot.Api/Controllers/BacktestsController.cs` — REST controller with MediatR
- `src/TradePilot.Api/Hubs/MarketDataHub.cs` — SignalR hub for real-time updates
- `frontend/trading-ui/src/app/app.routes.ts` — Lazy-loaded route per feature
- `frontend/trading-ui/src/app/app.component.html` — Navigation links
- `frontend/trading-ui/src/app/features/backtesting/backtest-page.component.ts` — Feature page with MatTabGroup
- `frontend/trading-ui/src/app/core/services/backtest.service.ts` — API service pattern

### [x] Phase 1: Backend — Domain Model & Sweep Engine

**Complexity**: High | **Risk**: Medium

- [x] Task 1.1: Create `OptimizationRun` domain entity
  - Details: .agent-context/3-develop/build/plans/details/20260405-strategy-optimizer-phase-01-details.md#task-11-create-optimizationrun-domain-entity

- [x] Task 1.2: Create `OptimizationResult` domain entity
  - Details: .agent-context/3-develop/build/plans/details/20260405-strategy-optimizer-phase-01-details.md#task-12-create-optimizationresult-domain-entity

- [x] Task 1.3: Create `SweepConfig` and `ParameterBounds` models
  - Details: .agent-context/3-develop/build/plans/details/20260405-strategy-optimizer-phase-01-details.md#task-13-create-sweepconfig-and-parameterbounds-models

- [x] Task 1.4: Create `FitnessThresholds` model
  - Details: .agent-context/3-develop/build/plans/details/20260405-strategy-optimizer-phase-01-details.md#task-14-create-fitnessthresholds-model

- [x] Task 1.5: Create `StrategyConfigGenerator` — random combo generation
  - Details: .agent-context/3-develop/build/plans/details/20260405-strategy-optimizer-phase-01-details.md#task-15-create-strategyconfiggenerator

- [x] Task 1.6: Create `FitnessScorer` — scoring and threshold filtering
  - Details: .agent-context/3-develop/build/plans/details/20260405-strategy-optimizer-phase-01-details.md#task-16-create-fitnessscorer

- [x] Task 1.7: Create `SweepRunner` — parallel backtest orchestration
  - Details: .agent-context/3-develop/build/plans/details/20260405-strategy-optimizer-phase-01-details.md#task-17-create-sweeprunner

- [x] Task 1.8: Write unit tests for `StrategyConfigGenerator`
  - Details: .agent-context/3-develop/build/plans/details/20260405-strategy-optimizer-phase-01-details.md#task-18-write-unit-tests-for-strategyconfiggenerator

- [x] Task 1.9: Write unit tests for `FitnessScorer`
  - Details: .agent-context/3-develop/build/plans/details/20260405-strategy-optimizer-phase-01-details.md#task-19-write-unit-tests-for-fitnessscorer

- [x] Task 1.10: Write unit tests for `SweepRunner`
  - Details: .agent-context/3-develop/build/plans/details/20260405-strategy-optimizer-phase-01-details.md#task-110-write-unit-tests-for-sweeprunner

### [x] Phase 2: Backend — Persistence & API

**Complexity**: Medium | **Risk**: Low

- [x] Task 2.1: Create `IOptimizationRunRepository` and EF implementation
  - Details: .agent-context/3-develop/build/plans/details/20260405-strategy-optimizer-phase-02-details.md#task-21-create-ioptimizationrunrepository

- [x] Task 2.2: Register `OptimizationRun` and `OptimizationResult` in DbContext
  - Details: .agent-context/3-develop/build/plans/details/20260405-strategy-optimizer-phase-02-details.md#task-22-register-entities-in-dbcontext

- [x] Task 2.3: Create `RunOptimizationCommand` MediatR handler
  - Details: .agent-context/3-develop/build/plans/details/20260405-strategy-optimizer-phase-02-details.md#task-23-create-runoptimizationcommand

- [x] Task 2.4: Create `GetOptimizationResultQuery` MediatR handler
  - Details: .agent-context/3-develop/build/plans/details/20260405-strategy-optimizer-phase-02-details.md#task-24-create-getoptimizationresultquery

- [x] Task 2.5: Create `GetOptimizationListQuery` MediatR handler
  - Details: .agent-context/3-develop/build/plans/details/20260405-strategy-optimizer-phase-02-details.md#task-25-create-getoptimizationlistquery

- [x] Task 2.6: Create `OptimizationProcessorService` background service
  - Details: .agent-context/3-develop/build/plans/details/20260405-strategy-optimizer-phase-02-details.md#task-26-create-optimizationprocessorservice

- [x] Task 2.7: Create `OptimizationsController` with REST endpoints
  - Details: .agent-context/3-develop/build/plans/details/20260405-strategy-optimizer-phase-02-details.md#task-27-create-optimizationscontroller

- [x] Task 2.8: Register DI services in `Program.cs`
  - Details: .agent-context/3-develop/build/plans/details/20260405-strategy-optimizer-phase-02-details.md#task-28-register-di-services

- [x] Task 2.9: Build solution and run all backend tests
  - Details: .agent-context/3-develop/build/plans/details/20260405-strategy-optimizer-phase-02-details.md#task-29-build-and-test

### [x] Phase 3: Frontend — Optimizer Tab & Configuration

**Complexity**: Medium | **Risk**: Low

- [x] Task 3.1: Create `optimizer.service.ts` API service
  - Details: .agent-context/3-develop/build/plans/details/20260405-strategy-optimizer-phase-03-details.md#task-31-create-optimizer-service

- [x] Task 3.2: Create optimizer TypeScript models
  - Details: .agent-context/3-develop/build/plans/details/20260405-strategy-optimizer-phase-03-details.md#task-32-create-optimizer-models

- [x] Task 3.3: Create `optimizer-page.component` — feature shell
  - Details: .agent-context/3-develop/build/plans/details/20260405-strategy-optimizer-phase-03-details.md#task-33-create-optimizer-page-component

- [x] Task 3.4: Create `optimizer-config-form.component` — parameter bounds form
  - Details: .agent-context/3-develop/build/plans/details/20260405-strategy-optimizer-phase-03-details.md#task-34-create-optimizer-config-form

- [x] Task 3.5: Add `/optimizer` route and navigation link
  - Details: .agent-context/3-develop/build/plans/details/20260405-strategy-optimizer-phase-03-details.md#task-35-add-route-and-nav-link

- [x] Task 3.6: Wire SignalR progress for optimization runs
  - Details: .agent-context/3-develop/build/plans/details/20260405-strategy-optimizer-phase-03-details.md#task-36-wire-signalr-progress

- [x] Task 3.7: Build frontend and lint
  - Details: .agent-context/3-develop/build/plans/details/20260405-strategy-optimizer-phase-03-details.md#task-37-build-and-lint

### [x] Phase 4: Frontend — Results Display & Promote to Strategy

**Complexity**: Medium | **Risk**: Low

- [x] Task 4.1: Create `optimizer-results-table.component` — ranked results display
  - Details: .agent-context/3-develop/build/plans/details/20260405-strategy-optimizer-phase-04-details.md#task-41-create-results-table

- [x] Task 4.2: Create `optimizer-result-detail.component` — expandable row detail
  - Details: .agent-context/3-develop/build/plans/details/20260405-strategy-optimizer-phase-04-details.md#task-42-create-result-detail

- [x] Task 4.3: Create `optimizer-history-list.component` — previous runs
  - Details: .agent-context/3-develop/build/plans/details/20260405-strategy-optimizer-phase-04-details.md#task-43-create-history-list

- [x] Task 4.4: Implement "Create Strategy" promotion flow
  - Details: .agent-context/3-develop/build/plans/details/20260405-strategy-optimizer-phase-04-details.md#task-44-create-strategy-promotion

- [x] Task 4.5: Build frontend, lint, and run unit tests
  - Details: .agent-context/3-develop/build/plans/details/20260405-strategy-optimizer-phase-04-details.md#task-45-build-lint-test

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: Backend — Domain Model & Sweep Engine | High | Medium |
| Phase 2: Backend — Persistence & API | Medium | Low |
| Phase 3: Frontend — Optimizer Tab & Configuration | Medium | Low |
| Phase 4: Frontend — Results Display & Promote to Strategy | Medium | Low |
| **Total** | **High** | **Medium** |

### Scoping Notes

- **Signal mode only, Long direction only** — Grid mode and Short/Both are out of scope for this initial implementation
- **Random sampling** — The system generates N random parameter combinations rather than exhaustive grid search; this keeps runtime bounded while providing good coverage
- **Parallel execution** — `SweepRunner` uses `Parallel.ForEachAsync` with configurable `maxDegreeOfParallelism` (default: `Environment.ProcessorCount`) to run backtests concurrently
- **No queue overhead** — Unlike individual backtests, the optimizer calls `IBacktestRunner.RunAsync` directly (not through the job queue) since it manages its own parallelism
- **Top 10 persisted** — Only the top 10 qualifying results are stored per optimization run; all other results are discarded after ranking
- **StrategyConfigJson round-trip** — Each result stores the exact `StrategyConfig` JSON that can be deserialized and used to create a new strategy; no translation layer needed
- **Fitness formula**: `(TotalPnL / MaxDrawdownAbsolute) × sqrt(TotalTrades)` — rewards profitable, low-drawdown strategies with more trades (avoiding overfitting to one lucky trade)
- **Configurable thresholds** with defaults: WinRate ≥ 40%, TotalTrades ≥ 10, MaxDrawdown < 30% of capital
- **Entry condition combinations**: 1-3 from {RSI, MACD, PriceVsEma}, with `EntryLogic.All/Any` for multi-signal combos = 11 distinct signal templates
- **Trend filter**: Optionally included in sweep (Enabled true/false, EmaCross fast/slow combos)
- **SupportResistance** excluded — not yet wired in the frontend union type

## Dependencies

- `IBacktestRunner` — existing backtest engine
- `BacktestResult` + `BacktestMetricsCalculator` — existing metrics pipeline
- `StrategyConfig`, `EntryConditionConfig`, params models — existing strategy config infrastructure
- Angular Material (`MatTable`, `MatProgressBar`, `MatFormField`, `MatSelect`, `MatChipSet`)
- SignalR (`MarketDataHub`) — existing real-time updates infrastructure

## Success Criteria

- Optimizer tab accessible from main navigation
- User can configure bounds and thresholds, then launch a sweep
- Progress bar updates via SignalR as combinations complete
- Top 10 results displayed with rank, fitness score, signal description, and metrics
- "Create Strategy" pre-fills strategy builder with the winning config
- History list shows previous optimization runs
- All backend tests pass
- Frontend builds and lints cleanly

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-04-04T23:55:49Z | 2026-04-04T23:55:49Z |
| Plan Implementer | completed | 2026-04-05T00:06:17Z | 2026-04-05T00:31:21Z |
