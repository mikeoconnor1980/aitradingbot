# PBI Specification: F2 — Candle Ingestion Service

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-27
**PRD:** [candle-persistence-backtesting-prd.md](../../prd/candle-persistence-backtesting-prd.md)
**Implementation Phase:** 2
**Risk Level:** Low
**Depends On:** F1 (Candle Data Persistence)

---

## Summary

Build a candle ingestion service that batch-fetches historical OHLCV candle data from Hyperliquid's REST API and persists it to the local SQLite database via the repository established in F1. Expose an API endpoint to trigger ingestion with support for incremental sync.

### User Story

> As an **Operator**, I want to **ingest all available BTC candle history from Hyperliquid for 15m, 1h, and 4h intervals in a single operation** so that **I have a complete local dataset for backtesting without manual data management**.

### Business Value

Populates the local candle database with historical market data required for backtesting. Without ingested data, the backtest engine (F3) has nothing to replay. Incremental sync ensures the dataset stays current without re-downloading everything, reducing API load and ingestion time.

---

## Problem Statement

The candle database (F1) is empty after setup. There is no mechanism to populate it with historical data from Hyperliquid. The existing `HyperliquidRestClient.GetCandlesAsync` fetches up to 500 candles per request but does not persist them. A batch ingestion service with pagination, rate limiting, and upsert semantics is needed.

---

## Requirements

### Functional Requirements

- [ ] An `ICandleIngestionService` interface is defined in `TradingApp.Application` with a method: `IngestAsync(symbol, intervals[], startTime?, endTime?)` returning an `IngestionResult`
- [ ] A `CandleIngestionService` implementation exists in `TradingApp.Infrastructure` that uses the existing `IHyperliquidRestClient` for data fetching
- [ ] The service fetches candles from Hyperliquid in batches of up to 500 candles per request, paginating forward by time
- [ ] The service supports ingestion for BTC across 15m, 1h, and 4h intervals
- [ ] For each interval, the service loops: fetch batch → upsert to DB via `ICandleRepository.BulkInsertAsync` → advance cursor by last candle timestamp → repeat until `endTime` or no more data
- [ ] Duplicate candles are skipped (upsert semantics via the composite unique index from F1)
- [ ] Rate limiting is handled gracefully — the service pauses between batch requests with a configurable delay (default: 200ms)
- [ ] If `startTime` is omitted, ingestion starts from the latest candle timestamp in the DB for that symbol/interval (via `ICandleRepository.GetLatestTimestampAsync`), or from the earliest available date on Hyperliquid if the DB is empty
- [ ] If `endTime` is omitted, ingestion fetches up to the current time
- [ ] Ingestion is idempotent — running it multiple times for the same range produces no duplicates
- [ ] The service handles empty responses from Hyperliquid gracefully (stops pagination for that interval when no data is returned)
- [ ] The service maps between Hyperliquid's candle format and the `Candle` domain entity using the existing `HyperliquidAssetMapper` for asset name resolution and interval conversion
- [ ] An `IngestionResult` DTO is defined with: total candles fetched, total inserted, total skipped, per-interval breakdown (interval, fetched, inserted, skipped), elapsed time
- [ ] An API endpoint `POST /api/candles/ingest` triggers ingestion
- [ ] The endpoint accepts a request body: `symbol` (required), `intervals` (string array, required), `startTime` (long, optional, unix ms), `endTime` (long, optional, unix ms)
- [ ] The endpoint returns the `IngestionResult` summary in the response body

### Non-Functional Requirements

- [ ] Candle ingestion for all 3 intervals (~137K candles total) completes in under 10 minutes
- [ ] Rate limiting delay between requests is configurable via `appsettings.json` (default: 200ms)
- [ ] All async operations use `CancellationToken` for cooperative cancellation
- [ ] Structured logging at key points: ingestion start, batch fetched, batch inserted, interval complete, ingestion complete
- [ ] The ingestion endpoint runs synchronously (blocks until complete) — background processing is out of scope

---

## User Flow

### Happy Path — Full Initial Ingestion

1. Operator calls `POST /api/candles/ingest` with body: `{ "symbol": "BTC", "intervals": ["15m", "1h", "4h"] }`
2. Service checks DB for latest timestamps per interval — finds none (empty DB)
3. For each interval, service begins fetching from the earliest available data on Hyperliquid
4. Service fetches 500 candles per request, inserts to DB, advances cursor, pauses 200ms, repeats
5. When Hyperliquid returns fewer than 500 candles or an empty response, pagination stops for that interval
6. Response returns: `{ "totalFetched": 137970, "totalInserted": 137970, "totalSkipped": 0, "elapsed": "PT7M32S", "intervals": [...] }`

### Happy Path — Incremental Sync

1. Operator calls `POST /api/candles/ingest` with body: `{ "symbol": "BTC", "intervals": ["15m", "1h", "4h"] }`
2. Service checks DB for latest timestamps per interval — finds recent data
3. For each interval, fetching begins from the latest timestamp + 1
4. Service fetches only new candles since the last ingestion
5. Response returns with small `totalInserted` count and `totalSkipped: 0`

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| Hyperliquid API unreachable | Service logs the error and throws; endpoint returns 502 with error detail |
| Hyperliquid returns rate-limit error (429) | Service logs warning, waits longer delay, retries the batch (up to 3 retries) |
| Empty response for a time range | Service stops pagination for that interval gracefully — not an error |
| Invalid symbol in request | Endpoint returns 400 with validation error |
| Invalid interval in request | Endpoint returns 400 with validation error listing valid intervals |
| Database write failure | Service logs error and throws; endpoint returns 500 with error detail |
| Request cancelled (client disconnect) | Service respects `CancellationToken` and stops fetching |

