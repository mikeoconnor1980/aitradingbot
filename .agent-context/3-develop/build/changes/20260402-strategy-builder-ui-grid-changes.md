<!-- markdownlint-disable-file -->
# Release Changes: F2 - Strategy Builder UI (Grid Template)

**Related Plan**: 20260402-strategy-builder-ui-grid-plan.instructions.md
**Implementation Date**: 2026-04-02

## Summary

Implements the F2 strategy builder experience across persistence, API, reference data, and Angular UI for the grid template, including the final list workflow, unsaved-changes protection, and F1-aligned backtest model migration.

## Changes

### Added

<!-- Phase 1: Domain Entities and Persistence -->
- src/TradingApp.Domain/Entities/Strategy.cs: Added the Strategy domain entity with create, update, and soft-delete behavior.
- src/TradingApp.Application/Abstractions/Repositories/IStrategyRepository.cs: Added the strategy repository abstraction for Phase 1 persistence operations.
- src/TradingApp.Persistence/Repositories/StrategyRepository.cs: Added the EF Core repository implementation for strategy persistence.
- src/TradingApp.Persistence/Migrations/20260402203000_AddStrategies.cs: Added the EF migration that creates the Strategies table and its indexes.
- tests/TradingApp.Domain.Tests/Entities/StrategyTests.cs: Added domain tests covering strategy creation, update, soft delete, and guard clauses.

<!-- Phase 2: Backend CQRS Commands, Queries, and Controller -->
- src/TradingApp.Application/StrategyAuthoring/Models/StrategyDto.cs: Added the full strategy read DTO used by the edit/details endpoint.
- src/TradingApp.Application/StrategyAuthoring/Models/StrategySummaryDto.cs: Added the strategy list DTO used by the list endpoint.
- src/TradingApp.Application/Abstractions/Exceptions/DuplicateStrategyNameException.cs: Added the typed duplicate-name conflict exception for HTTP 409 mapping.
- src/TradingApp.Application/StrategyAuthoring/Commands/CreateStrategyCommand.cs: Added the create command and handler with validation, uniqueness checks, serialization, and persistence.
- src/TradingApp.Application/StrategyAuthoring/Commands/UpdateStrategyCommand.cs: Added the update command and handler with tenant scoping, uniqueness checks, validation, and persistence.
- src/TradingApp.Application/StrategyAuthoring/Commands/DeleteStrategyCommand.cs: Added the delete command and handler with tenant scoping and soft-delete behavior.
- src/TradingApp.Application/StrategyAuthoring/Queries/GetStrategiesQuery.cs: Added the list query and handler that deserialize config into summary fields.
- src/TradingApp.Application/StrategyAuthoring/Queries/GetStrategyByIdQuery.cs: Added the by-id query and handler that return full strategy config for editing.
- src/TradingApp.Persistence/Migrations/20260402203000_AddStrategies.Designer.cs: Added the EF Core migration designer metadata so the Strategies migration is discovered and applied.

<!-- Phase 3: Reference Data API -->
- src/TradingApp.Api/Controllers/ReferenceDataController.cs: Added the reference-data API controller exposing GET /api/reference-data/markets.
- src/TradingApp.Api/Models/ReferenceDataResponse.cs: Added the API response model for markets and timeframes.
- tests/TradingApp.Api.Tests/Controllers/ReferenceDataControllerTests.cs: Added controller integration coverage for the new reference-data endpoint.

