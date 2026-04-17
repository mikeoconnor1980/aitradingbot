---
applyTo: ".agent-context/3-develop/build/changes/20260329-backtest-debug-audit-log-changes.md"
currentAgent: "None"
agentStartedAt: "2026-03-29T10:03:06Z"
status: "complete"
lastUpdated: "2026-03-29T10:20:00Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: Backtest Debug/Audit Log

## Overview

Add a comprehensive debug/audit log to backtesting results that traces every decision the grid engine made — per-candle evaluations with indicator snapshots, order lifecycle events with cancellation reasons, and grid cycle summaries — with API retrieval and UI expandable trade log rows.

## PBI Details

**PBI ID:** Draft
**Status:** Draft
**Feature:** Backtest Debug/Audit Log

As a developer, I want a debug/audit log available in the backtesting results so that I can trace every decision the grid engine made and verify the algorithm is correct.

### Acceptance Criteria

- [ ] **Given** a backtest config with `EnableAuditLog = true`, **When** the backtest completes, **Then** the result includes populated `CandleLogJson`, `OrderEventLogJson`, and `GridCycleLogJson` columns in the database.
- [ ] **Given** a backtest config with `EnableAuditLog = false`, **When** the backtest completes, **Then** the debug JSON columns are null and no debug data is captured during the run.
- [ ] **Given** a completed backtest with audit data, **When** the per-candle evaluation log is inspected, **Then** every 15m candle (including warmup) has an entry with timestamp, OHLCV, full indicator snapshot, SetupDetected result, grid lifecycle state, position state, signals emitted, and GridCycleId.
- [ ] **Given** a warmup candle entry in the log, **When** inspected, **Then** it has `IsWarmup = true` and no signals emitted.
- [ ] **Given** a grid cycle where buy orders are placed and some are cancelled, **When** the order event log is inspected, **Then** each cancellation has an enum reason code (e.g., `PositionOpened`).
- [ ] **Given** a completed grid cycle, **When** the grid cycle log is inspected, **Then** it contains cycle ID, deploy time, anchor price, levels placed, fill count, TP/SL prices, exit reason, cycle PnL, and duration.
- [ ] **Given** a backtest ID and a grid cycle ID, **When** `GET /api/backtests/{id}/debug?cycleId={cycleId}` is called, **Then** it returns the 3 log types filtered to that cycle.
- [ ] **Given** a backtest without audit data, **When** the debug endpoint is called, **Then** it returns 204 No Content.
- [ ] **Given** the trade log table in the UI, **When** a row for a run with audit data is clicked, **Then** it expands to show the grid cycle summary, order events, and candle evaluations for that cycle, loaded via the debug API.
- [ ] **Given** the expanded debug view, **When** the user applies a filter (signal type or SetupDetected), **Then** only matching candle evaluation rows are displayed.
- [ ] **Given** the expanded debug view, **When** the user clicks the JSON export button, **Then** the debug data for that cycle downloads as a `.json` file.
- [ ] **Given** the expanded debug view, **When** the user clicks the CSV export button, **Then** the debug data for that cycle downloads as a `.csv` file.
- [ ] **Given** order events in the expanded view, **When** displayed, **Then** they are color-coded: green (fills), red (cancels), blue (placements), orange (replacements).
- [ ] **Given** a pre-existing backtest run without debug data, **When** the trade log is displayed, **Then** the expand control is disabled with a tooltip "Debug data not available for this run."
- [ ] **Given** audit logging is enabled, **When** a backtest completes, **Then** the total runtime is no more than 20% slower than with audit logging disabled (for the same config and data).

## Objectives

- Create audit log model types and `IBacktestAuditCollector` abstraction with null-object pattern for zero-overhead disabled mode
- Wire the audit collector into `StrategyScheduler`, `BacktestPositionManager`, and `BacktestRunner` to capture per-candle evaluations, order events, and grid cycle completions
- Extend `BacktestRun` entity with debug JSON blob columns and EF Core migration
- Create `GET /api/backtests/{id}/debug?cycleId={cycleId}` CQRS query and API endpoint
- Build expandable trade log rows in the UI with debug panel, filtering, color-coding, and JSON/CSV export

### Discovery References

**Critical architectural gap**: `StrategyScheduler.HandleCandleClosedAsync()` returns `Task` (void). The `MarketContext` (with `IndicatorSnapshot`), `StrategyEvaluation` (SetupDetected), and emitted `TradingSignal` list are consumed internally and never exposed to `BacktestRunner`. The solution is an `IBacktestAuditCollector` interface injected into `StrategyScheduler` — with a `NullBacktestAuditCollector` default for live mode — so the scheduler can log evaluation data without changing its return type.

