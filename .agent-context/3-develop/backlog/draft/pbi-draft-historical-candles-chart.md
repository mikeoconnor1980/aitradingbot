# Historical Candles from Local Database on Price Chart

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-28T20:35:34Z

## User Story

As a trader, I want the Market Data price chart to display historical candles from the local database (backtest data) so that I can see price history going back months/years instead of only ~10 days from the live Hyperliquid API.

## Problem Statement

The Market Data page's PriceChartComponent currently fetches candles exclusively from the Hyperliquid REST API via `GET /api/market/candles`, which only returns approximately 10 days of 15-minute candles. The project already has a SQLite database (`data/tradingapp.db`) with a `Candles` table containing historical data back to 2019 (ingested from Binance), but this data is not surfaced in the chart. Traders need access to longer price history for analysis and strategy evaluation.

## Requirements

### Functional Requirements

1. The price chart must be able to display candles sourced from the local database (Binance historical data) in addition to live Hyperliquid data
2. A backend endpoint must serve candles from the local database, either via a new query parameter on the existing endpoint (e.g., `source=local`) or a separate endpoint (e.g., `GET /api/market/candles/history`)
3. Recent candles (e.g., last 24 hours) should still be sourced from Hyperliquid for real-time accuracy
4. The frontend must support a time range selector or similar mechanism to allow users to browse historical data across large date ranges
5. The chart must load a reasonable default window of data and support scrolling/paging backward through history
6. The existing `ICandleRepository.GetCandlesAsync(symbol, interval, startTime, endTime, source?)` should be leveraged for data retrieval

### Non-Functional Requirements

- Chart rendering performance must remain acceptable when displaying large date ranges (pagination/windowing required — years of 15m candles cannot be loaded at once)
- API response times for historical candle queries should be reasonable (consider server-side pagination or chunked loading)
- Existing backtest data ingestion (`POST /api/candles/ingest/binance`) and backtest functionality must not be affected

## Acceptance Criteria

- [ ] **Given** the Market Data page is loaded with a symbol that has historical data in the local database, **When** the price chart renders, **Then** it displays historical candles from the local database extending beyond the ~10 day Hyperliquid limit
- [ ] **Given** the user is viewing the price chart, **When** they scroll or navigate to recent time periods (e.g., last 24 hours), **Then** candles are sourced from Hyperliquid for real-time accuracy
- [ ] **Given** the local database contains years of 15-minute candle data, **When** the chart loads, **Then** it displays a reasonable default window and supports scrolling/paging backward without degraded performance
- [ ] **Given** the user selects a historical time range, **When** the chart fetches data, **Then** the API responds with paginated or windowed results within acceptable response times
- [ ] **Given** backtest data has been ingested via the Binance ingestion endpoint, **When** the user views the price chart, **Then** the ingested historical data is available for display on the chart
- [ ] **Given** the existing backtest functionality, **When** historical candle chart features are deployed, **Then** backtest ingestion and execution remain fully functional and unaffected

### Release Notes Information

- **Heading**: Historical Price Chart Data from Local Database
- **Release note type**: Feature
- **Release Note Summary**: The Market Data price chart now displays historical candles from the local database, allowing traders to view price history going back months or years instead of the ~10 day limit from the live Hyperliquid API.
- **Release Notes Audience**: Product
- **Breaking Change**: No

## Technical Considerations

### API Endpoints (if relevant)

- `GET /api/market/candles/history` (or modified existing `GET /api/market/candles` with `source` parameter) — serves candles from the local SQLite database with support for time range and pagination parameters
- Leverage existing `ICandleRepository.GetCandlesAsync(symbol, interval, startTime, endTime, source?)` for data access

### Integration Events (if relevant)

None anticipated — this is a read-path feature using existing data.

### Jobs (if relevant)

None — historical data is already ingested via the existing Binance ingestion pipeline.

## Out of Scope

- Importing candle data from new sources (beyond existing Binance ingestion)
- Real-time WebSocket streaming of candles (existing live polling is sufficient)
- Multi-interval switching (e.g., 1h, 4h, 1d aggregation) — can be a future enhancement
- Candle data deduplication or merging logic between Binance and Hyperliquid sources for overlapping time periods
