# PBI Specification: F4 — Backtest API & Results

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-27
**PRD:** [candle-persistence-backtesting-prd.md](../../prd/candle-persistence-backtesting-prd.md)
**Implementation Phase:** 4
**Risk Level:** Low
**Depends On:** F1 (Candle Data Persistence), F3 (Backtest Replay Engine)

---

## Summary

Expose the backtest replay engine (F3) via HTTP API endpoints that accept strategy configuration and date range parameters, run the backtest synchronously, persist the result to SQLite, and return a structured response with summary metrics and a full trade log. Additionally, provide a retrieval endpoint for previously saved results and a validation endpoint to check candle data coverage before running.

### User Story

> As an **Operator**, I want to **trigger a backtest via a REST API call with symbol, date range, and strategy config** so that **I can run backtests programmatically and compare strategy parameter variations**.

### Business Value

Provides a programmatic interface to the backtest engine, enabling rapid strategy iteration. Multiple backtests can be run over the same data range with different configurations to compare performance. Persisted results allow reviewing and comparing past runs without re-executing. This is the primary interface for strategy validation before live deployment.

---

## Problem Statement

The backtest replay engine (F3) runs in-process but has no external interface. Without an API, the only way to trigger a backtest is through code. HTTP endpoints are needed so the operator can trigger backtests programmatically (via curl, Postman, or future Angular UI), validate data availability beforehand, retrieve past results, and receive structured output.

---

## Requirements

### Functional Requirements

#### Run Backtest Endpoint — `POST /api/backtests`

- [ ] A `POST /api/backtests` endpoint exists in `TradePilot.Api`
- [ ] The endpoint accepts a JSON request body with: `symbol` (string, required), `intervals` (string array, required), `startDate` (ISO 8601 string, required), `endDate` (ISO 8601 string, required), `strategyConfig` (strongly-typed object, required)
- [ ] The `strategyConfig` is a strongly-typed DTO with fields: `gridLevels` (int), `gridSpacing` (decimal), `takeProfitPercent` (decimal), `breakdownThreshold` (decimal), `makerFee` (decimal), `takerFee` (decimal), `slippage` (decimal), `positionSize` (decimal), `leverage` (decimal), `stopLossPercent` (decimal)
- [ ] The endpoint validates inputs:
  - `symbol` must be a supported symbol (e.g., "BTC")
  - `intervals` must contain valid intervals (e.g., ["15m", "1h", "4h"])
  - `startDate` must be before `endDate`
  - `strategyConfig` fields are validated (e.g., `gridLevels > 0`, `leverage > 0`, `makerFee >= 0`)
- [ ] The endpoint delegates to `IBacktestRunner` from F3 to execute the backtest
- [ ] The backtest runs synchronously and the result is returned in the HTTP response
- [ ] The backtest supports cancellation via `CancellationToken` (propagated from `HttpContext.RequestAborted`) and a configurable server-side maximum timeout (default: 5 minutes)
- [ ] On success, the result is persisted to the SQLite database and the endpoint returns 200 with the `BacktestResult` including the assigned `backtestId`

#### Retrieve Backtest Result — `GET /api/backtests/{id}`

- [ ] A `GET /api/backtests/{id}` endpoint returns a previously persisted backtest result by its ID
- [ ] If the ID does not exist, the endpoint returns 404
- [ ] The response format is identical to the POST response

#### Validate Data Coverage — `GET /api/backtests/validate`

- [ ] A `GET /api/backtests/validate` endpoint accepts query parameters: `symbol` (required), `intervals` (required, comma-separated)
- [ ] The endpoint returns a coverage report per interval showing the available date range in the database (e.g., `{ "BTC/15m": { "from": "2024-01-01T00:00:00Z", "to": "2024-12-31T23:45:00Z", "candleCount": 35040 } }`)
- [ ] If no data exists for a symbol/interval combination, that entry shows `null` for from/to and `0` for candleCount

#### Request Validation & Error Handling

- [ ] Missing required fields return 400 with field-level validation errors
- [ ] Invalid date range (end before start) returns 400 with descriptive message
- [ ] Invalid strategy config returns 400 with config validation details (e.g., `"gridLevels must be > 0"`)
- [ ] Unknown symbol returns 400 with a list of supported symbols
- [ ] Invalid intervals return 400 with a list of valid interval values
- [ ] No candle data in DB for the requested range returns 404 with message: "No candle data found for {symbol}/{interval} between {startDate} and {endDate}"
- [ ] Backtest cancellation (client disconnect or server timeout) returns 408 Request Timeout or logs cancellation
- [ ] Internal errors during backtest execution return 500 with error detail

#### BacktestResult Response

