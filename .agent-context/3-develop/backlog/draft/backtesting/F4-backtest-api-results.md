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

Expose the backtest replay engine (F3) via an HTTP API endpoint that accepts strategy configuration and date range parameters, runs the backtest synchronously, and returns a structured result with summary metrics and a detailed trade log.

### User Story

> As an **Operator**, I want to **trigger a backtest via a REST API call with symbol, date range, and strategy config** so that **I can run backtests programmatically and compare strategy parameter variations**.

### Business Value

Provides a programmatic interface to the backtest engine, enabling rapid strategy iteration. Multiple backtests can be run over the same data range with different configurations to compare performance. This is the primary interface for strategy validation before live deployment.

---

## Problem Statement

The backtest replay engine (F3) runs in-process but has no external interface. Without an API, the only way to trigger a backtest is through code. An HTTP endpoint is needed so the operator can trigger backtests programmatically (via curl, Postman, or future Angular UI) and receive structured results.

---

## Requirements

### Functional Requirements

#### Backtest Endpoint

- [ ] A `POST /api/backtests` endpoint exists in `TradingApp.Api`
- [ ] The endpoint accepts a JSON request body with: `symbol` (string, required), `intervals` (string array, required), `startDate` (ISO 8601 string, required), `endDate` (ISO 8601 string, required), `strategyConfig` (JSON object, required — strategy-specific parameters)
- [ ] The endpoint validates inputs:
  - `symbol` must be a supported symbol (e.g., "BTC")
  - `intervals` must contain valid intervals (e.g., ["15m", "1h", "4h"])
  - `startDate` must be before `endDate`
  - `strategyConfig` must be a valid configuration object
- [ ] The endpoint delegates to `IBacktestRunner` from F3 to execute the backtest
- [ ] The backtest runs synchronously and the result is returned in the HTTP response
- [ ] On success, the endpoint returns 200 with the `BacktestResult`

#### Request Validation & Error Handling

- [ ] Missing required fields return 400 with field-level validation errors
- [ ] Invalid date range (end before start) returns 400 with descriptive message
- [ ] Invalid strategy config returns 400 with config validation details
- [ ] Unknown symbol returns 400 with a list of supported symbols
- [ ] Invalid intervals return 400 with a list of valid interval values
- [ ] No candle data in DB for the requested range returns 404 with message: "No candle data found for {symbol}/{interval} between {startDate} and {endDate}"
- [ ] Internal errors during backtest execution return 500 with error detail

#### BacktestResult Response

- [ ] The response contains summary metrics: `totalTrades`, `winningTrades`, `losingTrades`, `winRate` (%), `totalPnl`, `maxDrawdown`, `averageTradePnl`, `averageHoldTimeMinutes`, `hedgesOpened`, `totalFeesPaid`
- [ ] The response contains a `trades` array with individual trade details: `entryTime` (ISO 8601), `exitTime` (ISO 8601), `entryPrice`, `exitPrice`, `side` (long/short), `size`, `pnl`, `fees`
- [ ] The response contains metadata: `symbol`, `intervals`, `startDate`, `endDate`, `candlesReplayed`, `elapsedMs`
- [ ] When the backtest completes with no trades (strategy never triggered), the trades array is empty and all summary metrics are zero

### Non-Functional Requirements

- [ ] The endpoint responds within 30 seconds for a 1-year backtest (~35K candles)
- [ ] Request/response DTOs are well-typed and serialise cleanly to JSON
- [ ] The endpoint uses the standard `Envelope` response wrapper consistent with other API endpoints
- [ ] Structured logging captures: backtest request received, backtest started, backtest completed (with elapsed time and trade count)

---

## User Flow

### Happy Path