<!-- Phase 4: Frontend Strategy Builder Components -->
- frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts: Added the frontend strategy schema, DTOs, validation models, and template catalog.
- frontend/trading-ui/src/app/features/strategy-builder/services/strategy-api.service.ts: Added CRUD and validate API access for strategies.
- frontend/trading-ui/src/app/features/strategy-builder/services/reference-data.service.ts: Added cached reference-data retrieval for markets and timeframes.
- frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.ts: Added form-to-canonical-strategy mapping for save and preview flows.
- frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.ts: Added client-side validation plus server-side validation integration.
- frontend/trading-ui/src/app/features/strategy-builder/components/strategy-template-selector/strategy-template-selector.component.ts: Added template selection component logic.
- frontend/trading-ui/src/app/features/strategy-builder/components/strategy-template-selector/strategy-template-selector.component.html: Added template selector markup with coming-soon states.
- frontend/trading-ui/src/app/features/strategy-builder/components/strategy-template-selector/strategy-template-selector.component.scss: Added template selector styling aligned to the existing shell theme.
- frontend/trading-ui/src/app/features/strategy-builder/components/strategy-details-card/strategy-details-card.component.ts: Added details card logic and reference-data loading.
- frontend/trading-ui/src/app/features/strategy-builder/components/strategy-details-card/strategy-details-card.component.html: Added details card form fields for name, exchange, market, timeframe, and direction.
- frontend/trading-ui/src/app/features/strategy-builder/components/strategy-details-card/strategy-details-card.component.scss: Added details card layout styling.
- frontend/trading-ui/src/app/features/strategy-builder/components/grid-config-card/grid-config-card.component.ts: Added grid config card logic including manual-anchor visibility.
- frontend/trading-ui/src/app/features/strategy-builder/components/grid-config-card/grid-config-card.component.html: Added grid config form markup.
- frontend/trading-ui/src/app/features/strategy-builder/components/grid-config-card/grid-config-card.component.scss: Added grid config card styling.
- frontend/trading-ui/src/app/features/strategy-builder/components/exit-rules-card/exit-rules-card.component.ts: Added exit rules card logic for TP and SL inputs.
- frontend/trading-ui/src/app/features/strategy-builder/components/exit-rules-card/exit-rules-card.component.html: Added exit rules form markup with disabled coming-soon options.
- frontend/trading-ui/src/app/features/strategy-builder/components/exit-rules-card/exit-rules-card.component.scss: Added exit rules card styling.
- frontend/trading-ui/src/app/features/strategy-builder/components/risk-management-card/risk-management-card.component.ts: Added risk management card logic.
- frontend/trading-ui/src/app/features/strategy-builder/components/risk-management-card/risk-management-card.component.html: Added risk management form markup.
- frontend/trading-ui/src/app/features/strategy-builder/components/risk-management-card/risk-management-card.component.scss: Added risk management card styling.
- frontend/trading-ui/src/app/features/strategy-builder/components/trend-filter-card/trend-filter-card.component.ts: Added disabled trend-filter shell component.
- frontend/trading-ui/src/app/features/strategy-builder/components/trend-filter-card/trend-filter-card.component.html: Added disabled trend-filter shell markup.
- frontend/trading-ui/src/app/features/strategy-builder/components/trend-filter-card/trend-filter-card.component.scss: Added disabled trend-filter shell styling.
- frontend/trading-ui/src/app/features/strategy-builder/components/entry-conditions-card/entry-conditions-card.component.ts: Added disabled entry-conditions shell component.
- frontend/trading-ui/src/app/features/strategy-builder/components/entry-conditions-card/entry-conditions-card.component.html: Added disabled entry-conditions shell markup.
- frontend/trading-ui/src/app/features/strategy-builder/components/entry-conditions-card/entry-conditions-card.component.scss: Added disabled entry-conditions shell styling.
- frontend/trading-ui/src/app/features/strategy-builder/components/preview-summary-card/preview-summary-card.component.ts: Added reactive plain-English preview generation.
- frontend/trading-ui/src/app/features/strategy-builder/components/preview-summary-card/preview-summary-card.component.html: Added preview summary card markup.
- frontend/trading-ui/src/app/features/strategy-builder/components/preview-summary-card/preview-summary-card.component.scss: Added preview summary card styling.
- frontend/trading-ui/src/app/features/strategy-builder/components/validation-card/validation-card.component.ts: Added grouped validation card logic.
- frontend/trading-ui/src/app/features/strategy-builder/components/validation-card/validation-card.component.html: Added validation panel markup for errors, warnings, info, and empty state.
- frontend/trading-ui/src/app/features/strategy-builder/components/validation-card/validation-card.component.scss: Added validation panel styling.
- frontend/trading-ui/src/app/features/strategy-builder/components/json-preview-card/json-preview-card.component.ts: Added JSON preview toggle component logic.
- frontend/trading-ui/src/app/features/strategy-builder/components/json-preview-card/json-preview-card.component.html: Added JSON preview markup.
- frontend/trading-ui/src/app/features/strategy-builder/components/json-preview-card/json-preview-card.component.scss: Added JSON preview styling.
- frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts: Added the top-level builder page with create/edit loading, validation flow, save flow, and two-column composition.
- frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.html: Added builder page layout and card composition.
- frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.scss: Added responsive builder page layout styling.
- frontend/trading-ui/src/app/features/strategy-builder/strategy-list-page.component.ts: Added a minimal placeholder list page component so /strategies resolves during Phase 4.
- frontend/trading-ui/src/app/features/strategy-builder/strategy-list-page.component.html: Added placeholder strategies route markup with New Strategy navigation.
- frontend/trading-ui/src/app/features/strategy-builder/strategy-list-page.component.scss: Added placeholder strategies route styling.

