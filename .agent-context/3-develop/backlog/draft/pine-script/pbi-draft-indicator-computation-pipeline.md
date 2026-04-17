# Indicator Computation Pipeline

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-31T12:00:00Z
**Epic:** Pine Script Indicator Integration (Option F)

## User Story

As a **trader**, I want **my saved custom indicators to be computed against real candle data** so that **the indicator values are available for both chart display and automated strategy evaluation on each candle close**.

### Business Value

The extractor (PBI 1) identifies *what* to compute. The CRUD (PBI 2) persists it. This PBI bridges the gap by actually running the extracted indicator functions against candle data — producing the time-series values that the chart (PBI 4) and strategy engine (PBI 6) consume. Without this, custom indicators are just metadata with no computed output.

### Relationship to Existing TA Indicator PBIs

The existing `pbi-draft-ta-indicators-core.md` proposes a shared `IndicatorCalculator` service extracted from `BacktestMarketContextBuilder`. This PBI **builds on that foundation** — once `IndicatorCalculator` exists, custom indicators use the same calculation functions. If the Core TA PBI is not yet implemented, this PBI includes the minimal extraction needed.

---

## Requirements

### Functional Requirements

- [ ] **Frontend indicator computation via oakscriptjs** — Compute extracted `ta.*` functions in the browser using `oakscriptjs` (MIT). Input: OHLCV candle array + extracted indicator config. Output: named time-series arrays `{ time, value }[]` per indicator variable
- [ ] **Backend indicator computation via IndicatorCalculator** — Compute indicators server-side in C# using shared `IndicatorCalculator` (from TA Core PBI or newly built). Used by `MarketContextBuilder` for strategy evaluation on candle close
- [ ] **Computation endpoint** — `GET /api/indicators/{id}/compute?asset={asset}&timeframe={tf}&startTime={long}&endTime={long}` — computes indicator values for the requested candle range. Returns named series arrays
- [ ] **Input override application** — Apply user's `InputOverrides` to extracted indicator parameters before computation (e.g., if user overrode EMA period from 21 to 25, compute EMA(25))
- [ ] **Multi-indicator batch computation** — Single request can compute all indicators within a custom indicator definition (e.g., one script with 3 EMA + 1 RSI returns all 4 series)
- [ ] **Warmup handling** — Indicators requiring lookback (e.g., EMA 200 needs 200+ candles) automatically request sufficient warmup candles. Output series starts after warmup period
- [ ] **MarketContext extension** — Extend `MarketContext` with `Dictionary<string, CustomIndicatorResult> CustomIndicators` keyed by indicator ID. Each `CustomIndicatorResult` contains computed values and alert evaluations at the current candle
- [ ] **IndicatorSnapshot coexistence** — Custom indicator computation does not replace existing `IndicatorSnapshot` (EMA fast/slow/trend, RSI, ATR). Both coexist — built-in indicators for the grid strategy, custom indicators for user-defined strategies
- [ ] **Candle data source** — Reuse existing candle repository/API for historical data. For live candles, reuse existing `SignalRService` real-time feed

### Non-Functional Requirements

- [ ] Frontend computation of 4 indicators over 5000 candles completes in < 500ms (browser)
- [ ] Backend computation of 4 indicators over 5000 candles completes in < 200ms (server)
- [ ] No regression in existing `BacktestMarketContextBuilder` indicator calculations
- [ ] Memory-efficient: use streaming/windowed computation where possible rather than materializing full series in memory

---

## User Flow

### Chart Computation (Frontend)

1. User opens Market Data chart with an asset selected
2. User enables a saved custom indicator (see PBI 4 for toggle UI)
3. Frontend calls `GET /api/indicators/{id}/compute?asset=BTC-PERP&timeframe=15m&startTime=...&endTime=...`
4. API returns computed series: `{ "ema21": [{time, value}, ...], "rsi14": [{time, value}, ...] }`
5. Chart renders the series (PBI 4)
6. When user scrolls back to load more candles, frontend requests indicator computation for the extended range

### Strategy Evaluation (Backend)

1. `CandleClock` fires on candle close
2. `StrategyScheduler` fans out per-user
3. For each user with a custom indicator strategy, `MarketContextBuilder`:
   - Fetches candles as usual
   - Computes built-in `IndicatorSnapshot` as usual
   - Loads user's active custom indicators from DB
   - Computes each custom indicator's `ta.*` functions using `IndicatorCalculator`
   - Evaluates alert conditions (crossovers, thresholds) at the current candle
   - Attaches results to `MarketContext.CustomIndicators`
4. `StrategyEngine` / `CustomIndicatorStrategy` (PBI 6) reads custom indicator results

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| Indicator references unsupported `ta.*` function | Skip that indicator, log warning, return partial results |
| Insufficient candle data for warmup | Return empty series with warning `"Insufficient data: need 200 candles, have 50"` |
| Indicator computation throws (division by zero, etc.) | Catch, log, return `null` for that indicator's series. Do not fail the entire computation |
| Custom indicator entity not found | Return 404 |
| Asset/timeframe has no candle data | Return empty series array |

---

## Technical Considerations

### Bounded Context

**Context:** Application layer — `TradePilot.Application/PineScript/Services` for computation orchestration, `TradePilot.Application/Trading` for `MarketContext` extension.

### New/Modified Components

#### Backend (C#)

