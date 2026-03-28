# Charting Library

The UI uses TradingView Lightweight Charts.

Advantages:

fast  
open source  
built for financial charts

Installation:

npm install lightweight-charts

The chart component renders:

candles  
grid levels  
entry price  
take profit  
hedge levels

## Real-Time Line Chart

`PriceChartComponent` (`frontend/trading-ui/src/app/features/market-data/price-chart/price-chart.component.ts`) renders a live rolling 15-minute price line chart:

- `createChart()` with a `LineSeries` (`ISeriesApi<"Line">`)
- Seeded at init from REST candle data (`Candle[]` passed via `@Input() seedCandles`)
- Real-time updates consumed from `SignalRService.priceUpdate$` via `inject(SignalRService)`
- Rolling 900-second window — data points older than 15 min are dropped
- Uses `update()` for appending new points and `setData()` only when the rolling window prunes old data
- Responsive: `ResizeObserver` watches the container `ElementRef` and calls `chart.applyOptions({ width })`
- Cleanup: `ResizeObserver.disconnect()` + `chart.remove()` in `ngOnDestroy`

When adding overlays to the live price chart specifically (e.g., grid levels, take-profit bands), add additional `ISeriesApi` instances to `PriceChartComponent` rather than creating a new component. For a distinct visualization purpose (e.g., equity curves, backtesting), create a dedicated component rather than extending `PriceChartComponent`.

## Equity Chart (Backtesting)

`EquityChartComponent` (`frontend/trading-ui/src/app/features/backtesting/equity-chart/equity-chart.component.ts`) renders an area chart of the equity curve produced by a backtest run:

- `AreaSeries` (`ISeriesApi<"Area">`) for primary equity data (`EquitySnapshot[]`)
- Optional second `AreaSeries` for comparison overlay — added/removed reactively via `ngOnChanges` when `comparisonData` input changes
- Trade markers via `createSeriesMarkers()` — entry/exit points plotted directly on the primary series
- `@Input() primaryLabel` / `@Input() comparisonLabel` update series titles for the legend
- `ResizeObserver` + `ngOnDestroy` cleanup follow the same pattern as `PriceChartComponent`

The comparison overlay (two overlaid series, labelled) is the canonical pattern for side-by-side equity curve comparison in `BacktestCompareComponent`.