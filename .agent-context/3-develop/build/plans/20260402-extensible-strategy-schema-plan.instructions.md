applyTo: ".agent-context/3-develop/build/changes/20260402-extensible-strategy-schema-changes.md"
currentAgent: "None"
agentStartedAt: "2026-04-02T17:47:42Z"
status: "complete"
lastUpdated: "2026-04-02T18:34:21Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: F1 — Extensible Strategy Schema (v1 — Grid)

## Overview

Define the versioned, extensible strategy JSON schema and C# models that support the full composable structure from the Strategy Builder UI spec but only require grid fields for v1. Implement a three-level server-side validation pipeline and a `POST /api/strategies/validate` endpoint. Replace F0's `GridStrategyConfig` with the full `StrategyConfig`, shrink `ExecutionConfig` to FeeModel only, and update all consumers.

## PBI Details

**PBI**: F1 — Extensible Strategy Schema (v1 — Grid)
**Location**: `.agent-context/3-develop/backlog/draft/strategy-input/F1-extensible-strategy-schema.md`

> As a **platform developer**, I want an **extensible strategy schema with validation** so that **I can add new condition types and exit rules later without schema migrations**.

### Acceptance Criteria

- [ ] **Given** a grid strategy JSON matching the v1 schema, **When** deserialized, **Then** `StrategyConfig` has `StrategyMode = Grid` and `Grid` section populated
- [ ] **Given** `strategyMode = "grid"` and `grid = null`, **When** Level 3 validation runs, **Then** error: "Grid configuration required for grid mode"
- [ ] **Given** `strategyMode = "signal"` and no entry conditions, **When** Level 3 validation runs, **Then** error: "At least one entry condition required for signal mode"
- [ ] **Given** a strategy with `trendFilter` populated, **When** validated in v1, **Then** info message: "Trend filter not yet evaluated" (not blocking)
- [ ] **Given** `grid.levels = 0`, **When** Level 2 validation runs, **Then** error on `grid.levels`
- [ ] **Given** `strategyName = ""`, **When** Level 1 validation runs, **Then** error: "Strategy name is required"
- [ ] **Given** the new `StrategyConfig`, **When** used in `IStrategyEngine.EvaluateAsync`, **Then** `GridStrategyEngine` reads from `config.Grid.*` correctly
- [ ] **Given** the new `StrategyConfig`, **When** used in `IGridController.ProcessAsync`, **Then** `GridController` reads TP/SL from `config.Exit.*` correctly
- [ ] **Given** a `StrategyConfig` with an RSI entry condition, **When** serialized to JSON and back, **Then** the `RsiParams` are correctly round-tripped
- [ ] **Given** all existing tests, **When** run after updating to the new type, **Then** all pass
- [ ] **Given** a `POST /api/strategies/validate` request with an invalid config, **When** the endpoint runs, **Then** it returns all validation errors/warnings/info grouped by level
- [ ] **Given** `ExecutionConfig` after F1, **When** inspected, **Then** it contains only `FeeModel` (no leverage or positionSize)
- [ ] **Given** `StrategyConfig.Risk` with `leverage = 0`, **When** Level 2 validation runs, **Then** error: leverage must be ≥ 1

## Objectives

- Define a versioned, extensible `StrategyConfig` with `strategyMode` discriminator (grid/signal)
- Create all sub-models (GridConfig, ExitConfig, RiskConfig, TrendFilterConfig, EntryConditionConfig, etc.) and enums
- Implement a custom `JsonConverter` for polymorphic entry condition params deserialization
- Build a 3-level validation pipeline: schema → business rules → cross-field consistency
- Add `POST /api/strategies/validate` endpoint on a new `StrategiesController`
- Replace F0's `GridStrategyConfig` and shrink `ExecutionConfig` to FeeModel only
- Update all pipeline consumers (GridController, GridStrategyEngine, BacktestRunResponseMapper, RunBacktestCommand, BacktestProcessorService, BacktestsController)
- Update the `RunBacktestRequest` with a dedicated nested request DTO shape
- Ensure JSON round-trip fidelity with `System.Text.Json` (camelCase, string enums, null optionals)

### Discovery References

**Design Decisions (from alignment):**

