<!-- markdownlint-disable-file -->
# Release Changes: The Optimizer - Signal Strategy Parameter Sweep

**Related Plan**: 20260405-strategy-optimizer-plan.instructions.md
**Implementation Date**: 2026-04-05

## Summary

Completed the optimizer feature end to end. This release adds backend sweep orchestration, persistence and API support, plus an Angular optimizer workflow for configuring runs, tracking progress, reviewing winners, browsing history, and promoting results into the strategy builder.

## Changes

### Added

<!-- Phase 1: Backend - Domain Model & Sweep Engine -->
- src/TradingApp.Domain/Enums/OptimizationStatus.cs: Added the optimizer run lifecycle enum for queued, running, completed, and failed states.
- src/TradingApp.Domain/Entities/OptimizationRun.cs: Added the optimizer run aggregate root with queue, progress, completion, and failure state transitions.
- src/TradingApp.Domain/Entities/OptimizationResult.cs: Added persisted ranked result records for storing top sweep outputs and strategy JSON.
- src/TradingApp.Application/Optimization/Models/ParameterBounds.cs: Added the optimizer parameter-bounds model covering exit, leverage, sizing, indicator, and trend-filter ranges.
- src/TradingApp.Application/Optimization/Models/FitnessThresholds.cs: Added configurable optimizer qualification thresholds for win rate, trade count, and drawdown.
- src/TradingApp.Application/Optimization/Models/SweepConfig.cs: Added the top-level optimizer request model for symbol, range, capital, sample size, and thresholds.
- src/TradingApp.Application/Optimization/Services/StrategyConfigGenerator.cs: Added deterministic signal-strategy sampling across RSI, MACD, and Price-vs-EMA combinations with descriptions.
- src/TradingApp.Application/Optimization/Services/FitnessScorer.cs: Added result qualification and composite fitness scoring for ranking sweep outcomes.
- src/TradingApp.Application/Optimization/Services/SweepRunner.cs: Added parallel backtest orchestration, qualification filtering, ranking, and progress reporting.
- tests/TradingApp.Application.Tests/Optimization/StrategyConfigGeneratorTests.cs: Added generator tests for determinism, bounds compliance, and condition coverage.
- tests/TradingApp.Application.Tests/Optimization/FitnessScorerTests.cs: Added scorer tests for threshold checks and edge-case scoring behavior.
- tests/TradingApp.Application.Tests/Optimization/SweepRunnerTests.cs: Added sweep-runner tests for ranking, progress, cancellation, and partial-failure handling.

<!-- Phase 2: Backend - Persistence & API -->
- src/TradingApp.Application/Abstractions/Repositories/IOptimizationRunRepository.cs: Added the optimization persistence contract for runs, ranked results, and paged history retrieval.
- src/TradingApp.Application/Optimization/Models/OptimizationRunResponse.cs: Added the full optimizer run response model returned by API commands and queries.
- src/TradingApp.Application/Optimization/Models/OptimizationResultResponse.cs: Added the ranked-result response model with persisted metrics and strategy JSON.
- src/TradingApp.Application/Optimization/Models/OptimizationRunSummary.cs: Added the optimizer history summary model with top-result metrics for list views.
- src/TradingApp.Application/Optimization/OptimizationRunResponseMapper.cs: Added the internal mapper from optimization entities to API-facing response models.
- src/TradingApp.Application/Optimization/OptimizationJobQueue.cs: Added the channel-backed optimizer work queue for background sweep processing.
- src/TradingApp.Application/Optimization/RunOptimizationCommand.cs: Added the MediatR command that persists queued runs and enqueues optimizer jobs.
- src/TradingApp.Application/Optimization/GetOptimizationResultQuery.cs: Added the MediatR query for loading a run and its ranked results.
- src/TradingApp.Application/Optimization/GetOptimizationListQuery.cs: Added the paged history query for optimizer runs.
- src/TradingApp.Persistence/Repositories/OptimizationRunRepository.cs: Added the EF Core repository implementation for optimization runs, results, and paged history summaries.
- src/TradingApp.Api/Models/RunOptimizationRequest.cs: Added the optimizer API request model with bounds and threshold inputs.
- src/TradingApp.Api/Controllers/OptimizationsController.cs: Added the optimizer REST controller with start, list, and result endpoints.
- src/TradingApp.Api/Services/OptimizationProcessorService.cs: Added the background processor that executes sweeps, persists results, and broadcasts SignalR progress.
- src/TradingApp.Persistence/Migrations/20260405001920_AddOptimizationRuns.cs: Added the EF Core migration for the optimizer run and result tables.
- src/TradingApp.Persistence/Migrations/20260405001920_AddOptimizationRuns.Designer.cs: Added the generated EF Core model snapshot changes for optimizer persistence.

