# PBI Specification: F5 — Indicator Infrastructure & Condition Evaluator (RSI)

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-04-02
**PRD:** [02-strategy-input-pipeline.md](../../../prd-draft/02-strategy-input-pipeline.md)
**Reference:** [strategy-builder-ui-detailed.md](../../1-discover/prd/strategy-builder-ui-detailed.md)
**Implementation Phase:** 1b (First Signal Condition)
**Risk Level:** High
**Depends On:** F1 (Schema), F2 (Builder UI)

---

## Summary

Build the indicator calculation infrastructure and condition evaluator engine, delivering the **first signal-mode condition end-to-end: RSI**. This PBI introduces:
1. Dynamic indicator calculation in `IMarketContextBuilder` (configurable periods instead of hardcoded)
2. The `IConditionEvaluator` orchestrator with handler pattern
3. `RsiConditionHandler` — the first concrete handler
4. `IStrategyEngine` routing: grid mode → `GridStrategyEngine`, signal mode → condition evaluator

After this PBI, a strategy with `strategyMode = "signal"` and an RSI entry condition can be evaluated by the engine.

### User Story

> As a **trader**, I want the **engine to evaluate RSI conditions against market data** so that **the system only enters trades when RSI meets my defined threshold**.

### Business Value

This is the foundational investment in composable strategy evaluation. The handler pattern, indicator infrastructure, and evaluator orchestrator are built once here. Every subsequent condition type (EMA, MACD, Bollinger, etc.) becomes an incremental add — one handler + one UI card — with no architecture changes.

---

## Problem Statement

`GridStrategyEngine.EvaluateAsync` always returns `SetupDetected = true` when higher-TF candles exist. There is no mechanism to evaluate composable conditions. `IMarketContextBuilder` (via `BacktestMarketContextBuilder`) computes hardcoded EMA 9/21/55 and RSI 14 — periods are not configurable from the strategy config. The existing `IndicatorSnapshot` model has fixed properties only. There is no handler pattern or evaluator orchestrator.

### Design Decisions (from refinement)

1. **Evolve `IndicatorSnapshot`** — rename to `IndicatorContext`, keep existing properties as computed shorthands, add dictionary-backed dynamic lookups (`GetRsi(period)`, `GetEma(period)`, etc.). No new model alongside.
2. **Cross detection included** — `cross_above`/`cross_below` operators are in scope. `IndicatorContext` holds current + previous candle indicator values.
3. **Signal direction** — when signal-mode evaluation returns `SetupDetected = true`, the signal direction is derived from the strategy's `Direction` config (Long/Short). No new enum.
4. **CompositeStrategyEngine** — new class that delegates to `GridStrategyEngine` (grid mode) or `ConditionEvaluator` (signal mode). `GridStrategyEngine` is untouched.
5. **Fail on unknown condition types** — unknown `EntryConditionType` values cause evaluation failure (config is invalid), not a silent skip.
6. **Per-condition results on StrategyEvaluation** — `ConditionResults` list surfaced on `StrategyEvaluation` for logging/debugging.
7. **Build() signature change** — `IMarketContextBuilder.Build()` gains a `requiredIndicators` parameter (extracted from config) so the builder knows which indicators to compute.

---

## Requirements

### Functional Requirements

#### Indicator Infrastructure

- [ ] Rename `IndicatorSnapshot` → `IndicatorContext`; keep existing properties (`EmaFast`, `EmaSlow`, `EmaTrend`, `Rsi`, `Atr`) as computed shorthands backed by the dynamic dictionary
- [ ] `IndicatorContext` holds dictionary-backed dynamic lookups: `GetRsi(int period) → decimal?`, `GetEma(int period) → decimal?`, `GetMacd(int fast, int slow, int signal) → decimal?`
- [ ] `IndicatorContext` holds current + previous candle indicator values to support `cross_above` / `cross_below` detection (e.g., `GetPreviousRsi(int period) → decimal?`)
- [ ] `IMarketContextBuilder.Build()` extended with a `requiredIndicators` parameter: `Build(triggerCandle, latestOneHourCandle, latestFourHourCandle, requiredIndicators)` — computes only the requested indicators
- [ ] Indicator extraction utility: given a `StrategyConfig`, returns the set of required indicators (`RSI(14)`, `EMA(50)`, etc.) from trend filter + entry conditions
- [ ] `MarketContext.Indicators` property type changes from `IndicatorSnapshot` to `IndicatorContext`
- [ ] Calculation reuses existing EMA/RSI logic (from `BacktestMarketContextBuilder` or shared `IndicatorCalculator`)
- [ ] Grid-mode callers continue to work — `Build()` overload without `requiredIndicators` computes the existing defaults

