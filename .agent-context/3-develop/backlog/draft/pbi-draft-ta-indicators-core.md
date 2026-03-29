# TA Indicators on Market Data Chart — Core (EMA, RSI, ATR, Volume)

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-29T18:00:00Z

## User Story

As a **trader**, I want to **see core TA indicators (EMA, RSI, ATR) and volume bars on the Market Data price chart** so that **I can visually assess trend, momentum, volatility, and volume without switching to external charting tools**.

### Business Value

The strategy engine already computes EMA, RSI, and ATR internally, but these values are invisible to the user. Surfacing them on the chart builds trust in strategy decisions, aids manual analysis, and provides a foundation for future indicators. Volume bars fill a critical gap — OHLCV data is already available but volume is not displayed.

---

## Requirements

### Functional Requirements

- [ ] **Shared IndicatorCalculator service** — Extract EMA, RSI, ATR calculation logic from `BacktestMarketContextBuilder` into a shared `IndicatorCalculator` service in the Application layer, reusable by both the strategy engine and the API
- [ ] **Backend indicators endpoint** — New `GET /api/market/indicators` endpoint that accepts `asset`, `timeframe`, `indicators` (comma-separated: `ema`, `rsi`, `atr`, `volume`), optional `startTime`/`endTime`, and optional period overrides (`emaPeriods`, `rsiPeriod`, `atrPeriod`)
- [ ] **EMA overlay (line series)** — Display EMA lines on the candlestick chart as colored line series. Default periods: 9 (fast), 21 (slow), 55 (trend). Each line has a distinct color
- [ ] **RSI sub-pane** — Display RSI as a line chart in a separate pane below the candlestick chart. Default period: 14. Include horizontal reference lines at 30 (oversold) and 70 (overbought)
- [ ] **ATR sub-pane** — Display ATR as a line chart in a separate pane below the candlestick chart. Default period: 14
- [ ] **Volume sub-pane** — Display volume as a histogram in a separate pane below the candlestick chart. Color bars green (up candle) / red (down candle)
- [ ] **Indicator toolbar** — Row of toggle buttons/checkboxes above the chart allowing users to show/hide each indicator independently
- [ ] **User-configurable periods** — Users can adjust indicator periods (e.g., change EMA from 9 to 20) via the toolbar. Changes trigger a re-fetch from the backend
- [ ] **History alignment** — Indicator data aligns with candle timestamps; when user scrolls back to load more candles, indicators load for the same range
- [ ] **Strategy engine reuse** — `BacktestMarketContextBuilder` and `StrategyScheduler` refactored to use the shared `IndicatorCalculator` service instead of inline calculation

### Non-Functional Requirements

- [ ] Indicator calculation for 5000 candles completes in < 200ms on backend
- [ ] Chart remains responsive with all 4 indicators enabled simultaneously
- [ ] API responses are paginated/windowed consistent with existing candle endpoints
- [ ] No regression in strategy engine behaviour after refactoring to shared calculator

---

## User Flow

### Happy Path

1. User navigates to the Market Data page and selects an asset (e.g., BTC-PERP) and timeframe (e.g., 15m)
2. Candlestick chart loads as today (no indicators shown by default, or with EMA enabled by default — TBD)
3. User clicks "EMA" toggle in the toolbar → EMA 9/21/55 lines appear overlaid on the candlestick chart
4. User clicks "RSI" toggle → RSI sub-pane appears below the chart with the RSI line and 30/70 reference lines
5. User clicks "ATR" toggle → ATR sub-pane appears below the chart
6. User clicks "Volume" toggle → Volume histogram sub-pane appears below the chart
7. User adjusts EMA period from 9 to 12 → chart re-fetches and redraws the updated EMA line
8. User scrolls left to load more history → indicators extend to cover the newly loaded candles

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| Insufficient candle data for warmup (e.g., < 55 candles for EMA 55) | Indicator line starts after warmup period; no error shown |
| API timeout fetching indicators | Toast notification "Failed to load indicators"; chart remains with candles only |
| Invalid period value (e.g., 0 or negative) | Frontend validation prevents submission; minimum period = 2 |
| Backend calculation error | Return HTTP 500 with error detail; frontend shows toast and disables failed indicator |

---

## Technical Considerations

### Bounded Context

**Context:** MarketData (Application layer — indicators are a market data concern, not a trading concern)

### New/Modified Components

#### Backend

