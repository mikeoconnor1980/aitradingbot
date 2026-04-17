<!-- markdownlint-disable-file -->
# Release Changes: Backtest Debug/Audit Log

**Related Plan**: 20260329-backtest-debug-audit-log-plan.instructions.md
**Implementation Date**: 2026-03-29

## Summary

Implemented the backtest debug and audit log across the backtest engine, persistence model, API surface, and Angular trade log UI, including per-cycle drill-in and export support.

## Changes

### Added

<!-- Phase 1: Audit Log Models & Collector Infrastructure -->
- src/TradePilot.Application/Backtesting/Models/CandleEvaluationEntry.cs: Added the per-candle audit log entry model with OHLCV, indicators, setup result, position state, signals, and cycle context.
- src/TradePilot.Application/Backtesting/Models/OrderEventEntry.cs: Added the order lifecycle audit log entry model including fill and cancellation metadata.
- src/TradePilot.Application/Backtesting/Models/GridCycleEntry.cs: Added the completed grid cycle summary model for audit serialization.
- src/TradePilot.Application/Backtesting/Models/OrderEventType.cs: Added order event classification enum values.
- src/TradePilot.Application/Backtesting/Models/CancellationReason.cs: Added cancellation reason enum values.
- src/TradePilot.Application/Backtesting/Services/IBacktestAuditCollector.cs: Added the audit collector abstraction for candle, order, and grid-cycle logging.
- src/TradePilot.Application/Backtesting/Services/BacktestAuditCollector.cs: Added the active in-memory collector implementation using thread-safe queues and snapshot accessors.
- src/TradePilot.Application/Backtesting/Services/NullBacktestAuditCollector.cs: Added the singleton no-op collector for disabled/live usage.
- tests/TradePilot.Application.Tests/Backtesting/Services/BacktestAuditCollectorTests.cs: Added targeted unit coverage for entry capture, ordering, null guards, and null-collector behavior.

<!-- Phase 2: Entity, Persistence & Migration -->
- src/TradePilot.Persistence/Migrations/20260329093813_AddAuditLogToBacktestRun.cs: Added the EF Core migration that creates the four audit-log columns on BacktestRuns.
- src/TradePilot.Persistence/Migrations/20260329093813_AddAuditLogToBacktestRun.Designer.cs: Added the generated EF Core designer metadata for the new migration.

<!-- Phase 4: API Endpoint & CQRS Query -->
- src/TradePilot.Application/Backtesting/GetBacktestDebugQuery.cs: Added the CQRS query and handler that loads a backtest run, deserializes audit blobs, filters them by cycle ID, and returns nullable debug data.
- src/TradePilot.Application/Backtesting/Models/BacktestDebugResponse.cs: Added the debug endpoint response DTO that wraps filtered candle evaluations, order events, and the grid cycle summary.

<!-- Phase 5: Frontend - Expandable Debug Panel -->
- frontend/trading-ui/src/app/core/models/backtest-debug.model.ts: Added frontend DTOs and enums for backtest debug payloads.

### Modified

<!-- Phase 1: Audit Log Models & Collector Infrastructure -->
- src/TradePilot.Application/Backtesting/Models/BacktestConfig.cs: Added EnableAuditLog with a default value of true.

<!-- Phase 2: Entity, Persistence & Migration -->
- src/TradePilot.Domain/Entities/BacktestRun.cs: Added audit-log fields and extended factory and completion methods with backward-compatible optional parameters.
- src/TradePilot.Persistence/TradePilotDbContext.cs: Added EF Core property mappings for the new BacktestRun audit-log columns.
- src/TradePilot.Application/Backtesting/BacktestRunResponseMapper.cs: Added JSON serializers for candle, order-event, and grid-cycle audit logs.
- tests/TradePilot.Persistence.Tests/Repositories/BacktestRunRepositoryTests.cs: Added persistence coverage for enabled and disabled audit-log storage scenarios.
- src/TradePilot.Persistence/Migrations/TradePilotDbContextModelSnapshot.cs: Updated the EF Core snapshot to include the new BacktestRun columns.

