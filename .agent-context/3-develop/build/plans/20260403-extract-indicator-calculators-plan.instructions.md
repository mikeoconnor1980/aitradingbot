---
applyTo: ".agent-context/3-develop/build/changes/20260403-extract-indicator-calculators-changes.md"
currentAgent: "None"
agentStartedAt: "2026-04-03T13:37:03Z"
status: "plan-reviewed"
lastUpdated: "2026-04-03T13:40:28Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: F6.5 — Extract Indicator Calculators into Standalone Project

## Overview

Extract all indicator calculations from `BacktestMarketContextBuilder` private methods into a dedicated `TradingApp.Indicators` project as standalone, pure-math static classes. Fix incorrect RSI (Wilder smoothing), EMA (SMA seed), and ATR (Wilder smoothing) algorithms. Add MACD and Bollinger Bands calculators. Extend `IndicatorContext` for MACD multi-output. Refactor `BacktestMarketContextBuilder` to delegate to new calculators.

## PBI Details

**PBI ID:** Draft — F6.5
**Status:** Draft
**Reference:** `.agent-context/3-develop/backlog/draft/strategy-input/F6.5-extract-indicator-calculators.md`
**Depends On:** F5 (Indicator Infra + RSI Handler), F6 (UI: RSI Condition + Signal Mode)

### User Story

> As a **developer**, I want **indicator calculations (EMA, RSI, ATR, MACD, Bollinger Bands) extracted into a dedicated `TradingApp.Indicators` project as standalone, pure-math static classes** so that **calculations are independently testable, reusable across live and backtest contexts, and new indicators can be added without modifying existing services**.

### Acceptance Criteria

- [ ] **Given** the `TradingApp.Indicators` project exists, **When** I inspect its dependencies, **Then** it has no references to `TradingApp.Application`, `TradingApp.Domain`, or any infrastructure projects
- [ ] **Given** a known set of closing prices, **When** `EmaCalculator.Calculate()` is called with period 9, **Then** the result matches the TradingView EMA(9) value for the same data (SMA-seeded)
- [ ] **Given** a known set of closing prices, **When** `RsiCalculator.Calculate()` is called with period 14, **Then** the result matches the TradingView RSI(14) value (Wilder smoothing)
- [ ] **Given** a set of candles, **When** `AtrCalculator.Calculate()` is called with period 14, **Then** it returns the correct ATR using Wilder-smoothed true range calculation
- [ ] **Given** a known set of closing prices, **When** `MacdCalculator.Calculate()` is called with standard parameters (12, 26, 9), **Then** it returns MACD line, signal line, and histogram values matching TradingView MACD
- [ ] **Given** a known set of closing prices, **When** `BollingerBandsCalculator.Calculate()` is called with period 20 and 2 standard deviations, **Then** it returns upper, middle, and lower band values matching TradingView Bollinger Bands
- [ ] **Given** `BacktestMarketContextBuilder` has been refactored, **When** I run the existing backtest test suite, **Then** all tests pass (with expected value changes due to RSI/EMA/ATR corrections acknowledged and updated)
- [ ] **Given** a strategy config requiring MACD indicators, **When** the `BacktestMarketContextBuilder` builds `IndicatorContext`, **Then** MACD line, signal, and histogram values are populated and available via `IndicatorContext`
- [ ] **Given** fewer candles than the warmup period, **When** any calculator is invoked, **Then** it returns null or a partial series without throwing exceptions

## Objectives

- Create `TradingApp.Indicators` class library with zero dependencies
- Create `TradingApp.Indicators.Tests` test project with reference-verified tests
- Implement correct EMA (SMA-seeded), RSI (Wilder-smoothed), ATR (Wilder-smoothed) calculators
- Implement MACD calculator returning line, signal, and histogram
- Implement Bollinger Bands calculator returning upper, middle, and lower bands
- Extend `IndicatorContext` with MACD line/signal/histogram storage
- Refactor `BacktestMarketContextBuilder` to delegate to the new calculators
- Update existing tests affected by algorithm corrections

### Discovery References

- **Algorithm bugs confirmed**: EMA seeds from `closes[0]` not SMA; RSI uses simple average not Wilder smoothing; ATR uses SMA not Wilder smoothing
- **`IndicatorContext.SetMacd`** currently stores a single scalar — will be extended to store line, signal, and histogram as separate keyed values
- **`IndicatorExtractor`** already routes MACD requirements — only `BuildIndicatorContext` case "MACD" is missing
- **`IndicatorSnapshot`** (legacy fixed-field) remains unchanged — its EMA/RSI/ATR values will be computed via the new calculators
- **Bollinger Bands** — calculator created and tested but NOT wired to `IndicatorContext` (no condition handler exists yet)