**Order cancellation gap**: `SimulatedExecutionEngine.CancelAllOrdersAsync` is `_openOrders.RemoveAll(...)` — silent, no events, no return value. Cancellation logging must happen at the `BacktestPositionManager` layer where the grid cycle context and reason are known. The position manager will enumerate open orders before cancellation and record `Cancelled` events.

**Prerequisite gap**: `BacktestTradeResponse` does not include `GridCycleId` even though `BacktestTrade` has it. This must be fixed for the UI to associate trades with grid cycles.

**Warmup indicator snapshots**: Warmup candles only call `UpdateIndicators()` — no `Build()` is called, so no `IndicatorSnapshot` is produced. The audit collector will call `Build()` during warmup to capture indicators, but mark entries as `IsWarmup = true` with no signals.

**`MarkCompleted` signature**: Currently 14 parameters. Debug data will be added as 3 additional `string?` parameters plus 1 `bool` for `AuditLogEnabled`.

### Project Patterns

- `src/TradePilot.Domain/Entities/BacktestRun.cs` — Entity with JSON blob columns, factory methods, `MarkCompleted()` pattern
- `src/TradePilot.Application/Backtesting/Models/BacktestConfig.cs` — Configuration model (add `EnableAuditLog` here)
- `src/TradePilot.Application/Backtesting/Services/BacktestRunner.cs` — Main replay loop (wire collector here)
- `src/TradePilot.Application/Scheduling/StrategyScheduler.cs` — Evaluation pipeline (inject collector here)
- `src/TradePilot.Application/Trading/Services/BacktestPositionManager.cs` — Signal → order routing (log order events here)
- `src/TradePilot.Application/Backtesting/BacktestRunResponseMapper.cs` — JSON serialization pattern (add debug serializers)
- `src/TradePilot.Application/Backtesting/GetBacktestResultQuery.cs` — CQRS query pattern (follow for debug query)
- `src/TradePilot.Api/Controllers/BacktestsController.cs` — Controller pattern (add debug endpoint)
- `src/TradePilot.Api/Models/RunBacktestRequest.cs` — Request DTO (add `EnableAuditLog`)
- `src/TradePilot.Persistence/TradePilotDbContext.cs` — Inline entity config in `OnModelCreating`
- `src/TradePilot.Persistence/Repositories/BacktestRunRepository.cs` — Repository pattern
- `src/TradePilot.Persistence/Migrations/20260328204609_AddEquityTimeSeriesToBacktestRun.cs` — Latest migration
- `src/TradePilot.Application/Trading/Models/IndicatorSnapshot.cs` — Indicator values to capture
- `src/TradePilot.Application/Trading/Models/StrategyEvaluation.cs` — SetupDetected result to capture
- `src/TradePilot.Application/Trading/Models/TradingSignal.cs` — Signal type + parameters
- `src/TradePilot.Application/Trading/Models/GridState.cs` — Grid lifecycle + GridCycleId
- `src/TradePilot.Application/Backtesting/Models/SimulatedOrder.cs` — Order model (needs GridCycleId)
- `src/TradePilot.Application/Backtesting/Models/SimulatedFill.cs` — Fill model for order event logging
- `frontend/trading-ui/src/app/features/backtesting/trade-log-table/` — Trade log table to make expandable
- `frontend/trading-ui/src/app/features/dashboard/activity-feed/` — Expandable row pattern reference
- `frontend/trading-ui/src/app/features/dashboard/positions-table/` — Filter pattern reference
- `frontend/trading-ui/src/app/core/services/backtest.service.ts` — API service (add `getDebugData()`)
- `frontend/trading-ui/src/app/core/models/backtest.model.ts` — TypeScript models (add debug types)
- `tests/TradePilot.Application.Tests/Backtesting/Services/BacktestRunnerTests.cs` — Service test pattern
- `tests/TradePilot.Application.Tests/Backtesting/Services/RealBacktestRunnerTests.cs` — Integration test pattern
- `tests/TradePilot.Api.Tests/Controllers/BacktestsControllerTests.cs` — Controller test pattern
- `tests/TradePilot.Persistence.Tests/Repositories/BacktestRunRepositoryTests.cs` — Persistence test pattern

### [x] Phase 1: Audit Log Models & Collector Infrastructure

**Complexity**: Medium | **Risk**: Low

- [x] Task 1.1: Create audit log entry models (CandleEvaluationEntry, OrderEventEntry, GridCycleEntry)
  - Details: .agent-context/3-develop/build/plans/details/20260329-backtest-debug-audit-log-phase-01-details.md#task-11-create-audit-log-entry-models

- [x] Task 1.2: Create OrderEventType and CancellationReason enums
  - Details: .agent-context/3-develop/build/plans/details/20260329-backtest-debug-audit-log-phase-01-details.md#task-12-create-ordereventtype-and-cancellationreason-enums