| Component | Layer | Action |
|-----------|-------|--------|
| `IndicatorCalculator` | Application/MarketData/Services | **New** — Static or injectable service with `CalculateEma()`, `CalculateRsi()`, `CalculateAtr()` methods |
| `IndicatorDto` | Application/MarketData/Models | **New** — `{ Timestamp, EmaFast?, EmaSlow?, EmaTrend?, Rsi?, Atr?, Volume? }` |
| `GetIndicatorsQuery` | Application/MarketData/Queries | **New** — MediatR query handler; fetches candles from repository, computes requested indicators |
| `MarketDataController` | Api/Controllers | **Modified** — Add `GET /api/market/indicators` endpoint |
| `BacktestMarketContextBuilder` | Application/Trading/Services | **Modified** — Delegate EMA/RSI/ATR calculation to `IndicatorCalculator` |

#### Frontend

| Component | Action |
|-----------|--------|
| `IndicatorService` | **New** — Angular service calling `/api/market/indicators` |
| `IndicatorToolbarComponent` | **New** — Toolbar with toggle buttons and period inputs |
| `PriceChartComponent` | **Modified** — Add EMA `LineSeries`, RSI/ATR/Volume sub-panes using lightweight-charts `createPane()` |
| `MarketDataComponent` | **Modified** — Integrate toolbar and wire indicator data to chart |

### API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/market/indicators?asset={asset}&timeframe={tf}&indicators=ema,rsi,atr,volume&emaPeriods=9,21,55&rsiPeriod=14&atrPeriod=14&startTime={long}&endTime={long}` | Returns array of `IndicatorDto` aligned to candle timestamps |

### Response Shape

```json
{
  "indicators": [
    {
      "timestamp": 1711728000000,
      "emaFast": 84250.50,
      "emaSlow": 84180.25,
      "emaTrend": 83900.10,
      "rsi": 62.4,
      "atr": 185.30,
      "volume": 1250.75
    }
  ],
  "meta": {
    "asset": "BTC-PERP",
    "timeframe": "15m",
    "emaPeriods": [9, 21, 55],
    "rsiPeriod": 14,
    "atrPeriod": 14,
    "count": 500
  }
}
```

### Data Source

- Indicators computed from candles in the local SQLite database (same source as historical chart data)
- For live/recent candles not yet in DB, fetch from Hyperliquid API and compute on-the-fly
- Volume is sourced directly from candle OHLCV data (no calculation needed)

---

## Out of Scope

- Bollinger Bands, VWAP (see PBI: TA Indicators — Tier 2)
- MACD, Stochastic RSI (see PBI: TA Indicators — Tier 3)
- Indicator persistence/caching in database (compute on-the-fly for now)
- Drawing tools or custom overlays
- Indicator alerts/notifications
- Multi-timeframe indicator display on a single chart

---

## Dependencies

- Historical candles available in local SQLite database (PBI: Historical Candles from Local Database on Price Chart)
- lightweight-charts v5.1.0 pane support for sub-charts

---

## Open Questions

- [ ] Should any indicators be enabled by default when chart loads (e.g., Volume always on)?
- [ ] Should indicator state (which indicators are active + periods) persist in localStorage?

---

## Acceptance Criteria

- [ ] `IndicatorCalculator` service computes EMA, RSI, ATR with results matching the existing `BacktestMarketContextBuilder` calculations (verified by unit tests with known datasets)
- [ ] `GET /api/market/indicators` returns correct indicator values for requested asset/timeframe/indicators
- [ ] EMA lines render on the candlestick chart with distinct colors per period
- [ ] RSI renders in a sub-pane with 30/70 reference lines
- [ ] ATR renders in a sub-pane below the chart
- [ ] Volume renders as a colored histogram in a sub-pane
- [ ] Toolbar toggles show/hide each indicator independently
- [ ] User can change indicator periods and chart updates accordingly
- [ ] Scrolling back to load more history also loads indicator data for the extended range
- [ ] Strategy engine (BacktestMarketContextBuilder, StrategyScheduler) continues to function correctly after refactoring to shared IndicatorCalculator
- [ ] All unit tests pass with >80% code coverage for IndicatorCalculator
- [ ] Chart remains responsive with all indicators enabled on 5000+ candles

### Release Notes Information

- **Heading**: TA Indicators on Market Data Chart
- **Release note type**: Feature
- **Release Note Summary**: The Market Data price chart now displays core technical analysis indicators — EMA (9/21/55), RSI (14), ATR (14), and Volume bars — calculated server-side and rendered as chart overlays and sub-panes. Indicator periods are user-configurable.
- **Release Notes Audience**: Product
- **Breaking Change**: No