- [ ] The response contains an `id` field (GUID) uniquely identifying the persisted backtest run
- [ ] The response contains summary metrics: `totalTrades`, `winningTrades`, `losingTrades`, `winRate` (%), `totalPnl`, `maxDrawdown`, `averageTradePnl`, `averageHoldTimeMinutes`, `hedgesOpened`, `totalFeesPaid`
- [ ] The response contains a full `trades` array (uncapped) with individual trade details: `entryTime` (ISO 8601), `exitTime` (ISO 8601), `entryPrice`, `exitPrice`, `side` (long/short), `size`, `pnl`, `fees`
- [ ] The response contains metadata: `symbol`, `intervals`, `startDate`, `endDate`, `candlesReplayed`, `elapsedMs`
- [ ] The response contains the `strategyConfig` that was used (echoed back for reference)
- [ ] When the backtest completes with no trades (strategy never triggered), the trades array is empty and all summary metrics are zero

#### Result Persistence

- [ ] Backtest results are persisted to SQLite in the existing database
- [ ] The summary metrics and metadata are stored in a `BacktestRuns` table with columns for each metric
- [ ] The trade log is stored as a JSON blob column on the `BacktestRuns` row
- [ ] The strategy config used is stored as a JSON blob column for reproducibility
- [ ] Each persisted result has a GUID primary key and a `CreatedAt` timestamp

### Non-Functional Requirements

- [ ] The endpoint responds within 30 seconds for a 1-year backtest (~35K candles)
- [ ] A configurable server-side timeout (default: 5 minutes) cancels long-running backtests
- [ ] Request/response DTOs are strongly-typed and serialise cleanly to JSON
- [ ] The endpoint uses the standard `Envelope` response wrapper consistent with other API endpoints
- [ ] Structured logging captures: backtest request received, backtest started, backtest completed (with elapsed time and trade count), backtest cancelled (if applicable)
- [ ] No authentication required (POC — single operator)

---

## User Flow

### Happy Path

1. Operator optionally checks data coverage:
   ```
   GET /api/backtests/validate?symbol=BTC&intervals=15m,1h,4h
   ```
   Response:
   ```json
   {
     "BTC/15m": { "from": "2024-01-01T00:00:00Z", "to": "2024-12-31T23:45:00Z", "candleCount": 35040 },
     "BTC/1h": { "from": "2024-01-01T00:00:00Z", "to": "2024-12-31T23:00:00Z", "candleCount": 8760 },
     "BTC/4h": { "from": "2024-01-01T00:00:00Z", "to": "2024-12-31T20:00:00Z", "candleCount": 2190 }
   }
   ```

2. Operator sends `POST /api/backtests` with:
   ```json
   {
     "symbol": "BTC",
     "intervals": ["15m", "1h", "4h"],
     "startDate": "2024-01-01T00:00:00Z",
     "endDate": "2024-12-31T23:59:59Z",
     "strategyConfig": {
       "gridLevels": 10,
       "gridSpacing": 0.5,
       "takeProfitPercent": 1.0,
       "breakdownThreshold": -3.0,
       "makerFee": 0.0001,
       "takerFee": 0.00035,
       "slippage": 0,
       "positionSize": 100.0,
       "leverage": 3.0,
       "stopLossPercent": 5.0
     }
   }
   ```

3. API validates inputs — all valid

4. `IBacktestRunner` executes the backtest synchronously with `CancellationToken`

5. Result is persisted to SQLite

6. API returns 200 with `BacktestResult`:
   ```json
   {
     "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
     "symbol": "BTC",
     "intervals": ["15m", "1h", "4h"],
     "startDate": "2024-01-01T00:00:00Z",
     "endDate": "2024-12-31T23:59:59Z",
     "strategyConfig": {
       "gridLevels": 10,
       "gridSpacing": 0.5,
       "takeProfitPercent": 1.0,
       "breakdownThreshold": -3.0,
       "makerFee": 0.0001,
       "takerFee": 0.00035,
       "slippage": 0,
       "positionSize": 100.0,
       "leverage": 3.0,
       "stopLossPercent": 5.0
     },
     "candlesReplayed": 35040,
     "elapsedMs": 12500,
     "totalTrades": 847,
     "winningTrades": 612,
     "losingTrades": 235,
     "winRate": 72.3,
     "totalPnl": 4521.87,
     "maxDrawdown": -1234.56,
     "averageTradePnl": 5.34,
     "averageHoldTimeMinutes": 245,
     "hedgesOpened": 12,
     "totalFeesPaid": 89.23,
     "trades": [
       {
         "entryTime": "2024-01-02T08:15:00Z",
         "exitTime": "2024-01-02T12:30:00Z",
         "entryPrice": 42150.50,
         "exitPrice": 42361.25,
         "side": "long",
         "size": 0.001,
         "pnl": 0.21,
         "fees": 0.015
       }
     ]
   }
   ```