<!-- Phase 3: Frontend - Optimizer Tab & Configuration -->
- frontend/trading-ui/src/app/core/models/optimizer.model.ts: Added optimizer request, progress, run, result, and history contracts for the Angular client.
- frontend/trading-ui/src/app/core/services/optimizer.service.ts: Added the optimizer API service for starting runs and loading run history/results.
- frontend/trading-ui/src/app/features/optimizer/optimizer-page.component.ts: Added the optimizer feature shell with tab state, API orchestration, progress handling, and history/result loading.
- frontend/trading-ui/src/app/features/optimizer/optimizer-page.component.html: Added the optimizer page layout with configure, results, and history tabs.
- frontend/trading-ui/src/app/features/optimizer/optimizer-page.component.scss: Added page styling for optimizer layout, progress state, and responsive results panels.
- frontend/trading-ui/src/app/features/optimizer/optimizer-config-form/optimizer-config-form.component.ts: Added the reactive optimizer configuration form with typed controls and bounds validation.
- frontend/trading-ui/src/app/features/optimizer/optimizer-config-form/optimizer-config-form.component.html: Added the optimizer form sections for market settings, parameter bounds, and thresholds.
- frontend/trading-ui/src/app/features/optimizer/optimizer-config-form/optimizer-config-form.component.scss: Added optimizer form styling consistent with the existing Angular UI.

<!-- Phase 4: Frontend - Results Display & Promote to Strategy -->
- frontend/trading-ui/src/app/features/optimizer/optimizer-results-table/optimizer-results-table.component.ts: Added the ranked optimization results table component.
- frontend/trading-ui/src/app/features/optimizer/optimizer-results-table/optimizer-results-table.component.html: Added tabular rendering for rank, fitness, signal description, and key metrics.
- frontend/trading-ui/src/app/features/optimizer/optimizer-results-table/optimizer-results-table.component.scss: Added selected-row and profit/loss styling for ranked optimizer results.
- frontend/trading-ui/src/app/features/optimizer/optimizer-detail/optimizer-detail.component.ts: Added the optimizer result detail component with parsed strategy configuration display.
- frontend/trading-ui/src/app/features/optimizer/optimizer-detail/optimizer-detail.component.html: Added detailed result metrics and the create-strategy action.
- frontend/trading-ui/src/app/features/optimizer/optimizer-detail/optimizer-detail.component.scss: Added optimizer result detail card styling.
- frontend/trading-ui/src/app/features/optimizer/optimizer-history-list/optimizer-history-list.component.ts: Added the paged optimizer history list component.
- frontend/trading-ui/src/app/features/optimizer/optimizer-history-list/optimizer-history-list.component.html: Added history table rendering with progress and open actions.
- frontend/trading-ui/src/app/features/optimizer/optimizer-history-list/optimizer-history-list.component.scss: Added history list styling for loading, empty, and selected states.

### Modified

<!-- Phase 1: Backend - Domain Model & Sweep Engine -->
- src/TradingApp.Application/Trading/Services/SignalController.cs: Updated the signal controller to match the current `ISignalController` interface so backend validation can compile.
- tests/TradingApp.Application.Tests/Trading/Services/SignalControllerTests.cs: Updated signal-controller tests for the current grid-state-aware signature.
- tests/TradingApp.Application.Tests/Scheduling/StrategySchedulerTests.cs: Updated signal-controller mock expectations to the current signature.
- tests/TradingApp.Application.Tests/Backtesting/Services/BacktestRunnerTests.cs: Updated signal-controller mock setup to the current signature.

<!-- Phase 2: Backend - Persistence & API -->
- src/TradingApp.Application/Optimization/Models/SweepConfig.cs: Added `BacktestSymbol` so optimizer runs can backtest normalized Binance symbols while persisting deployable strategy markets.
- src/TradingApp.Application/Optimization/Services/SweepRunner.cs: Updated sweep execution to use the normalized backtest symbol while keeping generated strategy configs deployable.
- src/TradingApp.Persistence/TradingAppDbContext.cs: Registered optimizer tables, relationships, indexes, and numeric conversions.
- src/TradingApp.Persistence/PersistenceServiceExtensions.cs: Registered the optimizer repository in the persistence composition root.
- src/TradingApp.Api/Program.cs: Registered the optimizer queue, services, and background processor in the API host.

<!-- Phase 3: Frontend - Optimizer Tab & Configuration -->
- frontend/trading-ui/src/app/core/services/signalr.service.ts: Added optimizer progress event handling alongside the existing SignalR streams.
- frontend/trading-ui/src/app/app.routes.ts: Added the lazy route for the optimizer feature page.
- frontend/trading-ui/src/app/app.component.html: Added the optimizer navigation link to the main application shell.