<!-- Phase 5: Strategy List Page, Integration, and Backtest Fix -->
- frontend/trading-ui/src/app/features/strategy-builder/guards/unsaved-changes.guard.ts: Added a can-deactivate guard that prompts before leaving the builder with unsaved changes.
- frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.spec.ts: Added Angular unit tests for client-side strategy validation rules.
- frontend/trading-ui/src/app/features/strategy-builder/strategy-list-page.component.spec.ts: Added list-page tests for empty state, table rendering, and delete flow.
- frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.spec.ts: Added builder-page tests for clean cancel and dirty-form confirmation behavior.

### Modified

<!-- Phase 1: Domain Entities and Persistence -->
- src/TradingApp.Persistence/TradingAppDbContext.cs: Added the Strategies DbSet and EF Core entity configuration for Strategy.
- src/TradingApp.Persistence/PersistenceServiceExtensions.cs: Registered IStrategyRepository in DI.
- src/TradingApp.Persistence/Migrations/TradingAppDbContextModelSnapshot.cs: Updated the EF Core model snapshot to include Strategy.

<!-- Phase 2: Backend CQRS Commands, Queries, and Controller -->
- src/TradingApp.Api/Controllers/StrategiesController.cs: Replaced the validation-only stub with an ApiController-based CRUD controller while preserving POST /validate.
- src/TradingApp.Api/Infrastructure/Filters/HttpGlobalExceptionFilter.cs: Mapped DuplicateStrategyNameException to HTTP 409 with the duplicate_name error code.
- tests/TradingApp.Api.Tests/Controllers/StrategiesControllerTests.cs: Extended controller integration coverage for create, list, get, update, delete, duplicate-name conflict, and not-found behavior, and isolated the tests onto a temporary SQLite database.

<!-- Phase 3: Reference Data API -->
- src/TradingApp.Infrastructure/Hyperliquid/HyperliquidAssetMapper.cs: Added supported coin and timeframe accessors so the controller reuses existing Hyperliquid mapping infrastructure.

<!-- Phase 4: Frontend Strategy Builder Components -->
- frontend/trading-ui/src/app/app.routes.ts: Added lazy-loaded strategy builder routes for /strategies, /strategies/new, and /strategies/:id/edit.
- frontend/trading-ui/src/app/app.component.html: Added the Strategies link to the primary app navigation.