#### Condition Evaluator Engine

- [ ] `IConditionEvaluator` interface: `Evaluate(StrategyConfig, MarketContext) → ConditionEvaluationResult`
- [ ] `ConditionEvaluationResult`: `SetupDetected` (bool), `TrendFilterPassed` (bool?), `ConditionResults` (per-condition pass/fail with reason), `OverallReason` (string)
- [ ] `IConditionHandler` interface: `EntryConditionType ConditionType { get; }`, `ConditionResult Evaluate(EntryConditionConfig, IndicatorContext, MarketContext)`
- [ ] Handlers resolved by `condition.Type` (the `EntryConditionType` enum) — **unknown/unregistered types cause evaluation failure** (config is invalid)
- [ ] Entry logic: `all` = all enabled conditions pass; `any` = at least one passes; no enabled conditions = `SetupDetected = false`
- [ ] Disabled conditions excluded from evaluation

#### RSI Handler

- [ ] `RsiConditionHandler` evaluates: RSI(period) compared to value using operator (lt, lte, gt, gte, cross_above, cross_below)
- [ ] Returns `ConditionResult` with `passed` bool and human-readable `reason` (e.g., "RSI(14) = 35 < 40 — condition met")

#### Strategy Engine Routing

- [ ] New `CompositeStrategyEngine` implements `IStrategyEngine` and delegates by `strategyMode`:
  - `Grid` → `GridStrategyEngine` (existing, unchanged)
  - `Signal` → `ConditionEvaluator.Evaluate()` → maps to `StrategyEvaluation`
- [ ] `GridStrategyEngine` remains unchanged — no modifications to the grid evaluation path
- [ ] DI registration changes: `IStrategyEngine` → `CompositeStrategyEngine` (which receives `GridStrategyEngine` and `IConditionEvaluator`)

#### StrategyEvaluation Enhancement

- [ ] `StrategyEvaluation` extended with `ConditionResults` (list of per-condition pass/fail with reason) for logging/debugging
- [ ] `StrategyEvaluation` extended with `Direction` (from strategy config) — populated in signal mode so downstream consumers know the intended trade direction

### Non-Functional Requirements

- [ ] Evaluation of 3 conditions completes in < 5ms (excluding indicator calculation)
- [ ] Indicator calculation per candle (precomputed series) < 1ms
- [ ] Adding a new condition type requires only: new handler class + register — no evaluator changes

---

## Technical Considerations

### Architecture

```
StrategyConfig
  │
  ├─ strategyMode = grid → GridStrategyEngine (existing, unchanged)
  │
  └─ strategyMode = signal → ConditionEvaluator
                                ├── (TrendFilterEvaluator — F7, not this PBI)
                                └── RsiConditionHandler
                                    → ConditionEvaluationResult
                                    → StrategyEvaluation
```

### Handler Registration

```csharp
// DI registration
services.AddSingleton<IConditionHandler, RsiConditionHandler>();
// Future:
// services.AddSingleton<IConditionHandler, PriceVsEmaConditionHandler>();
// services.AddSingleton<IConditionHandler, MacdConditionHandler>();

// Evaluator resolves handlers by type
var handler = _handlers.FirstOrDefault(h => h.ConditionType == condition.Type);
```

### New Components

