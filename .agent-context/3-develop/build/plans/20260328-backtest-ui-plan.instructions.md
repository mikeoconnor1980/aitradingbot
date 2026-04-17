---
applyTo: ".agent-context/3-develop/build/changes/20260328-backtest-ui-changes.md"
currentAgent: "None"
agentStartedAt: "2026-03-28T17:06:26Z"
status: "complete"
lastUpdated: "2026-03-28T17:32:48Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: Backtest UI Dashboard (F5)

## Overview

Build an Angular dashboard at `/backtesting` for triggering backtests, viewing results with equity curve charts, browsing past runs, and comparing two backtest results side-by-side. Includes a new paginated list API endpoint.

## PBI Details

**PBI:** F5 — Backtest UI Dashboard  
**Depends On:** F4 (Backtest API & Results) — assumed complete  
**PRD:** candle-persistence-backtesting-prd.md

### User Story

> As an **Operator**, I want to **configure and run backtests from a web UI, view detailed results with an equity curve chart, and compare two runs side-by-side** so that **I can visually evaluate and iterate on strategy parameters without using curl or Postman**.

### Acceptance Criteria

- [ ] Given the Angular app is running, When the operator navigates to `/backtesting`, Then the backtesting page loads with the run form and past results list
- [ ] Given the backtesting page, When the operator views the nav bar, Then a "Backtesting" link is visible alongside the other nav items
- [ ] Given valid form values, When the operator clicks "Run Backtest", Then the button shows a loading state and the backtest runs via `POST /api/backtests`
- [ ] Given a backtest completes successfully, Then summary metric cards are displayed showing Total PnL, Win Rate, Max Drawdown, Total Trades, and other metrics
- [ ] Given a backtest completes, Then an equity curve chart is rendered using lightweight-charts showing equity over time
- [ ] Given a backtest completes with trades, Then trade entry/exit markers are plotted on the equity chart
- [ ] Given a backtest completes, Then a sortable trade log table is displayed with Entry Time, Exit Time, Entry Price, Exit Price, Side, Size, PnL, and Fees columns
- [ ] Given a backtest returns zero trades, Then an empty state message is shown
- [ ] Given the operator clicks "Validate Data", Then the coverage report shows available date ranges and candle counts per interval
- [ ] Given invalid form values, Then inline validation errors are shown and the Run Backtest button is disabled
- [ ] Given the API returns a validation error (400), Then the error is displayed as inline form errors
- [ ] Given the API returns a timeout (408), Then an error message suggests trying a shorter date range
- [ ] Given past backtest runs exist, Then the past results list shows a paginated table of previous runs with key metrics
- [ ] Given the operator clicks a past result, Then the full result detail is displayed
- [ ] Given the operator clicks "Re-run with changes" on a past result, Then the run form is pre-filled with that result's strategy config
- [ ] Given the operator selects two results for comparison, Then a side-by-side metrics table is shown with delta values
- [ ] Given two results are being compared, Then their equity curves are overlaid on the same chart in different colours
- [ ] Given `GET /api/backtests?page=1&pageSize=20` is called, Then a paginated list of backtest summaries is returned

## Objectives

- Add a `GET /api/backtests` paginated list endpoint to the backend
- Build the full Angular backtesting feature with form, results, list, and comparison components
- Integrate `lightweight-charts` for equity curve visualisation with trade markers
- Follow existing project patterns (standalone components, ApiRestClient, reactive forms, mat-table)

### Discovery References

- **Backtesting architecture**: `.agent-context/0-knowledge/18-backtesting-architecture.md` — ReplayEngine, SimulatedExecutionEngine, BacktestResult model
- **Charting library**: `.agent-context/0-knowledge/09-charting-library.md` — lightweight-charts v5 setup, `LineSeries`, `ResizeObserver`, cleanup pattern
- **Strategy config schema**: `.agent-context/0-knowledge/13-strategy-config-schema.md` — full config fields for form
- **Data model ERD**: `.agent-context/0-knowledge/data-model/data-model-erd.md` — BacktestRun entity schema
- **Angular conventions**: `.agent-context/0-knowledge/11-angular-instructions.md` — standalone, inject(), BehaviorSubject, AsyncPipe

### Project Patterns