<!-- Phase 5: Strategy List Page, Integration, and Backtest Fix -->
- frontend/trading-ui/src/app/app.routes.ts: Wired the unsaved-changes guard onto the strategy create and edit routes.
- frontend/trading-ui/src/app/app.component.spec.ts: Updated app-shell navigation expectations to include the Strategies link.
- frontend/trading-ui/src/app/core/models/backtest.model.ts: Replaced the flat backtest strategy config with the nested F1-shaped strategy config model.
- frontend/trading-ui/src/app/core/services/backtest.service.spec.ts: Updated backtest service tests to the nested F1 request and response shape.
- frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.ts: Updated request emission and prefill mapping to use nested strategyConfig.grid, exit, and risk fields.
- frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.spec.ts: Updated backtest form specs for nested config emission and prefill behavior.
- frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.ts: Switched result rendering logic to nested grid, exit, and risk values.
- frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.html: Updated displayed config values to use the migrated nested schema getters.
- frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.spec.ts: Updated result component tests to the F1-shaped backtest config.
- frontend/trading-ui/src/app/features/backtesting/backtest-compare/backtest-compare.component.ts: Updated comparison diffs to read nested grid, exit, and risk settings.
- frontend/trading-ui/src/app/features/backtesting/backtest-compare/backtest-compare.component.spec.ts: Updated compare component tests to the nested schema.
- frontend/trading-ui/src/app/features/strategy-builder/strategy-list-page.component.ts: Replaced the placeholder page with the real strategy list, load, edit, and delete behavior.
- frontend/trading-ui/src/app/features/strategy-builder/strategy-list-page.component.html: Added the final list-page table, loading state, empty state, and action buttons.
- frontend/trading-ui/src/app/features/strategy-builder/strategy-list-page.component.scss: Added responsive styling for the final strategy list page.
- frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts: Added cancel confirmation behavior, unsaved-change reporting, and save-time pristine reset to avoid double prompts.

### Removed

## Test Results

<!-- Phase 1: Domain Entities and Persistence -->
- TradingApp.Domain.Tests: 30/30 passed
- TradingApp.Application.Tests: 99/99 passed
- TradingApp.Infrastructure.Tests: 51/51 passed
- TradingApp.Persistence.Tests: 20/20 passed
- TradingApp.Api.Tests: 148/148 passed
- Full Test Suite: 348/348 passed
- Architecture Tests: N/A - no architecture test project exists in this repository

<!-- Phase 2: Backend CQRS Commands, Queries, and Controller -->
- TradingApp.Api.Tests: 155/155 passed
- TradingApp.Domain.Tests: 30/30 passed
- TradingApp.Application.Tests: 99/99 passed
- TradingApp.Persistence.Tests: 20/20 passed
- TradingApp.Infrastructure.Tests: 51/51 passed
- Architecture Tests: N/A - no architecture test project exists in this repository

<!-- Phase 3: Reference Data API -->
- ReferenceDataControllerTests: 1/1 passed
- Build: PASSED via `dotnet build TradingApp.sln --nologo`
- TradingApp.Domain.Tests: 30/30 passed
- TradingApp.Application.Tests: 99/99 passed
- TradingApp.Infrastructure.Tests: 51/51 passed
- TradingApp.Persistence.Tests: 20/20 passed
- TradingApp.Api.Tests: 156/156 passed
- Full Test Suite: 356/356 passed via `dotnet test TradingApp.sln --no-build --logger "console;verbosity=minimal"`
- Architecture Tests: N/A - no architecture test project exists in this repository

<!-- Phase 4: Frontend Strategy Builder Components -->
- Angular Build: PASSED with non-blocking warnings
- Angular Lint: PASSED
- Architecture Tests: N/A - no frontend architecture test target exists for this phase

<!-- Phase 5: Strategy List Page, Integration, and Backtest Fix -->
- Targeted Angular specs: 33/33 passed
- Angular Full Test Suite: 124/124 passed
- Angular Build: PASSED with non-blocking warnings
- Angular Lint: PASSED
- TradingApp.Domain.Tests: 30/30 passed
- TradingApp.Application.Tests: 99/99 passed
- TradingApp.Infrastructure.Tests: 51/51 passed
- TradingApp.Persistence.Tests: 20/20 passed
- TradingApp.Api.Tests: 156/156 passed
- Full .NET Test Suite: 356/356 passed
- Architecture Tests: N/A - no architecture test project exists in this repository