- [x] Task 1.3: Create IBacktestAuditCollector interface, BacktestAuditCollector, and NullBacktestAuditCollector
  - Details: .agent-context/3-develop/build/plans/details/20260329-backtest-debug-audit-log-phase-01-details.md#task-13-create-ibacktestauditcollector-interface-and-implementations

- [x] Task 1.4: Add EnableAuditLog to BacktestConfig
  - Details: .agent-context/3-develop/build/plans/details/20260329-backtest-debug-audit-log-phase-01-details.md#task-14-add-enableauditlog-to-backtestconfig

- [x] Task 1.5: Unit tests for BacktestAuditCollector
  - Details: .agent-context/3-develop/build/plans/details/20260329-backtest-debug-audit-log-phase-01-details.md#task-15-unit-tests-for-backtestauditcollector

### [x] Phase 2: Entity, Persistence & Migration

**Complexity**: Medium | **Risk**: Low

- [x] Task 2.1: Add audit log properties to BacktestRun entity
  - Details: .agent-context/3-develop/build/plans/details/20260329-backtest-debug-audit-log-phase-02-details.md#task-21-add-audit-log-properties-to-backtestrun-entity

- [x] Task 2.2: Create EF Core migration for new columns
  - Details: .agent-context/3-develop/build/plans/details/20260329-backtest-debug-audit-log-phase-02-details.md#task-22-create-ef-core-migration-for-new-columns

- [x] Task 2.3: Update DbContext configuration
  - Details: .agent-context/3-develop/build/plans/details/20260329-backtest-debug-audit-log-phase-02-details.md#task-23-update-dbcontext-configuration

- [x] Task 2.4: Add debug data serialization to BacktestRunResponseMapper
  - Details: .agent-context/3-develop/build/plans/details/20260329-backtest-debug-audit-log-phase-02-details.md#task-24-add-debug-data-serialization-to-backtestrunresponsemapper

- [x] Task 2.5: Persistence tests for new columns
  - Details: .agent-context/3-develop/build/plans/details/20260329-backtest-debug-audit-log-phase-02-details.md#task-25-persistence-tests-for-new-columns

### [x] Phase 3: Pipeline Integration

**Complexity**: High | **Risk**: Medium

- [x] Task 3.1: Update StrategyScheduler to accept and invoke IBacktestAuditCollector
  - Details: .agent-context/3-develop/build/plans/details/20260329-backtest-debug-audit-log-phase-03-details.md#task-31-update-strategyscheduler-to-accept-and-invoke-ibacktestauditcollector

- [x] Task 3.2: Update BacktestPositionManager to log order events via collector
  - Details: .agent-context/3-develop/build/plans/details/20260329-backtest-debug-audit-log-phase-03-details.md#task-32-update-backtestpositionmanager-to-log-order-events

- [x] Task 3.3: Update BacktestRunner to create/wire collector and log grid cycle completions
  - Details: .agent-context/3-develop/build/plans/details/20260329-backtest-debug-audit-log-phase-03-details.md#task-33-update-backtestrunner-to-wire-collector-and-log-grid-cycles

- [x] Task 3.4: Update BacktestProcessorService to persist debug data
  - Details: .agent-context/3-develop/build/plans/details/20260329-backtest-debug-audit-log-phase-03-details.md#task-34-update-backtestprocessorservice-to-persist-debug-data

- [x] Task 3.5: Add GridCycleId to BacktestTradeResponse and HasAuditLog to BacktestRunResponse
  - Details: .agent-context/3-develop/build/plans/details/20260329-backtest-debug-audit-log-phase-03-details.md#task-35-add-gridcycleid-and-hasauditlog-to-response-models

- [x] Task 3.6: Integration tests for audit log capture
  - Details: .agent-context/3-develop/build/plans/details/20260329-backtest-debug-audit-log-phase-03-details.md#task-36-integration-tests-for-audit-log-capture

### [x] Phase 4: API Endpoint & CQRS Query

**Complexity**: Medium | **Risk**: Low

- [x] Task 4.1: Create GetBacktestDebugQuery and handler
  - Details: .agent-context/3-develop/build/plans/details/20260329-backtest-debug-audit-log-phase-04-details.md#task-41-create-getbacktestdebugquery-and-handler

- [x] Task 4.2: Create BacktestDebugResponse DTOs
  - Details: .agent-context/3-develop/build/plans/details/20260329-backtest-debug-audit-log-phase-04-details.md#task-42-create-backtestdebugresponse-dtos

- [x] Task 4.3: Add debug endpoint to BacktestsController
  - Details: .agent-context/3-develop/build/plans/details/20260329-backtest-debug-audit-log-phase-04-details.md#task-43-add-debug-endpoint-to-backtestscontroller

