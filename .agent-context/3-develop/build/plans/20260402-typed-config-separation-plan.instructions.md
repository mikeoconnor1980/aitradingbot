---
applyTo: ".agent-context/3-develop/build/changes/20260402-typed-config-separation-changes.md"
currentAgent: "None"
agentStartedAt: "2026-04-02T15:40:06Z"
status: "complete"
lastUpdated: "2026-04-02T16:12:01Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: F0 — Typed Config & Execution Separation

## Overview

Refactor the core trading interfaces to separate strategy parameters from execution parameters, replace raw `string strategyConfigJson` with typed config objects (`IStrategyConfig`, `GridStrategyConfig`, `ExecutionConfig`), and split the database storage into two JSON columns. This is a mechanical, testable refactoring with no new features.

## PBI Details

**PBI**: F0 — Architecture: Typed Config & Execution Separation
**Location**: `.agent-context/3-develop/backlog/draft/strategy-input/F0-architecture-typed-config-separation.md`

> As a **platform developer**, I want **typed config objects instead of raw JSON strings in the trading pipeline** so that **config errors are caught at compile time and we have a clean foundation for the strategy builder**.

### Acceptance Criteria

- [ ] `IStrategyEngine.EvaluateAsync` accepts `IStrategyConfig` not `string`
- [ ] `IGridController.ProcessAsync` accepts `IStrategyConfig` not `string`
- [ ] No `JsonSerializer.Deserialize<GridStrategyConfig>` calls in engine, controller, or processor service
- [ ] `BacktestConfig` has separate `Strategy` (`IStrategyConfig`) and `Execution` (`ExecutionConfig`) properties, no standalone `FeeModel` property
- [ ] `BacktestRun` entity has `StrategyConfigJson` and `ExecutionConfigJson` as separate columns
- [ ] `GridStrategyConfig` implements `IStrategyConfig` and lives in `TradingApp.Domain`
- [ ] `ExecutionConfig` contains a `FeeModel` property plus `Leverage`
- [ ] Backtest results are identical for the same parameters before and after
- [ ] All existing tests pass
- [ ] Angular backtest form request body has separate `strategy` and `execution` sections

## Objectives

- Replace raw JSON string passing with typed `IStrategyConfig` through the entire trading pipeline
- Separate strategy-specific parameters from execution parameters into distinct types
- Eliminate fee duplication between `BacktestConfig.FeeModel` and `GridStrategyConfig` fee fields
- Split `BacktestRun` storage into two JSON columns (`StrategyConfigJson`, `ExecutionConfigJson`)
- Update API contract and Angular frontend for the new nested request/response shape

### Discovery References

**Design Decisions (from alignment):**

| Decision | Choice | Rationale |
|----------|--------|-----------|
| PositionSize placement | Stays on `GridStrategyConfig` | GridController and GridStrategyEngine both read `config.PositionSize`. Keeping it avoids adding `ExecutionConfig` to engine/controller interfaces. |
| ExecutionConfig contents | `FeeModel` + `Leverage` | Leverage is never used in the internal pipeline (only stored). Fees are pure execution concern. |
| FeeModel/OrderSide location | Move to `TradingApp.Domain` | `ExecutionConfig` is in Domain; Domain can't reference Application. `OrderSide` is a core domain concept. |
| BacktestEntryModes location | Move to `TradingApp.Domain.Trading` | `GridStrategyConfig` default value references it; Domain can't reference Application. |
| Response shape | Separate `StrategyConfig` + `ExecutionConfig` | Matches request shape and DB column split. |
| Validation approach | Data Annotations (existing pattern) | No FluentValidation in the project. Follow existing `[Required]`/`[Range]` + `ValidateRequest()` pattern. |
| Config record style | Sealed records with `{ get; init; }` | Simple data carriers as PBI specifies. No `required` keyword — keep deserialization simple. |

### Project Patterns

