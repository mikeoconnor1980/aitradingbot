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

Build a candle ingestion service that batch-fetches historical OHLCV candle data from Hyperliquid's REST API and persists it to the local SQLite database via the repository established in F1. Expose an API endpoint to trigger ingestion with support for incremental sync. Accepts any symbol known to `HyperliquidAssetMapper` and any supported interval (5m, 15m, 1h, 4h).

### User Story

> As an **Operator**, I want to **ingest historical candle data from Hyperliquid for any supported symbol and interval** so that **I have a complete local dataset for backtesting without manual data management**.

### Business Value

Populates the local candle database with historical market data required for backtesting. Without ingested data, the backtest engine (F3) has nothing to replay. Incremental sync ensures the dataset stays current without re-downloading everything, reducing API load and ingestion time.

---

## Problem Statement

The candle database (F1) is empty after setup. There is no mechanism to populate it with historical data from Hyperliquid. The existing `HyperliquidRestClient.GetCandlesAsync` fetches up to 500 candles per request but does not persist them, and only accepts `endTime` (calculating `startTime` internally). A batch ingestion service with forward pagination, rate limiting, and upsert semantics is needed, along with a new `GetCandlesAsync` overload that accepts both `startTime` and `endTime`.

---

## Requirements

### Functional Requirements

- [ ] A new `GetCandlesAsync` overload is added to `IHyperliquidRestClient` that accepts both `startTime` and `endTime` parameters (both `long`), enabling forward pagination by time range
- [ ] An `ICandleIngestionService` interface is defined in `TradePilot.Application` with a method: `IngestAsync(symbol, intervals[], startTime?, endTime?, cancellationToken)` returning an `IngestionResult`
- [ ] A `CandleIngestionService` implementation exists in `TradePilot.Infrastructure` that uses the new `IHyperliquidRestClient.GetCandlesAsync(asset, timeframe, startTime, endTime)` overload for data fetching
- [ ] The service fetches candles from Hyperliquid in batches of up to 500 candles per request, paginating forward by time
- [ ] The service accepts any symbol known to `HyperliquidAssetMapper` and any interval supported by `HyperliquidAssetMapper.GetIntervalMs` (currently: 5m, 15m, 1h, 4h)
- [ ] For each interval, the service loops: fetch batch → upsert to DB via `ICandleRepository.BulkInsertAsync` → advance cursor by last candle timestamp → repeat until `endTime` or no more data
- [ ] Duplicate candles are skipped (upsert semantics via the composite unique index from F1)
- [ ] Rate limiting is handled gracefully — the service pauses between batch requests with a configurable delay (default: 200ms)
- [ ] If `startTime` is omitted, ingestion starts from the latest candle timestamp in the DB for that symbol/interval (via `ICandleRepository.GetLatestTimestampAsync`), or from a hardcoded default start date if the DB is empty (Hyperliquid BTC launch: approximately November 2022)
- [ ] If `endTime` is omitted, ingestion fetches up to the current time
- [ ] Ingestion is idempotent — running it multiple times for the same range produces no duplicates
- [ ] The service handles empty responses from Hyperliquid gracefully (stops pagination for that interval when no data is returned)
- [ ] The service maps between Hyperliquid's candle format and the `Candle` domain entity, storing the coin symbol (e.g., `BTC` not `BTC-PERP`) as the Symbol field
- [ ] An `IngestionResult` DTO is defined with: total candles fetched, total inserted, total skipped, per-interval breakdown (interval, fetched, inserted, skipped, error), elapsed time
- [ ] If ingestion for one interval fails after retries, that interval is skipped and the remaining intervals continue processing; the per-interval result includes the error detail
- [ ] A concurrency guard prevents multiple simultaneous ingestion runs — concurrent requests receive a 409 Conflict response
- [ ] An API endpoint `POST /api/candles/ingest` triggers ingestion
- [ ] The endpoint accepts a request body: `symbol` (required), `intervals` (string array, required), `startTime` (long, optional, unix ms), `endTime` (long, optional, unix ms)
- [ ] The endpoint returns the `IngestionResult` summary in the response body
- [ ] The endpoint validates that `symbol` is known to `HyperliquidAssetMapper` and all `intervals` are supported; returns 400 with validation errors otherwise

### Non-Functional Requirements

