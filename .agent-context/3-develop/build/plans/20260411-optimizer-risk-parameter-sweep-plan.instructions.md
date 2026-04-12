---
applyTo: ".agent-context/3-develop/build/changes/20260411-optimizer-risk-parameter-sweep-changes.md"
currentAgent: "3-Develop: 3 Reviewer"
agentStartedAt: "2026-04-12T09:29:12Z"
status: "complete"
lastUpdated: "2026-04-12T09:35:00Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: Optimizer Risk Parameter Sweep

## Overview

Extend the strategy optimizer to sweep `riskPerTradePercent` when the user selects `RiskBased` sizing mode. The optimizer currently only generates `PercentWallet` candidates — this PBI adds `RiskBased` as a discrete mode the user can select, with its own parameter set. Signal-only (the optimizer does not generate grid strategies).

## PBI Details

### User Story

> As a **trader**, I want **the optimizer to test different risk percentages per trade** so that **I find the risk level that maximizes risk-adjusted returns for my strategy**.

### Problem Statement

The current optimizer sweeps `PositionSizeOptions` (percent of wallet) and `LeverageMin`/`LeverageMax` independently. With `RiskBased` sizing, position size and leverage are both derived from `riskPerTradePercent` and stop-loss distance, so the existing sweep dimensions don't apply. The optimizer needs a new parameter axis for risk percentage, and the leverage sweep must be conditional on whether auto-leverage is enabled.

### Acceptance Criteria

- [ ] **Given** `PositionSizeMode = RiskBased` and `RiskPerTradePercentOptions = [0.5, 1.0, 1.5, 2.0]`, **When** the optimizer generates candidates, **Then** all candidates use `PositionSizeType = RiskBased` with `RiskPerTradePercent` drawn from the options
- [ ] **Given** `PositionSizeMode = PercentWallet`, **When** the optimizer generates candidates, **Then** all candidates use `PercentWallet` with `PositionSizeValue` from `PositionSizeOptions` (existing behaviour)
- [ ] **Given** a `RiskBased` candidate with `AutoLeverage = true`, **When** the config is generated, **Then** leverage is NOT independently swept (derived from SL distance at runtime)
- [ ] **Given** a `RiskBased` candidate with `AutoLeverage = false`, **When** the config is generated, **Then** leverage IS swept from `LeverageMin`/`LeverageMax`
- [ ] **Given** `PositionSizeMode = RiskBased` and `IncludeAutoLeverage = true`, **When** candidates are generated, **Then** some have `AutoLeverage = true` and some have `AutoLeverage = false`
- [ ] **Given** `IncludeAutoLeverage = false`, **When** candidates are generated, **Then** all have `AutoLeverage = false` and leverage is swept normally

## Objectives

- Add `RiskBased` to `PositionSizeType` enum and extend `RiskConfig` with `RiskPerTradePercent` and `AutoLeverage` properties
- Create optimizer-specific `PositionSizeMode` enum and extend `ParameterBounds` with risk-based sweep fields
- Update `StrategyConfigGenerator.GenerateRiskConfig` to branch on the sizing mode
- Update `ValidateBounds`, `BuildDescription`, and the API contract (`RunOptimizationRequest` / `BuildBounds`)
- Add comprehensive unit tests covering all acceptance criteria

### Discovery References

- `.agent-context/0-knowledge/33-risk-management-and-trade-sizing.md` — defines the target RiskBased model, R-based sizing formula, AutoLeverage behaviour
- `.agent-context/0-knowledge/18-backtesting-architecture.md` — optimizer pipeline (SweepRunner → StrategyConfigGenerator → backtests)
- `.agent-context/0-knowledge/13-strategy-config-schema.md` — StrategyConfig/RiskConfig schema, serialization rules

### Project Patterns

- `src/TradingApp.Application/StrategyAuthoring/Models/PositionSizeType.cs` — existing 2-value enum, add `RiskBased`
- `src/TradingApp.Application/StrategyAuthoring/Models/RiskConfig.cs` — sealed record, add `RiskPerTradePercent` and `AutoLeverage`
- `src/TradingApp.Application/Optimization/Models/ParameterBounds.cs` — sealed record with init properties and default values
- `src/TradingApp.Application/Optimization/Services/StrategyConfigGenerator.cs` — `GenerateRiskConfig` (L260), `ValidateBounds` (L399), `BuildDescription` (L316)
- `src/TradingApp.Api/Models/RunOptimizationRequest.cs` — API contract with nullable override fields
- `src/TradingApp.Api/Controllers/OptimizationsController.cs` — `BuildBounds` (L130) maps request → `ParameterBounds`
- `tests/TradingApp.Application.Tests/Optimization/StrategyConfigGeneratorTests.cs` — MSTest + FluentAssertions, direct instantiation, seed-based determinism
- `tests/TradingApp.Application.Tests/StrategyAuthoring/Models/StrategyConfigSerializationTests.cs` — serialization round-trip and snake_case enum tests

### [x] Phase 1: Domain & Optimizer Model Extensions

**Complexity**: Low | **Risk**: Low

- [x] Task 1.1: Add `RiskBased` value to `PositionSizeType` enum
  - Details: .agent-context/3-develop/build/plans/details/20260411-optimizer-risk-parameter-sweep-phase-01-details.md#task-11-add-riskbased-to-positionsizetype-enum

- [x] Task 1.2: Add `RiskPerTradePercent` and `AutoLeverage` properties to `RiskConfig`
  - Details: .agent-context/3-develop/build/plans/details/20260411-optimizer-risk-parameter-sweep-phase-01-details.md#task-12-add-riskpertradePercent-and-autoleverage-to-riskconfig