- `src/TradingApp.Application/Abstractions/Services/IStrategyEngine.cs` — current `EvaluateAsync(MarketContext, string strategyConfigJson)` interface
- `src/TradingApp.Application/Abstractions/Services/IGridController.cs` — current `ProcessAsync(..., string strategyConfigJson)` interface
- `src/TradingApp.Application/Trading/Services/GridStrategyEngine.cs` — deserializes JSON inline, validates GridLevels/GridSpacing/PositionSize
- `src/TradingApp.Application/Trading/Services/GridController.cs` — deserializes JSON inline, reads strategy params + PositionSize
- `src/TradingApp.Application/Scheduling/StrategyScheduler.cs` — stores `string _strategyConfigJson`, passes through
- `src/TradingApp.Application/Backtesting/Models/BacktestConfig.cs` — has `FeeModel` + `StrategyConfigJson` (mixed concerns)
- `src/TradingApp.Application/Backtesting/Models/GridStrategyConfig.cs` — God object mixing strategy + execution params
- `src/TradingApp.Application/Backtesting/Models/FeeModel.cs` — standalone fee calculator with `CalculateFee`/`ApplySlippage`
- `src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs` — passes `config.StrategyConfigJson` to StrategyScheduler
- `src/TradingApp.Api/Services/BacktestProcessorService.cs` — `BuildConfig` extracts fees from JSON and constructs `FeeModel` + `StrategyConfigJson`
- `src/TradingApp.Application/Backtesting/BacktestRunResponseMapper.cs` — serialize/deserialize `GridStrategyConfig` ↔ JSON
- `src/TradingApp.Application/Backtesting/RunBacktestCommand.cs` — command carries typed `GridStrategyConfig`, handler serializes to JSON
- `src/TradingApp.Domain/Entities/BacktestRun.cs` — single `StrategyConfigJson` column
- `src/TradingApp.Api/Controllers/BacktestsController.cs` — maps `GridStrategyConfigRequest` → `GridStrategyConfig` field-by-field
- `src/TradingApp.Api/Models/RunBacktestRequest.cs` — `GridStrategyConfigRequest` DTO with mixed concerns
- `src/TradingApp.Application/Backtesting/Models/BacktestRunResponse.cs` — response with single `GridStrategyConfig StrategyConfig`
- `frontend/trading-ui/src/app/core/models/backtest.model.ts` — TS interfaces with flat `GridStrategyConfig`
- `frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.ts` — form builds flat request
- `tests/TradingApp.Application.Tests/Trading/Services/GridControllerTests.cs` — uses raw JSON constant
- `tests/TradingApp.Application.Tests/Scheduling/StrategySchedulerTests.cs` — uses `"{}"` raw JSON
- `tests/TradingApp.Application.Tests/Backtesting/Services/BacktestRunnerTests.cs` — `CreateConfig` with `StrategyConfigJson`
- `tests/TradingApp.Application.Tests/Backtesting/Services/RealBacktestRunnerTests.cs` — inline JSON per test
- `tests/TradingApp.Application.Tests/Backtesting/Services/CandleReplayEngineTests.cs` — `StrategyConfigJson = "{}"`
- `tests/TradingApp.Api.Tests/Controllers/BacktestsControllerTests.cs` — uses `GridStrategyConfigRequest`

### [x] Phase 1: Domain Types & Model Migration

**Complexity**: Medium | **Risk**: Low

- [x] Task 1.1: Create IStrategyConfig marker interface in TradingApp.Domain
  - Details: .agent-context/3-develop/build/plans/details/20260402-typed-config-separation-phase-01-details.md#task-11-create-istrategyconfig-marker-interface

- [x] Task 1.2: Move OrderSide enum to TradingApp.Domain.Enums
  - Details: .agent-context/3-develop/build/plans/details/20260402-typed-config-separation-phase-01-details.md#task-12-move-orderside-enum-to-domain

- [x] Task 1.3: Move FeeModel to TradingApp.Domain.Trading
  - Details: .agent-context/3-develop/build/plans/details/20260402-typed-config-separation-phase-01-details.md#task-13-move-feemodel-to-domain

- [x] Task 1.4: Move BacktestEntryModes to TradingApp.Domain.Trading as EntryModes
  - Details: .agent-context/3-develop/build/plans/details/20260402-typed-config-separation-phase-01-details.md#task-14-move-backtestentrymodes-to-domain