<!-- Phase 3: Pipeline Integration -->
- src/TradePilot.Application/Trading/Models/OrderRequest.cs: Added optional GridCycleId so simulated orders and fills retain cycle identity end to end.
- src/TradePilot.Application/Backtesting/Models/SimulatedOrder.cs: Added GridCycleId to persisted in-memory open orders.
- src/TradePilot.Application/Backtesting/Models/SimulatedFill.cs: Added GridCycleId to fill records for accurate audit and trade association.
- src/TradePilot.Application/Backtesting/BacktestExecutionContextAccessor.cs: Added CurrentTimestampUtc so order audit events use simulated candle time.
- src/TradePilot.Application/Scheduling/StrategyScheduler.cs: Added optional audit collector dependency and per-candle evaluation logging.
- src/TradePilot.Application/Trading/Services/GridController.cs: Propagated gridCycleId in DeployGrid and TakeProfit signal parameters.
- src/TradePilot.Application/Trading/Services/BacktestPositionManager.cs: Logged placed and cancelled order events with cancellation reasons and cycle IDs.
- src/TradePilot.Application/Backtesting/Services/SimulatedExecutionEngine.cs: Carried GridCycleId into simulated orders and fills and returned snapshot open-order lists.
- src/TradePilot.Application/Backtesting/Models/BacktestResult.cs: Added nullable candle, order-event, and grid-cycle audit payload properties.
- src/TradePilot.Application/Backtesting/Services/BacktestRunner.cs: Created and wired the collector, logged warmup and fill events, tracked completed grid cycles, and returned audit data with the result.
- src/TradePilot.Api/Services/BacktestProcessorService.cs: Persisted serialized audit JSON and passed AuditLogEnabled into BacktestConfig.
- src/TradePilot.Application/Backtesting/Models/BacktestTradeResponse.cs: Added GridCycleId to API trade responses.
- src/TradePilot.Application/Backtesting/Models/BacktestRunResponse.cs: Added HasAuditLog to backtest run responses.
- src/TradePilot.Application/Backtesting/BacktestRunResponseMapper.cs: Mapped GridCycleId on trades and HasAuditLog on runs.
- tests/TradePilot.Application.Tests/Backtesting/Services/BacktestRunnerTests.cs: Added audit-disabled verification and updated config helper support.
- tests/TradePilot.Application.Tests/Backtesting/Services/RealBacktestRunnerTests.cs: Added end-to-end audit-enabled coverage for warmup, order-event, and grid-cycle capture.

<!-- Phase 4: API Endpoint & CQRS Query -->
- src/TradePilot.Api/Controllers/BacktestsController.cs: Added GET /api/backtests/{id}/debug and passed EnableAuditLog through the run command.
- src/TradePilot.Api/Models/RunBacktestRequest.cs: Added EnableAuditLog with a default value of true.
- src/TradePilot.Application/Backtesting/RunBacktestCommand.cs: Extended the command record and queued run creation flow to carry the audit-log flag.
- tests/TradePilot.Api.Tests/Controllers/BacktestsControllerTests.cs: Added controller coverage for debug endpoint success, no-content, and not-found behavior, plus request flag verification.

<!-- Phase 5: Frontend - Expandable Debug Panel -->
- frontend/trading-ui/src/app/core/models/backtest.model.ts: Added audit-log awareness to backtest result and trade models.
- frontend/trading-ui/src/app/core/services/backtest.service.ts: Added the debug-data API method for cycle-level audit retrieval.
- frontend/trading-ui/src/app/features/backtesting/trade-log-table/trade-log-table.component.ts: Reworked the trade log into a sortable expandable table with lazy debug loading, per-cycle filters, export helpers, and disabled-state handling.
- frontend/trading-ui/src/app/features/backtesting/trade-log-table/trade-log-table.component.html: Added expandable debug rows, summary, order, and candle sections, filter controls, badges, and export buttons.
- frontend/trading-ui/src/app/features/backtesting/trade-log-table/trade-log-table.component.scss: Added styling for expandable rows, debug panel layout, filters, event badges, and responsive tables.
- frontend/trading-ui/src/app/features/backtesting/backtest-page.component.html: Passed backtest ID and audit-log availability into the trade-log component.
- frontend/trading-ui/src/app/features/backtesting/trade-log-table/trade-log-table.component.spec.ts: Updated component coverage for the new plain-table rendering, sorting, lazy loading, and disabled state.