## Issues

<!-- Phase 1: Domain Entities and Persistence -->
- `dotnet ef migrations add` did not produce a usable Strategies migration in this workspace. The migration was then added manually and the EF snapshot updated to match.
- The workspace already contained substantial valid Phase 1 code in an uncommitted state, so that implementation was reused where it matched the phase details.

<!-- Phase 2: Backend CQRS Commands, Queries, and Controller -->
- The existing Strategies migration was not being applied in tests because the manually added migration was missing its designer companion file, so EF migration discovery stopped before the Strategies table migration. This was resolved by adding the missing designer file.
- The strategy controller integration tests initially hit shared database state and background hosted services. The tests were isolated onto a temporary SQLite database and hosted services were removed from the test host.

<!-- Phase 3: Reference Data API -->
- The first targeted API test build failed because a FluentAssertions `OnlyContain` predicate compiled as an expression tree and rejected pattern-matching syntax. The assertion was rewritten to a simpler null-and-suffix check, and the targeted test then passed.

<!-- Phase 4: Frontend Strategy Builder Components -->
- Angular tried to apply its built-in `[formGroup]` directive to child component host elements because the child inputs were also named `formGroup`. This was resolved by renaming those inputs to `group`.
- Angular template expressions do not support array spread syntax in bindings. This was resolved by moving the combined error list into a page getter.
- TypeScript rejected `subscribe(...)` on the create/update observable union. This was resolved by splitting save handling into explicit create and update branches.
- `ng build` reported pre-existing non-blocking budget warnings unrelated to this phase’s new files: the initial bundle exceeded the warning budget, and existing SCSS files in the connection and backtesting features exceeded style warning budgets.

<!-- Phase 5: Strategy List Page, Integration, and Backtest Fix -->
- The first targeted Angular run failed because frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.spec.ts had a malformed object literal after the schema migration. The spec was fixed and the targeted suite rerun.
- The full Angular suite initially failed because frontend/trading-ui/src/app/app.component.spec.ts still expected four nav links even though Strategies is now part of the shell. The assertion was updated and the suite rerun.
- Angular tests still emit existing non-blocking warnings about disabled attributes used with reactive-form directives.
- Angular build still emits existing non-blocking budget warnings: the initial bundle exceeds the warning budget, frontend/trading-ui/src/app/features/backtesting/trade-log-table/trade-log-table.component.scss exceeds the style budget by 660 bytes, and frontend/trading-ui/src/app/features/connection/status-card.component.scss exceeds the style budget by 16 bytes.

## Design Decisions

<!-- Phase 1: Domain Entities and Persistence -->
- Reused the existing Phase 1 entity, repository, DI, and test work already present in the workspace because it matched the phase specification and avoided unnecessary churn.
- Kept the Phase 1 persistence model flattened to a single Strategies table with `ConfigJson`, consistent with the phase details.
- Preserved the filtered unique name index behavior by adding `IX_Strategies_UserId_Name` with filter `[IsActive] = 1` in the migration and snapshot.

<!-- Phase 2: Backend CQRS Commands, Queries, and Controller -->
- Soft-deleted strategies are treated as not found for get, update, and delete operations so CRUD semantics remain consistent after deletion and tenant-scoped reads do not surface inactive rows.
- The create command persists `StrategyType` as `GridStrategy`, matching the current grid-only scope defined by this phase.
- Strategy controller tests were isolated from the shared app database instead of relying on prior migration state because the phase requires stable integration coverage for CRUD behavior.

<!-- Phase 3: Reference Data API -->
- Reused `HyperliquidAssetMapper` for both supported coins and supported timeframes by exposing static accessor methods instead of duplicating reference data in the controller.
- Mapped markets from internal coin symbols to the required UI format using `{coin}-USD` so the endpoint returns `BTC-USD` style names and never exposes `BTC-PERP`.

