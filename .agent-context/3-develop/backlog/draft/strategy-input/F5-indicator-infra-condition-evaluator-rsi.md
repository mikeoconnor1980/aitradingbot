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

`GridStrategyEngine.EvaluateAsync` always returns `SetupDetected = true` when higher-TF candles exist. There is no mechanism to evaluate composable conditions. `IMarketContextBuilder` computes hardcoded EMA 20/50/200 and RSI 14 — periods are not configurable from the strategy config. There is no handler pattern or evaluator orchestrator.

---

## Requirements

### Functional Requirements

#### Indicator Infrastructure

- [ ] `IndicatorContext` model holds computed indicator values keyed by type and period: `GetRsi(int period)`, `GetEma(int period)`, `GetMacd(int fast, int slow, int signal)` — returns nullable decimals
- [ ] `IMarketContextBuilder` extended: `Build(triggerCandle, ...)` accepts a list of required indicators (extracted from the strategy config) and computes them
- [ ] Indicator extraction utility: given a `StrategyConfig`, returns the set of required indicators (`RSI(14)`, `EMA(50)`, etc.) from trend filter + entry conditions
- [ ] `MarketContext` extended to include `IndicatorContext`
- [ ] Calculation reuses existing EMA/RSI logic (from `BacktestMarketContextBuilder` or shared `IndicatorCalculator`)
- [ ] Cross detection (`cross_above`, `cross_below`) requires current + previous candle indicator values; `IndicatorContext` holds both

#### Condition Evaluator Engine

- [ ] `IConditionEvaluator` interface: `Evaluate(StrategyConfig, MarketContext) → ConditionEvaluationResult`
- [ ] `ConditionEvaluationResult`: `SetupDetected` (bool), `TrendFilterPassed` (bool?), `ConditionResults` (per-condition pass/fail with reason), `OverallReason` (string)
- [ ] `IConditionHandler` interface: `string ConditionType { get; }`, `ConditionResult Evaluate(EntryConditionConfig, IndicatorContext, MarketContext)`
- [ ] Handlers resolved by `condition.type` — unknown types produce a warning, not failure (forward compatibility)
- [ ] Entry logic: `all` = all enabled conditions pass; `any` = at least one passes; no enabled conditions = `SetupDetected = false`
- [ ] Disabled conditions excluded from evaluation

#### RSI Handler

- [ ] `RsiConditionHandler` evaluates: RSI(period) compared to value using operator (lt, lte, gt, gte, cross_above, cross_below)
- [ ] Returns `ConditionResult` with `passed` bool and human-readable `reason` (e.g., "RSI(14) = 35 < 40 — condition met")

#### Strategy Engine Routing

- [ ] `IStrategyEngine.EvaluateAsync` routes by `strategyMode`:
  - `grid` → existing `GridStrategyEngine` logic (unchanged)
  - `signal` → `IConditionEvaluator.Evaluate()` → maps to `StrategyEvaluation`
- [ ] This can be implemented as a `CompositeStrategyEngine` that delegates, or by modifying `GridStrategyEngine` to branch

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
| `IndicatorContext` | Application/Trading/Models | **New** |
| `IConditionEvaluator` | Application/StrategyAuthoring/Services | **New** |
| `ConditionEvaluator` | Application/StrategyAuthoring/Services | **New** |
| `IConditionHandler` | Application/StrategyAuthoring/Services | **New** |
| `RsiConditionHandler` | Application/StrategyAuthoring/Handlers | **New** |
| `ConditionEvaluationResult` | Application/StrategyAuthoring/Models | **New** |
| `ConditionResult` | Application/StrategyAuthoring/Models | **New** |
| `IndicatorExtractor` | Application/StrategyAuthoring/Services | **New** — extracts required indicators from config |
| `IMarketContextBuilder` | Application/Abstractions/Services | **Modified** — accept required indicators |
| `MarketContext` | Application/Trading/Models | **Modified** — include `IndicatorContext` |
| `IStrategyEngine` impl | Application/Trading/Services | **Modified** — route by `strategyMode` |

---

## Out of Scope

- Trend filter evaluation (F7)
- EMA / MACD / Bollinger condition handlers (F7/F8)
- UI for RSI condition (F6)
- Grid execution changes (grid path unchanged)

---

## Acceptance Criteria

- [ ] **Given** `strategyMode = "signal"` with RSI condition `operator = "lt", value = 40`, **When** RSI(14) = 35, **Then** `SetupDetected = true`
- [ ] **Given** RSI condition `operator = "lt", value = 40`, **When** RSI(14) = 45, **Then** `SetupDetected = false`
- [ ] **Given** RSI condition `operator = "cross_above", value = 30`, **When** previous RSI = 28 and current RSI = 32, **Then** `SetupDetected = true`
- [ ] **Given** `entryLogic = "all"` with RSI (passes) and an unknown type (warning), **When** evaluated, **Then** `SetupDetected = true` (unknown types don't block)
- [ ] **Given** `entryLogic = "any"` with RSI (fails) and no other conditions, **Then** `SetupDetected = false`
- [ ] **Given** one disabled RSI condition and entry logic `all`, **Then** `SetupDetected = false` (no enabled conditions)
- [ ] **Given** `strategyMode = "grid"`, **When** evaluated, **Then** existing `GridStrategyEngine` logic runs (unchanged)
- [ ] **Given** a strategy requiring RSI(14), **When** `IMarketContextBuilder.Build()` is called, **Then** `IndicatorContext.GetRsi(14)` returns a value
- [ ] **Given** existing grid strategy tests, **When** run, **Then** all pass (grid path untouched)

### Release Notes Information

- **Heading**: Condition Evaluator Engine & RSI Support
- **Release Note Summary**: New composable strategy evaluation engine with RSI condition support. Foundation for adding future condition types.
- **Breaking Change**: No (additive — grid path preserved)