### Removed

<!-- Phase 1: Audit Log Models & Collector Infrastructure -->
- None.

<!-- Phase 2: Entity, Persistence & Migration -->
- None.

<!-- Phase 3: Pipeline Integration -->
- None.

<!-- Phase 4: API Endpoint & CQRS Query -->
- None.

<!-- Phase 5: Frontend - Expandable Debug Panel -->
- None.

## Test Results

<!-- Phase 1: Audit Log Models & Collector Infrastructure -->
- BacktestAuditCollectorTests: 10/10 passed
- Architecture Tests: Not applicable — no architecture test project exists in this repository for this phase

<!-- Phase 2: Entity, Persistence & Migration -->
- BacktestRunRepositoryTests: 4/4 passed
- TradePilot.Persistence.Tests: 20/20 passed
- TradePilot.Application.Tests: 59/59 passed
- Architecture Tests: Not applicable — no architecture test project exists for this phase
- EF database update verification: PASSED

<!-- Phase 3: Pipeline Integration -->
- BacktestRunnerTests + RealBacktestRunnerTests filtered scope: 11/11 passed
- TradePilot.Application.Tests: 61/61 passed
- TradePilot.Api.Tests: 123/123 passed
- Architecture Tests: Not applicable — no architecture test project exists in this repository

<!-- Phase 4: API Endpoint & CQRS Query -->
- BacktestsControllerTests: 38/38 passed
- Architecture Tests: Not applicable — no architecture test project exists for this phase

<!-- Phase 5: Frontend - Expandable Debug Panel -->
- Frontend Build (npm run build): PASSED
- Frontend Lint (npm run lint): PASSED
- Architecture Tests: Not applicable — no architecture test project exists in this repository

## Issues

<!-- Phase 1: Audit Log Models & Collector Infrastructure -->
- The dedicated test runner did not resolve the single test file path, so the phase used dotnet test tests/TradePilot.Application.Tests --filter "FullyQualifiedName~BacktestAuditCollectorTests" instead. The tests passed.
- dotnet restore emitted an existing NU1903 warning for AutoMapper 12.0.1 in TradePilot.Application.csproj. This did not block the phase and was not introduced by these changes.

<!-- Phase 2: Entity, Persistence & Migration -->
- dotnet ef migrations add failed when using src/TradePilot.Api as the startup project because that project does not reference Microsoft.EntityFrameworkCore.Design. Resolved by using src/TradePilot.Persistence as the startup project, which already has the required design-time tooling and factory.
- dotnet ef database update initially failed because the design-time SQLite path was relative to the EF tooling execution location. Resolved by running the update with an explicit absolute connection string to the repository database.

<!-- Phase 3: Pipeline Integration -->
- Initial compile failure in src/TradePilot.Application/Backtesting/Services/BacktestRunner.cs from null-coalescing two different concrete collector types. Resolved by assigning through IBacktestAuditCollector.
- Initial integration failure where a closed cycle could be redeployed on the same candle before counting or logging, producing an empty grid-cycle audit log. Resolved by capturing closed-cycle completion immediately after fill processing, before scheduler execution.
- First API test invocation failed with an MSBuild host access-denied error during restore or node shutdown. Resolved by rerunning with single-process settings using /nodeReuse:false and -m:1.
- Existing NU1903 warning for AutoMapper 12.0.1 still appears during test runs. It was not introduced by this phase.

<!-- Phase 4: API Endpoint & CQRS Query -->
- None.

<!-- Phase 5: Frontend - Expandable Debug Panel -->
- ng build initially failed because Angular interpreted a literal @ in the template as a control-flow block marker. Resolved by encoding it as &#64;.
- ng build still reports a non-blocking global initial bundle budget warning.
- ng build reports a non-blocking component stylesheet budget warning for frontend/trading-ui/src/app/features/backtesting/trade-log-table/trade-log-table.component.scss.

## Design Decisions