<!-- Phase 4: Frontend - Results Display & Promote to Strategy -->
- frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts: Added strategy-builder prefill support for optimizer-promoted configurations passed through router state.
- frontend/trading-ui/src/app/app.component.spec.ts: Updated the app shell nav-link expectation for the new optimizer entry.
- frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.spec.ts: Updated the stop-loss mapper expectation to match the current mapper output shape used during frontend test validation.

### Removed

## Test Results

<!-- Phase 1: Backend - Domain Model & Sweep Engine -->
- TradingApp.Application.Tests (FullyQualifiedName~Optimization): 26/26 passed via `dotnet test tests/TradingApp.Application.Tests/TradingApp.Application.Tests.csproj --filter "FullyQualifiedName~Optimization"`

<!-- Phase 2: Backend - Persistence & API -->
- Solution build: passed via `dotnet build`
- Non-acceptance .NET test suite: 696/696 passed via `dotnet test --no-build --filter "FullyQualifiedName!~AcceptanceTests"`

<!-- Phase 3: Frontend - Optimizer Tab & Configuration -->
- Angular build: passed via `npm run build`
- Angular lint: passed via `npm run lint`

<!-- Phase 4: Frontend - Results Display & Promote to Strategy -->
- Angular unit tests: 174/174 passed via `npm test -- --watch=false --browsers=ChromeHeadless`

## Issues

<!-- Phase 1: Backend - Domain Model & Sweep Engine -->
- Existing stale signal-controller test call sites blocked the first validation run after phase 1 changes; updated them to the current interface signature so the optimizer slice could be built and tested.

<!-- Phase 2: Backend - Persistence & API -->
- A running `TradingApp.Api` host process initially locked API output assemblies and blocked `dotnet build`; stopping the running process resolved the build failure and the subsequent build passed cleanly.

<!-- Phase 3: Frontend - Optimizer Tab & Configuration -->
- `npm run build` completed successfully but still reports existing bundle-budget warnings elsewhere in the Angular app; these warnings were not introduced by the optimizer feature.

<!-- Phase 4: Frontend - Results Display & Promote to Strategy -->
- The first Angular test run exposed one optimizer-related shell expectation and one stale mapper-spec expectation; both test expectations were updated and the rerun passed cleanly.

## Design Decisions

<!-- Phase 1: Backend - Domain Model & Sweep Engine -->
- The optimizer generator now accepts the target symbol so every generated `StrategyConfig` persists a deployable `market` value instead of a placeholder.
- The sweep runner uses the simple `IBacktestRunner.RunAsync(config, cancellationToken)` overload and treats progress as strategy-completion progress rather than per-candle progress.

<!-- Phase 2: Backend - Persistence & API -->
- Optimizer runs persist the display-market symbol while carrying a separate `BacktestSymbol` through the in-memory sweep config so promoted strategies keep the correct market shape for the builder.
- The optimizer processor persists run progress during execution and broadcasts the same state via SignalR so history polling and live progress updates stay aligned.

<!-- Phase 3: Frontend - Optimizer Tab & Configuration -->
- The Angular optimizer page keeps run orchestration in the shell component while the form, history, and results panels remain focused child components, matching the existing backtesting page pattern.

<!-- Phase 4: Frontend - Results Display & Promote to Strategy -->
- Optimizer result promotion passes a full `StrategyConfig` through router state so the strategy builder can round-trip the winning configuration without introducing a translation layer.

## Review Hints

- Review the random sampling logic in `StrategyConfigGenerator` for whether the current bounds and template distribution match the intended optimizer search space.
- Review the decision to ignore individual backtest failures inside `SweepRunner` and confirm that partial-run behavior is acceptable for the optimizer UX.
- Review the optimizer progress persistence strategy in `OptimizationProcessorService`, especially the serialized progress-update path, for acceptable database write volume at higher sample sizes.
- Review the run summary contract to confirm the stored top-result metrics are the right ones to surface in optimizer history.
- Review the promoted strategy naming convention generated from optimizer results and confirm whether the builder should preserve the sampled strategy name instead of using the signal description.
- Review the optimizer history page sizing and sort order against expected operational usage once more runs accumulate in production.

## Release Summary

Delivered the strategy optimizer end to end across backend and frontend. Users can now launch signal-strategy sweeps, watch progress over SignalR, inspect ranked winners, open historical runs, and push a winning configuration straight into the strategy builder. Backend validation passed with 696 .NET tests green, and frontend validation passed with successful Angular build, lint, and 174 headless unit tests.