<!-- markdownlint-disable-file -->
# Release Changes: F3.5 - Strategy-Backtest Integration

**Related Plan**: 20260402-strategy-backtest-integration-plan.instructions.md
**Implementation Date**: 2026-04-03

## Summary

Implements strategy-linked backtesting across backend and frontend phases, including strategy metadata persistence, strategy-scoped history, and navigation between strategy authoring and backtest flows.

## Changes

### Added

<!-- Phase 1: Backend — Domain, Persistence & Tests -->
- src/TradePilot.Persistence/Migrations/20260403080004_AddStrategyLinkToBacktestRuns.cs: Added the EF Core migration for nullable BacktestRun strategy link columns and index.
- src/TradePilot.Persistence/Migrations/20260403080004_AddStrategyLinkToBacktestRuns.Designer.cs: Added the generated EF migration designer metadata.
- tests/TradePilot.Domain.Tests/Entities/BacktestRunTests.cs: Added domain coverage for CreateQueued strategy metadata behavior.

<!-- Phase 2: Backend — Application, API & Tests -->
- src/TradePilot.Application/Backtesting/GetBacktestsByStrategyQuery.cs: Added the strategy-scoped backtest query and ownership-validating handler.

<!-- Phase 4: Frontend — Navigation & Backtest History -->
- frontend/trading-ui/src/app/features/strategy-builder/components/strategy-backtest-history/strategy-backtest-history.component.ts: Added the standalone strategy-scoped backtest history component with revision grouping and navigation to saved results.
- frontend/trading-ui/src/app/features/strategy-builder/components/strategy-backtest-history/strategy-backtest-history.component.html: Added the history panel template with grouped tables, loading state, and empty state.
- frontend/trading-ui/src/app/features/strategy-builder/components/strategy-backtest-history/strategy-backtest-history.component.scss: Added styles for the history card, grouped tables, clickable rows, and metric states.

### Modified

<!-- Phase 1: Backend — Domain, Persistence & Tests -->
- src/TradePilot.Domain/Entities/BacktestRun.cs: Added nullable StrategyId and StrategyRevisionId properties and extended CreateQueued to accept optional strategy metadata.
- src/TradePilot.Persistence/TradePilotDbContext.cs: Configured BacktestRun strategy link columns and added an index on StrategyId.
- src/TradePilot.Application/Abstractions/Repositories/IBacktestRunRepository.cs: Added the strategy-scoped paged summary repository method contract.
- src/TradePilot.Persistence/Repositories/BacktestRunRepository.cs: Implemented strategy-scoped paged summary retrieval and projected strategy metadata.
- src/TradePilot.Application/Backtesting/Models/BacktestRunSummary.cs: Added nullable StrategyId, StrategyRevisionId, and StrategyName fields.
- src/TradePilot.Persistence/Migrations/TradePilotDbContextModelSnapshot.cs: Updated the EF model snapshot for the new BacktestRun columns and index.
- tests/TradePilot.Persistence.Tests/Repositories/BacktestRunRepositoryTests.cs: Added persistence coverage for strategy-scoped backtest summary queries.

<!-- Phase 2: Backend — Application, API & Tests -->
- src/TradePilot.Application/Backtesting/RunBacktestCommand.cs: Added optional strategy linkage to the command and resolved strategy revision metadata in the handler.
- src/TradePilot.Application/Backtesting/Models/BacktestRunResponse.cs: Added nullable strategy metadata fields to the backtest response model.
- src/TradePilot.Application/Backtesting/BacktestRunResponseMapper.cs: Mapped strategy id and revision id from BacktestRun into the response DTO.
- src/TradePilot.Persistence/Repositories/BacktestRunRepository.cs: Added strategy metadata to the global summary projection used by the backtest list.
- src/TradePilot.Api/Models/RunBacktestRequest.cs: Added optional StrategyId and conditional validation so strategy-based POST requests bypass manual strategy-config requirements.
- src/TradePilot.Api/Models/BacktestSummaryDto.cs: Added strategy id, revision id, and name fields to the API summary DTO.
- src/TradePilot.Api/Controllers/BacktestsController.cs: Added saved-strategy resolution for POST runs and API-layer strategy-name enrichment for the global list.
- src/TradePilot.Api/Controllers/StrategiesController.cs: Added GET /api/strategies/{id}/backtests and strategy-name enrichment for the returned summaries.
- tests/TradePilot.Api.Tests/Controllers/BacktestsControllerTests.cs: Added controller coverage for strategy-linked backtest submission and adjusted test wiring for strategy repositories.
- tests/TradePilot.Api.Tests/Controllers/StrategiesControllerTests.cs: Added coverage for the strategy-scoped backtest history endpoint.