### Project Patterns

- `src/TradingApp.Application/Trading/Services/BacktestMarketContextBuilder.cs` — current indicator calculation methods to extract
- `src/TradingApp.Application/Trading/Models/IndicatorContext.cs` — keyed dictionary for indicator values
- `src/TradingApp.Application/Trading/Models/IndicatorSnapshot.cs` — legacy fixed-property record
- `src/TradingApp.Application/StrategyAuthoring/Models/IndicatorRequirement.cs` — requirement record
- `src/TradingApp.Application/StrategyAuthoring/Services/IndicatorExtractor.cs` — static class pattern for new calculators
- `src/TradingApp.Domain/TradingApp.Domain.csproj` — minimal csproj template (no deps)
- `tests/TradingApp.Domain.Tests/TradingApp.Domain.Tests.csproj` — test project template
- `tests/TradingApp.Application.Tests/Usings.cs` — global usings pattern
- `tests/TradingApp.Application.Tests/Trading/Services/BacktestMarketContextBuilderIndicatorTests.cs` — existing tests to update
- `TradingApp.sln` — solution file for project registration

### [ ] Phase 1: Project Scaffolding + EMA and RSI Calculators with Tests

**Complexity**: Medium | **Risk**: Medium

- [ ] Task 1.1: Create `TradingApp.Indicators` project and `TradingApp.Indicators.Tests` project
  - Details: .agent-context/3-develop/build/plans/details/20260403-extract-indicator-calculators-phase-01-details.md#task-11-create-indicator-projects

- [ ] Task 1.2: Add both projects to `TradingApp.sln`
  - Details: .agent-context/3-develop/build/plans/details/20260403-extract-indicator-calculators-phase-01-details.md#task-12-add-projects-to-solution

- [ ] Task 1.3: Implement `EmaCalculator` with SMA-seeded algorithm
  - Details: .agent-context/3-develop/build/plans/details/20260403-extract-indicator-calculators-phase-01-details.md#task-13-implement-emacalculator

- [ ] Task 1.4: Implement `EmaCalculatorTests` with TradingView-verified values
  - Details: .agent-context/3-develop/build/plans/details/20260403-extract-indicator-calculators-phase-01-details.md#task-14-implement-emacalculatortests

- [ ] Task 1.5: Implement `RsiCalculator` with Wilder smoothing
  - Details: .agent-context/3-develop/build/plans/details/20260403-extract-indicator-calculators-phase-01-details.md#task-15-implement-rsicalculator

- [ ] Task 1.6: Implement `RsiCalculatorTests` with TradingView-verified values
  - Details: .agent-context/3-develop/build/plans/details/20260403-extract-indicator-calculators-phase-01-details.md#task-16-implement-rsicalculatortests

- [ ] Task 1.7: Build solution and run all tests
  - Details: .agent-context/3-develop/build/plans/details/20260403-extract-indicator-calculators-phase-01-details.md#task-17-build-and-run-tests

### [ ] Phase 2: ATR, MACD, and Bollinger Bands Calculators with Tests

**Complexity**: Medium | **Risk**: Low

- [ ] Task 2.1: Implement `AtrCalculator` with Wilder-smoothed algorithm
  - Details: .agent-context/3-develop/build/plans/details/20260403-extract-indicator-calculators-phase-02-details.md#task-21-implement-atrcalculator

- [ ] Task 2.2: Implement `AtrCalculatorTests` with verified values
  - Details: .agent-context/3-develop/build/plans/details/20260403-extract-indicator-calculators-phase-02-details.md#task-22-implement-atrcalculatortests

- [ ] Task 2.3: Implement `MacdCalculator` returning line, signal, and histogram
  - Details: .agent-context/3-develop/build/plans/details/20260403-extract-indicator-calculators-phase-02-details.md#task-23-implement-macdcalculator

- [ ] Task 2.4: Implement `MacdCalculatorTests` with TradingView-verified values
  - Details: .agent-context/3-develop/build/plans/details/20260403-extract-indicator-calculators-phase-02-details.md#task-24-implement-macdcalculatortests

