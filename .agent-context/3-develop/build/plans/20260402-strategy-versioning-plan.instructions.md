---
applyTo: ".agent-context/3-develop/build/changes/20260402-strategy-versioning-changes.md"
currentAgent: "None"
agentStartedAt: "2026-04-03T07:29:22Z"
status: "complete"
lastUpdated: "2026-04-03T08:52:59Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: Strategy Versioning & Revision History (F3)

## Overview

Implement strategy versioning so that every save creates a new revision with source metadata, auto-generated change summary, and full JSON snapshot. Provide paginated revision list, deep diff between any two revisions, and restore capability. Frontend revision history panel on the strategy builder page (edit mode).

## PBI Details

**PBI**: [F3-strategy-versioning.md](../../../backlog/draft/strategy-input/F3-strategy-versioning.md)

Every strategy save creates a new revision with source metadata (save origin, optional user label, and auto-generated change summary). Users can view paginated revision history, deep-diff between any two versions, and restore a previous revision as the active version. All data is tenant-scoped by UserId.

### Acceptance Criteria

- [x] **Given** a strategy with no revisions, **When** the trader saves for the first time, **Then** revision 1 is created with change summary "Initial version"
- [x] **Given** a strategy with revision 1 (spacing=0.5), **When** the trader saves with spacing=0.8, **Then** revision 2 is created with change summary listing "gridConfig.spacing: 0.5 → 0.8"
- [x] **Given** a strategy with 3 revisions, **When** the trader requests `GET /versions?page=1&pageSize=2`, **Then** 2 revision metadata items are returned with pagination info
- [x] **Given** revision 2, **When** the trader requests `GET /versions/2`, **Then** the full JSON snapshot for revision 2 is returned
- [x] **Given** revisions 1 and 3 with different grid spacing and exit config, **When** diff requested from=1&to=3, **Then** nested field-level changes are listed with JSON paths, old values, and new values
- [x] **Given** a diff request where from=2 and to=2, **When** submitted, **Then** 400 Bad Request is returned
- [x] **Given** a strategy that is paused, **When** the trader restores revision 1, **Then** a new revision N+1 is created from revision 1's snapshot with source "Restore" and label "Restored from revision 1"
- [x] **Given** a strategy that is actively running, **When** the trader attempts to restore a revision, **Then** 409 Conflict is returned
- [x] **Given** revision history UI, **When** two revisions are selected, **Then** the diff panel highlights changed fields with old/new values
- [x] **Given** a user, **When** they request revisions for another user's strategy, **Then** 404 Not Found is returned

## Objectives

- Add `StrategyRevision` entity and persistence layer for storing full JSON snapshots per revision
- Modify strategy create/update flows to automatically capture revisions with change summaries
- Expose 4 new API endpoints for revision list, detail, diff, and restore
- Add frontend revision history panel to the strategy builder page (edit mode)
- Maintain tenant isolation — all revision data scoped by UserId

### Discovery References

- Strategy entity uses `Version` (int, auto-incrementing on update) and `ConfigJson` (full JSON snapshot, overwritten in-place)
- No `StrategyRevision` entity exists — must create from scratch
- `StrategyEntryPoint` enum exists in Application layer (`UiBuilder`, `NaturalLanguage`, `PineImport`, `Migration`) — F3 adds a separate `RevisionSource` enum in Domain
- `PagedResult<T>` pagination wrapper already exists in Application/Abstractions/Models
- No JSON diff utility exists — custom `StrategyDiffService` needed
- No strategy detail page in frontend — revision panel goes on builder page in edit mode
- `IsRunning` property will be added to Strategy entity (stub, always false) for 409 restore guard
- Repositories share scoped `TradingAppDbContext` — atomicity achieved by adding revision to tracking before existing `SaveChangesAsync` call

### Project Patterns