<!-- Phase 3: Frontend — Strategy Picker & Backtest Form Refactor -->
- frontend/trading-ui/src/app/core/models/backtest.model.ts: Added optional strategy-linked request and response fields for strategy-aware backtest flows.
- frontend/trading-ui/src/app/core/services/backtest.service.ts: Added strategy-scoped backtest history retrieval against the nested strategies endpoint.
- frontend/trading-ui/src/app/core/services/backtest.service.spec.ts: Added coverage for the strategy-scoped backtest history API call.
- frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.ts: Replaced the manual strategy-config form with a saved-strategy picker, read-only preview, and strategy-derived validation and run payloads.
- frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.html: Reworked the template to show the strategy selector, preview panel, and reduced backtest-only inputs.
- frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.scss: Added styling for the new strategy preview and streamlined form layout.
- frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.spec.ts: Replaced legacy manual-form tests with strategy-picker, prefill, validation, and strategy-load failure coverage.
- frontend/trading-ui/src/app/features/backtesting/backtest-page.component.ts: Added strategyId query-param handling, GUID validation, and notification-based invalid-link handling.
- frontend/trading-ui/src/app/features/backtesting/backtest-page.component.html: Passed the resolved strategyId into the backtest form for deep-link preselection.

<!-- Phase 4: Frontend — Navigation & Backtest History -->
- frontend/trading-ui/src/app/features/strategy-builder/strategy-list-page.component.ts: Added strategy-to-backtest navigation and tooltip support for the new row action.
- frontend/trading-ui/src/app/features/strategy-builder/strategy-list-page.component.html: Added the Backtest action button to each strategy row.
- frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts: Added builder-page backtest navigation and registered the new history component.
- frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.html: Added the edit-mode backtest button and rendered the strategy backtest history panel in the side column.
- frontend/trading-ui/src/app/features/backtesting/backtest-list/backtest-list.component.ts: Added strategy navigation support, deleted-strategy detection, and the strategy column in the table model.
- frontend/trading-ui/src/app/features/backtesting/backtest-list/backtest-list.component.html: Rendered the new Strategy column with active links, deleted labels, and empty-state dash behavior.
- frontend/trading-ui/src/app/features/backtesting/backtest-list/backtest-list.component.scss: Added link and deleted-label styling for the strategy column.
- frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.ts: Added strategy edit-page navigation and deleted-strategy handling for result details.
- frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.html: Added strategy name and revision badge to the result configuration summary.
- frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.scss: Added strategy link and revision badge styles in the result detail view.
- frontend/trading-ui/src/app/features/backtesting/backtest-page.component.ts: Added rerun query-param navigation, result deep-link handling via viewResult, and router support.
- frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.ts: Extended rerun behavior to support unavailable saved strategies by falling back to the historical config snapshot for validation and rerun submission.
- frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.html: Added the snapshot fallback preview for reruns when the linked strategy can no longer be loaded.
- frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.scss: Added warning-state styling for the unavailable-strategy snapshot preview.

### Removed

## Test Results

<!-- Phase 1: Backend — Domain, Persistence & Tests -->
- TradePilot.Domain.Tests: 46/46 passed
- TradePilot.Persistence.Tests: 27/27 passed
- Focused file tests: 8/8 passed
- Architecture Tests: NOT RUN — no separate architecture test suite or project was present in the workspace
- Solution Build: PASSED

<!-- Phase 2: Backend — Application, API & Tests -->
- TradePilot.Api.Tests controller file scope: 96/96 passed
- TradePilot.Api.Tests project: 175/175 passed
- Architecture Tests: NOT RUN — no separate architecture test suite or project exists in the workspace
- Solution Build: PASSED

<!-- Phase 3: Frontend — Strategy Picker & Backtest Form Refactor -->
- Angular Build: PASSED (npx ng build --configuration development)
- Angular Lint: PASSED (npx ng lint)
- Architecture Tests: NOT RUN — no separate frontend architecture test task was defined for this phase

<!-- Phase 4: Frontend — Navigation & Backtest History -->
- Angular Build: PASSED
- Angular Lint: PASSED
- Architecture Tests: NOT RUN — no separate frontend architecture test task was defined for this phase

## Issues

<!-- Phase 1: Backend — Domain, Persistence & Tests -->
- The initial migration command using TradePilot.Api as the startup project failed because that project does not reference Microsoft.EntityFrameworkCore.Design. Resolved by generating the migration directly from TradePilot.Persistence using the existing design-time DbContext factory.
- The new persistence test initially failed to compile because a FluentAssertions expression used `is null` inside an expression tree. Resolved by replacing it with `== null`.

<!-- Phase 2: Backend — Application, API & Tests -->
- The initial verification failed because src/TradePilot.Api/Controllers/BacktestsController.cs referenced the Strategy entity without the domain namespace import. Resolved by adding the missing using and rerunning verification.
- A follow-up build failure in tests/TradePilot.Api.Tests/Controllers/StrategiesControllerTests.cs came from a missing import for BacktestSummaryDto. Resolved by adding the API model namespace.
- Final build surfaced nullable warnings in tests/TradePilot.Api.Tests/Controllers/BacktestsControllerTests.cs. Resolved with explicit null-checked local variables in the touched test setup.

