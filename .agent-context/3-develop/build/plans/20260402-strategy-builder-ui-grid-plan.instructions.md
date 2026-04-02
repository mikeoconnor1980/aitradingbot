applyTo: ".agent-context/3-develop/build/changes/20260402-strategy-builder-ui-grid-changes.md"
currentAgent: "None"
agentStartedAt: "2026-04-02T21:12:53Z"
status: "complete"
lastUpdated: "2026-04-02T21:12:53Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: F2 — Strategy Builder UI (Grid Template)

## Overview

Build the full Strategy Builder UI layout with grid as the only working template, including a strategy list page, backend CRUD API via MediatR CQRS, reference data endpoint, validation, preview, and two-column responsive layout. Also fix Angular backtest models broken by F1.

## PBI Details

**PBI**: F2 — Strategy Builder UI (Grid Template)
**Location**: `.agent-context/3-develop/backlog/draft/strategy-input/F2-strategy-builder-ui-grid.md`
**Depends On**: F1 (Extensible Strategy Schema) — fully implemented

> As a **trader**, I want to **build a grid strategy using a visual form** so that **I can configure, validate, and save strategies without editing JSON**.

### Acceptance Criteria

- [ ] **Given** the strategy list page (`/strategies`), **When** loaded, **Then** all active strategies for the current user are displayed with name, market, timeframe, direction, mode, created, and updated columns
- [ ] **Given** the strategy list page, **When** "New Strategy" is clicked, **Then** the user navigates to `/strategies/new`
- [ ] **Given** the Strategy Builder page, **When** loaded, **Then** two-column layout (desktop) or single-column (mobile) is displayed
- [ ] **Given** "Grid" template selected, **When** form renders, **Then** Grid Config card is active; Trend Filter and Entry Conditions cards show "Available in signal mode" (disabled); no Enabled toggle is shown
- [ ] **Given** the Strategy Details card, **When** Market dropdown is opened, **Then** options are populated from `GET /api/reference-data/markets` using BTC-USD format
- [ ] **Given** the grid config card, **When** levels = 10 and spacing = 0.5, **Then** preview shows "Deploy a long grid... with 10 levels at 0.5% spacing"
- [ ] **Given** grid levels set to 51, **When** the field loses focus, **Then** an inline validation error is shown and the Save button is disabled
- [ ] **Given** a valid grid strategy form, **When** "Save Strategy" clicked, **Then** canonical JSON is sent to `POST /api/strategies` with `strategyMode = "grid"` and `trendFilter = null`, `entryConditions = null`
- [ ] **Given** a successful save, **When** the server responds 201, **Then** the user navigates to `/strategies` and a success snackbar is displayed
- [ ] **Given** a strategy name already used by this user, **When** "Save Strategy" clicked, **Then** HTTP 409 is returned and the error is shown in the validation panel
- [ ] **Given** the server returns HTTP 400 with validation errors, **When** displayed, **Then** errors appear in the validation card with severity and field path
- [ ] **Given** `GET /api/strategies/{id}`, **When** an existing grid strategy is loaded in edit mode, **Then** the form is populated with all values
- [ ] **Given** the edit flow, **When** strategy is updated and saved, **Then** `PUT /api/strategies/{id}` is called and the user navigates to `/strategies` with a success snackbar
- [ ] **Given** the strategy list page, **When** "Delete" is clicked on a strategy, **Then** a confirmation dialog appears; on confirm, the strategy is soft-deleted and removed from the list
- [ ] **Given** unsaved changes in the builder, **When** the Cancel button is clicked, **Then** a confirmation dialog appears warning about unsaved changes
- [ ] **Given** "EMA Pullback" template, **When** selected, **Then** it shows "Coming soon" and is not selectable
- [ ] **Given** the exit rules card, **When** TP type dropdown is opened, **Then** `fixed_percent` is selectable; `risk_reward`, `atr_multiple` etc. show "Coming soon"
- [ ] **Given** the JSON preview (developer mode), **When** toggled on, **Then** the canonical JSON is displayed matching F1's schema

## Objectives

