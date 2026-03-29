# TA Indicators on Market Data Chart — Tier 3 (MACD, Stochastic RSI)

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-29T18:00:00Z

## User Story

As a **trader**, I want to **see MACD and Stochastic RSI on the Market Data price chart** so that **I can confirm momentum shifts and identify oversold pullback entries with higher precision than RSI alone**.

### Business Value

MACD provides momentum direction and crossover signals that complement the existing EMA trend filters. Stochastic RSI adds granularity to overbought/oversold detection — where standard RSI (Tier 1) may sit in a range for extended periods, Stochastic RSI oscillates more frequently, making it better suited for timing pullback entries in the grid strategy.

---

## Requirements

### Functional Requirements

- [ ] **MACD calculation** — Add `CalculateMacd(candles, fastPeriod, slowPeriod, signalPeriod)` to the shared `IndicatorCalculator` service. Returns MACD line, signal line, and histogram per timestamp
- [ ] **Stochastic RSI calculation** — Add `CalculateStochasticRsi(candles, rsiPeriod, stochPeriod, kSmoothing, dSmoothing)` to the shared `IndicatorCalculator` service. Returns %K and %D lines per timestamp
- [ ] **MACD sub-pane** — Display MACD as a sub-pane below the chart with: MACD line, signal line (two line series), and histogram (colored bars — green when MACD > signal, red when MACD < signal)
- [ ] **Stochastic RSI sub-pane** — Display as a sub-pane with %K and %D lines. Include horizontal reference lines at 20 (oversold) and 80 (overbought). Scale fixed 0–100
- [ ] **Backend endpoint integration** — Extend `GET /api/market/indicators` to accept `macd` and `stochrsi` in the `indicators` parameter, with optional period overrides (`macdFast`, `macdSlow`, `macdSignal`, `stochRsiPeriod`, `stochPeriod`, `stochK`, `stochD`)
- [ ] **Indicator toolbar integration** — Add MACD and Stochastic RSI toggle buttons to the existing indicator toolbar
- [ ] **User-configurable periods** — All MACD and Stochastic RSI periods adjustable. Defaults: MACD (12, 26, 9), Stoch RSI (14, 14, 3, 3)
- [ ] **IndicatorDto extension** — Add `MacdLine`, `MacdSignal`, `MacdHistogram`, `StochRsiK`, `StochRsiD` fields to `IndicatorDto`

### Non-Functional Requirements

- [ ] MACD + Stochastic RSI calculation for 5000 candles completes in < 100ms
- [ ] No regression in existing Tier 1 and Tier 2 indicators
- [ ] Chart remains responsive with all indicators from all tiers enabled simultaneously

---

## User Flow

### Happy Path

1. User has the Market Data chart open with candles loaded
2. User clicks "MACD" toggle → MACD sub-pane appears below chart with MACD line, signal line, and histogram
3. User clicks "Stoch RSI" toggle → Stochastic RSI sub-pane appears with %K/%D lines and 20/80 reference lines
4. User adjusts MACD fast period from 12 to 8 → chart re-fetches and redraws
5. User observes MACD histogram turning positive while Stoch RSI crosses above 20 — momentum confirmation for grid entry

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| Insufficient data for MACD warmup (< 26 candles for default) | MACD starts rendering after warmup; no error |
| Stoch RSI requires RSI first, so double warmup (< 28 candles) | Lines start after full warmup; no error |
| Too many sub-panes open (all indicators active) | Chart panes resize proportionally; minimum height per pane |

---

## Technical Considerations

### Modified/New Components

#### Backend

| Component | Layer | Action |
|-----------|-------|--------|
| `IndicatorCalculator` | Application/MarketData/Services | **Modified** — Add `CalculateMacd()` and `CalculateStochasticRsi()` |
| `IndicatorDto` | Application/MarketData/Models | **Modified** — Add MACD and Stoch RSI fields |
| `GetIndicatorsQuery` | Application/MarketData/Queries | **Modified** — Handle `macd` and `stochrsi` indicator types |

#### Frontend

| Component | Action |
|-----------|--------|
| `IndicatorService` | **Modified** — Pass new indicator types in API calls |
| `IndicatorToolbarComponent` | **Modified** — Add MACD and Stoch RSI toggle buttons + config inputs |
| `PriceChartComponent` | **Modified** — Add MACD sub-pane (2 lines + histogram) and Stoch RSI sub-pane (2 lines + ref lines) |

### Algorithms

**MACD:**
- MACD line = EMA(close, fastPeriod) - EMA(close, slowPeriod)
- Signal line = EMA(MACD line, signalPeriod)
- Histogram = MACD line - Signal line

**Stochastic RSI:**
1. Calculate RSI(close, rsiPeriod) for each candle
2. Apply Stochastic formula to RSI values over stochPeriod:
   - %K_raw = (RSI - lowest RSI over stochPeriod) / (highest RSI over stochPeriod - lowest RSI over stochPeriod) × 100
3. %K = SMA(%K_raw, kSmoothing)
4. %D = SMA(%K, dSmoothing)

---

## Dependencies

- PBI: TA Indicators — Core (shared IndicatorCalculator, indicators endpoint, toolbar, chart pane infrastructure)
- PBI: TA Indicators — Tier 2 is NOT a dependency (Tier 3 can be built independently of Tier 2)

## Out of Scope

- MACD divergence detection (algorithmic, not visual)
- Stochastic RSI alerts/notifications when crossing 20/80
- Custom indicator scripting/formulas

---

## Acceptance Criteria

- [ ] MACD calculates correctly against known test datasets (verified by unit tests comparing to reference values)
- [ ] Stochastic RSI calculates correctly against known test datasets (verified by unit tests)
- [ ] `GET /api/market/indicators?indicators=macd,stochrsi` returns correct values
- [ ] MACD sub-pane renders with MACD line, signal line, and colored histogram
- [ ] Stochastic RSI sub-pane renders with %K/%D lines and 20/80 reference lines
- [ ] Toolbar toggles show/hide MACD and Stochastic RSI independently
- [ ] User can adjust all period parameters for both indicators
- [ ] All existing Tier 1 (and Tier 2 if completed) indicators continue to work correctly
- [ ] All unit tests pass with >80% code coverage for new calculations
- [ ] Chart remains usable with 6+ sub-panes active (all indicators from all tiers)

### Release Notes Information

- **Heading**: MACD & Stochastic RSI Indicators
- **Release note type**: Feature
- **Release Note Summary**: The Market Data chart now supports MACD (with histogram) and Stochastic RSI indicators for momentum confirmation and pullback entry timing.
- **Release Notes Audience**: Product
- **Breaking Change**: No
