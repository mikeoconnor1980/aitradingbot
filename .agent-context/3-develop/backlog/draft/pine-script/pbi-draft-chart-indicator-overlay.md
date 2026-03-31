# Chart Indicator Overlay Rendering

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-31T12:00:00Z
**Epic:** Pine Script Indicator Integration (Option F)

## User Story

As a **trader**, I want to **see my custom indicators rendered on the price chart as overlays and sub-panes** so that **I can visually analyze indicator values alongside price action, just like in TradingView**.

### Business Value

Traders paste Pine Script because they want to *see* their indicators on a chart. This PBI delivers the visual payoff — computed indicator values (PBI 3) rendered as line series, histograms, and reference lines on the existing `lightweight-charts` price chart. Without this, indicator values are just numbers in an API response.

### Relationship to Existing TA Indicator PBIs

The existing `pbi-draft-ta-indicators-core.md` proposes adding EMA/RSI/ATR/Volume to the chart with an indicator toolbar. This PBI **extends that foundation** to support dynamically-configured custom indicators. If the Core TA PBI is implemented first, this PBI reuses its sub-pane infrastructure and toolbar pattern. If not, this PBI establishes the chart overlay framework that the Core TA PBI can also use.

### Library Integration

The `lightweight-charts-indicators` npm package (MIT, 446 indicators) provides pre-built indicator rendering that plugs directly into `lightweight-charts`. This PBI leverages that library where possible, falling back to manual series creation for custom configurations.

---

## Requirements

### Functional Requirements

- [ ] **Overlay indicators on price chart** — Indicators with `isOverlay: true` (e.g., EMA, Bollinger Bands) render as `LineSeries` on the main candlestick pane. Colors and styles from extracted `plot()` configuration
- [ ] **Sub-pane indicators** — Indicators with `isOverlay: false` (e.g., RSI, MACD) render in separate panes below the candlestick chart using `lightweight-charts` pane API. Each sub-pane has its own y-axis scale
- [ ] **Reference lines** — Extracted `hline()` calls render as horizontal dashed lines in the appropriate pane (e.g., RSI 30/70 overbought/oversold lines)
- [ ] **Plot styles** — Support extracted plot styles: `line` (default), `histogram`, `stepline`, `area`. Map Pine Script `plot.style_*` constants to `lightweight-charts` series types
- [ ] **Plot colors** — Apply extracted colors from `plot()` calls. Map Pine Script color constants (`color.blue`, `color.red`, `#hex`) to CSS colors. Default to a color palette if no color specified
- [ ] **Indicator toggle** — Each custom indicator can be toggled on/off from the chart UI. When toggled off, series and sub-panes are removed. When toggled on, data is fetched (or re-used from cache) and rendered
- [ ] **Indicator selector** — Dropdown or panel showing the user's saved custom indicators (from PBI 2). User can select which indicators to display on the current chart
- [ ] **Multi-indicator display** — Multiple custom indicators can be active simultaneously. Overlay indicators share the price pane. Sub-pane indicators each get their own pane
- [ ] **Time alignment** — Indicator series timestamps align with candle timestamps. When chart scrolls/zooms, indicator series follow
- [ ] **History loading** — When user scrolls back to load more candles, indicator computation is requested for the extended range and appended to existing series
- [ ] **Signal markers** — Extracted `plotshape()` conditions render as markers (triangles, circles) above/below bars where the condition is true. Colors and shapes from extracted configuration
- [ ] **Legend/tooltip** — On crosshair hover, show indicator values at the hovered timestamp in an info panel or tooltip. Display indicator name and value for each active indicator

### Non-Functional Requirements

- [ ] Chart remains responsive (60fps scroll/zoom) with 3 overlay indicators and 2 sub-pane indicators active on 5000 candles
- [ ] Adding/removing indicators does not cause full chart re-render — only add/remove specific series
- [ ] Sub-pane height is proportional (e.g., price chart 60%, sub-panes share remaining 40%)
- [ ] Works on standard desktop screen resolutions (1920x1080 minimum)

---

## User Flow

### Adding a Custom Indicator to the Chart

1. User has the Market Data chart open with BTC-PERP 15m candles
2. User clicks "Indicators" button in the chart toolbar
3. Indicator selector panel opens showing saved custom indicators (from PBI 2):
   - "My EMA Crossover" (overlay, valid ✓)
   - "RSI Divergence Setup" (sub-pane, valid ✓)
   - "Broken Script" (invalid ✗, greyed out)
4. User clicks "My EMA Crossover"
5. System calls `/api/indicators/{id}/compute` for the current chart range
6. Two EMA lines appear overlaid on the candlestick chart (blue EMA 21, red EMA 50)
7. Alert condition markers appear as green triangles below bars where crossover + RSI condition is met
8. Crosshair tooltip now shows "EMA 21: 84,250" and "EMA 50: 84,180" on hover
9. User also enables "RSI Divergence Setup"
10. A new sub-pane appears below the chart with RSI line and 30/70 reference lines

### Removing an Indicator

1. User clicks "Indicators" in toolbar
2. Active indicators show checkmarks
3. User unchecks "My EMA Crossover"
4. EMA lines and signal markers are removed from chart
5. Sub-pane indicators remain unaffected

### Scrolling Back

1. User scrolls left to load older candles
2. Chart fires the existing `onVisibleLogicalRangeChanged` event
3. System requests both candles and indicator computation for the extended range
4. Indicator series extend seamlessly to the left

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| Indicator computation fails | Toast: "Failed to compute [indicator name]". Chart remains with other indicators |
| Indicator has partial extraction (warnings) | Render what was extracted; show small warning icon on indicator label |
| No candle data for selected asset/timeframe | Indicator series empty; no error (consistent with empty chart) |
| Too many sub-panes (e.g., 5+) | Allow it, but panes become very small. Consider a scroll or collapse mechanism |