- [ ] Candle ingestion for 3 intervals (~137K candles total) completes in under 10 minutes
- [ ] Rate limiting delay between requests is configurable via `appsettings.json` (default: 200ms)
- [ ] A configurable maximum ingestion timeout is enforced (default: 15 minutes); if exceeded, the service stops and returns results collected so far
- [ ] All async operations use `CancellationToken` for cooperative cancellation
- [ ] Retries on transient HTTP errors (429, 500, 502, 503, timeout) with exponential backoff, up to a configurable max retries (default: 3)
- [ ] Structured logging at key points: ingestion start, batch fetched, batch inserted, interval complete, interval error, ingestion complete
- [ ] The ingestion endpoint runs synchronously (blocks until complete) — background processing is out of scope
- [ ] No authentication required (local operator-only tool for POC phase)

---

## User Flow

### Happy Path — Full Initial Ingestion

1. Operator calls `POST /api/candles/ingest` with body: `{ "symbol": "BTC", "intervals": ["15m", "1h", "4h"] }`
2. Service acquires the concurrency guard (or returns 409 if already running)
3. Service checks DB for latest timestamps per interval — finds none (empty DB)
4. For each interval, service begins fetching from the hardcoded default start date (~November 2022)
5. Service fetches 500 candles per request via the new `GetCandlesAsync(asset, timeframe, startTime, endTime)` overload, inserts to DB, advances cursor, pauses 200ms, repeats
6. When Hyperliquid returns fewer than 500 candles or an empty response, pagination stops for that interval
7. Response returns: `{ "totalFetched": 137970, "totalInserted": 137970, "totalSkipped": 0, "elapsedMs": 452000, "intervals": [...] }`

### Happy Path — Incremental Sync

1. Operator calls `POST /api/candles/ingest` with body: `{ "symbol": "BTC", "intervals": ["15m", "1h", "4h"] }`
2. Service acquires the concurrency guard
3. Service checks DB for latest timestamps per interval — finds recent data
4. For each interval, fetching begins from the latest timestamp + 1
5. Service fetches only new candles since the last ingestion
6. Response returns with small `totalInserted` count and `totalSkipped: 0`

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| Concurrent ingestion request | Endpoint returns 409 Conflict |
| Transient HTTP error (429, 500, 502, 503, timeout) | Service retries with exponential backoff up to max retries (default: 3) |
| One interval fails after all retries | That interval is skipped; remaining intervals continue; per-interval result includes error detail |
| Empty response for a time range | Service stops pagination for that interval gracefully — not an error |
| Invalid symbol in request | Endpoint returns 400 with validation error |
| Invalid interval in request | Endpoint returns 400 with validation error listing valid intervals |
| Database write failure | Service logs error for that interval; skips interval, continues others |
| Request cancelled (client disconnect) | Service respects `CancellationToken` and stops fetching |
| Ingestion timeout exceeded | Service stops and returns results collected so far |

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
    { "interval": "15m", "fetched": 105120, "inserted": 105120, "skipped": 0, "error": null },
    { "interval": "1h", "fetched": 26280, "inserted": 26280, "skipped": 0, "error": null },
    { "interval": "4h", "fetched": 6570, "inserted": 6570, "skipped": 0, "error": null }
  ]
}
```

### Key Components

| Component | Layer | Action |
|-----------|-------|--------|
| `ICandleIngestionService` | `TradePilot.Application` | Interface defining ingestion operations |
| `CandleIngestionService` | `TradePilot.Infrastructure` | Implementation: pagination, mapping, rate limiting, retry, upsert via repository |
| `IngestionRequest` | `TradePilot.Application` | Request DTO (symbol, intervals, startTime, endTime) |
| `IngestionResult` | `TradePilot.Application` | Result DTO (counts, per-interval breakdown with error field, elapsed time) |
| `CandleIngestionController` | `TradePilot.Api` | API controller exposing the ingestion endpoint with concurrency guard |
| `IHyperliquidRestClient` | `TradePilot.Infrastructure` | Existing interface — new overload: `GetCandlesAsync(asset, timeframe, startTime, endTime)` for forward pagination |
| `HyperliquidAssetMapper` | `TradePilot.Infrastructure` | Existing — maps asset names and resolves interval to milliseconds; used for validation |
| `ICandleRepository` | `TradePilot.Application` | From F1 — used for `BulkInsertAsync` and `GetLatestTimestampAsync` |

### Ingestion Flow

```
POST /api/candles/ingest (symbol, intervals, startTime?, endTime?)
  → Validate symbol and intervals against HyperliquidAssetMapper
  → Acquire concurrency guard (or return 409)
  → CandleIngestionService.IngestAsync
    → For each interval:
      → Try:
        → Determine start: GetLatestTimestampAsync or hardcoded default (~Nov 2022)
        → Loop:
          → Fetch 500 candles from HyperliquidRestClient.GetCandlesAsync(asset, timeframe, startTime, endTime)
          → Map HyperliquidCandle → Candle domain entity (Symbol = coin symbol, e.g., "BTC")
          → BulkInsertAsync (skip duplicates)
          → Advance cursor to last candle timestamp
          → Delay 200ms (rate limiting)
        → Until endTime reached or empty response
      → Catch transient errors: retry with exponential backoff up to MaxRetries
      → Catch final failure: record error in interval result, continue to next interval
    → Check ingestion timeout — stop if exceeded
    → Aggregate counts → return IngestionResult
  → Release concurrency guard
