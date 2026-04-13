# Charting Library

The UI uses TradingView Lightweight Charts for both market-data visualisation and backtesting equity views. The implementation is chart-first and financial-domain-specific, but it is not the overlay-heavy grid visualiser described in earlier planning notes.

## Overview

Two chart components exist today:

| Component | Location | Purpose |
|---|---|---|
| `PriceChartComponent` | `frontend/trading-ui/src/app/features/market-data/price-chart/` | Live and historical candlestick chart with indicator panes and trade markers |
| `EquityChartComponent` | `frontend/trading-ui/src/app/features/backtesting/equity-chart/` | Backtest equity curve and comparison chart |

Shared implementation themes:

- TradingView Lightweight Charts only
- Centralised theme constants in the component file
- `ResizeObserver`-based resizing
- explicit `ngOnDestroy()` cleanup
- `createSeriesMarkers()` for lifecycle/trade annotations

## PriceChartComponent

`PriceChartComponent` is a three-pane candlestick chart, not a single rolling line chart.

### Inputs and Outputs

| API | Type | Purpose |
|---|---|---|
| `seedCandles` | `Candle[]` | Initial candle payload from REST endpoints |
| `selectedAsset` | `string` | Active market symbol; chart resets on change |
| `selectedTimeframe` | `string` | Active timeframe; supported by the chart's internal timeframe map |
| `fills` | `FillEvent[]` | Historical fills used to build grouped markers |
| `showTradeMarkers` | `boolean` | Turns fill markers and tooltip groups on or off |
| `loadMoreCandles` | `EventEmitter<number>` | Emits the oldest loaded candle timestamp in Unix milliseconds when the user scrolls far enough left |

### Three-Pane Architecture

The chart is split into three synchronised Lightweight Charts instances:

| Pane | Backing Chart | Primary Series |
|---|---|---|
| Main price pane | `_chart` | `CandlestickSeries` |
| RSI pane | `_rsiChart` | `LineSeries` |
| MACD pane | `_macdChart` | `HistogramSeries` plus two `LineSeries` |

The main pane owns the visible logical range subscription. Whenever the user pans or zooms, the RSI and MACD panes inherit the same logical range so all three stay aligned.

### Main Pane Series

The main chart renders the candle body/wicks plus six overlay series:

| Series | Type | Toggle |
|---|---|---|
| Candles | `CandlestickSeries` | always on |
| Fast EMA | `LineSeries` | `emaFast` |
| Slow EMA | `LineSeries` | `emaSlow` |
| Trend EMA | `LineSeries` | `emaTrend` |
| Bollinger Upper | `LineSeries` | `bollinger` |
| Bollinger Middle | `LineSeries` | `bollinger` |
| Bollinger Lower | `LineSeries` | `bollinger` |

### Sub-Chart Series

| Pane | Series | Toggle |
|---|---|---|
| RSI | RSI line plus 70/30 guide lines | `rsi` |
| MACD | MACD line, signal line, histogram | `macd` |

Indicator data does not come from client-side recalculation. It is carried per candle through `ChartIndicatorValues` on the API candle payload.

### Indicator Toggle System

The component exposes six user-controlled toggles:

- `emaFast`
- `emaSlow`
- `emaTrend`
- `bollinger`
- `rsi`
- `macd`

Toggling is implemented by replacing the corresponding series data with either mapped indicator data or an empty array. The series remain allocated; only their data changes.

### Historical Loading and Live Updates

The chart is seeded from REST candle data and then updated with SignalR price ticks.

| Flow | Behaviour |
|---|---|
| Initial load | `MarketDataComponent` fetches historical candles and passes them via `seedCandles` |
| Live updates | `SignalRService.priceUpdate$` updates or rolls the current candle depending on the tick timestamp bucket |
| Older history | Scrolling left near the beginning triggers `loadMoreCandles.emit(oldestMs)` |
| Backfill | `MarketDataComponent.onLoadMoreCandles()` first calls `getHistoricalCandles()`, then falls back to `getCandles()` if empty |
| Merge logic | `prependCandles()` deduplicates by timestamp and prepends only genuinely older candles |

Unlike the earlier design note, there is no rolling 15-minute pruning window. All loaded candles remain in memory until the page reloads.

### Fill Markers and Tooltips

`fills` are rendered as grouped marker annotations rather than individual dots per fill.

Marker behaviour:

- fills are normalised to the selected asset
- timestamps are snapped to the candle bucket for the active timeframe
- fills at the same candle and side are consolidated into a `ConsolidatedFillGroup`
- grouped markers are drawn as `arrowUp` for buys and `arrowDown` for sells
- crosshair movement shows a tooltip containing the grouped fill details

The marker text summarises total size, weighted average price, and marker count when multiple fills are collapsed into one group.

### Time Window and Theme

Two small but important implementation details are easy to miss:

| Detail | Purpose |
|---|---|
| `timeWindowLabel` | Human-readable loaded range summary such as `48H` or `7D` |
| `PRICE_CHART_THEME` | Centralised colour map for pane backgrounds, grid lines, indicator colours, and marker colours |

## MarketDataComponent Integration

`MarketDataComponent` is the orchestration layer around `PriceChartComponent`.

It provides:

- asset selection
- timeframe selection
- candle table and market summary
- fill toggling
- manual refresh
- historical backfill via `onLoadMoreCandles()`

The current chart surface is therefore a market-data tool, not a dashboard widget.

## Not Implemented in the Chart Today

The following earlier concepts are not currently implemented in `PriceChartComponent`:

- grid level overlays
- entry price line overlays
- hedge line overlays
- take-profit bands
- position ladders tied to grid lifecycle state

Those should be treated as future work, not current capabilities.

## EquityChartComponent

`EquityChartComponent` is a separate area-chart implementation used in backtesting pages.

### Inputs

| Input | Purpose |
|---|---|
| `equityData` | Primary `EquitySnapshot[]` series |
| `trades` | Entry/exit markers |
| `cycleSummaries` | Grid deployment and exit markers |
| `comparisonData` | Optional second equity curve |
| `primaryLabel` | Title for the primary area series |
| `comparisonLabel` | Title for the overlay area series |

### Behaviour

| Capability | Implementation |
|---|---|
| Primary curve | `AreaSeries` with green profit-themed fill |
| Comparison curve | Optional second `AreaSeries`, created and removed reactively |
| Trade markers | Entry and exit markers from `BacktestTrade[]` |
| Cycle markers | Deployment and close markers from `GridCycleSummary[]` |
| Legend labels | Updated through `applyOptions()` when labels change |
| Resizing | `ResizeObserver` against the chart container |

`EQUITY_CHART_THEME` is the backtesting equivalent of the price-chart theme constant.

## Creating or Extending Charting

When extending the existing charting surface:

1. Add new price overlays as additional series on `PriceChartComponent` only if they belong to the same market-data visualisation.
2. Prefer consuming indicator values from API candle data rather than recalculating on the client.
3. Keep pane alignment driven by the main chart's visible logical range.
4. Use grouped markers rather than one marker per event when the chart would otherwise become unreadable.
5. Create a separate component when the visualisation has a different purpose, as `EquityChartComponent` already does.

## Future Recommendations

- Add grid-level and anchor-price overlays to the candlestick pane once the live execution model exposes stable chart-ready state.
- Add entry and exit markers tied directly to grid cycles so the market chart and equity chart tell the same story.
- Add VWAP or volume-profile style overlays for market-structure analysis.
- Add a multi-chart layout for comparing assets or timeframes side by side.