- [ ] Task 2.5: Implement `BollingerBandsCalculator` returning upper, middle, lower bands
  - Details: .agent-context/3-develop/build/plans/details/20260403-extract-indicator-calculators-phase-02-details.md#task-25-implement-bollingerbandscalculator

- [ ] Task 2.6: Implement `BollingerBandsCalculatorTests` with verified values
  - Details: .agent-context/3-develop/build/plans/details/20260403-extract-indicator-calculators-phase-02-details.md#task-26-implement-bollingerbandscalculatortests

- [ ] Task 2.7: Build solution and run all tests
  - Details: .agent-context/3-develop/build/plans/details/20260403-extract-indicator-calculators-phase-02-details.md#task-27-build-and-run-tests

### [ ] Phase 3: Refactor BacktestMarketContextBuilder + Extend IndicatorContext for MACD

**Complexity**: Medium | **Risk**: Medium

- [ ] Task 3.1: Extend `IndicatorContext` with MACD line, signal, and histogram storage
  - Details: .agent-context/3-develop/build/plans/details/20260403-extract-indicator-calculators-phase-03-details.md#task-31-extend-indicatorcontext-for-macd

- [ ] Task 3.2: Add `TradingApp.Indicators` project reference to `TradingApp.Application`
  - Details: .agent-context/3-develop/build/plans/details/20260403-extract-indicator-calculators-phase-03-details.md#task-32-add-indicators-project-reference

- [ ] Task 3.3: Refactor `BacktestMarketContextBuilder` to delegate to new calculators
  - Details: .agent-context/3-develop/build/plans/details/20260403-extract-indicator-calculators-phase-03-details.md#task-33-refactor-backtestmarketcontextbuilder

- [ ] Task 3.4: Add MACD case to `BuildIndicatorContext` switch
  - Details: .agent-context/3-develop/build/plans/details/20260403-extract-indicator-calculators-phase-03-details.md#task-34-add-macd-case-to-buildindicatorcontext

- [ ] Task 3.5: Update `BacktestMarketContextBuilderIndicatorTests` for refactored code
  - Details: .agent-context/3-develop/build/plans/details/20260403-extract-indicator-calculators-phase-03-details.md#task-35-update-existing-tests

- [ ] Task 3.6: Add MACD integration test for `BacktestMarketContextBuilder`
  - Details: .agent-context/3-develop/build/plans/details/20260403-extract-indicator-calculators-phase-03-details.md#task-36-add-macd-integration-test

- [ ] Task 3.7: Build solution and run all tests
  - Details: .agent-context/3-develop/build/plans/details/20260403-extract-indicator-calculators-phase-03-details.md#task-37-build-and-run-all-tests

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: Project Scaffolding + EMA and RSI Calculators | Medium | Medium |
| Phase 2: ATR, MACD, and Bollinger Bands Calculators | Medium | Low |
| Phase 3: Refactor BacktestMarketContextBuilder + MACD Wiring | Medium | Medium |
| **Total** | **Medium** | **Medium** |

### Scoping Notes

- RSI and EMA algorithm corrections are intentional breaking changes — backtest numeric results will differ from pre-F6.5
- ATR will be corrected from SMA(TR) to Wilder-smoothed — another intentional algorithm improvement
- Bollinger Bands calculator is implemented and tested but NOT wired to `IndicatorContext` (no condition handler yet)
- `IndicatorSnapshot` (legacy fixed-field) remains; its values are computed via the new calculators after refactoring
- No API endpoints, frontend changes, or deployment configuration changes are needed

## Dependencies

- .NET 8.0 SDK
- MSTest 3.0.4
- FluentAssertions 6.12.2
- coverlet.collector 6.0.0

## Success Criteria

- `TradingApp.Indicators` project exists with zero project/package dependencies (pure SDK project)
- All 5 calculators (EMA, RSI, ATR, MACD, Bollinger Bands) pass tests verified against TradingView reference values
- `BacktestMarketContextBuilder` has no private indicator calculation methods — all delegate to `TradingApp.Indicators`
- `IndicatorContext` exposes MACD line, signal, and histogram via separate storage keys
- Solution builds and all tests pass (`dotnet build TradingApp.sln && dotnet test TradingApp.sln`)

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-04-03T12:45:15Z | 2026-04-03T13:35:31Z |
| Plan Reviewer | plan-reviewed | 2026-04-03T13:37:03Z | 2026-04-03T13:40:28Z |
