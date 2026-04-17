<!-- markdownlint-disable-file -->
# Release Changes: Strategy Versioning & Revision History (F3)

**Related Plan**: 20260402-strategy-versioning-plan.instructions.md
**Implementation Date**: 2026-04-03

## Summary

Implements tenant-scoped strategy revision history across persistence, API, and builder UI, including revision snapshots, diffing, restore, and automated revision creation on save.

## Changes

### Added

<!-- Phase 1: Domain & Persistence Foundation -->
- src/TradePilot.Domain/Enums/RevisionSource.cs: Added the revision source enum for revision metadata.
- src/TradePilot.Domain/Entities/StrategyRevision.cs: Added the new strategy revision domain entity with guarded static factory creation.
- src/TradePilot.Application/Abstractions/Repositories/IStrategyRevisionRepository.cs: Added the application-layer repository contract for strategy revisions.
- src/TradePilot.Persistence/Repositories/StrategyRevisionRepository.cs: Added the EF Core repository implementation for revision persistence and retrieval.
- src/TradePilot.Persistence/Migrations/20260403050801_AddStrategyRevisions.cs: Added the EF migration creating StrategyRevisions and Strategy.IsRunning.
- src/TradePilot.Persistence/Migrations/20260403050801_AddStrategyRevisions.Designer.cs: Added the generated EF migration designer metadata.
- tests/TradePilot.Domain.Tests/Entities/StrategyRevisionTests.cs: Added domain tests covering StrategyRevision creation and guard behavior.
- tests/TradePilot.Persistence.Tests/Repositories/StrategyRevisionRepositoryTests.cs: Added persistence tests for revision add, lookup, pagination, and latest-number queries.

<!-- Phase 2: Revision Creation on Strategy Save -->
- src/TradePilot.Application/StrategyAuthoring/Services/IChangeSummaryGenerator.cs: Added the application service contract for revision change summary generation.
- src/TradePilot.Application/StrategyAuthoring/Services/ChangeSummaryGenerator.cs: Added the JSON snapshot comparer that produces bounded human-readable change summaries.
- src/TradePilot.Application/StrategyAuthoring/Services/RevisionSourceMapper.cs: Added shared mapping from strategy entry points to domain revision sources.

<!-- Phase 3: Revision Read Endpoints -->
- src/TradePilot.Application/StrategyAuthoring/Models/StrategyRevisionSummaryDto.cs: Added the revision list response DTO with revision metadata fields.
- src/TradePilot.Application/StrategyAuthoring/Models/StrategyRevisionDto.cs: Added the revision detail response DTO with deserialized strategy config.
- src/TradePilot.Application/StrategyAuthoring/Queries/GetStrategyVersionsQuery.cs: Added the paginated revision list query and handler with tenant ownership checks.
- src/TradePilot.Application/StrategyAuthoring/Queries/GetStrategyRevisionQuery.cs: Added the single revision detail query and handler with JSON snapshot deserialization.

<!-- Phase 4: Diff & Restore Endpoints -->
- src/TradePilot.Application/StrategyAuthoring/Models/FieldChangeDto.cs: Added field-level diff DTO for JSON path old and new values.
- src/TradePilot.Application/StrategyAuthoring/Models/StrategyDiffDto.cs: Added structured diff response DTO for revision comparisons.
- src/TradePilot.Application/StrategyAuthoring/Services/IStrategyDiffService.cs: Added diff service contract for comparing two strategy snapshots.
- src/TradePilot.Application/StrategyAuthoring/Services/StrategyDiffService.cs: Added deep JSON diff implementation for nested strategy config changes.
- src/TradePilot.Application/StrategyAuthoring/Queries/GetStrategyDiffQuery.cs: Added query and handler to load two revisions, validate ownership, and compute a diff.
- src/TradePilot.Application/StrategyAuthoring/Commands/RestoreStrategyVersionCommand.cs: Added command and handler to restore a prior revision as the new active version.
- src/TradePilot.Application/Abstractions/Exceptions/ConflictException.cs: Added application exception for 409 conflict responses.

<!-- Phase 5: Frontend Revision History Panel -->
- frontend/trading-ui/src/app/core/models/paged-result.model.ts: Added a shared generic pagination model for revision history and other frontend paged responses.
- frontend/trading-ui/src/app/features/strategy-builder/components/diff-view/diff-view.component.ts: Added the standalone diff view component class for rendering field-level revision changes.
- frontend/trading-ui/src/app/features/strategy-builder/components/diff-view/diff-view.component.html: Added the diff view template with empty and changed-state rendering.
- frontend/trading-ui/src/app/features/strategy-builder/components/diff-view/diff-view.component.scss: Added responsive styling for old and new revision value comparison rows.
- frontend/trading-ui/src/app/features/strategy-builder/components/revision-history-panel/revision-history-panel.component.ts: Added the standalone revision history panel with pagination, diff loading, and restore actions.
- frontend/trading-ui/src/app/features/strategy-builder/components/revision-history-panel/revision-history-panel.component.html: Added the revision history panel template with table, paginator, loading states, and inline diff display.
- frontend/trading-ui/src/app/features/strategy-builder/components/revision-history-panel/revision-history-panel.component.scss: Added styling for the revision table, paginator, and diff and loading states.