<!-- Phase 4: Frontend Strategy Builder Components -->
- Added a minimal `StrategyListPageComponent` placeholder so `/strategies` resolves as required by Phase 4 without pulling Phase 5 table and delete behavior into this phase.
- Kept validation split into client-side immediate checks plus server-side validation endpoint calls so the builder can show grouped warnings and info while still disabling save on actual errors.
- Suppressed global HTTP error snackbars for builder validation and save requests handled locally so the validation panel remains the source of truth for expected 400 and 409 authoring feedback.

<!-- Phase 5: Strategy List Page, Integration, and Backtest Fix -->
- Marked the builder form pristine immediately before navigating away after a confirmed cancel or successful save so the new can-deactivate guard does not trigger a second confirmation dialog.
- Kept backtest strategy typing separate from the builder page typing so the backtest UI can preserve its broader entry-mode values while still using the nested F1 schema shape.
- Mapped backtest position sizing into strategyConfig.risk as `fixed_notional` so the migrated request preserves the existing backtest UI semantics.

## Review Hints

<!-- Phase 1: Domain Entities and Persistence -->
- Review the new migration and snapshot together first to confirm the filtered unique index and table shape match the `TradingAppDbContext` configuration.
- Review the `Strategy` entity versioning behavior to confirm `Update()` increments `Version` and `SoftDelete()` preserves historical rows via `IsActive = false`.

<!-- Phase 2: Backend CQRS Commands, Queries, and Controller -->
- Review `src/TradingApp.Api/Controllers/StrategiesController.cs` together with the new CQRS files to confirm the endpoint status codes and tenant-scoped identity flow match the phase detail.
- Review `src/TradingApp.Persistence/Migrations/20260402203000_AddStrategies.Designer.cs`, `src/TradingApp.Persistence/Migrations/20260402203000_AddStrategies.cs`, and `src/TradingApp.Persistence/Migrations/TradingAppDbContextModelSnapshot.cs` together to confirm migration discovery and target model consistency.
- Review `tests/TradingApp.Api.Tests/Controllers/StrategiesControllerTests.cs` for the test-host isolation approach and the new CRUD coverage assertions.

<!-- Phase 3: Reference Data API -->
- Review the `BTC-USD` mapping logic in `ReferenceDataController` together with the new accessor methods on `HyperliquidAssetMapper` to confirm the UI-facing contract stays decoupled from internal `-PERP` naming.

<!-- Phase 4: Frontend Strategy Builder Components -->
- Review the builder page validation flow first, especially the interaction between `StrategyValidationService`, `StrategyMapperService`, and `StrategyBuilderPageComponent`.
- Review the placeholder `/strategies` route decision to confirm it is acceptable for this phase boundary and will be replaced cleanly in Phase 5.
- Review the generated canonical JSON path from form state to `StrategyConfig` to confirm the `source`, `strategyMode`, and disabled-section nulling match the intended contract.

<!-- Phase 5: Strategy List Page, Integration, and Backtest Fix -->
- Review frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts together with frontend/trading-ui/src/app/features/strategy-builder/guards/unsaved-changes.guard.ts to confirm the unsaved-changes flow only prompts once on confirmed cancel or save.
- Review frontend/trading-ui/src/app/core/models/backtest.model.ts and frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.ts together to confirm the nested F1 request shape matches the backend contract and preserves existing backtest behavior.
- Review frontend/trading-ui/src/app/features/strategy-builder/strategy-list-page.component.ts and frontend/trading-ui/src/app/features/strategy-builder/strategy-list-page.component.spec.ts to confirm the final list-page UX fully replaces the Phase 4 placeholder.

## Release Summary

Implemented all five phases of F2. The system now has persisted tenant-scoped strategies with CRUD APIs, reference-data endpoints, a full Angular strategy builder for the grid template, a final strategies list workflow with delete and unsaved-changes protection, and backtest UI models aligned to the F1 nested strategy schema. Verification passed across Angular build, Angular lint, Angular tests, .NET build, and the full .NET test suite, with only pre-existing non-blocking Angular warning-budget issues remaining.