- `frontend/trading-ui/src/app/app.routes.ts` — lazy-loaded route pattern (loadComponent)
- `frontend/trading-ui/src/app/app.component.html` — navigation link pattern (routerLink, routerLinkActive)
- `frontend/trading-ui/src/app/core/services/api-rest-client.service.ts` — HTTP service wrapper
- `frontend/trading-ui/src/app/features/order-entry/order-entry.component.ts` — reactive form pattern (FormBuilder, typed FormGroup)
- `frontend/trading-ui/src/app/features/market-data/price-chart/price-chart.component.ts` — lightweight-charts pattern (createChart, ResizeObserver, ngOnDestroy cleanup)
- `frontend/trading-ui/src/app/features/market-data/market-data.component.html` — mat-table pattern
- `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts` — sortable table pattern
- `src/TradePilot.Api/Infrastructure/ApiController.cs` — controller base class
- `src/TradePilot.Api/Controllers/CandlesController.cs` — MediatR controller action pattern
- `src/TradePilot.Application/Abstractions/Queries/Query.cs` — CQRS query base
- `src/TradePilot.Application/Backtesting/Models/BacktestResult.cs` — backtest result model
- `tests/TradePilot.Api.Tests/Infrastructure/BaseControllerTests.cs` — controller test base

### [x] Phase 1: Backend — Paginated List Endpoint

**Complexity**: Medium | **Risk**: Low

- [x] Task 1.1: Create PagedResult<T> generic model
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-ui-phase-01-details.md#task-11-create-pagedresult-generic-model

- [x] Task 1.2: Create BacktestSummaryDto
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-ui-phase-01-details.md#task-12-create-backtestsummarydto

- [x] Task 1.3: Create GetBacktestListQuery and handler
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-ui-phase-01-details.md#task-13-create-getbacktestlistquery-and-handler

- [x] Task 1.4: Add list endpoint to BacktestsController
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-ui-phase-01-details.md#task-14-add-list-endpoint-to-backtestscontroller

- [x] Task 1.5: Add controller integration tests
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-ui-phase-01-details.md#task-15-add-controller-integration-tests

- [x] Task 1.6: Build solution and run all tests
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-ui-phase-01-details.md#task-16-build-solution-and-run-all-tests

### [x] Phase 2: Frontend — Foundation & Navigation

**Complexity**: Low | **Risk**: Low

- [x] Task 2.1: Create backtest TypeScript models
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-ui-phase-02-details.md#task-21-create-backtest-typescript-models

- [x] Task 2.2: Create BacktestService
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-ui-phase-02-details.md#task-22-create-backtestservice

- [x] Task 2.3: Add routing and navigation
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-ui-phase-02-details.md#task-23-add-routing-and-navigation

- [x] Task 2.4: Create BacktestPageComponent with tab structure
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-ui-phase-02-details.md#task-24-create-backtestpagecomponent-with-tab-structure

- [x] Task 2.5: Add unit tests for BacktestService
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-ui-phase-02-details.md#task-25-add-unit-tests-for-backtestservice

- [x] Task 2.6: Frontend build and lint
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-ui-phase-02-details.md#task-26-frontend-build-and-lint

### [x] Phase 3: Frontend — Run Form & Coverage Validation

**Complexity**: High | **Risk**: Medium

- [x] Task 3.1: Create BacktestFormComponent with reactive form
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-ui-phase-03-details.md#task-31-create-backtestformcomponent-with-reactive-form

- [x] Task 3.2: Create CoverageReportComponent
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-ui-phase-03-details.md#task-32-create-coveragereportcomponent

- [x] Task 3.3: Wire form to BacktestService and handle responses
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-ui-phase-03-details.md#task-33-wire-form-to-backtestservice-and-handle-responses

- [x] Task 3.4: Add form validation and error handling
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-ui-phase-03-details.md#task-34-add-form-validation-and-error-handling

- [x] Task 3.5: Add unit tests for BacktestFormComponent
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-ui-phase-03-details.md#task-35-add-unit-tests-for-backtestformcomponent

- [x] Task 3.6: Frontend build and lint
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-ui-phase-03-details.md#task-36-frontend-build-and-lint

### [x] Phase 4: Frontend — Results Dashboard

**Complexity**: High | **Risk**: Medium

- [x] Task 4.1: Create BacktestResultComponent with metric cards and config echo
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-ui-phase-04-details.md#task-41-create-backtestresultcomponent-with-metric-cards

- [x] Task 4.2: Create EquityChartComponent
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-ui-phase-04-details.md#task-42-create-equitychartcomponent

- [x] Task 4.3: Create TradeLogTableComponent
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-ui-phase-04-details.md#task-43-create-tradelogtablecomponent