### Modified

<!-- Phase 1: Domain & Persistence Foundation -->
- src/TradePilot.Domain/Entities/Strategy.cs: Added IsRunning state and SetRunningState mutation method.
- src/TradePilot.Persistence/TradePilotDbContext.cs: Added StrategyRevisions DbSet, Strategy.IsRunning mapping, and StrategyRevision EF configuration.
- src/TradePilot.Persistence/PersistenceServiceExtensions.cs: Registered IStrategyRevisionRepository in DI.
- src/TradePilot.Persistence/Migrations/TradePilotDbContextModelSnapshot.cs: Updated the EF snapshot for Strategy.IsRunning and StrategyRevision.
- tests/TradePilot.Domain.Tests/Entities/StrategyTests.cs: Extended Strategy tests to cover IsRunning default and mutation behavior.

<!-- Phase 2: Revision Creation on Strategy Save -->
- src/TradePilot.Application/StrategyAuthoring/Commands/CreateStrategyCommand.cs: Added initial revision creation after strategy persistence using the new source mapper and change summary generator.
- src/TradePilot.Application/StrategyAuthoring/Commands/UpdateStrategyCommand.cs: Captured previous config, generated change summary, and persisted a new revision after each update.
- src/TradePilot.Api/Program.cs: Registered the change summary generator in DI.
- tests/TradePilot.Api.Tests/Controllers/StrategiesControllerTests.cs: Added and tightened integration coverage for initial versioning and persisted revision creation on create and update.

<!-- Phase 3: Revision Read Endpoints -->
- src/TradePilot.Api/Controllers/StrategiesController.cs: Added revision list and revision detail endpoints plus request validation for pagination and revision number inputs.
- tests/TradePilot.Api.Tests/Controllers/StrategiesControllerTests.cs: Added integration coverage for revision list, pagination, revision detail, and 404 scenarios.

<!-- Phase 4: Diff & Restore Endpoints -->
- src/TradePilot.Api/Infrastructure/Filters/HttpGlobalExceptionFilter.cs: Mapped ConflictException to HTTP 409 with error code conflict.
- src/TradePilot.Api/Program.cs: Registered StrategyDiffService in DI.
- src/TradePilot.Api/Controllers/StrategiesController.cs: Added diff and restore endpoints with request validation and response metadata.
- tests/TradePilot.Api.Tests/Controllers/StrategiesControllerTests.cs: Added integration coverage for diff success and error cases, restore success and not found, and running-strategy conflict.

<!-- Phase 5: Frontend Revision History Panel -->
- frontend/trading-ui/src/app/core/models/backtest.model.ts: Re-exported the shared paged result type instead of keeping a duplicate definition.
- frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts: Added revision summary, revision detail, field change, and diff DTO interfaces.
- frontend/trading-ui/src/app/features/strategy-builder/services/strategy-api.service.ts: Added revision list, revision detail, diff, and restore API methods.
- frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts: Registered the revision history panel and added reload handling after a restore.
- frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.html: Rendered the revision history panel in the side column for edit mode only.
- frontend/trading-ui/src/app/features/strategy-builder/components/exit-rules-card/exit-rules-card.component.ts: Replaced an existing lint-failing ternary expression with explicit control flow so phase lint verification could pass.

### Removed

## Test Results

<!-- Phase 1: Domain & Persistence Foundation -->
- Domain and Persistence focused test scope: 70/70 passed.
- Solution Build: PASSED.
- Architecture Tests: Not run — no separate architecture test suite/task was defined for this phase.

<!-- Phase 2: Revision Creation on Strategy Save -->
- StrategiesControllerTests: 20/20 passed.
- Solution Build: PASSED.
- Architecture Tests: Not run — no separate architecture test task was defined for this phase.

<!-- Phase 3: Revision Read Endpoints -->
- StrategiesControllerTests: 18/18 passed.
- Solution Build: PASSED.
- Architecture Tests: Not run — no separate architecture test task was defined for this phase.

<!-- Phase 4: Diff & Restore Endpoints -->
- StrategiesControllerTests: 24/24 passed.
- Solution Build: PASSED.
- Architecture Tests: Not run — no separate architecture test suite or task was defined for this phase.

<!-- Phase 5: Frontend Revision History Panel -->
- Angular Build: PASSED (npx ng build --configuration development).
- Angular Lint: PASSED (npx ng lint).
- Architecture Tests: Not run — no architecture test task was defined for this phase.

## Issues

<!-- Phase 1: Domain & Persistence Foundation -->
- The initial migration command using TradePilot.Api as the startup project failed because that project does not reference Microsoft.EntityFrameworkCore.Design. Resolved by generating the migration with TradePilot.Persistence as both project and startup project, using the existing design-time DbContext factory.