---

## Technical Considerations

### Bounded Context

**Context:** Frontend — `trading-ui/src/app/features/market-data/` components.

### New/Modified Components

#### Frontend

| Component | Action |
|-----------|--------|
| `PriceChartComponent` | **Modified** — Add methods to create/remove overlay `LineSeries`, sub-panes with `createPane()`, reference lines, signal markers |
| `IndicatorOverlayManager` | **New** — Service/class that manages active indicator series on the chart. Handles adding/removing series, sub-panes, and markers. Encapsulates `lightweight-charts` API calls |
| `IndicatorSelectorComponent` | **New** — Panel/dropdown for selecting which saved indicators to display. Shows indicator name, overlay/pane type, validity status |
| `IndicatorLegendComponent` | **New** — Shows active indicator values at crosshair position |
| `IndicatorComputeService` | **Modified** (from PBI 3) — Called when indicators are toggled on or chart range extends |
| `MarketDataComponent` | **Modified** — Wire indicator selector and overlay manager into the page |
| Color mapping utility | **New** — Map Pine Script color constants to CSS hex values |

#### Backend

No new backend components — this PBI consumes the `/api/indicators/{id}/compute` endpoint from PBI 3.

### lightweight-charts Integration Patterns

**Overlay series (EMA, Bollinger Bands):**
```typescript
const emaSeries = chart.addSeries(LineSeries, {
  color: '#2196F3',
  lineWidth: 2,
  priceScaleId: 'right', // share scale with candles
});
emaSeries.setData(computedEmaData);
```

**Sub-pane (RSI, MACD):**
```typescript
const rsiPane = chart.addPane({ height: 150 });
const rsiSeries = rsiPane.addSeries(LineSeries, {
  color: '#9C27B0',
  priceScaleId: 'rsi',
});
rsiSeries.setData(computedRsiData);
// Reference lines
const overbought = rsiPane.addSeries(LineSeries, {
  color: '#ff000040', lineStyle: LineStyle.Dashed,
});
```

**Signal markers:**
```typescript
const markers: SeriesMarker<Time>[] = alertPoints.map(p => ({
  time: p.time as UTCTimestamp,
  position: 'belowBar',
  shape: 'arrowUp',
  color: '#4CAF50',
  text: 'Buy',
}));
createSeriesMarkers(candleSeries, markers);
```

### Pine Script Color Mapping

| Pine Constant | Hex |
|--------------|-----|
| `color.blue` | `#2196F3` |
| `color.red` | `#F44336` |
| `color.green` | `#4CAF50` |
| `color.orange` | `#FF9800` |
| `color.purple` | `#9C27B0` |
| `color.yellow` | `#FFEB3B` |
| `color.white` | `#FFFFFF` |
| `color.black` | `#000000` |
| `color.gray` | `#9E9E9E` |
| `#RRGGBB` | Pass through |

---

## Dependencies

- **PBI: Custom Indicator CRUD** — provides list of saved indicators
- **PBI: Indicator Computation Pipeline** — provides computed series data via `/api/indicators/{id}/compute`
- **PBI: TA Indicators Core** (optional) — if implemented first, provides sub-pane infrastructure and toolbar pattern to reuse
- **Existing:** `PriceChartComponent`, `lightweight-charts` v5.x

---

## Out of Scope

- `lightweight-charts-indicators` npm package integration (evaluate as optimization — start with manual series creation from computed data)
- Client-side indicator computation via `oakscriptjs` (future optimization)
- Indicator drawing tools (`line.new`, `label.new`)
- Indicator comparison (overlay two different assets' indicators)
- Responsive/mobile layout for sub-panes
- Persisting which indicators are active per chart (localStorage — nice-to-have)

---

## Acceptance Criteria

- [ ] Overlay indicators (EMA) render as colored line series on the candlestick chart
- [ ] Sub-pane indicators (RSI) render in a separate pane below the chart with correct y-axis scale
- [ ] Reference lines (RSI 30/70) render as dashed horizontal lines in the sub-pane
- [ ] Signal markers (`plotshape` conditions) render as arrows/shapes above/below bars
- [ ] Indicator selector shows saved custom indicators with validity status
- [ ] Invalid indicators are greyed out in the selector
- [ ] Toggling an indicator on fetches computation data and renders it
- [ ] Toggling an indicator off removes its series/pane cleanly
- [ ] Multiple overlay indicators can be active simultaneously on the price pane
- [ ] Multiple sub-pane indicators each get their own pane
- [ ] Crosshair hover shows indicator values in tooltip/legend
- [ ] Scrolling back to load more candles also loads indicator data for the extended range
- [ ] Chart scroll/zoom remains smooth (60fps) with 3+ active indicators on 5000 candles
- [ ] Pine Script colors from `plot()` are correctly mapped and applied
- [ ] Plot styles (line, histogram) are correctly rendered

### Release Notes Information

- **Heading**: Custom Indicators on Price Chart
- **Release note type**: Feature
- **Release Note Summary**: Custom indicators imported from Pine Script now render directly on the Market Data price chart. Overlay indicators appear on the candlestick chart, oscillators display in sub-panes, and signal conditions show as chart markers — bringing a TradingView-like experience to your trading dashboard.
- **Release Notes Audience**: Product
- **Breaking Change**: No
