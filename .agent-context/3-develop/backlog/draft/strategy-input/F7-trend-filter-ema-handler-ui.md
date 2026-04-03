# PBI Specification: F7 — Trend Filter + EMA Condition Handler + UI

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-04-02
**Last Updated:** 2026-04-03T10:00:00Z
**PRD:** [02-strategy-input-pipeline.md](../../../prd-draft/02-strategy-input-pipeline.md)
**Reference:** [strategy-builder-ui-detailed.md](../../1-discover/prd/strategy-builder-ui-detailed.md)
**Implementation Phase:** 1c (Incremental Conditions)
**Risk Level:** Medium
**Depends On:** F5 (Condition Evaluator + Indicator Infra), F6 (Signal Mode UI)

---

## Summary

Deliver three capabilities in one vertical slice:
1. **Trend filter evaluator** — evaluates `ema_cross`, `sma_cross`, and `price_above_ema` trend filter types before entry conditions
2. **Price vs EMA condition handler** — new handler in the evaluator for `price_vs_ema` entry conditions
3. **UI updates** — trend filter card enabled in signal mode; "Add Price vs EMA" button and condition item; "EMA Pullback" template now selectable

After this PBI, the "EMA Pullback" template works end-to-end: trend filter (EMA 50 > 200) + price near EMA 50 + RSI < 40 → evaluates → executes.

### User Story

> As a **trader**, I want to **use EMA trend filters and price-vs-EMA conditions** so that **I can build EMA pullback strategies that the engine evaluates correctly**.

### Business Value

Trend filters are the most-requested pre-condition for entry signals. This PBI unlocks the core "pullback" family of strategies (EMA pullback, SMA pullback, price-above-MA confirmation) which represent the majority of retail quant setups. Once delivered, users can build and evaluate production-ready multi-condition strategies end-to-end.

---

## Problem Statement

Signal-mode strategies from F5/F6 can evaluate RSI conditions but have no trend filter gate. Without a trend filter, entries fire regardless of the prevailing trend direction, leading to counter-trend trades. Additionally, there is no `price_vs_ema` condition handler, so the "EMA Pullback" template — the primary strategy template — cannot be evaluated by the engine. The trend filter card in the UI is currently disabled/greyed out.

---

## Requirements

### Functional Requirements

#### Trend Filter Evaluator

- [ ] `TrendFilterEvaluator` handles three trend filter types:
  - **`ema_cross`**: compares EMA(fast) vs EMA(slow) — operators: `gt`, `lt`, `cross_above`, `cross_below`
  - **`sma_cross`**: compares SMA(fast) vs SMA(slow) — same operators as `ema_cross`
  - **`price_above_ema`**: compares candle close price vs EMA(period) — operators: `above`, `below`, `cross_above`, `cross_below`
- [ ] Cross detection (`cross_above`, `cross_below`) compares indicator values on the **previous confirmed candle** vs the **current confirmed candle** (e.g., EMA(fast) was below EMA(slow) on previous candle and above on current candle = `cross_above`)
- [ ] If `trendFilter.enabled = false`, filter passes automatically (skipped)
- [ ] If `appliesTo` doesn't match strategy direction, filter is skipped (auto-passes). Example: `direction=long`, `appliesTo=short` → filter skipped
- [ ] Indicators (EMA/SMA fast/slow periods) extracted and computed via `IndicatorContext`
- [ ] Filter runs **before** entry conditions — if filter fails, conditions are skipped entirely and `SetupDetected = false`
- [ ] If insufficient candle history exists to compute required indicators (e.g., fewer than 200 candles for EMA(200)), the trend filter **fails closed** — `SetupDetected = false`, no entry signal generated
- [ ] Unknown trend filter types produce a warning log, and the filter fails closed

#### Price vs EMA Condition Handler