- `src/TradingApp.Domain/Entities/Strategy.cs` — Entity pattern: sealed class, static factory, private setters, Unix ms timestamps
- `src/TradingApp.Application/StrategyAuthoring/Commands/UpdateStrategyCommand.cs` — CQRS command handler pattern (revision hook point)
- `src/TradingApp.Application/StrategyAuthoring/Commands/CreateStrategyCommand.cs` — Create command pattern (initial revision hook)
- `src/TradingApp.Application/Backtesting/GetBacktestListQuery.cs` — Paginated query handler pattern
- `src/TradingApp.Persistence/Repositories/BacktestRunRepository.cs` — Paginated repository pattern
- `src/TradingApp.Application/Abstractions/Models/PagedResult.cs` — Pagination envelope
- `src/TradingApp.Api/Controllers/StrategiesController.cs` — Controller pattern with MediatR dispatch
- `src/TradingApp.Api/Infrastructure/Filters/HttpGlobalExceptionFilter.cs` — Exception-to-HTTP mapping
- `src/TradingApp.Persistence/TradingAppDbContext.cs` — Inline EF entity configuration
- `tests/TradingApp.Api.Tests/Controllers/StrategiesControllerTests.cs` — Integration test pattern
- `tests/TradingApp.Domain.Tests/Entities/StrategyTests.cs` — Entity test pattern
- `tests/TradingApp.Persistence.Tests/Repositories/CandleRepositoryTests.cs` — Persistence test pattern
- `frontend/trading-ui/src/app/features/strategy-builder/components/json-preview-card/` — Toggle panel pattern
- `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-api.service.ts` — API service pattern

### [x] Phase 1: Domain & Persistence Foundation

**Complexity**: Medium | **Risk**: Low

- [x] Task 1.1: Create StrategyRevision entity and RevisionSource enum
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-versioning-phase-01-details.md#task-11-create-strategyrevision-entity-and-revisionsource-enum

- [x] Task 1.2: Add IsRunning property to Strategy entity
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-versioning-phase-01-details.md#task-12-add-isrunning-property-to-strategy-entity

- [x] Task 1.3: Create IStrategyRevisionRepository and StrategyRevisionRepository
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-versioning-phase-01-details.md#task-13-create-istrategyrevisionrepository-and-strategyrevisionrepository

- [x] Task 1.4: Configure EF Core mapping, DbSet, DI registration, and generate migration
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-versioning-phase-01-details.md#task-14-configure-ef-core-mapping-dbset-di-registration-and-generate-migration

- [x] Task 1.5: Add domain entity tests for StrategyRevision and Strategy.IsRunning
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-versioning-phase-01-details.md#task-15-add-domain-entity-tests

- [x] Task 1.6: Add persistence tests for StrategyRevisionRepository
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-versioning-phase-01-details.md#task-16-add-persistence-tests

### [x] Phase 2: Revision Creation on Strategy Save

**Complexity**: Medium | **Risk**: Medium

- [x] Task 2.1: Create ChangeSummaryGenerator service
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-versioning-phase-02-details.md#task-21-create-changesummarygenerator-service

- [x] Task 2.2: Modify CreateStrategyCommand to create initial revision
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-versioning-phase-02-details.md#task-22-modify-createstrategycommand-to-create-initial-revision

- [x] Task 2.3: Modify UpdateStrategyCommand to create new revision with change summary
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-versioning-phase-02-details.md#task-23-modify-updatestrategycommand-to-create-new-revision

- [x] Task 2.4: Add controller integration tests for revision creation
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-versioning-phase-02-details.md#task-24-add-controller-integration-tests-for-revision-creation

### [x] Phase 3: Revision Read Endpoints

**Complexity**: Medium | **Risk**: Low

- [x] Task 3.1: Create revision DTOs
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-versioning-phase-03-details.md#task-31-create-revision-dtos

- [x] Task 3.2: Create GetStrategyVersionsQuery and GetStrategyRevisionQuery handlers
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-versioning-phase-03-details.md#task-32-create-query-handlers

- [x] Task 3.3: Add controller endpoints for revision list and detail
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-versioning-phase-03-details.md#task-33-add-controller-endpoints

- [x] Task 3.4: Add controller integration tests for revision read endpoints
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-versioning-phase-03-details.md#task-34-add-controller-integration-tests