| Decision | Choice | Rationale |
|----------|--------|-----------|
| FluentValidation vs DataAnnotations | Keep DataAnnotations (existing pattern) | No FluentValidation in codebase. DataAnnotations for request-level; `IStrategyValidator` for domain-level 3-level pipeline. |
| RunBacktestRequest shape | Dedicated request DTO | Keep API surface separate from domain models. Updated `StrategyConfigRequest` with nested sub-DTOs maps to `StrategyConfig` in controller. |
| StrategyConfig location | `TradingApp.Application/StrategyAuthoring/Models/` | Implements `IStrategyConfig` (Domain marker). Concrete type in Application per PBI. |
| ExecutionConfig after F1 | FeeModel only, remove Leverage | Leverage moves to `StrategyConfig.Risk.Leverage`. |
| EntryCondition params | Typed records + Dictionary fallback | Custom `JsonConverter` using `type` discriminator for polymorphic deserialization. |
| Exit rule params | Flat nullable fields | `ExitRuleConfig` has nullable `Value` and `Lookback`. No typed params for v1. |
| Old backtest data | Clean break | Clear old rows in EF migration. No data migration of F0-shaped JSON. |
| JSON serialization | Shared `JsonSerializerOptions` | Replace per-file instances with a shared static. Add `JsonStringEnumConverter`. |
| EntryModes constants | Retained for grid mode | `EntryModes` class stays in Domain. Used by `GridController` for `EntryMode` string values. |

### Project Patterns

- `src/TradingApp.Domain/Trading/IStrategyConfig.cs` — Marker interface (stays)
- `src/TradingApp.Domain/Trading/GridStrategyConfig.cs` — F0 record (to be removed)
- `src/TradingApp.Domain/Trading/ExecutionConfig.cs` — FeeModel + Leverage (Leverage to be removed)
- `src/TradingApp.Domain/Trading/EntryModes.cs` — String constants (stays)
- `src/TradingApp.Application/Trading/Services/GridController.cs` — Type alias + cast pattern
- `src/TradingApp.Application/Trading/Services/GridStrategyEngine.cs` — Cast pattern
- `src/TradingApp.Application/Backtesting/BacktestRunResponseMapper.cs` — Hardcoded GridStrategyConfig
- `src/TradingApp.Application/Backtesting/RunBacktestCommand.cs` — Typed to GridStrategyConfig
- `src/TradingApp.Application/Backtesting/Models/BacktestRunResponse.cs` — GridStrategyConfig response
- `src/TradingApp.Application/Backtesting/Models/BacktestConfig.cs` — Already uses IStrategyConfig
- `src/TradingApp.Api/Controllers/BacktestsController.cs` — Manual mapping + ValidateRequest
- `src/TradingApp.Api/Models/RunBacktestRequest.cs` — Flat StrategyConfigRequest
- `src/TradingApp.Api/Services/BacktestProcessorService.cs` — Deserializes GridStrategyConfig
- `src/TradingApp.Api/Program.cs` — DI registration, no global JSON options
- `tests/TradingApp.Application.Tests/Trading/Services/GridControllerTests.cs` — DefaultConfig inline
- `tests/TradingApp.Application.Tests/Backtesting/Services/BacktestRunnerTests.cs` — CreateConfig helpers
- `tests/TradingApp.Api.Tests/Controllers/BacktestsControllerTests.cs` — Full API tests

### [x] Phase 1: Foundation — Schema Models, Enums & JSON Serialization

**Complexity**: Medium | **Risk**: Low

- [x] Task 1.1: Create enums for the strategy schema
  - Details: .agent-context/3-develop/build/plans/details/20260402-extensible-strategy-schema-phase-01-details.md#task-11-create-enums
- [x] Task 1.2: Create sub-model classes for the strategy schema
  - Details: .agent-context/3-develop/build/plans/details/20260402-extensible-strategy-schema-phase-01-details.md#task-12-create-sub-model-classes
- [x] Task 1.3: Create typed entry condition params with custom JsonConverter
  - Details: .agent-context/3-develop/build/plans/details/20260402-extensible-strategy-schema-phase-01-details.md#task-13-create-typed-entry-condition-params
