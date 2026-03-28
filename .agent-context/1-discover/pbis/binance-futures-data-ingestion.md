# PBI Specification: Binance USDⓈ-M Futures Data Ingestion

**Date:** 2026-03-28
**Author:** Copilot / User
**Status:** Draft

---

## Summary

Add Binance USDⓈ-M Futures as the primary historical market data source for backtesting. Hyperliquid only retains ~5,000–6,000 candles of history per interval (~60 days at 15m), which is insufficient for meaningful strategy backtesting. Binance Futures has data from inception (~Sep 2019), providing 6+ years of perpetual futures data including klines, funding rates, and mark price — the exact data needed for a perp-aware backtester.

### User Story

> As a **strategy developer**, I want to **ingest historical Binance USDⓈ-M Futures data (candles, funding rates, mark prices)** so that **I can backtest grid strategies against years of real perpetual futures data that matches the product I plan to trade**.

### Business Value

- Enables meaningful backtesting with 6+ years of data vs. 60 days from Hyperliquid
- Data comes from perpetual futures (not spot), matching the actual trading instrument
- Funding rates enable realistic PnL simulation for carry/funding strategies
- Mark price data enables future liquidation-aware position sizing
- All endpoints are public (no API key required), removing authentication barriers

---

## Requirements

### Functional Requirements

#### Phase 1 — Futures Klines
- [ ] Create `BinanceRestClient` targeting `https://fapi.binance.com` base URL
- [ ] Implement `GET /fapi/v1/klines` integration (max 1500 candles per request)
- [ ] Support symbols: BTC, ETH, SOL, DOGE, AVAX, ARB, LINK, OP (mapped to BTCUSDT, ETHUSDT, etc.)
- [ ] Support intervals: 5m, 15m, 1h, 4h, 1d
- [ ] Add `Source` column to existing `Candle` entity/table (values: `Hyperliquid`, `Binance`)
- [ ] Create `BinanceCandleIngestionService` with forward-pagination and binary-search gap-finding
- [ ] Default start date: 2019-09-01 (Binance USDⓈ-M launch)
- [ ] Expose `POST /api/candles/ingest/binance` endpoint
- [ ] Return `IngestionResult` with `EarliestCandle`/`LatestCandle` date range per interval

#### Phase 2 — Funding Rate
- [ ] Create `FundingRate` entity with fields: Symbol, Timestamp, FundingRate, MarkPrice
- [ ] Create `FundingRate` table with unique index on (Symbol, Timestamp)
- [ ] Implement `GET /fapi/v1/fundingRate` integration (max 1000 records per request)
- [ ] Create `FundingRateIngestionService` with forward-pagination
- [ ] Expose `POST /api/funding/ingest` endpoint (or extend Binance ingest endpoint)
- [ ] Include funding rate data in ingestion results

#### Phase 3 — Mark Price Klines
- [ ] Implement `GET /fapi/v1/markPriceKlines` integration
- [ ] Store mark price candles in `Candle` table with a distinct interval prefix (e.g., `mark-15m`) or a `CandleType` discriminator
- [ ] Include mark price ingestion in the Binance ingest workflow

### Non-Functional Requirements

- [ ] Respect Binance rate limit: 1200 request weight/minute (klines = 5 weight each)
- [ ] Simple delay-based throttling between batches (configurable `BatchDelayMs`)
- [ ] Page size: 1500 candles per klines request, 1000 per funding rate request
- [ ] Timeout: configurable max ingestion timeout (reuse existing `MaxIngestionTimeoutMs` pattern)
- [ ] Idempotent: skip duplicate candles on re-ingestion (INSERT OR IGNORE pattern)
- [ ] Concurrent ingestion guard (one ingestion at a time, like existing Hyperliquid service)

---

## User Flow

### Happy Path

1. User sends `POST /api/candles/ingest/binance` with `{ "symbol": "BTC", "intervals": ["15m", "1h", "4h", "1d"] }`
2. Service resolves `BTC` → `BTCUSDT` for Binance Futures API
3. For each interval, service paginates forward from default start (2019-09-01) or last stored candle
4. When consecutive empty batches are detected, binary search finds next data boundary
5. Candles are bulk-inserted with `Source = "Binance"`
6. Response returns total counts and date range per interval with human-readable timestamps
7. User separately triggers `POST /api/funding/ingest` with `{ "symbol": "BTC" }` (Phase 2)

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| Binance API returns 429 (rate limited) | Retry with exponential backoff, log warning |
| Binance API returns 451 (IP banned) | Stop ingestion, return error in result |
| Network timeout | Retry up to MaxRetries, then fail interval with error message |
| Invalid symbol (e.g., "INVALID") | Return 400 Bad Request with validation error |
| Concurrent ingestion attempt | Return 409 Conflict (IngestionAlreadyRunningException) |
| Ingestion timeout exceeded | Cancel gracefully, return partial results with "Cancelled or timed out" error |

---

## Technical Considerations

### Bounded Contexts

**Context:** Infrastructure (Binance client) + Application (ingestion commands) + Persistence (extended schema)

### Symbol Mapping

| Display Name | Binance Futures Symbol |
|-------------|----------------------|
| BTC | BTCUSDT |
| ETH | ETHUSDT |
| SOL | SOLUSDT |
| DOGE | DOGEUSDT |
| AVAX | AVAXUSDT |
| ARB | ARBUSDT |
| LINK | LINKUSDT |
| OP | OPUSDT |

### Binance API Endpoints

