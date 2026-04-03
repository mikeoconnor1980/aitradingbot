# PBI Specification: F8 — MACD Condition Handler + UI Card

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-04-02
**Last Updated:** 2026-04-03T16:37:58Z
**PRD:** [02-strategy-input-pipeline.md](../../../prd-draft/02-strategy-input-pipeline.md)
**Reference:** [strategy-builder-ui-detailed.md](../../1-discover/prd/strategy-builder-ui-detailed.md)
**Implementation Phase:** 1c (Incremental Conditions)
**Risk Level:** Low
**Depends On:** F5 (Condition Evaluator + Indicator Infra), F6 (Signal Mode UI), F6.5 (Extract Indicator Calculators — MACD calculation), F7 (EMA Trend Filter Handler — establishes multi-condition-type UI pattern)

---

## Summary

Add MACD as a new entry condition type following the same `IConditionHandler` pattern as RSI (F5) and PriceVsEma (F7). Delivers a backend handler with 6 operators, a polymorphic UI card, and the "MACD Cross" template.

After this PBI the "MACD Cross" template works end-to-end. Combined with F7, all three Phase 1 templates are functional: Grid, EMA Pullback, MACD Cross.

### User Story

> As a **trader**, I want to **use MACD-based entry conditions** so that **I can build momentum crossover strategies evaluated by the engine**.

### Business Value

MACD is one of the most widely used momentum indicators. Adding it as an entry condition unlocks the "MACD Cross" template — the third and final Phase 1 template — completing the initial strategy-builder feature set and enabling traders to express momentum-based strategies without writing code.

---

## Requirements

### Functional Requirements

#### MACD Condition Handler (Backend)

- [ ] `MacdConditionHandler` implements `IConditionHandler` for `EntryConditionType.Macd`
- [ ] Operators: `cross_above_signal`, `cross_below_signal`, `above_zero`, `below_zero`, `histogram_rising`, `histogram_falling`
- [ ] Parameters via existing `MacdParams`: `FastPeriod` (default 12), `SlowPeriod` (default 26), `SignalPeriod` (default 9), `Operator`
- [ ] Reads MACD line, signal line, and histogram from `IndicatorContext` (current + previous values for cross/rising/falling detection)
- [ ] Missing data handling: returns `ConditionResult { Passed = false }` with descriptive reason (fail closed), consistent with `RsiConditionHandler` pattern
- [ ] Unknown operator: returns `ConditionResult { Passed = false }` with reason describing the unrecognised operator
- [ ] DI registration as `IConditionHandler` in `Program.cs` alongside existing RSI and PriceVsEma handlers
- [ ] Maximum 1 MACD condition per strategy (enforced by `BusinessRuleValidator`)

#### UI Polymorphic Condition Support (Frontend)

- [ ] Refactor `EntryConditionConfig.params` from `RsiParams` to a discriminated union (`RsiParams | MacdConditionParams`) keyed on `type`
- [ ] `MacdConditionParams` interface: `fastPeriod: number`, `slowPeriod: number`, `signalPeriod: number`, `operator: MacdOperator`
- [ ] `MacdOperator` type: `"cross_above_signal" | "cross_below_signal" | "above_zero" | "below_zero" | "histogram_rising" | "histogram_falling"`
- [ ] `ConditionFactoryService.createMacdCondition()` method with defaults (12/26/9, `cross_above_signal`)
- [ ] `StrategyMapperService` maps MACD conditions to/from the API schema

#### UI Card

- [ ] "Add MACD" button in `EntryConditionsCardComponent` actions (signal mode), alongside existing "Add RSI"
- [ ] "Add MACD" button disabled when a MACD condition already exists (max 1 per strategy)
- [ ] New `MacdConditionItemComponent` rendered for `type === "macd"` conditions
- [ ] MACD item fields: Fast Period, Slow Period, Signal Period (number inputs), Operator (dropdown with 6 options)
- [ ] Validation — all periods > 0; fast ∈ [2, 50]; slow ∈ [5, 200]; signal ∈ [2, 50]; fast < slow
- [ ] Remove and duplicate buttons per item (duplicate respects max-1 rule)
- [ ] Preview text: e.g. "when MACD(12,26,9) crosses above signal line"
- [ ] Duplicate-blocking: exact-same type + params combination is prevented, consistent with RSI rules

#### Template