- Create `Strategy` and `StrategyConfig` domain entities with EF Core persistence
- Implement MediatR CQRS commands/queries for strategy CRUD
- Extend existing `StrategiesController` with full CRUD endpoints
- Create `ReferenceDataController` for markets/timeframes
- Build complete Angular Strategy Builder UI with all card components
- Build Strategy List page with edit/delete operations
- Fix Angular backtest models broken by F1 migration

### Discovery References

- F1 provides: `StrategyConfig` record, `IStrategyValidator`, `StrategyJsonOptions.Default`, `POST /api/strategies/validate`
- `HyperliquidAssetMapper` has static `DisplayToCoin` dictionary and `TimeframeToIntervalMs` — uses `BTC-PERP` display format (PBI wants `BTC-USD`)
- `IHyperliquidAssetMetadataCache.GetAllAsync()` pools live assets from exchange — available in `TradingApp.Api.Services`
- Existing controller pattern: `ApiController` base with `IMediator` + `IdentityService`; `StrategiesController` currently inherits `ControllerBase` directly
- `HttpGlobalExceptionFilter` already maps `DomainException` → 400, `NotFoundException` → 404
- Angular: standalone components, `inject()`, `ApiRestClient`, reactive forms with typed `FormGroup`, Angular Material dark theme, `@for`/`@if` control flow

### Project Patterns

- `src/TradingApp.Domain/Entities/BacktestRun.cs` — Domain entity with static factory method + private setters
- `src/TradingApp.Persistence/Repositories/BacktestRunRepository.cs` — Repository pattern (scoped, DbContext injected)
- `src/TradingApp.Persistence/TradingAppDbContext.cs` — Inline OnModelCreating, `HasConversion<double>()` for decimals
- `src/TradingApp.Persistence/PersistenceServiceExtensions.cs` — DI registration for repos
- `src/TradingApp.Application/Abstractions/Commands/Command.cs` — `CreateCommand : IRequest<Guid>`, `Command : IRequest<Unit>`
- `src/TradingApp.Application/Abstractions/Commands/CommandHandler.cs` — Base handler classes
- `src/TradingApp.Application/Abstractions/Queries/Query.cs` — `Query<T> : IRequest<T>`
- `src/TradingApp.Application/Backtesting/RunBacktestCommand.cs` — Command + Handler co-located
- `src/TradingApp.Api/Infrastructure/ApiController.cs` — Base controller with `IMediator` + `IdentityService`
- `src/TradingApp.Api/Controllers/StrategiesController.cs` — Existing stub with `POST /api/strategies/validate`
- `src/TradingApp.Api/Controllers/BacktestsController.cs` — MediatR-based controller extending `ApiController`
- `src/TradingApp.Infrastructure/Hyperliquid/HyperliquidAssetMapper.cs` — Static reference data source
- `frontend/trading-ui/src/app/core/services/api-rest-client.service.ts` — Base HTTP wrapper
- `frontend/trading-ui/src/app/core/services/notification.service.ts` — MatSnackBar wrapper
- `frontend/trading-ui/src/app/features/backtesting/backtest-page.component.ts` — Feature page pattern
- `frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.ts` — Reactive form pattern
- `tests/TradingApp.Api.Tests/Infrastructure/BaseControllerTests.cs` — WebApplicationFactory base
- `tests/TradingApp.Api.Tests/Controllers/StrategiesControllerTests.cs` — Existing strategy test pattern
- `tests/TradingApp.Application.Tests/StrategyAuthoring/Validation/SchemaValidatorTests.cs` — Validator test pattern

### [x] Phase 1: Domain Entities and Persistence

**Complexity**: Medium | **Risk**: Low

- [x] Task 1.1: Create Strategy domain entity
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-01-details.md#task-11-create-strategy-domain-entity

- [x] Task 1.2: Create IStrategyRepository interface
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-01-details.md#task-12-create-istrategyrepository-interface

- [x] Task 1.3: Add Strategy to DbContext and create EF migration
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-01-details.md#task-13-add-strategy-to-dbcontext-and-create-ef-migration