- [x] Task 4.4: Integrate results into BacktestPageComponent
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-ui-phase-04-details.md#task-44-integrate-results-into-backtestpagecomponent

- [x] Task 4.5: Add unit tests for result components
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-ui-phase-04-details.md#task-45-add-unit-tests-for-result-components

- [x] Task 4.6: Frontend build and lint
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-ui-phase-04-details.md#task-46-frontend-build-and-lint

### [x] Phase 5: Frontend — Past Results & Comparison

**Complexity**: High | **Risk**: Medium

- [x] Task 5.1: Create BacktestListComponent
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-ui-phase-05-details.md#task-51-create-backtestlistcomponent

- [x] Task 5.2: Create BacktestCompareComponent with run labels, config diff, and metrics
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-ui-phase-05-details.md#task-52-create-backtestcomparecomponent

- [x] Task 5.3: Implement "Re-run with changes" workflow
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-ui-phase-05-details.md#task-53-implement-re-run-with-changes-workflow

- [x] Task 5.4: Implement error handling for all API states
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-ui-phase-05-details.md#task-54-implement-error-handling-for-all-api-states

- [x] Task 5.5: Add unit tests for list and comparison components
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-ui-phase-05-details.md#task-55-add-unit-tests-for-list-and-comparison-components

- [x] Task 5.6: Frontend build and lint
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-ui-phase-05-details.md#task-56-frontend-build-and-lint

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: Backend — Paginated List Endpoint | Medium | Low |
| Phase 2: Frontend — Foundation & Navigation | Low | Low |
| Phase 3: Frontend — Run Form & Coverage Validation | High | Medium |
| Phase 4: Frontend — Results Dashboard | High | Medium |
| Phase 5: Frontend — Past Results & Comparison | High | Medium |
| **Total** | **High** | **Medium** |

### Scoping Notes

- **F4 prerequisite verification required**: `BacktestsController.cs`, `IBacktestRunRepository.cs`, `BacktestRunRepository.cs`, and `BacktestRun` entity do not yet exist in the codebase. The implementer MUST verify F4 (Backtest API & Results) is fully complete before starting Phase 1. If F4 is not complete, Phase 1 tasks 1.3 and 1.4 (which reference these files as modifications) will fail.
- The C# `BacktestResult` model currently lacks `Id` and `Config` properties. The TypeScript `BacktestResult` interface in Phase 2 expects both. These should be available via the `BacktestRun` entity wrapper that F4 creates — the API layer maps `BacktestRun` (with Id, Config, and nested `BacktestResult`) to the frontend response shape.

- F4 (Backtest API & Results) is assumed complete — BacktestsController, BacktestRun entity, repository, POST/GET/{id}/validate endpoints all exist
- `lightweight-charts` v5.1 is already installed in package.json — no npm install needed
- Angular Material 19 with M3 dark theme is already configured
- No authentication required (POC — single operator with hardcoded dev-user identity)
- SignalR real-time progress during backtest execution is explicitly out of scope
- The "re-run with changes" feature pre-fills the form by fetching the full result from GET /api/backtests/{id}
- Comparison mode uses checkboxes in the past results list to select two runs

## Dependencies

- F4 (Backtest API & Results) — POST /api/backtests, GET /api/backtests/{id}, GET /api/backtests/validate
- `lightweight-charts` v5.1 (already installed)
- Angular Material 19 (already configured)
- MediatR (already configured)

## Success Criteria

- `/backtesting` route loads in the Angular app with nav link visible
- Operator can configure and trigger a backtest run from the form
- Results display with summary metric cards, equity curve chart with trade markers, and sortable trade log
- Past results are listed with pagination and can be viewed in detail
- Two results can be compared side-by-side with overlaid equity curves
- "Re-run with changes" pre-fills the form from a past result
- All error states (400, 404, 408, network) show appropriate user-friendly messages
- `GET /api/backtests` returns paginated backtest summaries
- Frontend builds and lints without errors
- Backend builds and all tests pass

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-03-28T14:08:46Z | 2026-03-28T14:24:44Z |
| Plan Reviewer | plan-reviewed | 2026-03-28T14:26:01Z | 2026-03-28T14:32:43Z |
| 3-Develop: 2 Implementer | implemented | 2026-03-28T15:46:38Z | 2026-03-28T16:41:17Z |
| 3-Develop: 3 Reviewer | complete | 2026-03-28T17:06:26Z | 2026-03-28T17:32:48Z |