- [ ] "MACD Cross" template added to `STRATEGY_TEMPLATES` with `available: true`
- [ ] Template pre-populates: MACD `cross_above_signal` with defaults (12/26/9), strategyMode `signal`, TP 2% (fixed_percent), SL 1.5% (fixed_percent)
- [ ] Template loads correctly into form, validates, and produces correct schema JSON
- [ ] Selecting a different template clears any existing MACD conditions

### Non-Functional Requirements

- [ ] Handler unit tests cover all 6 operators, missing data paths, and unknown operator fallback
- [ ] UI component tests for MACD condition item rendering, validation, and max-1 enforcement
- [ ] No breaking changes to existing RSI condition behaviour or API contract

---

## User Flow

### Happy Path

1. User opens Strategy Builder and selects "MACD Cross" template (or selects "Custom Signal" and clicks "Add MACD")
2. MACD condition item appears with default values: Fast 12, Slow 26, Signal 9, Operator "crosses above signal line"
3. User adjusts parameters if desired (e.g. Fast 8, Slow 21, Signal 5)
4. User configures exit rules (TP/SL)
5. User clicks Save — form validates, maps to API schema, and submits

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| `fastPeriod >= slowPeriod` | Inline validation error: "Fast period must be less than slow period" |
| Period out of bounds (e.g. fast = 0, slow = 300) | Inline validation error with allowed range |
| User clicks "Add MACD" when one exists | Button is disabled; tooltip explains max 1 |
| Insufficient candle history at runtime | Handler returns `Passed = false` with reason "MACD(12,26,9) data not available" |
| Unknown operator string in handler | Handler returns `Passed = false` with reason describing the unknown operator |

---

## Acceptance Criteria

- [ ] **Given** `macd` condition with `operator = "cross_above_signal"`, **When** MACD line crosses above signal line on current candle (previous: line < signal; current: line >= signal), **Then** condition passes with descriptive reason
- [ ] **Given** `macd` condition with `operator = "cross_below_signal"`, **When** MACD line crosses below signal line on current candle, **Then** condition passes
- [ ] **Given** `macd` condition with `operator = "above_zero"`, **When** MACD line > 0, **Then** condition passes
- [ ] **Given** `macd` condition with `operator = "below_zero"`, **When** MACD line < 0, **Then** condition passes
- [ ] **Given** `macd` condition with `operator = "histogram_rising"`, **When** current histogram > previous histogram, **Then** condition passes
- [ ] **Given** `macd` condition with `operator = "histogram_falling"`, **When** current histogram < previous histogram, **Then** condition passes
- [ ] **Given** MACD data not available in `IndicatorContext`, **When** handler evaluates, **Then** condition returns `Passed = false` with reason (fail closed)
- [ ] **Given** "MACD Cross" template, **When** selected, **Then** form pre-populates MACD condition (12/26/9 cross_above_signal) + exits (TP 2%, SL 1.5%)
- [ ] **Given** MACD condition in UI, **When** `fastPeriod >= slowPeriod`, **Then** validation error shown inline
- [ ] **Given** MACD condition in UI, **When** `fastPeriod` set to 0 or 51, **Then** validation error shown with bounds [2, 50]
- [ ] **Given** one MACD condition already exists, **When** user clicks "Add MACD", **Then** button is disabled
- [ ] **Given** both RSI and MACD conditions with entry logic = "all", **When** evaluated, **Then** both must pass for entry signal
- [ ] **Given** no MACD handler registered, **When** `macd` condition evaluated by `ConditionEvaluator`, **Then** existing unknown-handler behaviour applies (safety net)
- [ ] **Given** existing strategy with RSI conditions, **When** user saves after adding MACD, **Then** RSI conditions are unchanged

### Release Notes Information

- **Heading**: MACD Entry Conditions
- **Release note type**: Feature
- **Release Note Summary**: MACD crossover entry conditions are now available in the strategy builder. Six operators supported: signal line crosses, zero-line tests, and histogram momentum. The new "MACD Cross" template enables one-click setup of momentum crossover strategies.
- **Release Notes Audience**: Product
- **Breaking Change**: No

---

## Out of Scope

- MACD divergence detection (bullish/bearish divergence patterns)
- Multi-timeframe MACD (comparing MACD across different candle intervals)
- MACD histogram as a standalone indicator chart/visualization
- Custom MACD calculation methods (e.g. weighted, double-smoothed)
- More than 1 MACD condition per strategy (can revisit if users request)