<!-- Phase 2: Revision Creation on Strategy Save -->
- None.

<!-- Phase 3: Revision Read Endpoints -->
- The dedicated test tool reported stale project build failure results for the controller test file even after the project compiled successfully. Resolved by verifying with a direct dotnet test filter for StrategiesControllerTests and then confirming with a full solution build.

<!-- Phase 4: Diff & Restore Endpoints -->
- The dedicated VS Code test tool reported stale project build failures for the controller test file despite a clean compile. Resolved by verifying with a direct filtered dotnet test run for StrategiesControllerTests and confirming with a full solution build.

<!-- Phase 5: Frontend Revision History Panel -->
- ng build initially failed because TypeScript isolated modules requires type-only re-exports. Resolved by changing the shared pagination re-export in frontend/trading-ui/src/app/core/models/backtest.model.ts to export type.
- ng lint initially failed on a pre-existing unused-expression issue in frontend/trading-ui/src/app/features/strategy-builder/components/exit-rules-card/exit-rules-card.component.ts. Resolved with a minimal explicit if statement so the required lint task could complete cleanly.

## Design Decisions

<!-- Phase 1: Domain & Persistence Foundation -->
- Used the existing repository pattern where AddAsync persists immediately with SaveChangesAsync, matching current StrategyRepository and BacktestRunRepository behavior.
- Generated the migration through the persistence project's design-time factory instead of adding EF design tooling to the API project, which kept the change set minimal and aligned with the current repo setup.

<!-- Phase 2: Revision Creation on Strategy Save -->
- Used a dedicated application service for change summary generation so the same JSON comparison logic can be reused later by the diff feature.
- Added a shared RevisionSourceMapper utility to avoid duplicating StrategyEntryPoint to RevisionSource mapping logic across commands.
- Persisted revisions after the existing repository save calls, matching the current repository pattern in the codebase rather than introducing transactional infrastructure in this phase.

<!-- Phase 3: Revision Read Endpoints -->
- Added controller-level validation for page, pageSize, and rev so invalid inputs return HTTP 400 through the existing DomainException mapping, while retaining handler-level defensive checks.
- Kept revision response mapping in the query handlers rather than introducing a mapper abstraction, which matches the existing application-layer query patterns in this codebase.

<!-- Phase 4: Diff & Restore Endpoints -->
- Added controller-level validation for from, to, and rev so invalid revision numbers return HTTP 400 through the existing DomainException mapping before hitting handlers.
- Added an extra integration test for the 409 running-strategy restore guard by toggling IsRunning directly in the SQLite-backed test database, since the phase acceptance criteria required that behavior even though the runtime state is otherwise stubbed.

<!-- Phase 5: Frontend Revision History Panel -->
- Introduced a shared paged-result model in frontend/trading-ui/src/app/core/models/paged-result.model.ts and wired the existing backtest model to re-export it, which avoids keeping multiple divergent pagination contracts in the frontend.
- Used Angular Material table, paginator, expansion panel, and dialog primitives in the new revision panel so the new UI stays consistent with the rest of the frontend and matches the phase detail guidance.
- Reset revision selections when the strategy changes, the page changes, or a restore succeeds so the diff view does not show stale comparisons across different data sets.

## Review Hints

<!-- Phase 1: Domain & Persistence Foundation -->
- Review whether future Phase 2 work needs transactional coordination between StrategyRepository and StrategyRevisionRepository, because both currently save independently following the repo’s existing persistence pattern.

<!-- Phase 2: Revision Creation on Strategy Save -->
- Review whether Phase 4 restore flow should reuse the same revision creation helper pattern introduced here to keep source mapping and summary behavior consistent.
- Review whether future phases should introduce transactional coordination between strategy saves and revision saves if stronger atomicity is required.

<!-- Phase 3: Revision Read Endpoints -->
- Review the choice to expose RevisionSource using Enum.ToString() values such as Ui; if later phases or the frontend need snake_case or display labels, this is the point that will need normalizing.

<!-- Phase 4: Diff & Restore Endpoints -->
- Review whether restore and revision-save flows should eventually be wrapped in an explicit transaction if stronger atomicity is required between StrategyRepository and StrategyRevisionRepository saves.
- Review whether future frontend work wants enum display values normalized, since revision source is still exposed via Enum.ToString() values such as Ui and Restore.

<!-- Phase 5: Frontend Revision History Panel -->
- Review whether the raw revision source strings returned by the backend should later be normalized to friendlier display labels in the UI.
- Review whether revision comparison should eventually allow cross-page selection; the current implementation intentionally resets selection on paginator changes to keep the state predictable.

## Release Summary

Implemented all 5 phases of strategy versioning and revision history. The backend now persists tenant-scoped strategy revisions on create, update, and restore; exposes paginated list, detail, diff, and restore endpoints; and maps conflict conditions for running strategies. The frontend now includes an edit-mode revision history panel with paginated revision browsing, field-level diff visualization, and restore actions. Validation completed with focused backend controller tests, domain and persistence tests, solution builds, and frontend build and lint checks.