1. Operator sends `POST /api/backtests` with:
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
       "slippage": 0
     }
   }
   ```
2. API validates inputs — all valid
3. `IBacktestRunner` executes the backtest synchronously
4. API returns 200 with `BacktestResult`:
   ```json
   {
     "symbol": "BTC",
     "intervals": ["15m", "1h", "4h"],
     "startDate": "2024-01-01T00:00:00Z",
     "endDate": "2024-12-31T23:59:59Z",
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
5. Operator adjusts `strategyConfig` parameters and re-runs to compare results

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
| Backtest engine throws runtime error | 500: Internal server error with logged detail |

---

## Technical Considerations

### API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/backtests` | Run a backtest with the specified parameters; returns results synchronously |

### Key Components

| Component | Layer | Action |
|-----------|-------|--------|
| `BacktestController` | `TradingApp.Api` | API controller exposing the backtest endpoint |
| `BacktestRequest` | `TradingApp.Application` | Request DTO with validation attributes |
| `BacktestResult` | `TradingApp.Application` | Response DTO with summary metrics and trade log (defined in F3) |
| `BacktestTrade` | `TradingApp.Application` | Individual trade DTO (defined in F3) |
| `IBacktestRunner` | `TradingApp.Application` | Backtest orchestrator interface (defined in F3) |
| `BacktestRequestValidator` | `TradingApp.Application` | Validates request parameters before execution |

### Request DTO

```csharp
public class BacktestRequest
{
    public string Symbol { get; set; }
    public string[] Intervals { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public JsonElement StrategyConfig { get; set; }
}
```

### Validation Rules

| Field | Rule |
|-------|------|
| `Symbol` | Required, must be in supported symbols list |
| `Intervals` | Required, non-empty, each must be a valid interval |
| `StartDate` | Required, valid date |
| `EndDate` | Required, valid date, must be after StartDate |
| `StrategyConfig` | Required, must be valid JSON, strategy-specific validation |

---

## Out of Scope

- Backtest result persistence to database
- Comparison endpoint for multiple backtest results
- Batch/sweep endpoint for multiple configs
- Angular frontend for triggering backtests or viewing results
- WebSocket/SignalR progress reporting during execution
- Pagination of the trade log
- CSV/Excel export of results

---

## Open Questions

- [ ] Should backtest results be persisted to the database for later retrieval and comparison, or is synchronous return sufficient for POC?
- [ ] Should the trade log be paginated or capped (e.g., max 1000 trades) to prevent very large response payloads for multi-year backtests?
- [ ] Should there be a `GET /api/backtests/validate` endpoint that checks whether sufficient candle data exists for a given range before running?

---

## Acceptance Criteria

- [ ] **Given** valid parameters, **When** `POST /api/backtests` is called, **Then** a backtest runs and the result is returned in the response body with 200 status
- [ ] **Given** an invalid date range (end before start), **When** the endpoint is called, **Then** a 400 error is returned with a descriptive message
- [ ] **Given** no candle data exists for the requested range, **When** the endpoint is called, **Then** a 404 error is returned with a message indicating missing data
- [ ] **Given** an invalid strategy config, **When** the endpoint is called, **Then** a 400 error is returned with config validation details
- [ ] **Given** a missing required field, **When** the endpoint is called, **Then** a 400 error is returned with field-level validation errors
- [ ] **Given** an unknown symbol, **When** the endpoint is called, **Then** a 400 error is returned listing supported symbols
- [ ] **Given** a backtest completes with trades, **Then** the result includes summary metrics (totalTrades, winRate, totalPnl, maxDrawdown, etc.) and a trades array with entry/exit details
- [ ] **Given** a backtest completes with no trades, **Then** the trades array is empty and summary metrics are zero
- [ ] **Given** a 1-year backtest with ~35K 15m candles, **When** the endpoint is called, **Then** the response is returned within 30 seconds
- [ ] **Given** the same inputs, **When** the endpoint is called twice, **Then** identical results are returned (deterministic)

### Release Notes Information

- **Heading**: Backtest API
- **Release note type**: Feature
- **Release Note Summary**: Trigger backtests via REST API with configurable strategy parameters and date ranges, receiving detailed performance metrics and trade logs in the response.
- **Release Notes Audience**: Product
- **Breaking Change**: No

---

## Related Features

- **F1** — Candle Data Persistence provides the data queried during backtest execution
- **F2** — Candle Ingestion Service populates the database with the data needed for backtests
- **F3** — Backtest Replay Engine contains the core execution logic invoked by this API