```

### Configuration Shape

```json
{
  "CandleIngestion": {
    "BatchDelayMs": 200,
    "MaxRetries": 3,
    "MaxIngestionTimeoutMs": 900000,
    "DefaultStartDate": "2022-11-01T00:00:00Z"
  }
}
```

---

## Out of Scope

- Automated/scheduled candle sync via Worker (future enhancement)
- Background processing with progress reporting via SignalR
- Candle data validation (e.g., gap detection, OHLC consistency checks)
- Frontend UI for ingestion progress or management
- Authentication/authorization for the ingestion endpoint (local POC only)

---

## Acceptance Criteria

- [ ] **Given** the database is empty, **When** `POST /api/candles/ingest` is called with `symbol=BTC` and `intervals=[15m, 1h, 4h]`, **Then** candles are fetched from the configured default start date to now
- [ ] **Given** ingestion completes, **Then** the response includes counts: total fetched, total inserted, total skipped, per-interval breakdown (including error field)
- [ ] **Given** Hyperliquid returns no data for a time range, **Then** the service stops pagination for that interval gracefully
- [ ] **Given** the database already contains candles, **When** ingestion is re-run, **Then** duplicates are skipped (no errors, no duplicate rows)
- [ ] **Given** the database has candles up to timestamp T for a given symbol/interval, **When** ingestion is called without `startTime`, **Then** fetching begins from T+1
- [ ] **Given** no new candles exist on Hyperliquid, **Then** the response shows 0 inserted
- [ ] **Given** a request with an unknown symbol, **When** `POST /api/candles/ingest` is called, **Then** a 400 response with a validation error is returned
- [ ] **Given** a request with an unsupported interval, **When** `POST /api/candles/ingest` is called, **Then** a 400 response listing valid intervals is returned
- [ ] **Given** an ingestion is already running, **When** a second `POST /api/candles/ingest` is received, **Then** a 409 Conflict response is returned
- [ ] **Given** ingestion for one interval fails after retries, **When** other intervals remain, **Then** the failed interval is skipped and remaining intervals continue; the result includes error details for the failed interval
- [ ] **Given** a transient HTTP error occurs during a batch fetch, **When** the service retries, **Then** exponential backoff is applied up to the configured max retries
- [ ] **Given** the configured max ingestion timeout is reached, **When** ingestion is in progress, **Then** the service stops and returns results collected so far
- [ ] **Given** the ingestion is in progress, **When** structured logs are inspected, **Then** logs show batch progress (batches fetched, inserted counts) per interval
- [ ] **Given** multiple batch requests, **When** observing timing between requests, **Then** at least 200ms delay exists between consecutive Hyperliquid API calls
- [ ] **Given** candles are stored, **Then** the Symbol field contains the coin symbol (e.g., `BTC`) not the display name (e.g., `BTC-PERP`)

### Release Notes Information

- **Heading**: Candle Data Ingestion from Hyperliquid
- **Release note type**: Feature
- **Release Note Summary**: Batch ingest historical candle data from Hyperliquid into local SQLite storage for any supported symbol and interval, with incremental sync, retry on transient errors, and rate limiting.
- **Release Notes Audience**: Product
- **Breaking Change**: No

---

## Related Features

- **F1** — Candle Data Persistence provides the repository and database that this service writes to
- **F3** — Backtest Replay Engine consumes the candle data ingested by this service
- **F4** — Backtest API depends on candle data being available in the database
- **F4** — Backtest API depends on candle data being available in the database
