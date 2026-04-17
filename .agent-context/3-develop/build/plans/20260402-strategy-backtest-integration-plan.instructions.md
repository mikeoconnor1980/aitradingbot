---
applyTo: ".agent-context/3-develop/build/changes/20260402-strategy-backtest-integration-changes.md"
currentAgent: "None"
agentStartedAt: "2026-04-03T07:57:08Z"
status: "implemented"
lastUpdated: "2026-04-03T08:48:58Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: F3.5 — Strategy–Backtest Integration

## Overview

Connect saved strategies to the backtesting system: add a strategy picker to the backtest form, link every `BacktestRun` to its source `Strategy` and revision, provide strategy-scoped backtest history, and enable bidirectional navigation between strategies and backtests.

## PBI Details

### Summary

Replace the manual-entry backtest form with a strategy-picker workflow where the user selects a saved strategy (read-only), configures only backtest-specific parameters (date range, capital, fees), and runs the backtest. Link every `BacktestRun` to its source `Strategy` and `StrategyRevision`. Provide a strategy-scoped backtest history view so users can compare performance across revisions.

### User Story

> As a **trader**, I want to **select a saved strategy and backtest it directly** so that **I can validate my strategy against historical data and track how parameter changes affect performance across revisions**.

### Acceptance Criteria

- [ ] **Given** I am on the backtest page, **When** I open the strategy picker, **Then** I see my saved strategies listed by name
- [ ] **Given** I select a saved strategy, **When** the form updates, **Then** strategy parameters display as read-only and only date range, capital, and fees are editable
- [ ] **Given** I am on the strategy list page, **When** I click "Backtest" on a strategy row, **Then** I am navigated to the backtest page with that strategy pre-selected
- [ ] **Given** I am editing a strategy in the builder, **When** I click "Backtest this strategy", **Then** I am navigated to the backtest page with that strategy pre-selected
- [ ] **Given** I run a backtest, **When** the run completes, **Then** the `BacktestRun` record includes the `StrategyId` and `StrategyRevisionId`
- [ ] **Given** I view a backtest result, **When** I see the strategy name, **Then** clicking it navigates to the strategy edit page
- [ ] **Given** I am on a strategy's detail page, **When** I view the backtest history panel, **Then** I see all backtests grouped by revision with key metrics
- [ ] **Given** a strategy has been deleted, **When** I view a backtest that referenced it, **Then** the strategy name shows with a "deleted" indicator and the link is disabled
- [ ] **Given** I navigate to `/backtesting?strategyId=invalid-guid`, **When** the page loads, **Then** I see a notification that the strategy was not found and the picker is empty

## Objectives

- Add `StrategyId` (nullable Guid) and `StrategyRevisionId` (nullable int) to `BacktestRun` entity with EF migration
- Update `RunBacktestCommand` to accept optional `StrategyId`, resolve strategy and capture revision number at submission
- Add `GetBacktestsByStrategyQuery` and `GET /api/strategies/{id}/backtests` endpoint for strategy-scoped history
- Update `BacktestRunSummary`, `BacktestRunResponse`, and `BacktestSummaryDto` to include strategy metadata
- Refactor frontend backtest form to strategy-picker workflow (strategy selection → read-only preview + backtest params)
- Add bidirectional navigation: strategy list → backtest, strategy builder → backtest, backtest result → strategy
- Add strategy-scoped backtest history panel on strategy detail page

### Discovery References

- F3 (Strategy Versioning) introduces `StrategyRevision` entity with auto-incrementing `RevisionNumber` per strategy — assumed to be implemented. `IStrategyRevisionRepository.GetLatestRevisionNumberAsync(strategyId)` returns `Task<int>` (the revision number directly, not a `StrategyRevision` entity)
- `BacktestRun.StrategyConfigJson` already stores a full JSON snapshot — `StrategyId`/`StrategyRevisionId` are metadata links alongside, not replacements
- `BacktestRun` has no `UserId` — tenant scoping for strategy-scoped queries routes through `Strategy.UserId` ownership validation
- `EntryMode` type mismatch between strategy models (snake_case: `auto_from_signal_candle`) and backtest models (PascalCase: `AutoFromSignalCandle`) — requires mapping
- `RunBacktestRequest.StrategyConfig` is currently `[Required]` — when `StrategyId` is provided, the backend resolves the config from the strategy; cross-field validation required
- No existing nested cross-resource route pattern — `GET /api/strategies/{id}/backtests` will be the first

### Project Patterns