- [x] Task 1.4: Implement StrategyRepository
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-01-details.md#task-14-implement-strategyrepository

- [x] Task 1.5: Register repository in DI
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-01-details.md#task-15-register-repository-in-di

- [x] Task 1.6: Write Strategy entity tests
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-01-details.md#task-16-write-strategy-entity-tests

- [x] Task 1.7: Build and run all tests
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-01-details.md#task-17-build-and-run-all-tests

### [x] Phase 2: Backend CQRS Commands, Queries, and Controller

**Complexity**: High | **Risk**: Medium

- [x] Task 2.1: Create strategy DTOs
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-02-details.md#task-21-create-strategy-dtos

- [x] Task 2.2: Create DuplicateStrategyNameException
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-02-details.md#task-22-create-duplicatestrategynameexception

- [x] Task 2.3: Create CreateStrategyCommand + Handler
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-02-details.md#task-23-create-createstrategycommand--handler

- [x] Task 2.4: Create UpdateStrategyCommand + Handler
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-02-details.md#task-24-create-updatestrategycommand--handler

- [x] Task 2.5: Create GetStrategiesQuery + Handler
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-02-details.md#task-25-create-getstrategiesquery--handler

- [x] Task 2.6: Create GetStrategyByIdQuery + Handler
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-02-details.md#task-26-create-getstrategybyidquery--handler

- [x] Task 2.7: Create DeleteStrategyCommand + Handler
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-02-details.md#task-27-create-deletestrategycommand--handler

- [x] Task 2.8: Refactor StrategiesController to extend ApiController and add CRUD endpoints
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-02-details.md#task-28-refactor-strategiescontroller-to-extend-apicontroller-and-add-crud-endpoints

- [x] Task 2.9: Register DuplicateStrategyNameException in HttpGlobalExceptionFilter
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-02-details.md#task-29-register-duplicatestrategynameexception-in-httpglobalexceptionfilter

- [x] Task 2.10: Write controller integration tests
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-02-details.md#task-210-write-controller-integration-tests

- [x] Task 2.11: Build and run all tests
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-02-details.md#task-211-build-and-run-all-tests

### [x] Phase 3: Reference Data API

**Complexity**: Low | **Risk**: Low

- [x] Task 3.1: Create ReferenceDataController with markets endpoint
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-03-details.md#task-31-create-referencedatacontroller-with-markets-endpoint

- [x] Task 3.2: Write ReferenceDataController tests
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-03-details.md#task-32-write-referencedatacontroller-tests

- [x] Task 3.3: Build and run all tests
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-03-details.md#task-33-build-and-run-all-tests

### [x] Phase 4: Frontend Strategy Builder Components

**Complexity**: High | **Risk**: Medium

- [x] Task 4.1: Create TypeScript strategy models and enums
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-04-details.md#task-41-create-typescript-strategy-models-and-enums

- [x] Task 4.2: Create strategy API service and reference data service
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-04-details.md#task-42-create-strategy-api-service-and-reference-data-service

- [x] Task 4.3: Create StrategyTemplateSelectorComponent
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-04-details.md#task-43-create-strategytemplateselectorcomponent

- [x] Task 4.4: Create StrategyDetailsCardComponent
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-04-details.md#task-44-create-strategydetailscardcomponent

- [x] Task 4.5: Create GridConfigCardComponent
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-04-details.md#task-45-create-gridconfigcardcomponent

- [x] Task 4.6: Create ExitRulesCardComponent
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-04-details.md#task-46-create-exitrulescardcomponent

- [x] Task 4.7: Create RiskManagementCardComponent
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-04-details.md#task-47-create-riskmanagementcardcomponent

- [x] Task 4.8: Create TrendFilterCardComponent and EntryConditionsCardComponent (disabled shells)
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-04-details.md#task-48-create-trendfiltercard-and-entryconditionscard-disabled-shells

- [x] Task 4.9: Create PreviewSummaryCardComponent
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-04-details.md#task-49-create-previewsummarycardcomponent

