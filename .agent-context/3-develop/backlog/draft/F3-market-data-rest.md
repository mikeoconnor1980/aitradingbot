# PBI Specification: F3 — Market Data (REST)

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-24
**PRD:** [hyperliquid-poc-prd.md](../../prd-approved/hyperliquid-poc-prd.md)
**Implementation Phase:** 2
**Risk Level:** Low
**Depends On:** F1

---

## Summary

Fetch and display market metadata and recent candle data for perpetual assets via the Hyperliquid REST API. This is the lowest-risk feature that retrieves real exchange data and validates the response parsing pipeline end-to-end.

### User Story

> As a **developer**, I want to **view current market information for a selected perpetual asset** so that **I can confirm the REST API client works against Hyperliquid before attempting authenticated or write operations**.

### Business Value

Proves the REST client can reliably fetch and parse market data from Hyperliquid. This validates the data pipeline before attempting any authenticated or write operations and provides the market info (mid price) that F5 depends on for limit order pre-population.

---

## Problem Statement

Before attempting any authenticated or write operations (order placement, signing), we need to prove the Hyperliquid REST client can reliably fetch and parse market data. This is the lowest-risk feature that retrieves real exchange data (after the health check in F1) and validates the response parsing pipeline end-to-end.

---

## Requirements

### Functional Requirements

- [ ] Asset selector dropdown populated with a hardcoded list of popular perpetual pairs (BTC-PERP, ETH-PERP, SOL-PERP, etc.) with BTC-PERP as the default selection
- [ ] Fetch market metadata from Hyperliquid for the selected asset (available markets / asset info)
- [ ] Market info card displays: mid price, mark price, index price, funding rate, 24h volume, open interest, and 24h price change %
- [ ] Fetch recent candle data for 15m, 1H, and 4H timeframes via REST for the selected asset
- [ ] Candle table displays 50 most recent candles with OHLCV data, sorted newest first
- [ ] Default timeframe on page load is 15m
- [ ] Timeframe selector allows switching between 15m, 1H, 4H — switching reloads candle data
- [ ] Manual refresh button reloads both market info and candle data on demand
- [ ] Market info card auto-refreshes on a 10-second polling interval
- [ ] Candle table refreshes only on timeframe change, asset change, or manual refresh (no auto-polling)
- [ ] Changing the selected asset reloads both market info and candle data

### Non-Functional Requirements

- [ ] Market info polling does not block the UI (async refresh in background)
- [ ] API response time for market info endpoint < 1 second under normal conditions
- [ ] Candle data limited to 50 candles per request to keep payload small

---

## User Flow

### Happy Path

1. Developer navigates to the Market Data route in the Angular UI
2. Page loads with BTC-PERP selected by default in the asset dropdown
3. Market info card shows mid price, mark price, index price, funding rate, 24h volume, open interest, and 24h price change %
4. Below the market info card, the candle table displays 50 recent 15m candles (newest first)
5. Developer switches timeframe to 1H — candle table reloads with 1H data
6. Developer selects ETH-PERP from the asset dropdown — both market info and candles reload for ETH-PERP
7. Market info card auto-updates every 10 seconds
8. Developer clicks the manual refresh button — both market info and candles reload immediately

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| Hyperliquid API unreachable | Market info card shows error banner; retries on next 10s poll cycle |
| Invalid/unexpected response format | Backend logs parsing error with structured logging; API returns 500 with meaningful detail |
| No candle data available for timeframe | Table shows empty state message ("No candle data available") |
| Selected asset not found on exchange | Market info card shows "Asset not available" message |
| Network timeout on candle fetch | Error message displayed inline; manual refresh available to retry |

---

## Technical Considerations

### API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/market/info?asset={asset}` | Returns metadata for the specified asset (mid price, mark price, index price, funding, volume, OI, 24h change) |
| GET | `/api/market/candles?asset={asset}&timeframe={tf}` | Returns 50 recent candles for specified asset and timeframe (15m, 1h, 4h) |

### Key Components

| Component | Action |
|-----------|--------|
| `MarketDataController` | Exposes market info and candle endpoints with asset and timeframe query parameters |
| `HyperliquidRestClient` | Calls Hyperliquid REST API for market metadata and candle data |
| `hyperliquid-api.service.ts` | Angular service making HTTP calls to market endpoints |
| Market Data feature component | Renders asset selector, market info card, timeframe selector, candle table, and refresh button |

### Data Models

**Market Info:**
- Asset name, mid price, mark price, index price, funding rate, 24h volume, open interest, 24h price change %

**Candle:**
- Timestamp, open, high, low, close, volume

---

## Out of Scope

- Charting / candlestick visualisation (table/list is sufficient for POC)
- Real-time streaming via WebSocket (covered by F4)
- Historical data persistence or caching
- Dynamic asset list fetched from the exchange API (hardcoded list only)
- Pagination or infinite scroll for candle data

---

## Open Questions

*None at this time.*

---

## Acceptance Criteria

- [ ] **Given** the Market Data page is loaded, **When** the page renders, **Then** BTC-PERP is selected by default in the asset dropdown
- [ ] **Given** the page has loaded, **When** market data is fetched, **Then** the market info card displays mid price, mark price, index price, funding rate, 24h volume, open interest, and 24h price change % for the selected asset
- [ ] **Given** the page has loaded, **When** candle data is fetched, **Then** the candle table displays 50 recent candles sorted newest first with OHLCV columns
- [ ] **Given** the candle table is displayed, **When** the default timeframe loads, **Then** the 15m timeframe is selected
- [ ] **Given** the candle table is displayed, **When** the user selects a different timeframe (1H or 4H), **Then** the table reloads with candles for the new timeframe
- [ ] **Given** the asset dropdown is displayed, **When** the user selects a different asset, **Then** both the market info card and candle table reload for the new asset
- [ ] **Given** the page is displayed, **When** 10 seconds elapse, **Then** the market info card auto-refreshes with latest data (candle table does not auto-refresh)
- [ ] **Given** the page is displayed, **When** the user clicks the manual refresh button, **Then** both market info and candle data reload immediately
- [ ] **Given** the Hyperliquid API is unreachable, **When** a fetch attempt fails, **Then** the UI displays a meaningful error message and retries on the next poll cycle
- [ ] **Given** no candle data is available for the selected timeframe, **When** the table renders, **Then** an empty state message is shown

### Release Notes Information

- **Heading**: Market Data Dashboard (REST)
- **Release note type**: Feature
- **Release Note Summary**: View real-time market information and recent candle data for perpetual assets via the Hyperliquid REST API, with auto-refresh and timeframe selection.
- **Release Notes Audience**: Product
- **Breaking Change**: No

---

## Related Features

- **F1** — Connectivity must be established before market data can be fetched
- **F4** — WebSocket streaming (F4) builds on the market data concept proven here