<!-- Phase 3: Frontend — Strategy Picker & Backtest Form Refactor -->
- The initial large backtest form refactor left duplicated legacy content appended in the component, template, styles, and spec files, which caused template and compile failures. Resolved by fully rewriting the affected files to a clean single-definition state and rerunning build and lint.
- Lint failed once on an Array<T> style violation in the new form component. Resolved by switching to T[] and rerunning lint.

<!-- Phase 4: Frontend — Navigation & Backtest History -->
- Angular problems initially reported stale metadata-analysis errors on the builder page imports, but the actual Angular build completed successfully without source changes.
- The first build command attempted to change into a duplicated frontend path because the shared terminal was already in the frontend working directory; rerunning from the existing cwd completed successfully.

## Design Decisions

<!-- Phase 1: Backend — Domain, Persistence & Tests -->
- Kept StrategyId and StrategyRevisionId nullable on BacktestRun and added no foreign key, matching the requirement that backtest history must survive strategy soft-deletion.
- Limited the domain factory change to CreateQueued exactly as specified; the synchronous Create path remains unchanged and leaves the new nullable fields unset.
- Left StrategyName as a nullable summary field populated as null from persistence-layer queries, with API-layer enrichment deferred to a later phase.

<!-- Phase 2: Backend — Application, API & Tests -->
- Used conditional request validation in src/TradePilot.Api/Models/RunBacktestRequest.cs so manual runs still validate Symbol and Intervals, while strategy-based runs can omit inline strategy config entirely.
- Kept strategy-name enrichment in the API controllers instead of pushing it into persistence because the repository contract does not expose a batch strategy lookup and the plan explicitly placed name enrichment at the API layer.
- Treated inactive strategies as not found for strategy-based execution and strategy-scoped history reads to stay aligned with existing strategy ownership and read behavior.

<!-- Phase 3: Frontend — Strategy Picker & Backtest Form Refactor -->
- The form now emits strategy-based backtest requests with strategyId only and does not send inline strategyConfig, symbol, or intervals, because the backend resolves the saved strategy configuration server-side.
- Coverage validation derives symbol and intervals from the selected strategy’s market and timeframe, keeping the frontend aligned with the saved-strategy execution path.
- Query-param handling validates the incoming strategyId as a GUID before preselecting the form, matching the acceptance criterion for invalid deep links.

<!-- Phase 4: Frontend — Navigation & Backtest History -->
- Added query-param handling for viewResult in the backtesting page so the new strategy history panel can deep-link directly to a saved backtest result instead of only landing on the generic backtesting page.
- Added a rerun fallback path in the backtest form for deleted or unavailable saved strategies, using the historical backtest snapshot to keep rerun behavior functional rather than blocking the user on a missing strategy record.
- Treated deleted strategies purely from the API-provided strategyName suffix, which kept the Phase 4 change frontend-only and aligned with the existing backend enrichment behavior.

## Review Hints

- Phase 2 should confirm whether the existing non-strategy GetPagedSummariesAsync projection also needs StrategyId and StrategyRevisionId when wiring the global backtest list.
- Review whether any direct backtest creation flows using the synchronous BacktestRun.Create path should eventually capture strategy metadata as well.

<!-- Phase 2: Backend — Application, API & Tests -->
- Review the saved-strategy execution path in src/TradePilot.Api/Controllers/BacktestsController.cs: it currently derives the backtest symbol directly from StrategyConfig.Market, so if saved strategies use a different market format than the backtesting pipeline expects, that cross-feature mapping will need to be handled in a later phase.

<!-- Phase 3: Frontend — Strategy Picker & Backtest Form Refactor -->
- Review the strategy preview labels against the exact strategy-builder terminology, especially entry mode and position sizing formatting, since this phase intentionally presents a read-only summary rather than the full editable schema.
- Review the prefill path for rerun scenarios together with the Phase 4 navigation changes, since this phase prepares the form to accept strategy-linked rerun inputs but does not yet implement the broader cross-page navigation work.

<!-- Phase 4: Frontend — Navigation & Backtest History -->
- Review the deleted-strategy UI path end-to-end, especially the reliance on the backend sending the exact " (deleted)" suffix, because the frontend currently infers disabled-link behavior from that display contract alone.

## Release Summary

Implemented all four phases of strategy-linked backtesting. Backend work now persists optional strategy and revision metadata on backtest runs, exposes strategy-scoped backtest history, and enriches list and detail responses with strategy context. Frontend work replaces manual strategy entry with a saved-strategy picker, supports deep links and reruns by strategy, adds bidirectional navigation between strategies and backtests, and introduces a revision-grouped backtest history panel on the strategy builder page.

Verification completed with passing solution build, passing domain, persistence, and API test suites touched by the backend changes, and passing Angular build and lint for the frontend changes. No separate architecture test suite was present in the workspace.