<!-- Phase 1: Audit Log Models & Collector Infrastructure -->
- Used ConcurrentQueue in src/TradePilot.Application/Backtesting/Services/BacktestAuditCollector.cs to keep insertion order while providing thread-safe in-memory collection.
- Kept src/TradePilot.Application/Backtesting/Services/NullBacktestAuditCollector.cs as a singleton with a private constructor to reinforce the null-object pattern and avoid unnecessary allocations.

<!-- Phase 2: Entity, Persistence & Migration -->
- Kept new BacktestRun factory and MarkCompleted parameters optional where appropriate so existing call sites remain source-compatible until later phases wire real audit payloads through the pipeline.
- Left audit JSON columns nullable and only enforced the bool column as non-nullable with defaultValue false to preserve backward compatibility for existing rows.

<!-- Phase 3: Pipeline Integration -->
- Added GridCycleId to OrderRequest, SimulatedOrder, and SimulatedFill so cancellation, fill, trade, and audit records all reference the same cycle without guessing from mutable grid state.
- Kept live and non-audit compatibility by making the scheduler and backtest position manager accept optional collectors that default to the null collector.
- Constructed a collector-aware backtest position manager inside BacktestRunner only when the injected position manager is the backtest implementation, avoiding broader DI changes.
- Computed grid-cycle completion data inside the runner from tracked open orders and fills rather than expanding public scheduler return contracts, preserving the existing CandleClock and StrategyScheduler flow.

<!-- Phase 4: API Endpoint & CQRS Query -->
- Used a nullable query result to preserve the required API distinction: missing backtest returns 404 via NotFoundException, while missing audit data returns 204 from the controller.
- Reused BacktestRunResponseMapper serialization helpers in controller tests so the audit JSON fixtures match production serialization behavior.

<!-- Phase 5: Frontend - Expandable Debug Panel -->
- Used a plain HTML table with component-managed sort state instead of mat-table so expandable sibling detail rows match the existing UI pattern while preserving sort behavior across expand and collapse actions.
- Typed frontend/trading-ui/src/app/core/services/backtest.service.ts getDebugData as nullable to handle the API's 204 No Content response safely in the component.
- Stored signal and setup filters per cycle so multiple expanded rows can coexist without sharing a single global filter state.

## Review Hints

<!-- Phase 1: Audit Log Models & Collector Infrastructure -->
- Review the field set on the new audit entry models against Phase 3 integration points to confirm no additional runtime-only fields are required before wiring the scheduler and position manager.

<!-- Phase 2: Entity, Persistence & Migration -->
- Review the default behavior choice on CreateQueued and Create, which currently defaults auditLogEnabled to true per the phase details; Phase 4 needs to ensure the request DTO controls that value end to end.

<!-- Phase 3: Pipeline Integration -->
- Review the cancellation-reason mapping in src/TradePilot.Application/Trading/Services/BacktestPositionManager.cs, especially the stop-loss detection branch that infers StopLossTriggered from signal reason text.
- Review the grid-cycle summary values in src/TradePilot.Application/Backtesting/Services/BacktestRunner.cs, particularly the fallback behavior for StopLossPrice and TakeProfitPrice when the cycle closes without a tracked resting limit order.

<!-- Phase 4: API Endpoint & CQRS Query -->
- Review the debug query’s current full-blob deserialization approach in src/TradePilot.Application/Backtesting/GetBacktestDebugQuery.cs if audit payload sizes grow significantly; it is correct for this phase but intentionally in-memory filtered.

<!-- Phase 5: Frontend - Expandable Debug Panel -->
- Review the UX around duplicate trades sharing the same gridCycleId; debug data is cached per cycle while expansion state is tracked per row, which avoids duplicate fetches but still allows repeated views of the same cycle.
- Review the stylesheet budget warning on frontend/trading-ui/src/app/features/backtesting/trade-log-table/trade-log-table.component.scss if the team wants a warning-free frontend build.

## Release Summary

Added end-to-end audit logging for backtests with opt-in capture, persisted JSON payloads, a filtered debug query and endpoint, and an expandable Angular trade-log experience with per-cycle filtering and JSON or CSV export.

Completed 5 phases and 27 tasks.
Files created: 14.
Files modified: 33.
Files removed: 0.
