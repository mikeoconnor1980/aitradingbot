---
applyTo: ".agent-context/3-develop/build/changes/20260328-historical-candles-chart-changes.md"
currentAgent: "None"
agentStartedAt: "2026-03-28T20:42:51Z"
status: "planned"
lastUpdated: "2026-03-28T20:42:51Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: Historical Candles from Local Database on Price Chart

## Overview

Serve the Market Data price chart from the local SQLite candle database instead of the Hyperliquid REST API. The local DB already contains historical candles back to 2019 (ingested from Binance). Recent candles (last few hours) continue to come from Hyperliquid for real-time accuracy. The chart gains deep historical scrollback with faster load times and no exchange API dependency.

## PBI Details

**PBI ID:** Draft
**Status:** Draft
**Feature:** Historical Candles Chart
**PBI Path:** `.agent-context/3-develop/backlog/draft/pbi-draft-historical-candles-chart.md`

### Acceptance Criteria

- [ ] **Given** the Market Data page is loaded with a symbol that has historical data in the local database, **When** the price chart renders, **Then** it displays historical candles from the local database extending beyond the ~10 day Hyperliquid limit
- [ ] **Given** the user is viewing the price chart, **When** they scroll or navigate to recent time periods (e.g., last 24 hours), **Then** candles are sourced from Hyperliquid for real-time accuracy
- [ ] **Given** the local database contains years of 15-minute candle data, **When** the chart loads, **Then** it displays a reasonable default window and supports scrolling/paging backward without degraded performance
- [ ] **Given** the user selects a historical time range, **When** the chart fetches data, **Then** the API responds with paginated results within acceptable response times
- [ ] **Given** the existing backtest functionality, **When** historical candle chart features are deployed, **Then** backtest ingestion and execution remain fully functional and unaffected

## Objectives

- Create a new CQRS query that reads candles from `ICandleRepository` for chart display
- Add a new API endpoint `GET /api/market/candles/history` with pagination support
- Update the Angular `MarketDataService` with a method to call the new endpoint
- Update `MarketDataComponent` to load initial candles from the local DB and fall back to Hyperliquid for "load more" when DB data runs out
- Unit tests for the new query handler and integration tests for the controller

### Discovery References

- `ICandleRepository.GetCandlesAsync(symbol, interval, startTime, endTime, source?)` already exists — no new repo methods needed
- `CandleRepository` returns candles ordered by timestamp ascending — matches chart expectations
- `PriceChartComponent` already has `prependCandles()` and `loadMoreCandles` event for infinite scroll — frontend pagination is already wired
- `CandleDto` already exists in `TradingApp.Application.MarketData.Models` — reuse for the new endpoint
- The Candle entity uses `Symbol` (e.g., "BTC") while the API uses `Asset` (e.g., "BTC-PERP") — the handler must map between them (strip `-PERP` suffix)
- `MarketDataController` inherits from `ApiController` which provides `Mediator` and `IdentityService`
- Frontend `Candle` model matches `CandleDto` shape (timestamp, OHLCV)

### Project Patterns

- `src/TradingApp.Application/MarketData/Queries/GetCandlesQuery.cs` — Existing CQRS query pattern (sealed record + QueryHandler)
- `src/TradingApp.Api/Controllers/MarketDataController.cs` — Controller pattern with MediatR dispatch
- `src/TradingApp.Persistence/Repositories/CandleRepository.cs` — Repository implementation pattern
- `frontend/trading-ui/src/app/core/services/market-data.service.ts` — Angular service pattern with `ApiRestClient`

### Phase 1: Backend — New CQRS Query & API Endpoint

**Complexity**: Low | **Risk**: Low

- [ ] Task 1.1: Create `GetHistoricalCandlesQuery` and handler
  - Details: .agent-context/3-develop/build/plans/details/20260328-historical-candles-chart-phase-01-details.md#task-11-create-gethistoricalcandlesquery-and-handler

- [ ] Task 1.2: Add `GetHistoricalCandlesAsync` endpoint to `MarketDataController`
  - Details: .agent-context/3-develop/build/plans/details/20260328-historical-candles-chart-phase-01-details.md#task-12-add-gethistoricalcandlesasync-endpoint

- [ ] Task 1.3: Write unit tests for `GetHistoricalCandlesQueryHandler`
  - Details: .agent-context/3-develop/build/plans/details/20260328-historical-candles-chart-phase-01-details.md#task-13-write-unit-tests

- [ ] Task 1.4: Write controller integration tests
  - Details: .agent-context/3-develop/build/plans/details/20260328-historical-candles-chart-phase-01-details.md#task-14-write-controller-integration-tests

- [ ] Task 1.5: Build solution and run all tests
  - Details: .agent-context/3-develop/build/plans/details/20260328-historical-candles-chart-phase-01-details.md#task-15-build-and-test

### Phase 2: Frontend — Wire Chart to Local DB Endpoint

**Complexity**: Low | **Risk**: Low

- [ ] Task 2.1: Add `getHistoricalCandles()` method to `MarketDataService`
  - Details: .agent-context/3-develop/build/plans/details/20260328-historical-candles-chart-phase-02-details.md#task-21-add-gethistoricalcandles-method

- [ ] Task 2.2: Update `MarketDataComponent` to load initial candles from history endpoint
  - Details: .agent-context/3-develop/build/plans/details/20260328-historical-candles-chart-phase-02-details.md#task-22-update-marketdatacomponent

- [ ] Task 2.3: Update `onLoadMoreCandles` to use history endpoint with DB fallback
  - Details: .agent-context/3-develop/build/plans/details/20260328-historical-candles-chart-phase-02-details.md#task-23-update-onloadmorecandles

- [ ] Task 2.4: Manual smoke test — verify chart loads with historical data
  - Details: .agent-context/3-develop/build/plans/details/20260328-historical-candles-chart-phase-02-details.md#task-24-smoke-test

## Scoping Summary

| Phase | Complexity | Risk |
|-------|-----------|------|
| Phase 1: Backend — Query & API Endpoint | Low | Low |
| Phase 2: Frontend — Wire Chart to History | Low | Low |
| **Total** | **Low** | **Low** |

### Scoping Notes

- No new domain entities or migrations needed — `Candle` table already exists with all required data
- No new repository methods — `ICandleRepository.GetCandlesAsync()` already supports time-range queries with optional source filtering
- The `PriceChartComponent` already supports `prependCandles()` and infinite scroll — no chart library changes needed
- The main mapping concern is `Asset` ("BTC-PERP") → `Symbol` ("BTC") — handled in the query handler
- Default window: last 500 candles (≈5 days of 15m data) — reasonable initial load
- Pagination via `endTime` + `limit` parameters — same pattern as existing Hyperliquid endpoint

## Dependencies

- `ICandleRepository` (exists in `TradingApp.Application.Abstractions.Repositories`)
- `CandleDto` (exists in `TradingApp.Application.MarketData.Models`)
- `Candle` entity (exists in `TradingApp.Domain.Entities`)
- `MarketDataController` (exists — adding new endpoint)
- `MarketDataService` (exists — adding new method)
- `MarketDataComponent` (exists — modifying candle loading logic)
- Lightweight Charts library (already installed)

## Success Criteria

- The Market Data chart loads candles from the local SQLite database on initial page load
- Scrolling backward loads older candles from the DB with no perceptible delay
- Live price updates (via SignalR) continue to work for real-time candles
- The existing `GET /api/market/candles` (Hyperliquid) endpoint remains unchanged
- All existing and new tests pass
- Solution builds cleanly

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-03-28T20:42:51Z | 2026-03-28T20:42:51Z |
