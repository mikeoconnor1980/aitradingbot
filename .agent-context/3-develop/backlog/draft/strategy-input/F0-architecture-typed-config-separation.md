# PBI Specification: F0 — Architecture: Typed Config & Execution Separation

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-04-02
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

Foundation for everything that follows. Without this, every downstream feature inherits the raw-JSON-string pattern. Separating strategy from execution config also clarifies what the strategy schema is responsible for vs what belongs to the backtest/execution layer.

---

## Problem Statement

`string strategyConfigJson` passes through `IStrategyEngine`, `IGridController`, `StrategyScheduler`, and `BacktestRunner`. Both `GridStrategyEngine` and `GridController` independently deserialize it into `GridStrategyConfig`, which mixes strategy params (grid levels, spacing, TP/SL) with execution params (fees, slippage, position size, leverage).

---

## Requirements

### Functional Requirements

#### Config Separation

- [ ] `StrategyConfig` record in `TradingApp.Application/Trading/Models/` — strategy parameters only: GridLevels, GridSpacing, TakeProfitPercent, StopLossPercent, BreakdownThreshold, EntryMode, ManualAnchorPrice
- [ ] `ExecutionConfig` record in `TradingApp.Application/Trading/Models/` — execution parameters only: MakerFee, TakerFee, Slippage, PositionSize, Leverage
- [ ] Existing `GridStrategyConfig` kept temporarily in `Backtesting/Models/` for legacy deserialization only

#### Interface Refactoring

- [ ] `IStrategyEngine.EvaluateAsync(MarketContext, StrategyConfig, CancellationToken)` — replaces `string strategyConfigJson`
- [ ] `IGridController.ProcessAsync(..., StrategyConfig, CancellationToken)` — replaces `string strategyConfigJson`
- [ ] `GridStrategyEngine` — remove `JsonSerializer.Deserialize`, receive typed `StrategyConfig`
- [ ] `GridController` — remove `JsonSerializer.Deserialize`, receive typed `StrategyConfig`
- [ ] `StrategyScheduler` constructor — accept `StrategyConfig` instead of `string strategyConfigJson`
- [ ] `BacktestConfig` — replace `string StrategyConfigJson` with `StrategyConfig Strategy` + `ExecutionConfig Execution`
- [ ] `BacktestRunner` — deserialize config **once** at the entry point, pass typed objects downstream

#### API Contract

- [ ] `RunBacktestRequest` updated to separate strategy and execution parameter sections
- [ ] `BacktestsController` maps request into typed `StrategyConfig` + `ExecutionConfig`
- [ ] Angular `BacktestService` updated to send the new request shape
- [ ] Existing saved backtest results in DB are not affected (historical JSON preserved as-is)
- [ ] Utility method to convert legacy `GridStrategyConfig` JSON → typed `StrategyConfig` + `ExecutionConfig` (for loading old backtest runs)

### Non-Functional Requirements

- [ ] All existing tests pass (updated to new signatures)
- [ ] Identical backtest results for the same parameters before and after
- [ ] Zero `JsonSerializer.Deserialize<GridStrategyConfig>` calls in engine or controller

---

## Technical Considerations

### Files Changed

| File | Change |
|------|--------|
| `IStrategyEngine.cs` | Signature: `string` → `StrategyConfig` |
| `IGridController.cs` | Signature: `string` → `StrategyConfig` |
| `GridStrategyEngine.cs` | Remove JSON deserialization |
| `GridController.cs` | Remove JSON deserialization |
| `StrategyScheduler.cs` | Constructor: `string` → `StrategyConfig` |
| `BacktestRunner.cs` | Deserialize once; pass typed objects |
| `BacktestConfig.cs` | `StrategyConfigJson` → `Strategy` + `Execution` |
| `BacktestsController.cs` | Request mapping |
| `RunBacktestRequest` / API models | Separate sections |
| `SimulatedExecutionEngine` | Receives `ExecutionConfig` |
| All test files | Updated to new signatures |
| Angular `BacktestService` + models | Request shape |

### What Does NOT Change

`IRiskEngine`, `IPositionManager`, `IExecutionEngine`, `IMarketContextBuilder`, `TradingSignal`, candle entities, saved backtest DB records.

---

## Out of Scope

- Full canonical schema (F1)
- New validation pipeline (F1)
- Strategy Builder UI (F2)
- New strategy types or condition evaluation
- Trend/bias/risk schema sections (added in F1)

---

## Acceptance Criteria

- [ ] **Given** `IStrategyEngine`, **When** inspected, **Then** `EvaluateAsync` accepts `StrategyConfig` not `string`
- [ ] **Given** `IGridController`, **When** inspected, **Then** `ProcessAsync` accepts `StrategyConfig` not `string`
- [ ] **Given** `GridStrategyEngine` and `GridController` source, **When** inspected, **Then** no `JsonSerializer.Deserialize` calls exist
- [ ] **Given** `BacktestConfig`, **When** inspected, **Then** separate `Strategy` and `Execution` properties exist
- [ ] **Given** a backtest with `{ gridLevels: 10, gridSpacing: 0.5, ... }`, **When** run before and after refactoring, **Then** results are identical
- [ ] **Given** all existing tests, **When** run, **Then** all pass

### Release Notes Information

- **Heading**: Strategy Configuration Architecture Refactoring
- **Release note type**: Breaking Change
- **Release Note Summary**: Backtest API request now separates strategy and execution parameters.
- **Breaking Change**: Yes — API contract change for `POST /api/backtests`
