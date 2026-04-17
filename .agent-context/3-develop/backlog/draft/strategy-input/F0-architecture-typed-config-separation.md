# PBI Specification: F0 — Architecture: Typed Config & Execution Separation

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-04-02
**Last Updated:** 2026-04-02
**PRD:** [02-strategy-input-pipeline.md](../../../prd-draft/02-strategy-input-pipeline.md)
**Implementation Phase:** 1a (Foundation)
**Risk Level:** High
**Depends On:** None

---

## Summary

Refactor the core trading interfaces to separate **strategy parameters** from **execution parameters**, and replace raw `string strategyConfigJson` passing with typed config objects throughout the pipeline. This is a mechanical, testable refactoring — no new features, no schema changes, no UI. All existing backtesting and grid strategy functionality must remain working with identical results.

### User Story

> As a **platform developer**, I want **typed config objects instead of raw JSON strings in the trading pipeline** so that **config errors are caught at compile time and we have a clean foundation for the strategy builder**.

### Business Value

Foundation for everything that follows. Without this, every downstream feature inherits the raw-JSON-string pattern. Separating strategy from execution config also clarifies what the strategy schema is responsible for vs what belongs to the backtest/execution layer. The `IStrategyConfig` marker interface ensures new strategy types (DCA, momentum, etc.) can be added without touching core pipeline signatures.

---

## Problem Statement

`string strategyConfigJson` passes through `IStrategyEngine`, `IGridController`, `StrategyScheduler`, and `BacktestRunner`. Both `GridStrategyEngine` and `GridController` independently deserialize it into `GridStrategyConfig`, which mixes strategy params (grid levels, spacing, TP/SL) with execution params (fees, slippage, position size, leverage). Fee information is also duplicated between `GridStrategyConfig` and the separate `FeeModel` on `BacktestConfig`.

---

## Requirements

### Functional Requirements

#### Config Type Hierarchy (TradePilot.Domain)

- [ ] `IStrategyConfig` marker interface in `TradePilot.Domain` — enables polymorphic strategy config passing through the pipeline; new strategy types implement this interface
- [ ] `GridStrategyConfig` record implementing `IStrategyConfig` in `TradePilot.Domain` — strategy parameters only: GridLevels, GridSpacing, TakeProfitPercent, StopLossPercent, BreakdownThreshold, EntryMode, ManualAnchorPrice
- [ ] `ExecutionConfig` record in `TradePilot.Domain` — execution parameters only: FeeModel (contains MakerFee, TakerFee, Slippage), PositionSize, Leverage
- [ ] Old `GridStrategyConfig` class in `Backtesting/Models/` removed (no legacy conversion needed — old DB records will be cleaned out)

#### Interface Refactoring