- [ ] `PriceVsEmaConditionHandler` for `price_vs_ema` conditions
- [ ] Operators: `near` (within distance), `above`, `below`, `cross_above`, `cross_below`, `touch`
- [ ] **`touch` operator**: candle wick touched the EMA value — high ≥ EMA and low ≤ EMA (the EMA value falls within the candle's high/low range)
- [ ] **`cross_above` / `cross_below`**: previous candle close was on the opposite side of EMA compared to current candle close
- [ ] Distance types: `percent`, `atr_multiple`, `absolute` — only applicable when operator = `near`
- [ ] Requires `IndicatorContext.GetEma(period)` — already in indicator infra from F5
- [ ] If the referenced EMA period cannot be computed (insufficient data), the condition **fails** with a warning logged in `ConditionResult.Reason`
- [ ] Returns `ConditionResult` with `passed` bool and human-readable `reason` (e.g., "Price 42,150 is within 0.25% of EMA(50) = 42,050 — condition met")

#### UI Updates

- [ ] Trend filter card **enabled** in signal mode (was greyed out in F6)
- [ ] Trend filter type dropdown includes: `ema_cross`, `sma_cross`, `price_above_ema`
  - For `ema_cross` / `sma_cross`: show Fast Period + Slow Period + Operator (gt, lt, cross_above, cross_below) + Applies To
  - For `price_above_ema`: show Period + Operator (above, below, cross_above, cross_below) + Applies To (hide Fast/Slow Period fields)
- [ ] "Add Price vs EMA" button in entry conditions card
- [ ] Price vs EMA condition item fields: EMA Period (number, default 50), Operator (dropdown: near, above, below, cross_above, cross_below, touch; default near), Distance Type (dropdown: percent, atr_multiple, absolute; default percent), Distance Value (number, default 0.25)
- [ ] Distance Type and Distance Value fields are **only visible** when operator = `near`; hidden for all other operators
- [ ] "EMA Pullback" template now selectable: pre-populates direction=long, trend filter (ema_cross, EMA 50 > 200, appliesTo=long) + price near EMA 50 (0.25%, percent) + RSI(14) < 40 + TP 3% fixed_percent + SL swing_low lookback 5
- [ ] Preview text updates for trend filter: "when the 50 EMA is above the 200 EMA"
- [ ] Preview text updates for price_vs_ema: "price is within 0.25% of the 50 EMA"

### Non-Functional Requirements

- [ ] Trend filter evaluation (excluding indicator calculation) completes in < 2ms
- [ ] `price_vs_ema` condition evaluation completes in < 1ms (excluding indicator calculation)
- [ ] Adding a new trend filter type requires only: new case in `TrendFilterEvaluator` — no changes to the evaluator orchestrator or engine routing

---

## Acceptance Criteria

### Trend Filter — ema_cross

- [ ] **Given** `trendFilter.type = "ema_cross"`, operator = `"gt"`, fast = 50, slow = 200, **When** EMA(50) > EMA(200), **Then** trend filter passes
- [ ] **Given** `trendFilter.type = "ema_cross"`, operator = `"gt"`, fast = 50, slow = 200, **When** EMA(50) < EMA(200), **Then** trend filter fails and `SetupDetected = false`
- [ ] **Given** `trendFilter.type = "ema_cross"`, operator = `"cross_above"`, **When** EMA(fast) was below EMA(slow) on previous candle and above on current candle, **Then** trend filter passes

### Trend Filter — sma_cross

- [ ] **Given** `trendFilter.type = "sma_cross"`, operator = `"gt"`, fast = 20, slow = 50, **When** SMA(20) > SMA(50), **Then** trend filter passes

### Trend Filter — price_above_ema

- [ ] **Given** `trendFilter.type = "price_above_ema"`, operator = `"above"`, period = 200, **When** close price > EMA(200), **Then** trend filter passes
- [ ] **Given** `trendFilter.type = "price_above_ema"`, operator = `"cross_above"`, period = 50, **When** previous close < EMA(50) and current close > EMA(50), **Then** trend filter passes

### Trend Filter — Edge Cases

- [ ] **Given** `trendFilter.enabled = false`, **When** evaluated, **Then** filter auto-passes (skipped)
- [ ] **Given** `direction = "long"`, `appliesTo = "short"`, **When** evaluated, **Then** filter auto-passes (skipped)
- [ ] **Given** insufficient candle history for EMA(200), **When** trend filter evaluated, **Then** filter fails closed (`SetupDetected = false`)
- [ ] **Given** trend filter of unknown type, **When** evaluated, **Then** warning logged and filter fails closed

### Price vs EMA Condition

- [ ] **Given** `price_vs_ema` condition with `operator = "near"`, `distanceType = "percent"`, `distanceValue = 0.25`, EMA(50) = 42,050, **When** close = 42,150 (within 0.25%), **Then** condition passes
- [ ] **Given** `price_vs_ema` condition with `operator = "near"`, `distanceType = "percent"`, `distanceValue = 0.25`, EMA(50) = 42,050, **When** close = 43,000 (outside 0.25%), **Then** condition fails
- [ ] **Given** `price_vs_ema` condition with `operator = "touch"`, EMA(50) = 42,000, **When** candle high = 42,100, low = 41,900 (wick spans EMA), **Then** condition passes
- [ ] **Given** `price_vs_ema` condition with `operator = "touch"`, EMA(50) = 42,000, **When** candle high = 42,500, low = 42,100 (wick above EMA), **Then** condition fails
- [ ] **Given** `price_vs_ema` condition with `operator = "above"`, EMA(50) = 42,000, **When** close = 42,500, **Then** condition passes
- [ ] **Given** `price_vs_ema` condition with `operator = "cross_above"`, EMA(50) = 42,000, **When** previous close < 42,000 and current close > 42,000, **Then** condition passes
- [ ] **Given** insufficient data to compute EMA for a `price_vs_ema` condition, **When** evaluated, **Then** condition fails with warning in `ConditionResult.Reason`

### End-to-End — EMA Pullback Template

- [ ] **Given** "EMA Pullback" template selected, **When** form loads, **Then** pre-populates: direction=long, trend filter ema_cross (50 > 200, appliesTo=long), entry conditions: price near EMA 50 (0.25%, percent) + RSI(14) < 40, exit: TP 3% + SL swing_low lookback 5
- [ ] **Given** trend filter fails, **When** evaluated, **Then** entry conditions are skipped entirely and `SetupDetected = false`

### UI

- [ ] **Given** signal mode strategy, **When** trend filter card displayed, **Then** all fields are editable (not greyed out)
- [ ] **Given** trend filter type = `price_above_ema`, **When** displayed, **Then** shows Period field (not Fast/Slow Period fields)
- [ ] **Given** price_vs_ema condition with operator = `near`, **When** displayed, **Then** Distance Type and Distance Value fields visible
- [ ] **Given** price_vs_ema condition with operator = `above`, **When** displayed, **Then** Distance Type and Distance Value fields hidden
- [ ] **Given** strategy saved with trend filter and price_vs_ema condition, **When** canonical JSON produced, **Then** JSON includes `trendFilter` object and `entryConditions` array with `price_vs_ema` entry

### Release Notes Information

- **Heading**: EMA Trend Filter & Price vs EMA Conditions
- **Release note type**: Feature
- **Release Note Summary**: EMA-based trend filtering (ema_cross, sma_cross, price_above_ema) and price-vs-EMA entry conditions now available. The "EMA Pullback" template is fully functional end-to-end.
- **Release Notes Audience**: Product
- **Breaking Change**: No

---

## Technical Considerations

### Bounded Contexts

Evaluation logic lives in **TradingApp.Application** (condition evaluator, handlers, trend filter evaluator). Indicator calculation in **TradingApp.Domain** or shared infrastructure. UI components in **frontend/trading-ui** strategy builder module.

### Integration Events (if relevant)

None — this is internal evaluation logic with no event publishing.

### Jobs (if relevant)

None — trend filter and condition evaluation run synchronously within the existing strategy evaluation pipeline.

---

## Out of Scope

- `macd_trend` trend filter type (future PBI)
- MACD, Bollinger, ATR, Volume, Candle Pattern condition handlers (future PBIs)
- "RSI Reversal" template (requires no new conditions but separate PBI for template definition)
- Backtesting validation of trend filter (covered by backtesting PBI)
- Multi-timeframe trend filter evaluation (e.g., trend on 4h, entry on 15m)
