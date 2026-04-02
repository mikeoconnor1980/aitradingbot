# PBI Specification: F7 — Trend Filter + EMA Condition Handler + UI

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-04-02
**PRD:** [02-strategy-input-pipeline.md](../../../prd-draft/02-strategy-input-pipeline.md)
**Reference:** [strategy-builder-ui-detailed.md](../../1-discover/prd/strategy-builder-ui-detailed.md)
**Implementation Phase:** 1c (Incremental Conditions)
**Risk Level:** Medium
**Depends On:** F5 (Condition Evaluator + Indicator Infra), F6 (Signal Mode UI)

---

## Summary

Deliver three capabilities in one vertical slice:
1. **Trend filter evaluator** — evaluates `ema_cross` trend filter before entry conditions
2. **Price vs EMA condition handler** — new handler in the evaluator
3. **UI updates** — trend filter card enabled in signal mode; "Add Price vs EMA" button and condition item; "EMA Pullback" template now selectable

After this PBI, the "EMA Pullback" template works end-to-end: trend filter (EMA 50 > 200) + price near EMA 50 + RSI < 40 → evaluates → executes.

### User Story

> As a **trader**, I want to **use EMA trend filters and price-vs-EMA conditions** so that **I can build EMA pullback strategies that the engine evaluates correctly**.

---

## Requirements

### Functional Requirements

#### Trend Filter Evaluator

- [ ] `TrendFilterEvaluator` handles `ema_cross` type: evaluates whether EMA(fast) is gt/lt/cross_above/cross_below EMA(slow)
- [ ] If `trendFilter.enabled = false`, filter passes automatically
- [ ] If `appliesTo` doesn't match strategy direction, filter is skipped (passes)
- [ ] Indicators (EMA fast/slow periods) extracted and computed via `IndicatorContext`
- [ ] Filter runs **before** entry conditions — if filter fails, conditions are skipped and `SetupDetected = false`

#### Price vs EMA Condition Handler

- [ ] `PriceVsEmaConditionHandler` for `price_vs_ema` conditions
- [ ] Operators: `near` (within distance), `above`, `below`, `cross_above`, `cross_below`, `touch`
- [ ] Distance types: `percent`, `atr_multiple`, `absolute` (for `near` operator)
- [ ] Requires `IndicatorContext.GetEma(period)` — already in indicator infra from F5

#### UI Updates

- [ ] Trend filter card **enabled** in signal mode (was greyed out)
- [ ] "Add Price vs EMA" button in entry conditions card
- [ ] Price vs EMA condition item (from UI spec): EMA Period, Operator, Distance Type, Distance Value
- [ ] "EMA Pullback" template now selectable: pre-populates trend filter (EMA 50 > 200) + price near EMA 50 + RSI < 40 + TP 3% + SL swing low
- [ ] Preview text updates for EMA: "when the 50 EMA is above the 200 EMA, price is within 0.25% of the 50 EMA"

---

## Acceptance Criteria

- [ ] **Given** `trendFilter.type = "ema_cross"`, operator = `"gt"`, **When** EMA(50) > EMA(200), **Then** trend filter passes
- [ ] **Given** `trendFilter.enabled = false`, **When** evaluated, **Then** filter skipped (passes)
- [ ] **Given** `price_vs_ema` condition with `operator = "near"`, `distanceType = "percent"`, `distanceValue = 0.25`, **When** price within 0.25% of EMA(50), **Then** condition passes
- [ ] **Given** "EMA Pullback" template, **When** selected, **Then** form pre-populates with complete strategy and all conditions evaluate correctly
- [ ] **Given** trend filter fails, **When** evaluated, **Then** entry conditions skipped and `SetupDetected = false`
- [ ] **Given** trend filter card in signal mode, **When** displayed, **Then** all fields are editable

### Release Notes Information

- **Heading**: EMA Trend Filter & Price vs EMA Conditions
- **Release Note Summary**: EMA-based trend filtering and price-vs-EMA entry conditions now available. EMA Pullback template fully functional.
- **Breaking Change**: No