| Endpoint | Weight | Max Per Request | Use |
|----------|--------|----------------|-----|
| `GET /fapi/v1/klines` | 5 | 1500 candles | Main candle data |
| `GET /fapi/v1/fundingRate` | 1 | 1000 records | 8h funding rate history |
| `GET /fapi/v1/markPriceKlines` | 5 | 1500 candles | Mark/fair-value candles |

### Klines Request Parameters

```
GET /fapi/v1/klines?symbol=BTCUSDT&interval=15m&startTime=1567296000000&limit=1500
```

Response: Array of arrays `[openTime, open, high, low, close, volume, closeTime, quoteVolume, trades, takerBuyBaseVol, takerBuyQuoteVol, ignore]`

### Interval Mapping

| App Interval | Binance Interval |
|-------------|-----------------|
| 5m | 5m |
| 15m | 15m |
| 1h | 1h |
| 4h | 4h |
| 1d | 1d |

### New/Modified Projects

| Project | Changes |
|---------|---------|
| `TradingApp.Domain` | Add `Source` property to `Candle`; create `FundingRate` entity |
| `TradingApp.Application` | `BinanceIngestionOptions`, `IngestBinanceCandlesCommand`, `IngestFundingRatesCommand`, `IBinanceRestClient` interface, `IBinanceCandleIngestionService` interface, `IFundingRateRepository` interface |
| `TradingApp.Infrastructure` | `BinanceRestClient`, `BinanceCandleIngestionService`, `BinanceAssetMapper`, Binance API models |
| `TradingApp.Persistence` | EF migration for `Source` column + `FundingRate` table, `FundingRateRepository` |
| `TradingApp.Api` | `BinanceCandlesController` (or extend `CandlesController`) |

### API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/candles/ingest/binance` | Ingest Binance Futures klines |
| POST | `/api/funding/ingest` | Ingest Binance funding rate history (Phase 2) |

### Database Changes

**Modify `Candle` entity:**
- Add `Source` column (string, max 20, required, default `"Hyperliquid"`)
- Update unique index to include Source: `IX_Candles_Source_Symbol_Interval_Timestamp`

**New `FundingRate` entity:**

| Field | Type | Notes |
|-------|------|-------|
| Id | long | Auto-generated PK |
| Symbol | string(20) | e.g., "BTC" |
| Timestamp | long | Unix ms |
| Rate | decimal | Funding rate (e.g., 0.0001) |
| MarkPrice | decimal | Mark price at funding time |

Unique index: `IX_FundingRates_Symbol_Timestamp`

### Configuration

```json
{
  "BinanceIngestion": {
    "BatchDelayMs": 250,
    "MaxRetries": 3,
    "MaxIngestionTimeoutMs": 7200000,
    "DefaultStartDate": "2019-09-01T00:00:00Z",
    "PageSize": 1500,
    "BaseUrl": "https://fapi.binance.com"
  }
}
```

---

## Out of Scope

- Binance API key / authenticated endpoints
- Binance spot market data
- `GET /fapi/v1/premiumIndexKlines` (can be added later)
- `GET /fapi/v1/indexPriceKlines` (can be added later)
- Replacing Hyperliquid for live trading (Hyperliquid remains the live execution exchange)
- Real-time Binance WebSocket streaming
- Backtester engine changes (separate PBI)
- Open interest data

---

## Open Questions

- [ ] Should the existing `/api/candles/ingest` endpoint accept a `source` parameter to dispatch to either Hyperliquid or Binance, or keep separate endpoints?
- [ ] Should `Source` be an enum or string? (Enum is safer; string is more extensible)
- [ ] For symbols not available on Binance from 2019 (e.g., ARB launched 2023), should ingestion auto-detect the actual start date or fail gracefully?
- [ ] Should mark price candles share the `Candle` table (with a type discriminator) or get a separate `MarkPriceCandle` table?

---

## Acceptance Criteria

- [ ] `POST /api/candles/ingest/binance` with `{ "symbol": "BTC", "intervals": ["15m"] }` returns 50,000+ candles dating back to 2019
- [ ] Ingestion response includes human-readable `earliestCandle` and `latestCandle` dates
- [ ] Re-running ingestion skips already-stored candles (idempotent)
- [ ] Existing Hyperliquid candle ingestion continues to work unchanged
- [ ] Binance candles have `Source = "Binance"`, Hyperliquid candles have `Source = "Hyperliquid"`
- [ ] Funding rate ingestion stores 8h funding snapshots with correct rates
- [ ] Rate limiting is respected (no 429 responses during normal ingestion)
- [ ] All unit tests pass with >80% code coverage on new services
- [ ] Empty time ranges are traversed efficiently (binary search, not linear crawl)
- [ ] No tech stack compliance violations

---

## Phased Delivery

| Phase | Scope | Endpoints |
|-------|-------|-----------|
| **1** | Binance klines + Source column on Candle + BinanceRestClient + ingestion service | `POST /api/candles/ingest/binance` |
| **2** | FundingRate entity + table + ingestion | `POST /api/funding/ingest` |
| **3** | Mark price klines ingestion | Extend Phase 1 endpoint |
| **4** (future) | Premium index / index price klines | Extend Phase 1 endpoint |

---

## Appendix

### References

- [Binance Futures API Documentation](https://developers.binance.com/docs/derivatives/usds-margined-futures/market-data)
- [Existing Hyperliquid Integration Knowledge](../../0-knowledge/02-hyperliquid-integration.md)
- [Domain Model](../../0-knowledge/04-domain-model.md)
- [Project Structure](../../0-knowledge/06-project-structure.md)

### Related Features

- Candle Data Persistence (existing, Hyperliquid)
- Backtesting Engine (depends on this PBI for data)
- Grid Strategy Backtesting