- [x] Task 1.4: Create the main StrategyConfig class
  - Details: .agent-context/3-develop/build/plans/details/20260402-extensible-strategy-schema-phase-01-details.md#task-14-create-strategyconfigclass
- [x] Task 1.5: Create shared JsonSerializerOptions
  - Details: .agent-context/3-develop/build/plans/details/20260402-extensible-strategy-schema-phase-01-details.md#task-15-create-shared-jsonserializeroptions
- [x] Task 1.6: Add JSON serialization round-trip tests
  - Details: .agent-context/3-develop/build/plans/details/20260402-extensible-strategy-schema-phase-01-details.md#task-16-add-json-serialization-tests
- [x] Task 1.7: Build and run tests
  - Details: .agent-context/3-develop/build/plans/details/20260402-extensible-strategy-schema-phase-01-details.md#task-17-build-and-run-tests

### [x] Phase 2: Validation Pipeline & API Endpoint

**Complexity**: Medium | **Risk**: Low

- [x] Task 2.1: Create validation models (ValidationError, ValidationResult)
  - Details: .agent-context/3-develop/build/plans/details/20260402-extensible-strategy-schema-phase-02-details.md#task-21-create-validation-models
- [x] Task 2.2: Create IStrategyValidator and Level 1 SchemaValidator
  - Details: .agent-context/3-develop/build/plans/details/20260402-extensible-strategy-schema-phase-02-details.md#task-22-create-istrategyvalidator-and-schema-validator
- [x] Task 2.3: Create Level 2 BusinessRuleValidator
  - Details: .agent-context/3-develop/build/plans/details/20260402-extensible-strategy-schema-phase-02-details.md#task-23-create-business-rule-validator
- [x] Task 2.4: Create Level 3 CrossFieldValidator
  - Details: .agent-context/3-develop/build/plans/details/20260402-extensible-strategy-schema-phase-02-details.md#task-24-create-cross-field-validator
- [x] Task 2.5: Create CompositeStrategyValidator and register in DI
  - Details: .agent-context/3-develop/build/plans/details/20260402-extensible-strategy-schema-phase-02-details.md#task-25-create-composite-validator-and-di
- [x] Task 2.6: Create StrategiesController with POST /api/strategies/validate
  - Details: .agent-context/3-develop/build/plans/details/20260402-extensible-strategy-schema-phase-02-details.md#task-26-create-strategiescontroller
- [x] Task 2.7: Add validation unit tests
  - Details: .agent-context/3-develop/build/plans/details/20260402-extensible-strategy-schema-phase-02-details.md#task-27-add-validation-unit-tests
- [x] Task 2.8: Add API controller tests for validate endpoint
  - Details: .agent-context/3-develop/build/plans/details/20260402-extensible-strategy-schema-phase-02-details.md#task-28-add-api-controller-tests
- [x] Task 2.9: Build and run tests
  - Details: .agent-context/3-develop/build/plans/details/20260402-extensible-strategy-schema-phase-02-details.md#task-29-build-and-run-tests

### [x] Phase 3: Consumer Migration & Domain Cleanup

**Complexity**: High | **Risk**: Medium

- [x] Task 3.1: Remove Leverage from ExecutionConfig
  - Details: .agent-context/3-develop/build/plans/details/20260402-extensible-strategy-schema-phase-03-details.md#task-31-remove-leverage-from-executionconfig
- [x] Task 3.2: Remove GridStrategyConfig from Domain
  - Details: .agent-context/3-develop/build/plans/details/20260402-extensible-strategy-schema-phase-03-details.md#task-32-remove-gridstrategyconfig-from-domain
- [x] Task 3.3: Update GridStrategyEngine to use new StrategyConfig
  - Details: .agent-context/3-develop/build/plans/details/20260402-extensible-strategy-schema-phase-03-details.md#task-33-update-gridstrategyengine
- [x] Task 3.4: Update GridController to use new StrategyConfig
  - Details: .agent-context/3-develop/build/plans/details/20260402-extensible-strategy-schema-phase-03-details.md#task-34-update-gridcontroller