| Component | Layer | Action |
|-----------|-------|--------|
| `CustomIndicatorComputeService` | Application/PineScript/Services | **New** — Orchestrates indicator computation: loads config, fetches candles, calls IndicatorCalculator, evaluates alert conditions |
| `CustomIndicatorResult` | Application/PineScript/Models | **New** — `{ IndicatorValues: Dictionary<string, decimal>, AlertsTriggered: string[] }` — values at a single point in time |
| `ComputedIndicatorSeries` | Application/PineScript/Models | **New** — `{ VariableName, Values: List<TimestampedValue> }` — full series for chart rendering |
| `ComputeIndicatorQuery` | Application/PineScript/Queries | **New** — MediatR query for the compute endpoint |
| `IndicatorController` | Api/Controllers | **Modified** — Add compute endpoint |
| `MarketContext` | Application/Trading/Models | **Modified** — Add `CustomIndicators` dictionary |
| `MarketContextBuilder` / `BacktestMarketContextBuilder` | Application/Trading/Services | **Modified** — Compute custom indicators for users with active indicator strategies |
| `IndicatorCalculator` | Application/MarketData/Services | **New or existing** — Shared calculation functions (if TA Core PBI not yet done, create minimal version with functions needed by extracted indicators) |

#### Frontend (TypeScript)

| Component | Action |
|-----------|--------|
| `IndicatorComputeService` | **New** — Angular service calling `/api/indicators/{id}/compute` |
| `OakscriptBridge` | **New** — Wrapper around `oakscriptjs` for client-side computation (optional optimization to avoid API calls for chart-only rendering) |

### API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/indicators/{id}/compute?asset={asset}&timeframe={tf}&startTime={long}&endTime={long}` | Compute indicator series for the requested range |

### Response Shape

```json
{
  "indicatorId": "guid",
  "asset": "BTC-PERP",
  "timeframe": "15m",
  "series": {
    "ema21": [
      { "time": 1711728000, "value": 84250.50 },
      { "time": 1711728900, "value": 84270.25 }
    ],
    "ema50": [
      { "time": 1711728000, "value": 84180.75 },
      { "time": 1711728900, "value": 84195.10 }
    ],
    "rsi14": [
      { "time": 1711728000, "value": 62.4 },
      { "time": 1711728900, "value": 58.1 }
    ]
  },
  "meta": {
    "warmupCandles": 50,
    "computedCandles": 500,
    "computeTimeMs": 45
  }
}
```

### Computation Strategy

**Frontend (chart rendering):** Call the `/compute` endpoint which runs server-side C# IndicatorCalculator. This keeps the frontend simple and avoids shipping `oakscriptjs` initially. If latency is an issue for interactive scrolling, add client-side `oakscriptjs` computation as an optimization later.

**Backend (strategy evaluation):** Run `IndicatorCalculator` in-process within `MarketContextBuilder`. No sidecar needed. Same code path as the `/compute` endpoint but for a single candle point rather than a range.

### Supported Indicator Functions (C# IndicatorCalculator)

Must support at minimum the same functions as the Pine Script extractor's supported `ta.*` registry:
- SMA, EMA, WMA, VWMA
- RSI, MACD (line + signal + histogram)
- ATR, Bollinger Bands (upper + middle + lower)
- Stochastic (%K, %D)
- CCI, MFI, OBV
- Crossover/crossunder detection (boolean at each point)
- Highest, Lowest, Change, ROC

If the TA Core PBI (`pbi-draft-ta-indicators-core.md`) is already implemented, reuse its `IndicatorCalculator`. If not, implement the functions needed by common Pine Script patterns.

---

## Dependencies

- **PBI: Pine Script Pattern Extractor** — provides `ExtractionResult` with indicator configs
- **PBI: Custom Indicator CRUD** — provides persisted `CustomIndicator` entities
- **PBI: TA Indicators Core** (optional) — if implemented first, provides shared `IndicatorCalculator`
- **Existing:** Candle repository for historical data, `SignalRService` for live candles

---

## Out of Scope

- Chart rendering of computed series (see PBI: Chart Indicator Overlay Rendering)
- Alert-to-signal conversion (see PBI: Alert Condition → Signal Mapping)
- Client-side `oakscriptjs` computation (future optimization — start with server-side only)
- Caching computed indicator values in database
- Incremental computation (compute only new candles since last computation)
- Multi-timeframe indicator computation (`request.security()`)

---

## Acceptance Criteria

- [ ] `GET /api/indicators/{id}/compute` returns correct indicator values for EMA, RSI, ATR against known candle data (verified against reference calculations)
- [ ] Multi-indicator computation returns all series from a single custom indicator definition
- [ ] Input overrides are correctly applied (e.g., EMA period override changes computation)
- [ ] Warmup candles are automatically fetched — output series starts after warmup
- [ ] `MarketContext.CustomIndicators` is populated during strategy evaluation pipeline
- [ ] Existing `IndicatorSnapshot` (built-in EMA/RSI/ATR) continues to work unchanged
- [ ] Computation of 4 indicators over 5000 candles completes in < 200ms server-side
- [ ] Errors in one indicator do not fail the entire computation — partial results returned
- [ ] Alert condition evaluation (crossovers, thresholds) correctly identifies triggered alerts at each candle
- [ ] Unit tests verify each `IndicatorCalculator` function against known input/output pairs
- [ ] Integration test verifies full pipeline: load custom indicator → fetch candles → compute → return series

### Release Notes Information

- **Heading**: Custom Indicator Computation
- **Release note type**: Feature
- **Release Note Summary**: Custom indicators extracted from Pine Script are now computed against real market data. Indicator values are available for chart rendering and automated strategy evaluation.
- **Release Notes Audience**: Technical
- **Breaking Change**: No (`MarketContext` gains a new optional property; existing consumers unaffected)