| Component | Layer | Action |
|-----------|-------|--------|
| `IndicatorContext` | Application/Trading/Models | **Renamed** from `IndicatorSnapshot` — adds dynamic lookups + previous values |
| `IConditionEvaluator` | Application/StrategyAuthoring/Services | **New** |
| `ConditionEvaluator` | Application/StrategyAuthoring/Services | **New** |
| `IConditionHandler` | Application/StrategyAuthoring/Services | **New** |
| `RsiConditionHandler` | Application/StrategyAuthoring/Handlers | **New** |
| `ConditionEvaluationResult` | Application/StrategyAuthoring/Models | **New** |
| `ConditionResult` | Application/StrategyAuthoring/Models | **New** |
| `IndicatorExtractor` | Application/StrategyAuthoring/Services | **New** — extracts required indicators from config |
| `CompositeStrategyEngine` | Application/Trading/Services | **New** — delegates by `StrategyMode` |
| `IMarketContextBuilder` | Application/Abstractions/Services | **Modified** — `Build()` gains `requiredIndicators` param |
| `MarketContext` | Application/Trading/Models | **Modified** — `Indicators` type changes to `IndicatorContext` |
| `StrategyEvaluation` | Application/Trading/Models | **Modified** — adds `ConditionResults` + `Direction` |
| `GridStrategyEngine` | Application/Trading/Services | **Unchanged** — no modifications |

---

## Out of Scope

- Trend filter evaluation (F7)
- EMA / MACD / Bollinger condition handlers (F7/F8)
- UI for RSI condition (F6)
- Grid execution changes (grid path unchanged)

---

## Acceptance Criteria

### RSI Condition Evaluation

- [ ] **Given** `strategyMode = "Signal"` with RSI condition `operator = "lt", value = 40`, **When** RSI(14) = 35, **Then** `SetupDetected = true` and `Direction` matches the strategy's configured direction
- [ ] **Given** RSI condition `operator = "lt", value = 40`, **When** RSI(14) = 45, **Then** `SetupDetected = false`
- [ ] **Given** RSI condition `operator = "cross_above", value = 30`, **When** previous RSI = 28 and current RSI = 32, **Then** `SetupDetected = true`
- [ ] **Given** RSI condition `operator = "cross_below", value = 70`, **When** previous RSI = 72 and current RSI = 68, **Then** `SetupDetected = true`

### Entry Logic

- [ ] **Given** `entryLogic = "All"` with RSI (passes) and a second condition (passes), **When** evaluated, **Then** `SetupDetected = true`
- [ ] **Given** `entryLogic = "All"` with RSI (passes) and a second condition (fails), **When** evaluated, **Then** `SetupDetected = false`
- [ ] **Given** `entryLogic = "Any"` with RSI (fails) and no other enabled conditions, **Then** `SetupDetected = false`
- [ ] **Given** `entryLogic = "Any"` with RSI (fails) and a second condition (passes), **Then** `SetupDetected = true`
- [ ] **Given** one disabled RSI condition and entry logic `All`, **Then** `SetupDetected = false` (no enabled conditions)

### Unknown/Invalid Condition Types

- [ ] **Given** an entry condition with `EntryConditionType.Unknown` or unregistered type, **When** evaluated, **Then** evaluation fails (config is invalid)

### Strategy Engine Routing

- [ ] **Given** `strategyMode = "Grid"`, **When** evaluated via `CompositeStrategyEngine`, **Then** `GridStrategyEngine` logic runs (unchanged)
- [ ] **Given** `strategyMode = "Signal"`, **When** evaluated via `CompositeStrategyEngine`, **Then** `ConditionEvaluator` is invoked

### Indicator Infrastructure

- [ ] **Given** a strategy requiring RSI(14), **When** `IMarketContextBuilder.Build()` is called with the required indicators, **Then** `IndicatorContext.GetRsi(14)` returns a computed value
- [ ] **Given** a `cross_above` operator, **When** `Build()` is called, **Then** `IndicatorContext.GetPreviousRsi(14)` returns the previous candle's RSI value
- [ ] **Given** `Build()` called without `requiredIndicators` (grid-mode default), **Then** existing default indicators are computed (backward compatible)

### Regression Safety

- [ ] **Given** existing grid strategy tests, **When** run, **Then** all pass (grid path untouched)

### Observability

- [ ] **Given** any signal-mode evaluation, **When** result is returned, **Then** `StrategyEvaluation.ConditionResults` contains per-condition pass/fail with human-readable reasons

### Release Notes Information

- **Heading**: Condition Evaluator Engine & RSI Support
- **Release Note Summary**: New composable strategy evaluation engine with RSI condition support. Foundation for adding future condition types.
- **Breaking Change**: No (additive — grid path preserved)