---

## Technical Considerations

### API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/candles/ingest` | Triggers candle ingestion for the specified symbol and intervals; returns ingestion results |

#### Request Shape

```json
{
  "symbol": "BTC",
  "intervals": ["15m", "1h", "4h"],
  "startTime": null,
  "endTime": null
}
```

#### Response Shape

```json
{
  "totalFetched": 137970,
  "totalInserted": 137970,
  "totalSkipped": 0,
  "elapsedMs": 452000,
  "intervals": [
    { "interval": "15m", "fetched": 105120, "inserted": 105120, "skipped": 0 },
    { "interval": "1h", "fetched": 26280, "inserted": 26280, "skipped": 0 },
    { "interval": "4h", "fetched": 6570, "inserted": 6570, "skipped": 0 }
  ]
}
```

### Key Components

| Component | Layer | Action |
|-----------|-------|--------|
| `ICandleIngestionService` | `TradingApp.Application` | Interface defining ingestion operations |
| `CandleIngestionService` | `TradingApp.Infrastructure` | Implementation: pagination, mapping, rate limiting, upsert via repository |
| `IngestionRequest` | `TradingApp.Application` | Request DTO (symbol, intervals, startTime, endTime) |
| `IngestionResult` | `TradingApp.Application` | Result DTO (counts, per-interval breakdown, elapsed time) |
| `CandleIngestionController` | `TradingApp.Api` | API controller exposing the ingestion endpoint |
| `IHyperliquidRestClient` | `TradingApp.Infrastructure` | Existing — used for batch candle fetching |
| `HyperliquidAssetMapper` | `TradingApp.Infrastructure` | Existing — maps asset names and resolves interval to milliseconds |
| `ICandleRepository` | `TradingApp.Application` | From F1 — used for `BulkInsertAsync` and `GetLatestTimestampAsync` |

### Ingestion Flow

```
POST /api/candles/ingest (symbol, intervals, startTime?, endTime?)
  → CandleIngestionService.IngestAsync
    → For each interval:
      → Determine start: GetLatestTimestampAsync or earliest available
      → Loop:
        → Fetch 500 candles from HyperliquidRestClient.GetCandlesAsync
        → Map HyperliquidCandle → Candle domain entity
        → BulkInsertAsync (skip duplicates)
        → Advance cursor to last candle timestamp
        → Delay 200ms (rate limiting)
      → Until endTime reached or empty response
    → Aggregate counts → return IngestionResult
```

### Configuration Shape

```json
{
  "CandleIngestion": {
    "BatchDelayMs": 200,
    "MaxRetries": 3
  }
}
```

---

## Out of Scope

- Automated/scheduled candle sync via Worker (future enhancement)
- Background processing with progress reporting via SignalR
- Multi-asset ingestion beyond BTC
- Candle data validation (e.g., gap detection, OHLC consistency checks)
- Frontend UI for ingestion progress or management

---

## Open Questions

- [ ] What is the earliest BTC candle available on Hyperliquid? Should the service auto-discover this or use a hardcoded start date?
- [ ] Should the ingestion endpoint run synchronously (blocking until complete) or return immediately and process in the background? For ~137K candles at 200ms throttle, ingestion takes ~5–8 minutes.
- [ ] Should there be a maximum time range per ingestion request to prevent very long-running HTTP requests?

---

## Acceptance Criteria

- [ ] **Given** the database is empty, **When** `POST /api/candles/ingest` is called with `symbol=BTC` and `intervals=[15m, 1h, 4h]`, **Then** candles are fetched from the earliest available date to now
- [ ] **Given** ingestion completes, **Then** the response includes counts: total fetched, total inserted, total skipped, per-interval breakdown
- [ ] **Given** Hyperliquid returns no data for a time range, **Then** the service stops pagination for that interval gracefully
- [ ] **Given** the database already contains candles, **When** ingestion is re-run, **Then** duplicates are skipped (no errors, no duplicate rows)
- [ ] **Given** the database has candles up to timestamp T for a given symbol/interval, **When** ingestion is called without `startTime`, **Then** fetching begins from T+1
- [ ] **Given** no new candles exist on Hyperliquid, **Then** the response shows 0 inserted
- [ ] **Given** a request with an invalid symbol, **When** `POST /api/candles/ingest` is called, **Then** a 400 response with a validation error is returned
- [ ] **Given** a request with an invalid interval, **When** `POST /api/candles/ingest` is called, **Then** a 400 response listing valid intervals is returned
- [ ] **Given** the ingestion is in progress, **When** structured logs are inspected, **Then** logs show batch progress (batches fetched, inserted counts) per interval
- [ ] **Given** multiple batch requests, **When** observing timing between requests, **Then** at least 200ms delay exists between consecutive Hyperliquid API calls

### Release Notes Information

- **Heading**: Candle Data Ingestion from Hyperliquid
- **Release note type**: Feature
- **Release Note Summary**: Batch ingest historical BTC candle data (15m, 1h, 4h) from Hyperliquid into local SQLite storage with incremental sync support and rate limiting.
- **Release Notes Audience**: Product
- **Breaking Change**: No

---

## Related Features

- **F1** — Candle Data Persistence provides the repository and database that this service writes to
- **F3** — Backtest Replay Engine consumes the candle data ingested by this service
- **F4** — Backtest API depends on candle data being available in the database
