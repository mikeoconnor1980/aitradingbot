# Show Trades on Main Chart

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-30T19:47:03Z

## User Story

As a trader, I want to see my live fills displayed as markers on the main price chart so that I can visually correlate my entries and exits with price action.

## Problem Statement

The Market Data price chart (PriceChartComponent) currently displays candlestick data and live price updates, but does not show any trade markers. Traders need to visually see where their fills occurred on the chart to evaluate execution quality, timing, and strategy performance in context of price movement.

## Requirements

### Functional Requirements

1. Display fill events as arrow markers on the main price chart, matching the marker style used in the backtesting cycle chart:
   - **Buy fills**: green (`#26a69a`) arrowUp below the bar, with price label text
   - **Sell fills**: amber (`#f59e0b`) arrowDown above the bar, with price label text
2. Load all historical fills for the currently selected asset when the chart initializes, via the existing `GET /api/account/fills` endpoint (extended with an `asset` query parameter)
3. Stream new fills in real-time via the existing SignalR user event WebSocket and add markers as fills arrive
4. Only display fills for the currently selected asset on the chart
5. Provide a toggle button/checkbox on the Market Data page to show or hide trade markers
6. Show a detailed hover tooltip on each marker displaying: side, price, size, fee, and closed PnL
7. When no fills exist for the selected asset, display a clean chart with no markers (no error or empty-state message)
8. Fills that fall outside the currently loaded candle time range should be silently ignored (no markers rendered for them)

### Non-Functional Requirements

- All fills for the selected asset are loaded in a single API call (no pagination needed — fills are few relative to candles)
- Adding markers must not degrade chart rendering performance
- The marker rendering approach must use the `lightweight-charts` `createSeriesMarkers` plugin API, consistent with the backtest cycle chart

## Acceptance Criteria

- [ ] **Given** the Market Data page is loaded with a selected asset, **When** fill history exists for that asset, **Then** arrow markers appear on the chart at the correct candle times and prices matching the backtest chart style
- [ ] **Given** a new fill event arrives via SignalR for the selected asset, **When** the chart is visible, **Then** a new marker is added to the chart in real-time without requiring a page refresh
- [ ] **Given** the user switches the selected asset, **When** fills are re-fetched, **Then** only fills for the newly selected asset are shown
- [ ] **Given** the trade markers toggle is off, **When** the chart renders, **Then** no fill markers are displayed even if fills exist
- [ ] **Given** the trade markers toggle is on and the user hovers over a marker, **When** the tooltip appears, **Then** it shows side, price, size, fee, and closed PnL
- [ ] **Given** no fills exist for the selected asset, **When** the chart loads, **Then** the chart renders cleanly with no markers and no error messages
- [ ] **Given** fills exist that are outside the loaded candle time range, **When** markers are rendered, **Then** those out-of-range fills are silently excluded
- [ ] **Given** the `GET /api/account/fills` endpoint, **When** called with an `asset` query parameter, **Then** it returns only fills matching that asset

### Release Notes Information

- **Heading**: Trade Markers on Price Chart
- **Release note type**: Feature
- **Release Note Summary**: The main price chart now displays live trade fills as arrow markers, allowing traders to visually correlate entries and exits with price action. Markers match the backtesting chart style with buy/sell arrows and detailed hover tooltips.
- **Release Notes Audience**: Product
- **Breaking Change**: No

## Technical Considerations

### API Endpoints (if relevant)

- `GET /api/account/fills?asset={asset}` — extend existing endpoint to support optional `asset` query parameter for filtering fills by asset

### Integration Events (if relevant)

- Existing SignalR `Fill` user event stream already delivers real-time fill events — no new events needed

## Out of Scope

- Backtest trade markers on the main chart (backtest trades already display on the cycle chart)
- Trade lines connecting entry to exit markers
- Fill persistence to local database (fills are sourced from Hyperliquid API)
- Aggregation or grouping of fills (each fill is shown individually)
