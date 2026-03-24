# PBI Specification: F3 — Market Data (REST)

**Date:** 2026-03-24  
**Author:** PRD Agent  
**Status:** Draft  
**PRD:** [hyperliquid-poc-prd.md](../../prd-approved/hyperliquid-poc-prd.md)  
**Implementation Phase:** 2  
**Risk Level:** Low  
**Depends On:** F1

---

## Summary

Fetch and display current BTC-PERP market information and recent candle data via the Hyperliquid REST API.

### User Story

> As a **developer**, I want to **view current market information for BTC-PERP** so that **I can confirm the REST API client works against Hyperliquid before attempting authenticated or write operations**.

### Business Value

Low-risk, read-only integration that proves the REST client and response parsing work. This is the first feature that retrieves real exchange data (after the health check in F1). Success here builds confidence for the higher-risk features.

---

## Requirements

### Functional Requirements

- [ ] Fetch BTC-PERP market metadata from Hyperliquid (available markets / asset info)
- [ ] Display mid price, mark price, funding rate, 24h volume
- [ ] Fetch recent candle data for 15m, 1H, and 4H timeframes via REST
- [ ] Angular UI shows market info card with current prices and stats
- [ ] Angular UI shows a simple candle data table/list

### Non-Functional Requirements

- [ ] Market data refreshes on a polling interval or manual refresh button
- [ ] Candle data limited to recent history (e.g. last 50 candles per timeframe)

---

## User Flow

### Happy Path

1. Developer navigates to Market Data tab in Angular UI
2. Market info card shows BTC-PERP mid price, mark price, funding rate, 24h volume
3. Candle table shows recent candles with open, high, low, close, volume
4. Developer can switch between 15m, 1H, 4H timeframes

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| Hyperliquid API unreachable | Market info card shows error message; retries on next poll |
| Invalid response format | Backend logs parsing error; API returns 500 with detail |
| No candle data available for timeframe | Table shows empty state |

---

## Technical Considerations

### API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/market/info` | Returns BTC-PERP metadata (mid price, mark price, funding, volume) |
| GET | `/api/market/candles?timeframe={tf}` | Returns recent candles for specified timeframe (15m, 1h, 4h) |

### Key Components

| Component | Action |
|-----------|--------|
| `MarketDataController` | Exposes market info and candle endpoints |
| `HyperliquidRestClient` | Calls Hyperliquid REST API for market metadata and candle data |
| `hyperliquid-api.service.ts` | Angular service making HTTP calls to market endpoints |
| Market Data feature component | Renders market info card and candle table with timeframe selector |

### Data Models

**Market Info:**
- Asset name, mid price, mark price, funding rate, 24h volume, open interest

**Candle:**
- Timestamp, open, high, low, close, volume

---

## Out of Scope

- Charting / candlestick visualisation (table/list is sufficient)
- Real-time streaming (covered by F4)
- Historical data persistence
- Multiple assets (BTC-PERP only)

---

## Open Questions

*None at this time.*

---

## Acceptance Criteria

- [ ] Market info card displays mid price, mark price, funding rate, and 24h volume for BTC-PERP
- [ ] Candle data table renders recent candles with OHLCV data
- [ ] Timeframe selector allows switching between 15m, 1H, 4H
- [ ] API errors are surfaced to the UI with a meaningful message
- [ ] Response parsing handles the Hyperliquid-specific JSON format correctly

---

## Related Features

- **F1** — Connectivity must be established before market data can be fetched
- **F4** — WebSocket streaming (F4) builds on the market data concept proven here