- `src/TradePilot.Domain/Entities/BacktestRun.cs` — Entity with private setters, `CreateQueued` factory, `ArgumentException` guards
- `src/TradePilot.Domain/Entities/Strategy.cs` — Entity with `Version` (int), `ConfigJson`, in-place `Update()`, `SoftDelete()`
- `src/TradePilot.Application/Backtesting/RunBacktestCommand.cs` — Sealed record command + handler, MediatR
- `src/TradePilot.Application/Backtesting/GetBacktestListQuery.cs` — Query + handler, paged results
- `src/TradePilot.Application/Backtesting/BacktestRunResponseMapper.cs` — Static mapper, JSON serialization
- `src/TradePilot.Application/Backtesting/Models/BacktestRunSummary.cs` — Application-layer summary DTO
- `src/TradePilot.Application/Backtesting/Models/BacktestRunResponse.cs` — Application-layer detail DTO
- `src/TradePilot.Api/Controllers/BacktestsController.cs` — REST controller, manual StrategyConfig mapping
- `src/TradePilot.Api/Controllers/StrategiesController.cs` — CRUD controller with IdentityService
- `src/TradePilot.Api/Models/RunBacktestRequest.cs` — Request DTO with DataAnnotations
- `src/TradePilot.Api/Models/BacktestSummaryDto.cs` — API-layer summary DTO
- `src/TradePilot.Persistence/Repositories/BacktestRunRepository.cs` — EF repository with typed Select projection
- `src/TradePilot.Persistence/TradePilotDbContext.cs` — Inline OnModelCreating, no separate config files
- `frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.ts` — Current manual form with 18 controls
- `frontend/trading-ui/src/app/features/backtesting/backtest-page.component.ts` — Orchestrating page with tabs
- `frontend/trading-ui/src/app/features/strategy-builder/strategy-list-page.component.ts` — Strategy list with edit/delete actions
- `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-api.service.ts` — Strategy CRUD API service

### [x] Phase 1: Backend — Domain, Persistence & Tests

**Complexity**: Medium | **Risk**: Low

- [x] Task 1.1: Add StrategyId and StrategyRevisionId properties to BacktestRun entity
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-backtest-integration-phase-01-details.md#task-11-add-strategy-fields-to-backtest-run-entity

- [x] Task 1.2: Update BacktestRun.CreateQueued factory to accept optional strategy parameters
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-backtest-integration-phase-01-details.md#task-12-update-createqueued-factory

- [x] Task 1.3: Update EF DbContext configuration for new columns
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-backtest-integration-phase-01-details.md#task-13-update-ef-dbcontext-configuration

- [x] Task 1.4: Add EF migration for StrategyId and StrategyRevisionId columns
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-backtest-integration-phase-01-details.md#task-14-add-ef-migration

- [x] Task 1.5: Add GetPagedSummariesByStrategyAsync to IBacktestRunRepository and implementation
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-backtest-integration-phase-01-details.md#task-15-add-strategy-scoped-repository-method

- [x] Task 1.6: Update BacktestRunSummary to include strategy metadata fields
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-backtest-integration-phase-01-details.md#task-16-update-backtest-run-summary

- [x] Task 1.7: Add domain and persistence tests
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-backtest-integration-phase-01-details.md#task-17-add-domain-and-persistence-tests

- [x] Task 1.8: Run architecture tests and verify build
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-backtest-integration-phase-01-details.md#task-18-run-architecture-tests

### [x] Phase 2: Backend — Application, API & Tests

**Complexity**: High | **Risk**: Medium

- [x] Task 2.1: Update RunBacktestCommand to accept optional StrategyId
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-backtest-integration-phase-02-details.md#task-21-update-runbacktestcommand

- [x] Task 2.2: Update RunBacktestCommandHandler to resolve strategy and capture revision
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-backtest-integration-phase-02-details.md#task-22-update-handler-to-resolve-strategy

- [x] Task 2.3: Add GetBacktestsByStrategyQuery and handler
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-backtest-integration-phase-02-details.md#task-23-add-get-backtests-by-strategy-query

- [x] Task 2.4: Update BacktestRunResponse and BacktestRunResponseMapper
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-backtest-integration-phase-02-details.md#task-24-update-response-and-mapper

- [x] Task 2.5: Update RunBacktestRequest and BacktestSummaryDto API models
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-backtest-integration-phase-02-details.md#task-25-update-api-models

- [x] Task 2.6: Update BacktestsController.RunAsync to handle optional StrategyId
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-backtest-integration-phase-02-details.md#task-26-update-backtests-controller

- [x] Task 2.7: Add GetBacktestsByStrategy endpoint to StrategiesController
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-backtest-integration-phase-02-details.md#task-27-add-strategy-backtests-endpoint

- [x] Task 2.8: Update BacktestsController.GetBacktestsAsync to map strategy fields
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-backtest-integration-phase-02-details.md#task-28-update-backtest-list-mapping