7. Operator retrieves a past result:
   ```
   GET /api/backtests/a1b2c3d4-e5f6-7890-abcd-ef1234567890
   ```

8. Operator adjusts `strategyConfig` parameters and re-runs to compare results

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| Missing `symbol` in request | 400: `"symbol is required"` |
| Missing `startDate` or `endDate` | 400: `"startDate and endDate are required"` |
| `endDate` before `startDate` | 400: `"endDate must be after startDate"` |
| Unknown symbol (e.g., "DOGE") | 400: `"Unknown symbol 'DOGE'. Supported: BTC"` |
| Invalid interval (e.g., "5m") | 400: `"Invalid interval '5m'. Valid: 15m, 1h, 4h"` |
| No candle data for date range | 404: `"No candle data found for BTC/15m between 2020-01-01 and 2020-12-31"` |
| Invalid strategy config | 400: Config validation errors (e.g., `"gridLevels must be > 0"`) |
| `gridLevels` is 0 or negative | 400: `"gridLevels must be > 0"` |
| `leverage` is 0 or negative | 400: `"leverage must be > 0"` |
| `positionSize` is 0 or negative | 400: `"positionSize must be > 0"` |
| Client disconnects mid-backtest | Backtest is cancelled; no result persisted |
| Server timeout exceeded (5 min) | 408: `"Backtest execution exceeded maximum timeout"` |
| Backtest result ID not found | 404: `"Backtest result not found"` |
| Backtest engine throws runtime error | 500: Internal server error with logged detail |

---

## Technical Considerations

### API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/backtests` | Run a backtest with the specified parameters; persist and return results |
| GET | `/api/backtests/{id}` | Retrieve a previously persisted backtest result by ID |
| GET | `/api/backtests/validate` | Check candle data coverage for a symbol and intervals |

### Key Components

| Component | Layer | Action |
|-----------|-------|--------|
| `BacktestController` | `TradePilot.Api` | API controller exposing all three endpoints |
| `BacktestRequest` | `TradePilot.Application` | Request DTO with strongly-typed `GridStrategyConfig` |
| `GridStrategyConfig` | `TradePilot.Application` | Strongly-typed strategy config DTO with validation |
| `BacktestResult` | `TradePilot.Application` | Response DTO with summary metrics and trade log (defined in F3) |
| `BacktestTrade` | `TradePilot.Application` | Individual trade DTO (defined in F3) |
| `IBacktestRunner` | `TradePilot.Application` | Backtest orchestrator interface (defined in F3) |
| `BacktestRequestValidator` | `TradePilot.Application` | Validates request parameters before execution |
| `IBacktestResultRepository` | `TradePilot.Application` | Repository interface for persisting/retrieving results |
| `BacktestResultRepository` | `TradePilot.Persistence` | SQLite implementation of result persistence |
| `CandleCoverageReport` | `TradePilot.Application` | DTO for validation endpoint response |

### Request DTO

```csharp
public class BacktestRequest
{
    public string Symbol { get; set; }
    public string[] Intervals { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public GridStrategyConfig StrategyConfig { get; set; }
}

public class GridStrategyConfig
{
    public int GridLevels { get; set; }
    public decimal GridSpacing { get; set; }
    public decimal TakeProfitPercent { get; set; }
    public decimal BreakdownThreshold { get; set; }
    public decimal MakerFee { get; set; }
    public decimal TakerFee { get; set; }
    public decimal Slippage { get; set; }
    public decimal PositionSize { get; set; }
    public decimal Leverage { get; set; }
    public decimal StopLossPercent { get; set; }
}
```

### Validation Rules

| Field | Rule |
|-------|------|
| `Symbol` | Required, must be in supported symbols list |
| `Intervals` | Required, non-empty, each must be a valid interval (15m, 1h, 4h) |
| `StartDate` | Required, valid date |
| `EndDate` | Required, valid date, must be after StartDate |
| `StrategyConfig` | Required |
| `GridLevels` | Required, must be > 0 |
| `GridSpacing` | Required, must be > 0 |
| `TakeProfitPercent` | Required, must be > 0 |
| `BreakdownThreshold` | Required (can be negative — represents percentage drop) |
| `MakerFee` | Required, must be >= 0 |
| `TakerFee` | Required, must be >= 0 |
| `Slippage` | Required, must be >= 0 |
| `PositionSize` | Required, must be > 0 |
| `Leverage` | Required, must be > 0 |
| `StopLossPercent` | Required, must be > 0 |

