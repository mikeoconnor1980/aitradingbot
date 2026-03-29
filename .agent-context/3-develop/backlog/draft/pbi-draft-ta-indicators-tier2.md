# TA Indicators on Market Data Chart — Tier 2 (Bollinger Bands, VWAP)

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-29T18:00:00Z

## User Story

As a **trader**, I want to **see Bollinger Bands and VWAP on the Market Data price chart** so that **I can identify mean-reversion opportunities and intraday bias — both critical for pullback grid entry timing**.

### Business Value

Bollinger Bands highlight when price is stretched relative to its moving average, directly supporting the grid strategy's pullback detection. VWAP is already referenced in the strategy documentation for 1H bias confirmation but is not yet calculated or displayed. Adding these indicators closes the gap between what the strategy conceptually uses and what the trader can see.

---

## Requirements

### Functional Requirements

- [ ] **Bollinger Bands calculation** — Add `CalculateBollingerBands(candles, period, stdDevMultiplier)` to the shared `IndicatorCalculator` service. Returns upper band, middle band (SMA), and lower band per timestamp
- [ ] **VWAP calculation** — Add `CalculateVwap(candles)` to the shared `IndicatorCalculator` service. VWAP resets each trading day (00:00 UTC)
- [ ] **Bollinger Bands overlay** — Display as 3 line series on the candlestick chart: upper band, middle SMA, lower band. Optional: shaded fill between upper and lower bands
- [ ] **VWAP overlay** — Display as a single line series on the candlestick chart with distinct color/style (e.g., dashed line)
- [ ] **Backend endpoint integration** — Extend `GET /api/market/indicators` to accept `bollinger` and `vwap` in the `indicators` parameter, with optional `bollingerPeriod` (default: 20) and `bollingerStdDev` (default: 2.0)
- [ ] **Indicator toolbar integration** — Add Bollinger Bands and VWAP toggle buttons to the existing indicator toolbar
- [ ] **User-configurable periods** — Bollinger period and standard deviation multiplier adjustable from the toolbar. VWAP has no configurable period (daily reset)
- [ ] **IndicatorDto extension** — Add `BollingerUpper`, `BollingerMiddle`, `BollingerLower`, `Vwap` fields to `IndicatorDto`

### Non-Functional Requirements

- [ ] Bollinger Bands + VWAP calculation for 5000 candles completes in < 100ms
- [ ] No regression in existing EMA/RSI/ATR/Volume indicators
- [ ] Chart remains responsive with Bollinger Bands + VWAP + existing Tier 1 indicators enabled simultaneously

---

## User Flow

### Happy Path

1. User has the Market Data chart open with candles loaded
2. User clicks "BB" toggle in the toolbar → Bollinger Bands (upper, middle, lower) appear overlaid on candlesticks
3. User clicks "VWAP" toggle → VWAP line appears on the chart
4. User adjusts Bollinger period from 20 to 30 → chart re-fetches and redraws bands
5. User observes price touching lower Bollinger Band near VWAP — potential pullback entry zone

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| Insufficient data for Bollinger warmup (< 20 candles) | Bands start rendering after warmup; no error |
| VWAP on daily timeframe (no intraday reset) | VWAP renders as cumulative for the day; still useful |
| Invalid std dev value (e.g., 0 or negative) | Frontend validation; minimum = 0.5 |

---

## Technical Considerations

### Modified/New Components

#### Backend

| Component | Layer | Action |
|-----------|-------|--------|
| `IndicatorCalculator` | Application/MarketData/Services | **Modified** — Add `CalculateBollingerBands()` and `CalculateVwap()` |
| `IndicatorDto` | Application/MarketData/Models | **Modified** — Add Bollinger and VWAP fields |
| `GetIndicatorsQuery` | Application/MarketData/Queries | **Modified** — Handle `bollinger` and `vwap` indicator types |

#### Frontend

| Component | Action |
|-----------|--------|
| `IndicatorService` | **Modified** — Pass new indicator types in API calls |
| `IndicatorToolbarComponent` | **Modified** — Add BB and VWAP toggle buttons + BB config inputs |
| `PriceChartComponent` | **Modified** — Add Bollinger Bands line series (3 lines + optional fill) and VWAP line series |

### Algorithms

**Bollinger Bands:**
- Middle = SMA(close, period)
- Upper = Middle + (stdDev × multiplier)
- Lower = Middle - (stdDev × multiplier)

**VWAP:**
- VWAP = Σ(typical price × volume) / Σ(volume), resetting at 00:00 UTC daily
- Typical price = (high + low + close) / 3

---

## Dependencies

- PBI: TA Indicators — Core (EMA, RSI, ATR, Volume) — shared IndicatorCalculator, indicators endpoint, toolbar, and chart pane infrastructure must exist first

## Out of Scope

- MACD, Stochastic RSI (see PBI: TA Indicators — Tier 3)
- Bollinger Band squeeze detection alerts
- VWAP bands (standard deviation bands around VWAP)

---

## Acceptance Criteria

- [ ] Bollinger Bands calculate correctly against known test datasets (verified by unit tests)
- [ ] VWAP calculates correctly with daily reset at 00:00 UTC (verified by unit tests)
- [ ] `GET /api/market/indicators?indicators=bollinger,vwap` returns correct values
- [ ] Bollinger upper/middle/lower bands render as 3 distinct lines on the candlestick chart
- [ ] VWAP renders as a dashed line overlay on the candlestick chart
- [ ] Toolbar toggles show/hide Bollinger Bands and VWAP independently
- [ ] User can adjust Bollinger period and std dev multiplier
- [ ] All existing Tier 1 indicators continue to work correctly alongside new indicators
- [ ] All unit tests pass with >80% code coverage for new calculations

### Release Notes Information

- **Heading**: Bollinger Bands & VWAP Indicators
- **Release note type**: Feature
- **Release Note Summary**: The Market Data chart now supports Bollinger Bands (configurable period and standard deviation) and VWAP overlays for mean-reversion analysis and intraday bias confirmation.
- **Release Notes Audience**: Product
- **Breaking Change**: No
