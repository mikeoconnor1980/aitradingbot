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

When adding further chart series (e.g., grid levels, take-profit bands), extend `PriceChartComponent` with additional `ISeriesApi` instances rather than creating a new component.