### Persistence Schema

```sql
CREATE TABLE BacktestRuns (
    Id TEXT PRIMARY KEY,          -- GUID
    Symbol TEXT NOT NULL,
    Intervals TEXT NOT NULL,      -- JSON array
    StartDate TEXT NOT NULL,      -- ISO 8601
    EndDate TEXT NOT NULL,        -- ISO 8601
    StrategyConfig TEXT NOT NULL, -- JSON blob
    CandlesReplayed INTEGER NOT NULL,
    ElapsedMs INTEGER NOT NULL,
    TotalTrades INTEGER NOT NULL,
    WinningTrades INTEGER NOT NULL,
    LosingTrades INTEGER NOT NULL,
    WinRate REAL NOT NULL,
    TotalPnl REAL NOT NULL,
    MaxDrawdown REAL NOT NULL,
    AverageTradePnl REAL NOT NULL,
    AverageHoldTimeMinutes REAL NOT NULL,
    HedgesOpened INTEGER NOT NULL,
    TotalFeesPaid REAL NOT NULL,
    Trades TEXT NOT NULL,         -- JSON blob (full trade log)
    CreatedAt TEXT NOT NULL       -- ISO 8601
);
```

---

## Out of Scope

- Comparison endpoint for multiple backtest results side-by-side
- Batch/sweep endpoint for running multiple configs in one request
- Angular frontend for triggering backtests or viewing results
- WebSocket/SignalR progress reporting during execution
- Pagination of the trade log
- CSV/Excel export of results
- Authentication/authorization (POC phase)
- List/search of past backtest results (only GET by ID)

---

## Acceptance Criteria

- [ ] **Given** valid parameters, **When** `POST /api/backtests` is called, **Then** a backtest runs, the result is persisted to SQLite, and the full result (including `id`) is returned with 200 status
- [ ] **Given** an invalid date range (end before start), **When** the endpoint is called, **Then** a 400 error is returned with a descriptive message
- [ ] **Given** no candle data exists for the requested range, **When** the endpoint is called, **Then** a 404 error is returned with a message indicating missing data
- [ ] **Given** an invalid strategy config (e.g., gridLevels = 0), **When** the endpoint is called, **Then** a 400 error is returned with field-level config validation details
- [ ] **Given** a missing required field, **When** the endpoint is called, **Then** a 400 error is returned with field-level validation errors
- [ ] **Given** an unknown symbol, **When** the endpoint is called, **Then** a 400 error is returned listing supported symbols
- [ ] **Given** a backtest completes with trades, **Then** the result includes summary metrics (totalTrades, winRate, totalPnl, maxDrawdown, etc.) and a full trades array with entry/exit details
- [ ] **Given** a backtest completes with no trades, **Then** the trades array is empty and summary metrics are zero
- [ ] **Given** a 1-year backtest with ~35K 15m candles, **When** the endpoint is called, **Then** the response is returned within 30 seconds
- [ ] **Given** the same inputs, **When** the endpoint is called twice, **Then** identical results are returned (deterministic)
- [ ] **Given** a persisted backtest result, **When** `GET /api/backtests/{id}` is called with its ID, **Then** the full result is returned with 200 status
- [ ] **Given** a non-existent backtest ID, **When** `GET /api/backtests/{id}` is called, **Then** a 404 error is returned
- [ ] **Given** valid symbol and intervals, **When** `GET /api/backtests/validate?symbol=BTC&intervals=15m,1h,4h` is called, **Then** a coverage report is returned showing available date ranges and candle counts per interval
- [ ] **Given** no candle data exists for a symbol/interval pair, **When** the validate endpoint is called, **Then** that interval shows null dates and zero candle count
- [ ] **Given** a client disconnects during backtest execution, **When** the CancellationToken fires, **Then** the backtest is cancelled and no result is persisted
- [ ] **Given** a backtest exceeds the server-side timeout (5 minutes), **When** the timeout fires, **Then** the request returns 408 with a timeout message

### Release Notes Information

- **Heading**: Backtest API
- **Release note type**: Feature
- **Release Note Summary**: Trigger backtests via REST API with configurable strategy parameters and date ranges. Results are persisted to SQLite and include detailed performance metrics and full trade logs. Includes data coverage validation and result retrieval endpoints.
- **Release Notes Audience**: Product
- **Breaking Change**: No

---

## Related Features

- **F1** — Candle Data Persistence provides the data queried during backtest execution
- **F2** — Candle Ingestion Service populates the database with the data needed for backtests
- **F3** — Backtest Replay Engine contains the core execution logic invoked by this API