- [x] Task 1.3: Create `PositionSizeMode` enum in Optimization models
  - Details: .agent-context/3-develop/build/plans/details/20260411-optimizer-risk-parameter-sweep-phase-01-details.md#task-13-create-positionsizemode-enum

- [x] Task 1.4: Extend `ParameterBounds` with risk-based sweep fields
  - Details: .agent-context/3-develop/build/plans/details/20260411-optimizer-risk-parameter-sweep-phase-01-details.md#task-14-extend-parameterbounds-with-risk-based-fields

- [x] Task 1.5: Add serialization round-trip test for `RiskBased` enum and new `RiskConfig` fields
  - Details: .agent-context/3-develop/build/plans/details/20260411-optimizer-risk-parameter-sweep-phase-01-details.md#task-15-add-serialization-tests

- [x] Task 1.6: Run all existing tests to verify backward compatibility
  - Details: .agent-context/3-develop/build/plans/details/20260411-optimizer-risk-parameter-sweep-phase-01-details.md#task-16-run-all-existing-tests

### [x] Phase 2: Generator Logic, API Wiring & Comprehensive Tests

**Complexity**: Medium | **Risk**: Low

- [x] Task 2.1: Update `GenerateRiskConfig` to branch on `PositionSizeMode`
  - Details: .agent-context/3-develop/build/plans/details/20260411-optimizer-risk-parameter-sweep-phase-02-details.md#task-21-update-generateriskconfig

- [x] Task 2.2: Update `ValidateBounds` for `RiskBased` mode validation
  - Details: .agent-context/3-develop/build/plans/details/20260411-optimizer-risk-parameter-sweep-phase-02-details.md#task-22-update-validatebounds

- [x] Task 2.3: Update `BuildDescription` for `RiskBased` candidates
  - Details: .agent-context/3-develop/build/plans/details/20260411-optimizer-risk-parameter-sweep-phase-02-details.md#task-23-update-builddescription

- [x] Task 2.4: Add `PositionSizeMode`, `RiskPerTradePercentOptions`, and `IncludeAutoLeverage` to `RunOptimizationRequest`
  - Details: .agent-context/3-develop/build/plans/details/20260411-optimizer-risk-parameter-sweep-phase-02-details.md#task-24-update-runoptimizationrequest

- [x] Task 2.5: Update `BuildBounds` in `OptimizationsController` to wire new fields
  - Details: .agent-context/3-develop/build/plans/details/20260411-optimizer-risk-parameter-sweep-phase-02-details.md#task-25-update-buildbounds

- [x] Task 2.6: Add unit tests for `RiskBased` config generation and `AutoLeverage` behaviour
  - Details: .agent-context/3-develop/build/plans/details/20260411-optimizer-risk-parameter-sweep-phase-02-details.md#task-26-add-riskbased-unit-tests

- [x] Task 2.7: Verify existing `PercentWallet` tests pass unchanged and run all tests
  - Details: .agent-context/3-develop/build/plans/details/20260411-optimizer-risk-parameter-sweep-phase-02-details.md#task-27-verify-existing-tests-and-run-all

## Scoping Summary

| Phase | Complexity | Risk |
|-------|----------|------|
| Phase 1: Domain & Optimizer Model Extensions | Low | Low |
| Phase 2: Generator Logic, API Wiring & Comprehensive Tests | Medium | Low |
| **Total** | **Medium** | **Low** |

### Scoping Notes

- `PositionSizeResolver` changes (runtime RiskBased calculation) are out of scope — that is the dependency PBI "P1 R-Based Position Sizing"
- Frontend optimizer UI changes for `RiskBased` mode selection are out of scope per PBI
- Fitness function requires no changes — it evaluates backtest output metrics independent of sizing mode
- `EvolutionaryRunner` crossover swaps `Risk` objects wholesale — no changes needed (inherits correct `PositionSizeType`)
- `RiskBased` enum value and `RiskConfig` fields (RiskPerTradePercent, AutoLeverage) are added in this PBI as prerequisites for the optimizer sweep; they enable the dependency PBI but do not implement runtime usage
- No database migration needed — `ParameterBounds` is in-memory only, `StrategyConfigJson` serializes whatever `RiskConfig` holds
- Knowledge doc 33 "Optimizer Integration" section describes `IncludeRiskBasedSizing` (boolean mixing approach) — this PBI uses `PositionSizeMode` (discrete enum) instead; doc 33 should be updated after implementation

## Dependencies

- .NET 9 / C# 13 (current stack)
- MSTest, FluentAssertions v6, Moq (existing test dependencies)
- Existing `StrategyJsonOptions.Default` with `JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)` for enum serialization

## Success Criteria

- All 6 acceptance criteria pass as automated unit tests
- All existing `PercentWallet`-mode optimizer tests pass unchanged
- Serialization round-trip tests verify `risk_based` snake_case enum value
- `ValidateBounds` rejects empty `RiskPerTradePercentOptions` when mode is `RiskBased`
- `BuildDescription` produces meaningful text for `RiskBased` candidates (showing R% instead of Size%)
- API contract accepts new fields (`PositionSizeMode`, `RiskPerTradePercentOptions`, `IncludeAutoLeverage`)

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|----------|
| Implementation Planner | planned | 2026-04-11T12:34:00Z | 2026-04-11T12:43:00Z |
| Plan Reviewer | plan-reviewed | 2026-04-11T00:47:54Z | 2026-04-11T00:49:25Z |
| 3-Develop: 2 Implementer | implemented | 2026-04-12T08:05:29Z | 2026-04-12T09:27:12Z |