- [x] Task 2.9: Add API controller tests
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-backtest-integration-phase-02-details.md#task-29-add-api-controller-tests

- [x] Task 2.10: Run architecture tests and verify build
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-backtest-integration-phase-02-details.md#task-210-run-architecture-tests

### [x] Phase 3: Frontend — Strategy Picker & Backtest Form Refactor

**Complexity**: High | **Risk**: Medium

- [x] Task 3.1: Update frontend models with strategy fields
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-backtest-integration-phase-03-details.md#task-31-update-frontend-models

- [x] Task 3.2: Add strategy-scoped backtest methods to BacktestService
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-backtest-integration-phase-03-details.md#task-32-update-backtest-service

- [x] Task 3.3: Refactor backtest-form component with strategy picker and read-only preview
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-backtest-integration-phase-03-details.md#task-33-refactor-backtest-form

- [x] Task 3.4: Update backtest-page component to support strategyId query param
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-backtest-integration-phase-03-details.md#task-34-update-backtest-page

- [x] Task 3.5: Build and lint verification
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-backtest-integration-phase-03-details.md#task-35-build-and-lint

### [x] Phase 4: Frontend — Navigation & Backtest History

**Complexity**: Medium | **Risk**: Low

- [x] Task 4.1: Add "Backtest" action to strategy list page
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-backtest-integration-phase-04-details.md#task-41-add-backtest-action-to-strategy-list

- [x] Task 4.2: Add "Backtest this strategy" button to strategy builder page
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-backtest-integration-phase-04-details.md#task-42-add-backtest-button-to-builder

- [x] Task 4.3: Add strategy name column and link to backtest list
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-backtest-integration-phase-04-details.md#task-43-add-strategy-column-to-backtest-list

- [x] Task 4.4: Add strategy link and revision info to backtest result detail
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-backtest-integration-phase-04-details.md#task-44-add-strategy-link-to-result-detail

- [x] Task 4.5: Add backtest history panel to strategy builder page
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-backtest-integration-phase-04-details.md#task-45-add-backtest-history-panel

- [x] Task 4.6: Update re-run action with strategyId pre-fill
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-backtest-integration-phase-04-details.md#task-46-update-rerun-action

- [x] Task 4.7: Build and lint verification
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-backtest-integration-phase-04-details.md#task-47-build-and-lint

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: Backend — Domain, Persistence & Tests | Medium | Low |
| Phase 2: Backend — Application, API & Tests | High | Medium |
| Phase 3: Frontend — Strategy Picker & Form Refactor | High | Medium |
| Phase 4: Frontend — Navigation & Backtest History | Medium | Low |
| **Total** | **High** | **Medium** |

### Scoping Notes

- F3 (Strategy Versioning) is assumed to be implemented — `StrategyRevision` entity and repository methods available
- `StrategyId` is optional on the API (nullable) — the UI enforces strategy selection but the API supports standalone backtests for backward compatibility
- `BacktestRun.StrategyConfigJson` snapshot is preserved as the canonical execution record — `StrategyId`/`StrategyRevisionId` are metadata links
- `EntryMode` enum casing mismatch between strategy models (snake_case) and backtest models (PascalCase) — the backend resolves strategy config server-side, eliminating the need for frontend mapping
- No FK constraint in SQLite — backtest history survives strategy soft-deletion
- `StrategyName` enrichment on `BacktestSummaryDto` and `BacktestRunResponse` is resolved at the API layer (Tasks 2.7/2.8) via strategy lookup; deleted strategies detected via `IsActive = false` flag to satisfy the "deleted indicator" acceptance criterion
- `BacktestRun` remains without `UserId` — tenant scoping on strategy-scoped queries validates strategy ownership before returning backtests

## Dependencies

- F2 (Strategy Builder UI) — strategies must exist to select
- F3 (Strategy Versioning) — `StrategyRevision` entity for revision tracking
- MediatR — CQRS command/query pattern
- Angular Material — `mat-select`, `mat-table` components
- Entity Framework Core — migrations, repository queries

## Success Criteria

- All backtest runs linked to a strategy carry `StrategyId` and `StrategyRevisionId`
- Strategy picker is the primary entry point for backtesting (manual entry removed from UI)
- Bidirectional navigation works: strategy list → backtest, builder → backtest, result → strategy
- Strategy-scoped backtest history panel shows runs grouped by revision
- Deleted strategies show "deleted" indicator on linked backtests
- All backend tests pass; frontend builds and lints cleanly

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-04-02T22:36:25Z | 2026-04-03T07:26:04Z |
| Plan Implementer | implemented | 2026-04-03T07:57:08Z | 2026-04-03T08:48:58Z |