### [x] Phase 4: Diff & Restore Endpoints

**Complexity**: High | **Risk**: Medium

- [x] Task 4.1: Create StrategyDiffService and diff DTOs
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-versioning-phase-04-details.md#task-41-create-strategydiffservice-and-diff-dtos

- [x] Task 4.2: Create GetStrategyDiffQuery handler
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-versioning-phase-04-details.md#task-42-create-getstrategydiffquery-handler

- [x] Task 4.3: Create RestoreStrategyVersionCommand handler
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-versioning-phase-04-details.md#task-43-create-restorestrategyversion-command-handler

- [x] Task 4.4: Add ConflictException and update exception filter
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-versioning-phase-04-details.md#task-44-add-conflictexception-and-update-exception-filter

- [x] Task 4.5: Add controller endpoints for diff and restore
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-versioning-phase-04-details.md#task-45-add-controller-endpoints

- [x] Task 4.6: Add controller integration tests for diff and restore
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-versioning-phase-04-details.md#task-46-add-controller-integration-tests

### [x] Phase 5: Frontend Revision History Panel

**Complexity**: Medium | **Risk**: Medium

- [x] Task 5.1: Add TypeScript models and extend StrategyApiService
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-versioning-phase-05-details.md#task-51-add-typescript-models-and-extend-strategyapiservice

- [x] Task 5.2: Create RevisionHistoryPanelComponent
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-versioning-phase-05-details.md#task-52-create-revisionhistorypanelcomponent

- [x] Task 5.3: Create DiffViewComponent
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-versioning-phase-05-details.md#task-53-create-diffviewcomponent

- [x] Task 5.4: Integrate panel into StrategyBuilderPageComponent
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-versioning-phase-05-details.md#task-54-integrate-panel-into-strategybuilderpagecomponent

- [x] Task 5.5: Frontend build and lint
  - Details: .agent-context/3-develop/build/plans/details/20260402-strategy-versioning-phase-05-details.md#task-55-frontend-build-and-lint

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: Domain & Persistence Foundation | Medium | Low |
| Phase 2: Revision Creation on Strategy Save | Medium | Medium |
| Phase 3: Revision Read Endpoints | Medium | Low |
| Phase 4: Diff & Restore Endpoints | High | Medium |
| Phase 5: Frontend Revision History Panel | Medium | Medium |
| **Total** | **Medium-High** | **Medium** |

### Scoping Notes

- `IsRunning` property is a stub (always false) — enforced on restore but no mechanism to set it true until execution features land
- No revision pruning/retention policies (explicitly out of scope per PBI)
- Change summary is a simple text format: "field.path: oldValue → newValue" (not LLM-generated)
- Diff computation uses custom JSON comparison via `System.Text.Json` — no external library
- Frontend revision history panel is an expandable section on the builder page (edit mode only), not a separate detail page
- Handler tests go through controller integration tests only (per testing standards)

## Dependencies

- .NET / C# / EF Core (existing)
- System.Text.Json (existing — used for diff computation)
- Angular Material (existing — MatTable, MatExpansionPanel for panel UI)
- MSTest + Moq + FluentAssertions (existing — testing)

## Success Criteria

- All 10 acceptance criteria from the PBI pass
- 4 new API endpoints functional with correct HTTP status codes
- Revision created automatically on every strategy create and update
- Paginated revision list responds within 200ms for up to 100 revisions
- Diff computation responds within 500ms
- All revision endpoints are tenant-scoped (404 for other users' strategies)
- Frontend panel shows revision list, diff view, and restore functionality
- All existing tests continue to pass
- New tests pass for all new functionality

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-04-02T22:33:59Z | 2026-04-02T23:57:39Z |
| Plan Reviewer | plan-reviewed | 2026-04-03T00:01:38Z | 2026-04-03T00:04:55Z |
| Plan Implementer | implemented | 2026-04-03T00:04:55Z | 2026-04-03T05:04:45Z |
| Implementation Reviewer | complete | 2026-04-03T07:29:22Z | 2026-04-03T08:52:59Z |