- [x] Task 4.10: Create ValidationCardComponent
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-04-details.md#task-410-create-validationcardcomponent

- [x] Task 4.11: Create JsonPreviewCardComponent
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-04-details.md#task-411-create-jsonpreviewcardcomponent

- [x] Task 4.12: Create StrategyMapperService and StrategyValidationService
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-04-details.md#task-412-create-strategymapperservice-and-strategyvalidationservice

- [x] Task 4.13: Create StrategyBuilderPageComponent with two-column layout
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-04-details.md#task-413-create-strategybuilderpagecomponent-with-two-column-layout

- [x] Task 4.14: Add routes and navigation
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-04-details.md#task-414-add-routes-and-navigation

- [x] Task 4.15: Frontend build and lint
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-04-details.md#task-415-frontend-build-and-lint

### [x] Phase 5: Strategy List Page, Integration, and Backtest Fix

**Complexity**: Medium | **Risk**: Low

- [x] Task 5.1: Create StrategyListPageComponent
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-05-details.md#task-51-create-strategylistpagecomponent

- [x] Task 5.2: Create UnsavedChangesGuard
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-05-details.md#task-52-create-unsavedchangesguard

- [x] Task 5.3: Wire confirmation dialogs for cancel and delete
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-05-details.md#task-53-wire-confirmation-dialogs-for-cancel-and-delete

- [x] Task 5.4: Fix Angular backtest models for F1 schema
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-05-details.md#task-54-fix-angular-backtest-models-for-f1-schema

- [x] Task 5.5: Write Angular component tests
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-05-details.md#task-55-write-angular-component-tests

- [x] Task 5.6: Frontend build, lint, and full test suite
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-builder-ui-grid-phase-05-details.md#task-56-frontend-build-lint-and-full-test-suite

## Scoping Summary

| Phase | Complexity | Risk |
|-------|-----------|------|
| Phase 1: Domain Entities and Persistence | Medium | Low |
| Phase 2: Backend CQRS Commands, Queries, and Controller | High | Medium |
| Phase 3: Reference Data API | Low | Low |
| Phase 4: Frontend Strategy Builder Components | High | Medium |
| Phase 5: Strategy List Page, Integration, and Backtest Fix | Medium | Low |
| **Total** | **High** | **Medium** |

### Scoping Notes

- F1 is fully implemented — all models, validators, serialization, and `POST /api/strategies/validate` are available
- `StrategiesController` already exists at `api/strategies` with `validate` endpoint; will be refactored to extend `ApiController` and add CRUD
- No `Strategy`/`StrategyConfig` domain entities exist yet — created in Phase 1
- `HyperliquidAssetMapper` uses `BTC-PERP` display format; F2 PBI wants `BTC-USD` format — the reference data endpoint will map to `BTC-USD` format
- Angular backtest models still use old flat `GridStrategyConfig` from F0 — will be fixed in Phase 5
- Strategy activation (Enabled toggle, worker integration) is deferred per PBI decision #13

## Dependencies

- F1 (Extensible Strategy Schema) — fully implemented
- Angular Material — already installed
- MediatR — already registered in Program.cs
- EF Core + SQLite — already configured

## Success Criteria

- All backend CRUD endpoints pass integration tests
- Strategy Builder UI renders correctly in desktop and mobile layouts
- Grid strategy can be created, edited, and deleted through the UI
- Validation errors display inline and in the validation panel
- Duplicate name returns HTTP 409 and displays error in UI
- Backtest UI continues to work after model migration
- `ng build` and `dotnet build` succeed with no errors

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|----------|
| Implementation Planner | planned | 2026-04-02T18:00:00Z | 2026-04-02T18:15:11Z |
| Plan Reviewer | reviewed | 2026-04-02T18:30:00Z | 2026-04-02T18:45:00Z |
| Plan Implementer | in-progress | 2026-04-02T20:00:41Z | - |
| Plan Implementer | implemented | 2026-04-02T20:11:22Z | 2026-04-02T21:02:58Z |
| Implementation Reviewer | complete | 2026-04-02T21:12:53Z | 2026-04-02T21:12:53Z |