- [x] Task 3.5: Update BacktestRunResponseMapper
  - Details: .agent-context/3-develop/build/plans/details/20260402-extensible-strategy-schema-phase-03-details.md#task-35-update-backtestrunresponsemapper
- [x] Task 3.6: Update RunBacktestCommand and BacktestRunResponse
  - Details: .agent-context/3-develop/build/plans/details/20260402-extensible-strategy-schema-phase-03-details.md#task-36-update-runbacktestcommand-and-response
- [x] Task 3.7: Update BacktestProcessorService
  - Details: .agent-context/3-develop/build/plans/details/20260402-extensible-strategy-schema-phase-03-details.md#task-37-update-backtestprocessorservice
- [x] Task 3.8: Update RunBacktestRequest with nested DTO shape
  - Details: .agent-context/3-develop/build/plans/details/20260402-extensible-strategy-schema-phase-03-details.md#task-38-update-runbacktestrequest
- [x] Task 3.9: Update BacktestsController mapping
  - Details: .agent-context/3-develop/build/plans/details/20260402-extensible-strategy-schema-phase-03-details.md#task-39-update-backtestscontroller
- [x] Task 3.10: Configure global JSON serialization options
  - Details: .agent-context/3-develop/build/plans/details/20260402-extensible-strategy-schema-phase-03-details.md#task-310-configure-global-json-options
- [x] Task 3.11: Clean old backtest data via EF migration
  - Details: .agent-context/3-develop/build/plans/details/20260402-extensible-strategy-schema-phase-03-details.md#task-311-ef-migration-clean-old-data
- [x] Task 3.12: Update all existing tests
  - Details: .agent-context/3-develop/build/plans/details/20260402-extensible-strategy-schema-phase-03-details.md#task-312-update-all-existing-tests
- [x] Task 3.13: Build and run ALL tests
  - Details: .agent-context/3-develop/build/plans/details/20260402-extensible-strategy-schema-phase-03-details.md#task-313-build-and-run-all-tests

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: Foundation — Models, Enums & JSON | Medium | Low |
| Phase 2: Validation Pipeline & API Endpoint | Medium | Low |
| Phase 3: Consumer Migration & Domain Cleanup | High | Medium |
| **Total** | **High** | **Medium** |

### Scoping Notes

- Angular TypeScript models and frontend updates are deferred to F2 (Strategy Builder UI). Temporary breakage of the existing backtest UI between F1 and F2 is accepted per PBI.
- No FluentValidation package introduced — existing DataAnnotations pattern for request DTOs, `IStrategyValidator` for domain-level pipeline.
- Entry condition params (RsiParams, PriceVsEmaParams, MacdParams) are structurally present with typed deserialization but are not evaluated by any engine in v1.
- Trend filter config is structurally present but not evaluated in v1; validator emits info-level message.
- Signal mode is a valid `StrategyMode` enum value but not supported for execution in v1; validator emits info-level message.
- `EntryModes` string constants in Domain are retained — used by `GridController` for grid entry mode logic (WaitForLimitPrice, AutoFromSignalCandle, etc.).
- Old backtest data is cleaned out via EF migration (clean break, no migration of F0-shaped JSON).

## Dependencies

- F0 — Typed Config Separation (must be completed first — provides `IStrategyConfig`, typed pipeline)
- System.Text.Json (built-in)
- EF Core migrations (existing tooling)

## Success Criteria

- All 13 acceptance criteria from PBI pass
- JSON round-trip serialization produces identical output for grid and signal mode schemas
- Custom `JsonConverter` correctly handles polymorphic entry condition params
- 3-level validation pipeline catches all error cases defined in PBI
- `POST /api/strategies/validate` returns grouped errors/warnings/info
- All existing tests pass after consumer migration
- Adding a new entry condition type requires only: enum value + params type + validation rules (no schema/serialization changes)

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-04-02T15:40:11Z | 2026-04-02T16:07:42Z |
| Plan Reviewer | plan-reviewed | 2026-04-02T16:08:38Z | 2026-04-02T16:15:26Z |
| Plan Implementer | implemented | 2026-04-02T16:27:41Z | 2026-04-02T17:18:46Z |
| Implementation Reviewer | complete | 2026-04-02T17:47:42Z | 2026-04-02T18:34:21Z |