- [ ] `IStrategyEngine.EvaluateAsync(MarketContext, IStrategyConfig, CancellationToken)` — replaces `string strategyConfigJson`
- [ ] `IGridController.ProcessAsync(..., IStrategyConfig, CancellationToken)` — replaces `string strategyConfigJson`
- [ ] `GridStrategyEngine` — remove `JsonSerializer.Deserialize`, receive typed `IStrategyConfig`
- [ ] `GridController` — remove `JsonSerializer.Deserialize`, receive typed `IStrategyConfig`
- [ ] `StrategyScheduler` constructor — accept `IStrategyConfig` instead of `string strategyConfigJson`
- [ ] `BacktestConfig` — replace `string StrategyConfigJson` and `FeeModel FeeModel` with `IStrategyConfig Strategy` + `ExecutionConfig Execution` (single source of truth for fees via `ExecutionConfig.FeeModel`; `InitialCapital` stays on `BacktestConfig` since it's backtest-specific)
- [ ] `BacktestRunner` — deserialize config **once** at the entry point, pass typed objects downstream
- [ ] `BacktestProcessorService.BuildConfig` — refactor to use typed config instead of deserializing `GridStrategyConfig` from JSON

#### API Contract (Clean Break)

- [ ] `RunBacktestRequest` updated to separate strategy and execution parameter sections
- [ ] `BacktestsController` maps request into typed `GridStrategyConfig` (as `IStrategyConfig`) + `ExecutionConfig`
- [ ] FluentValidation on `RunBacktestRequest` at the API boundary (e.g. GridLevels > 0, Leverage >= 1); config records stay simple data carriers — full validation pipeline arrives in F1
- [ ] Angular `BacktestService` and `backtest.model.ts` updated to send the new nested request shape
- [ ] Clean break-and-replace: no backward compatibility for old request shape (frontend and backend deploy together)

#### Database Changes

- [ ] `BacktestRun` entity updated to store two separate JSON columns: `StrategyConfigJson` (strategy params only) + `ExecutionConfigJson` (execution params only)
- [ ] Old backtest records cleaned out (no legacy conversion utility needed)
- [ ] `BacktestRunResponseMapper` updated to deserialize from new column layout

### Non-Functional Requirements

- [ ] All existing tests pass (updated to new signatures)
- [ ] Identical backtest results for the same parameters before and after
- [ ] Zero `JsonSerializer.Deserialize<GridStrategyConfig>` calls in engine, controller, or processor service
- [ ] New strategy types can be added by implementing `IStrategyConfig` without modifying pipeline interfaces

---

## Technical Considerations

### Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Strategy config polymorphism | `IStrategyConfig` marker interface | Lightweight extensibility. Each strategy type defines its own params. No JSON polymorphic serialization overhead. New strategies implement the interface without touching core pipeline. |
| ExecutionConfig ↔ FeeModel | `ExecutionConfig` contains `FeeModel` | `FeeModel` retains its calculation methods (`CalculateFee`, `ApplySlippage`). `ExecutionConfig` adds `PositionSize` and `Leverage`. Single source of truth — `BacktestConfig.FeeModel` removed. |
| Fee duplication | Eliminated | Fees previously existed in both `BacktestConfig.FeeModel` AND `GridStrategyConfig` JSON. Now only in `ExecutionConfig.FeeModel`. |
| InitialCapital placement | Stays on `BacktestConfig` | Backtest-specific concept (live trading uses account balance). Keeps `ExecutionConfig` reusable across live and backtest contexts. |
| Config namespace | `TradePilot.Domain` | `IStrategyConfig`, `GridStrategyConfig`, `ExecutionConfig` are core domain concepts. Domain layer is dependency-free and referenced by all other layers. |
| Legacy DB records | Clean out | No conversion utility needed. Old backtest records deleted before migration. |
| API versioning | Clean break | Single deployment (frontend + backend). No external consumers. No backward compatibility shim. |
| Validation | API boundary only | FluentValidation on `RunBacktestRequest` DTO in controller layer. Config records are simple data carriers. Full validation pipeline deferred to F1. |

### What Does NOT Change

`IRiskEngine`, `IPositionManager`, `IExecutionEngine` (interface), `IMarketContextBuilder`, `TradingSignal`, candle entities, `FeeModel` class (retained with its calculation methods, now owned by `ExecutionConfig`).

---

## Out of Scope

- Full canonical schema (F1)
- New validation pipeline (F1)
- Strategy Builder UI (F2)
- New strategy types or condition evaluation
- Trend/bias/risk schema sections (added in F1)
- Constructor-level validation guards on config records

---

## Acceptance Criteria

- [ ] **Given** `IStrategyEngine`, **When** inspected, **Then** `EvaluateAsync` accepts `IStrategyConfig` not `string`
- [ ] **Given** `IGridController`, **When** inspected, **Then** `ProcessAsync` accepts `IStrategyConfig` not `string`
- [ ] **Given** `GridStrategyEngine`, `GridController`, and `BacktestProcessorService` source, **When** inspected, **Then** no `JsonSerializer.Deserialize<GridStrategyConfig>` calls exist
- [ ] **Given** `BacktestConfig`, **When** inspected, **Then** separate `Strategy` (`IStrategyConfig`) and `Execution` (`ExecutionConfig`) properties exist, and no standalone `FeeModel` property
- [ ] **Given** `BacktestRun` entity, **When** inspected, **Then** `StrategyConfigJson` and `ExecutionConfigJson` are stored as separate columns
- [ ] **Given** `GridStrategyConfig`, **When** inspected, **Then** it implements `IStrategyConfig` and lives in `TradePilot.Domain`
- [ ] **Given** `ExecutionConfig`, **When** inspected, **Then** it contains a `FeeModel` property plus `PositionSize` and `Leverage`
- [ ] **Given** a backtest with `{ gridLevels: 10, gridSpacing: 0.5, ... }`, **When** run before and after refactoring, **Then** results are identical
- [ ] **Given** all existing tests, **When** run, **Then** all pass
- [ ] **Given** the Angular backtest form, **When** a backtest is submitted, **Then** the request body has separate `strategy` and `execution` sections

### Release Notes Information

- **Heading**: Strategy Configuration Architecture Refactoring
- **Release note type**: Breaking Change
- **Release Note Summary**: Backtest API request now separates strategy and execution parameters into distinct sections. Config types use a marker interface (`IStrategyConfig`) for extensibility to future strategy types.
- **Release Notes Audience**: Product
- **Breaking Change**: Yes — API contract change for `POST /api/backtests`