- [x] Task 1.5: Create ExecutionConfig record in TradingApp.Domain.Trading
  - Details: .agent-context/3-develop/build/plans/details/20260402-typed-config-separation-phase-01-details.md#task-15-create-executionconfig-record

- [x] Task 1.6: Create GridStrategyConfig record in TradingApp.Domain.Trading
  - Details: .agent-context/3-develop/build/plans/details/20260402-typed-config-separation-phase-01-details.md#task-16-create-gridstrategyconfig-record

- [x] Task 1.7: Update all using statements and fix compilation
  - Details: .agent-context/3-develop/build/plans/details/20260402-typed-config-separation-phase-01-details.md#task-17-update-using-statements

- [x] Task 1.8: Run build and all tests
  - Details: .agent-context/3-develop/build/plans/details/20260402-typed-config-separation-phase-01-details.md#task-18-run-build-and-tests

### [x] Phase 2: Core Pipeline Refactoring

**Complexity**: High | **Risk**: Medium

- [x] Task 2.1: Refactor IStrategyEngine and GridStrategyEngine
  - Details: .agent-context/3-develop/build/plans/details/20260402-typed-config-separation-phase-02-details.md#task-21-refactor-istrategyengine-and-gridstrategyengine

- [x] Task 2.2: Refactor IGridController and GridController
  - Details: .agent-context/3-develop/build/plans/details/20260402-typed-config-separation-phase-02-details.md#task-22-refactor-igridcontroller-and-gridcontroller

- [x] Task 2.3: Refactor StrategyScheduler
  - Details: .agent-context/3-develop/build/plans/details/20260402-typed-config-separation-phase-02-details.md#task-23-refactor-strategyscheduler

- [x] Task 2.4: Refactor BacktestConfig
  - Details: .agent-context/3-develop/build/plans/details/20260402-typed-config-separation-phase-02-details.md#task-24-refactor-backtestconfig

- [x] Task 2.5: Refactor BacktestRunner
  - Details: .agent-context/3-develop/build/plans/details/20260402-typed-config-separation-phase-02-details.md#task-25-refactor-backtestrunner

- [x] Task 2.6: Refactor BacktestProcessorService.BuildConfig (temporary bridge)
  - Details: .agent-context/3-develop/build/plans/details/20260402-typed-config-separation-phase-02-details.md#task-26-refactor-backtestprocessorservice-buildconfig

- [x] Task 2.7: Update pipeline tests
  - Details: .agent-context/3-develop/build/plans/details/20260402-typed-config-separation-phase-02-details.md#task-27-update-pipeline-tests

- [x] Task 2.8: Run build and all tests
  - Details: .agent-context/3-develop/build/plans/details/20260402-typed-config-separation-phase-02-details.md#task-28-run-build-and-tests

### [x] Phase 3: Entity, Command, Mapper & API Contract

**Complexity**: High | **Risk**: Medium

- [x] Task 3.1: Delete old Application.Backtesting.Models.GridStrategyConfig
  - Details: .agent-context/3-develop/build/plans/details/20260402-typed-config-separation-phase-03-details.md#task-31-delete-old-gridstrategyconfig

- [x] Task 3.2: Update RunBacktestRequest with separate sections
  - Details: .agent-context/3-develop/build/plans/details/20260402-typed-config-separation-phase-03-details.md#task-32-update-runbacktestrequest

- [x] Task 3.3: Update BacktestsController mapping and validation
  - Details: .agent-context/3-develop/build/plans/details/20260402-typed-config-separation-phase-03-details.md#task-33-update-backtestscontroller

- [x] Task 3.4: Update RunBacktestCommand and handler
  - Details: .agent-context/3-develop/build/plans/details/20260402-typed-config-separation-phase-03-details.md#task-34-update-runbacktestcommand-and-handler

- [x] Task 3.5: Update BacktestRun entity
  - Details: .agent-context/3-develop/build/plans/details/20260402-typed-config-separation-phase-03-details.md#task-35-update-backtestrun-entity

- [x] Task 3.6: Update BacktestRunResponseMapper
  - Details: .agent-context/3-develop/build/plans/details/20260402-typed-config-separation-phase-03-details.md#task-36-update-backtestrunresponsemapper

