# PBI Specification: F8 — MACD Condition Handler + UI Card

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-04-02
**PRD:** [02-strategy-input-pipeline.md](../../../prd-draft/02-strategy-input-pipeline.md)
**Reference:** [strategy-builder-ui-detailed.md](../../1-discover/prd/strategy-builder-ui-detailed.md)
**Implementation Phase:** 1c (Incremental Conditions)
**Risk Level:** Low
**Depends On:** F5 (Condition Evaluator + Indicator Infra), F6 (Signal Mode UI), F6.5 (Extract Indicator Calculators — MACD calculation)

---

## Summary

Add MACD as a new entry condition type following the same pattern as RSI (F5/F6). Delivers a handler, a UI card and enables the "MACD Cross" template.

After this PBI the "MACD Cross" template works end-to-end. Combined with F7, all three Phase 1 templates are functional: Grid, EMA Pullback, MACD Cross.

### User Story

> As a **trader**, I want to **use MACD-based entry conditions** so that **I can build momentum crossover strategies evaluated by the engine**.

---

## Requirements

### Functional Requirements

#### MACD Condition Handler

- [ ] `MacdConditionHandler` implements `IConditionHandler` for `macd_cross` conditions
- [ ] Operators: `cross_above_signal`, `cross_below_signal`, `above_zero`, `below_zero`, `histogram_rising`, `histogram_falling`
- [ ] Parameters: `fastPeriod` (default 12), `slowPeriod` (default 26), `signalPeriod` (default 9)
- [ ] Computes MACD line, signal line, histogram via `IndicatorContext`
- [ ] `IndicatorContext.GetMacd(fast, slow, signal)` added to indicator infrastructure
- [ ] DI registration alongside existing RSI handler

#### UI Card

- [ ] "Add MACD Cross" button in entry conditions card (signal mode)
- [ ] MACD condition item fields: Fast Period, Slow Period, Signal Period, Operator (dropdown)
- [ ] Validation: all periods > 0; fast < slow; signal < slow
- [ ] Remove button per item
- [ ] Preview text: "when MACD(12,26,9) crosses above signal line"

#### Template

- [ ] "MACD Cross" template selectable: pre-populates MACD cross_above_signal with defaults (12/26/9) + TP 2% + SL 1.5%
- [ ] Template loads correctly into form, validates, produces correct schema JSON

---

## Acceptance Criteria

- [ ] **Given** `macd_cross` condition with `operator = "cross_above_signal"`, **When** MACD line crosses above signal line on current candle, **Then** condition passes
- [ ] **Given** "MACD Cross" template, **When** selected, **Then** form pre-populates MACD condition + exits and evaluates correctly
- [ ] **Given** MACD condition in UI, **When** `fastPeriod >= slowPeriod`, **Then** validation error shown
- [ ] **Given** no MACD handler registered, **When** `macd_cross` condition evaluated, **Then** `UnknownConditionTypeException` thrown (safety net)

### Release Notes Information

- **Heading**: MACD Entry Conditions
- **Release Note Summary**: MACD crossover entry conditions now available. MACD Cross template fully functional.
- **Breaking Change**: No