- [x] Task 4.4: Add EnableAuditLog to RunBacktestRequest
  - Details: .agent-context/3-develop/build/plans/details/20260329-backtest-debug-audit-log-phase-04-details.md#task-44-add-enableauditlog-to-runbacktestrequest

- [x] Task 4.5: Controller tests for debug endpoint
  - Details: .agent-context/3-develop/build/plans/details/20260329-backtest-debug-audit-log-phase-04-details.md#task-45-controller-tests-for-debug-endpoint

### [x] Phase 5: Frontend — Expandable Debug Panel

**Complexity**: High | **Risk**: Medium

- [x] Task 5.1: Add debug TypeScript models and enums
  - Details: .agent-context/3-develop/build/plans/details/20260329-backtest-debug-audit-log-phase-05-details.md#task-51-add-debug-typescript-models-and-enums

- [x] Task 5.2: Add getDebugData method to BacktestService and update BacktestTrade model
  - Details: .agent-context/3-develop/build/plans/details/20260329-backtest-debug-audit-log-phase-05-details.md#task-52-add-getdebugdata-method-and-update-backtesttrade-model

- [x] Task 5.3: Make trade log table expandable with debug panel
  - Details: .agent-context/3-develop/build/plans/details/20260329-backtest-debug-audit-log-phase-05-details.md#task-53-make-trade-log-table-expandable-with-debug-panel

- [x] Task 5.4: Build debug panel sub-sections (grid cycle summary, order events, candle evaluations)
  - Details: .agent-context/3-develop/build/plans/details/20260329-backtest-debug-audit-log-phase-05-details.md#task-54-build-debug-panel-sub-sections

- [x] Task 5.5: Add filtering, color-coding, and export functionality
  - Details: .agent-context/3-develop/build/plans/details/20260329-backtest-debug-audit-log-phase-05-details.md#task-55-add-filtering-color-coding-and-export

- [x] Task 5.6: Handle disabled state for pre-existing runs and run build/lint
  - Details: .agent-context/3-develop/build/plans/details/20260329-backtest-debug-audit-log-phase-05-details.md#task-56-handle-disabled-state-and-run-build-lint

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: Audit Log Models & Collector Infrastructure | Medium | Low |
| Phase 2: Entity, Persistence & Migration | Medium | Low |
| Phase 3: Pipeline Integration | High | Medium |
| Phase 4: API Endpoint & CQRS Query | Medium | Low |
| Phase 5: Frontend — Expandable Debug Panel | High | Medium |
| **Total** | **High** | **Medium** |

### Scoping Notes

- Phase 3 is the highest-risk phase due to modifications to the shared `StrategyScheduler` — the audit collector must be wired without breaking live trading compatibility
- The `NullBacktestAuditCollector` ensures zero overhead in live mode and when audit is disabled
- `MarkCompleted` parameter count grows from 14 to 17 — acceptable for this entity pattern; refactoring to a value object is out of scope
- SQLite TEXT columns for debug JSON blobs: ~17,280 candle entries × ~200 bytes ≈ 3.5 MB per run — well within SQLite limits
- No architecture tests exist in this project — skip architecture test step in phases
- Warmup candles will call `Build()` during audit-enabled runs to capture indicator snapshots; this is a minor performance cost within the 20% budget

## Dependencies

- .NET 9 / C# 13 (existing)
- Entity Framework Core + SQLite (existing)
- System.Text.Json (existing)
- Angular 19 + Angular Material (existing)
- MediatR (existing)

## Success Criteria

- Backtest runs with `EnableAuditLog = true` produce populated CandleLogJson, OrderEventLogJson, and GridCycleLogJson
- Backtest runs with `EnableAuditLog = false` produce null debug columns with zero logging overhead
- `GET /api/backtests/{id}/debug?cycleId={cycleId}` returns filtered debug data (200), 204 for no data, 404 for missing run
- Trade log table rows are expandable showing grid cycle summary, order events timeline, and candle evaluations
- Filtering by signal type and SetupDetected works in the expanded view
- Order events are color-coded by type
- JSON and CSV export buttons download debug data for the selected cycle
- Pre-existing runs show disabled expand control with tooltip
- All tests pass across domain, application, persistence, API, and frontend layers

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-03-29T09:01:15Z | 2026-03-29T09:22:45Z |
| Plan Reviewer | plan-reviewed | 2026-03-29T09:23:41Z | 2026-03-29T09:29:39Z |
| Plan Implementer | implemented | 2026-03-29T09:31:52Z | 2026-03-29T10:00:37Z |
| Implementation Reviewer | complete | 2026-03-29T10:03:06Z | 2026-03-29T10:20:00Z |