- [x] Task 3.7: Update BacktestRunResponse
  - Details: .agent-context/3-develop/build/plans/details/20260402-typed-config-separation-phase-03-details.md#task-37-update-backtestrunresponse

- [x] Task 3.8: Update BacktestProcessorService.BuildConfig (final form)
  - Details: .agent-context/3-develop/build/plans/details/20260402-typed-config-separation-phase-03-details.md#task-38-update-backtestprocessorservice-buildconfig-final

- [x] Task 3.9: Update DbContext and add EF migration
  - Details: .agent-context/3-develop/build/plans/details/20260402-typed-config-separation-phase-03-details.md#task-39-update-dbcontext-and-migration

- [x] Task 3.10: Update API and controller tests
  - Details: .agent-context/3-develop/build/plans/details/20260402-typed-config-separation-phase-03-details.md#task-310-update-api-and-controller-tests

- [x] Task 3.11: Run build and all tests
  - Details: .agent-context/3-develop/build/plans/details/20260402-typed-config-separation-phase-03-details.md#task-311-run-build-and-tests

### [x] Phase 4: Frontend

**Complexity**: Medium | **Risk**: Low

- [x] Task 4.1: Update TypeScript models
  - Details: .agent-context/3-develop/build/plans/details/20260402-typed-config-separation-phase-04-details.md#task-41-update-typescript-models

- [x] Task 4.2: Update backtest-form component
  - Details: .agent-context/3-develop/build/plans/details/20260402-typed-config-separation-phase-04-details.md#task-42-update-backtest-form-component

- [x] Task 4.3: Update backtest-page component (prefill)
  - Details: .agent-context/3-develop/build/plans/details/20260402-typed-config-separation-phase-04-details.md#task-43-update-backtest-page-component

- [x] Task 4.4: Run frontend build and lint
  - Details: .agent-context/3-develop/build/plans/details/20260402-typed-config-separation-phase-04-details.md#task-44-run-frontend-build-and-lint

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: Domain Types & Model Migration | Medium | Low |
| Phase 2: Core Pipeline Refactoring | High | Medium |
| Phase 3: Entity, Command, Mapper & API Contract | High | Medium |
| Phase 4: Frontend | Medium | Low |
| **Total** | **High** | **Medium** |

### Scoping Notes

- Phase 2 uses old Application.GridStrategyConfig temporarily in `BuildConfig` for backward-compatible deserialization from the single JSON column
- Phase 3 deletes the old GridStrategyConfig and switches to two-column storage — this is the point of no return for old data
- Old backtest records should be cleaned out before running the Phase 3 migration (per PBI decision)
- The PBI specifies FluentValidation but the project uses Data Annotations — this plan follows the existing pattern
- PositionSize stays on GridStrategyConfig (deviation from PBI) per alignment decision — GridController and GridStrategyEngine both need it
- No architecture tests exist in the project — the "run architecture tests" PBI requirement is N/A

## Dependencies

- .NET SDK (existing)
- Entity Framework Core tools for migration (`dotnet ef`)
- Angular CLI for frontend build/lint
- No new NuGet packages or npm packages required

## Success Criteria

- All `JsonSerializer.Deserialize<GridStrategyConfig>` calls removed from engine, controller, and processor service
- `IStrategyEngine` and `IGridController` interfaces accept `IStrategyConfig` not `string`
- `BacktestConfig` has `IStrategyConfig Strategy` + `ExecutionConfig Execution` (no standalone `FeeModel`)
- `BacktestRun` entity stores `StrategyConfigJson` + `ExecutionConfigJson` as separate columns
- All existing tests pass after signature updates
- Backtest results are identical for the same parameters
- Angular backtest form sends `{ strategy: {...}, execution: {...} }` request shape
- Frontend builds and lints cleanly

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-04-02T13:52:01Z | 2026-04-02T14:16:13Z |
| Plan Reviewer | reviewed | 2026-04-02T14:16:48Z | 2026-04-02T14:21:40Z |
| Plan Implementer | implemented | 2026-04-02T14:30:00Z | 2026-04-02T15:45:00Z |
| Implementation Reviewer | complete | 2026-04-02T15:40:06Z | 2026-04-02T16:12:01Z |
