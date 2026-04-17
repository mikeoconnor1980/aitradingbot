---
applyTo: ".agent-context/3-develop/build/changes/20260327-f2-candle-ingestion-service-changes.md"
currentAgent: "Implementation Reviewer"
agentStartedAt: "2026-03-27T22:00:00Z"
status: "in-review"
lastUpdated: "2026-03-27T22:00:00Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: F2 — Candle Ingestion Service

## Overview

Build a candle ingestion service that batch-fetches historical OHLCV candle data from Hyperliquid's REST API and persists it to the local SQLite database via the repository established in F1, with an API endpoint to trigger ingestion with incremental sync support.

## PBI Details

**PBI ID:** Draft  
**Status:** Draft  
**Depends On:** F1 (Candle Data Persistence)  
**PRD:** candle-persistence-backtesting-prd.md  

> As an **Operator**, I want to **ingest historical candle data from Hyperliquid for any supported symbol and interval** so that **I have a complete local dataset for backtesting without manual data management**.

### Acceptance Criteria

- [ ] Given the database is empty, When `POST /api/candles/ingest` is called with `symbol=BTC` and `intervals=[15m, 1h, 4h]`, Then candles are fetched from the configured default start date to now
- [ ] Given ingestion completes, Then the response includes counts: total fetched, total inserted, total skipped, per-interval breakdown (including error field)
- [ ] Given Hyperliquid returns no data for a time range, Then the service stops pagination for that interval gracefully
- [ ] Given the database already contains candles, When ingestion is re-run, Then duplicates are skipped (no errors, no duplicate rows)
- [ ] Given the database has candles up to timestamp T for a given symbol/interval, When ingestion is called without `startTime`, Then fetching begins from T+1
- [ ] Given no new candles exist on Hyperliquid, Then the response shows 0 inserted
- [ ] Given a request with an unknown symbol, When `POST /api/candles/ingest` is called, Then a 400 response with a validation error is returned
- [ ] Given a request with an unsupported interval, When `POST /api/candles/ingest` is called, Then a 400 response listing valid intervals is returned
- [ ] Given an ingestion is already running, When a second `POST /api/candles/ingest` is received, Then a 409 Conflict response is returned
- [ ] Given ingestion for one interval fails after retries, When other intervals remain, Then the failed interval is skipped and remaining intervals continue; the result includes error details for the failed interval
- [ ] Given a transient HTTP error occurs during a batch fetch, When the service retries, Then exponential backoff is applied up to the configured max retries
- [ ] Given the configured max ingestion timeout is reached, When ingestion is in progress, Then the service stops and returns results collected so far
- [ ] Given the ingestion is in progress, When structured logs are inspected, Then logs show batch progress (batches fetched, inserted counts) per interval
- [ ] Given multiple batch requests, When observing timing between requests, Then at least 200ms delay exists between consecutive Hyperliquid API calls
- [ ] Given candles are stored, Then the Symbol field contains the coin symbol (e.g., `BTC`) not the display name (e.g., `BTC-PERP`)

## Objectives

- Add a new `GetCandleSnapshotsAsync(asset, timeframe, startTime, endTime)` method to `IHyperliquidRestClient` for forward pagination
- Create `ICandleIngestionService` interface and `CandleIngestionService` implementation with batch pagination, mapping, rate limiting, and upsert logic
- Create `CandleIngestionOptions` configuration class with batch delay, max retries, timeout, and default start date
- Create `IngestCandlesCommand` MediatR command and handler orchestrating the ingestion workflow
- Create `CandlesController` with `POST /api/candles/ingest` endpoint including concurrency guard, validation, and proper response types
- Add `IngestionAlreadyRunningException` with 409 mapping in `HttpGlobalExceptionFilter`
- Register all new services and configuration in `Program.cs`
- Write comprehensive unit and integration tests for all layers

### Discovery References

- F1 (Candle Data Persistence) is **not yet implemented**. This plan assumes F1 delivers: `Candle` entity, `ICandleRepository` (with `BulkInsertAsync`, `GetLatestTimestampAsync`), `TradePilotDbContext`, `AddPersistence()` extension
- Existing Polly resilience pipeline on `IHyperliquidRestClient` handles HTTP-level retry (429/5xx) with exponential backoff — F2's retry logic handles ingestion-level retry per interval
- `CandleSnapshotPayload` already carries both `StartTime` and `EndTime` as `long` fields — no wire model changes needed
- `HyperliquidAssetMapper.ToCoin()` strips `-PERP` suffix but does not validate against known symbols; `IsValidTimeframe()` validates intervals
- `HyperliquidCandle` wire model includes `NumTrades` (int) which `CandleDto` lacks — mapping to `Candle` entity captures this
- No 409 Conflict exception mapping exists in current `HttpGlobalExceptionFilter`

### Project Patterns

- `src/TradePilot.Application/Abstractions/Services/IHyperliquidRestClient.cs` — Interface placement pattern for cross-layer services
- `src/TradePilot.Infrastructure/Services/HyperliquidRestClient.cs` — REST client implementation with `PostInfoAsync<T>` and candle mapping
- `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidAssetMapper.cs` — Static validation: `ToCoin()`, `IsValidTimeframe()`, `GetIntervalMs()`
- `src/TradePilot.Infrastructure/Hyperliquid/Models/HyperliquidCandle.cs` — Wire model for candle API response
- `src/TradePilot.Application/Abstractions/Configuration/HyperliquidOptions.cs` — Options class with `SectionName` constant + data annotations
- `src/TradePilot.Application/Abstractions/Commands/Command.cs` — `Command<T>` base record for MediatR commands
- `src/TradePilot.Api/Infrastructure/ApiController.cs` — Base controller with `IMediator` + `IdentityService`
- `src/TradePilot.Api/Infrastructure/Filters/HttpGlobalExceptionFilter.cs` — Exception-to-HTTP status mapping
- `src/TradePilot.Api/Controllers/MarketDataController.cs` — MediatR controller pattern for market data endpoints
- `src/TradePilot.Api/Program.cs` — DI composition root, options binding, Polly pipeline
- `tests/TradePilot.Api.Tests/Infrastructure/BaseControllerTests.cs` — Integration test base with `WebApplicationFactory`
- `tests/TradePilot.Api.Tests/Controllers/MarketDataControllerTests.cs` — Controller integration test pattern

### [x] Phase 1: REST Client Overload & Configuration

**Complexity**: Low | **Risk**: Low

- [x] Task 1.1: Add `GetCandlesAsync` overload with explicit `startTime` and `endTime` to `IHyperliquidRestClient`
  - Details: .agent-context/3-develop/build/plans/details/20260327-f2-candle-ingestion-service-phase-01-details.md#task-11-add-getcandlesasync-overload-to-interface

- [x] Task 1.2: Implement the new `GetCandlesAsync` overload in `HyperliquidRestClient`
  - Details: .agent-context/3-develop/build/plans/details/20260327-f2-candle-ingestion-service-phase-01-details.md#task-12-implement-getcandlesasync-overload

- [x] Task 1.3: Create `CandleIngestionOptions` configuration class
  - Details: .agent-context/3-develop/build/plans/details/20260327-f2-candle-ingestion-service-phase-01-details.md#task-13-create-candleingestionoptions

- [x] Task 1.4: Add configuration section to `appsettings.json`
  - Details: .agent-context/3-develop/build/plans/details/20260327-f2-candle-ingestion-service-phase-01-details.md#task-14-add-configuration-to-appsettings

- [x] Task 1.5: Write unit tests for the new `GetCandlesAsync` overload
  - Details: .agent-context/3-develop/build/plans/details/20260327-f2-candle-ingestion-service-phase-01-details.md#task-15-write-unit-tests-for-overload

- [x] Task 1.6: Build and run tests to verify Phase 1
  - Details: .agent-context/3-develop/build/plans/details/20260327-f2-candle-ingestion-service-phase-01-details.md#task-16-build-and-run-tests

### [x] Phase 2: Ingestion Service Implementation

**Complexity**: High | **Risk**: Medium

- [x] Task 2.1: Create `ICandleIngestionService` interface with DTOs
  - Details: .agent-context/3-develop/build/plans/details/20260327-f2-candle-ingestion-service-phase-02-details.md#task-21-create-icandleingestionservice-interface-with-dtos

- [x] Task 2.2: Implement `CandleIngestionService` with batch pagination, mapping, and rate limiting
  - Details: .agent-context/3-develop/build/plans/details/20260327-f2-candle-ingestion-service-phase-02-details.md#task-22-implement-candleingestionservice

- [x] Task 2.3: Register `ICandleIngestionService` in DI (`Program.cs`) as scoped with static concurrency guard
  - Details: .agent-context/3-develop/build/plans/details/20260327-f2-candle-ingestion-service-phase-02-details.md#task-23-register-in-di

- [x] Task 2.4: Write unit tests for `CandleIngestionService`
  - Details: .agent-context/3-develop/build/plans/details/20260327-f2-candle-ingestion-service-phase-02-details.md#task-24-write-unit-tests-for-candleingestionservice

- [x] Task 2.5: Build and run tests to verify Phase 2
  - Details: .agent-context/3-develop/build/plans/details/20260327-f2-candle-ingestion-service-phase-02-details.md#task-25-build-and-run-tests

### [x] Phase 3: API Endpoint & Exception Handling

**Complexity**: Medium | **Risk**: Low

- [x] Task 3.1: Create `IngestionAlreadyRunningException` and add 409 mapping to `HttpGlobalExceptionFilter`
  - Details: .agent-context/3-develop/build/plans/details/20260327-f2-candle-ingestion-service-phase-03-details.md#task-31-create-exception-and-409-mapping

- [x] Task 3.2: Create `IngestCandlesCommand` MediatR command and handler
  - Details: .agent-context/3-develop/build/plans/details/20260327-f2-candle-ingestion-service-phase-03-details.md#task-32-create-ingestcandlescommand

- [x] Task 3.3: Create `CandlesController` with `POST /api/candles/ingest` endpoint (includes adding `IsValidCoin()` to `HyperliquidAssetMapper` for symbol validation)
  - Details: .agent-context/3-develop/build/plans/details/20260327-f2-candle-ingestion-service-phase-03-details.md#task-33-create-candlescontroller

- [x] Task 3.4: Write integration tests for `CandlesController`
  - Details: .agent-context/3-develop/build/plans/details/20260327-f2-candle-ingestion-service-phase-03-details.md#task-34-write-integration-tests

- [x] Task 3.5: Build and run all tests to verify Phase 3
  - Details: .agent-context/3-develop/build/plans/details/20260327-f2-candle-ingestion-service-phase-03-details.md#task-35-build-and-run-all-tests

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: REST Client Overload & Configuration | Low | Low |
| Phase 2: Ingestion Service Implementation | High | Medium |
| Phase 3: API Endpoint & Exception Handling | Medium | Low |
| **Total** | **Medium** | **Low** |

### Scoping Notes

- F1 (Candle Data Persistence) must be implemented before this plan can be executed — `Candle` entity, `ICandleRepository`, `TradePilotDbContext`, and `AddPersistence()` must exist
- The existing Polly resilience pipeline handles HTTP-level retries on `IHyperliquidRestClient`; F2 adds ingestion-level retry per interval (retrying the entire fetch loop for a failed interval)
- The concurrency guard uses a `static SemaphoreSlim` on the ingestion service (registered as scoped), with `IngestionAlreadyRunningException` for 409 Conflict responses
- `HyperliquidAssetMapper.ToCoin()` accepts both raw coin symbols (`BTC`) and display names (`BTC-PERP`); the F2 validation step validates intervals via `IsValidTimeframe()` and symbol validation by attempting `ToCoin()` within a try-catch
- The `HyperliquidRestClient` new overload returns `List<CandleDto>` (not `HyperliquidCandle`) to maintain the existing abstraction; the ingestion service maps `CandleDto` → `Candle` entity adding `Symbol`, `Interval`, and `NumTrades` fields
- Since `CandleDto` lacks `NumTrades`, the new overload will return a new `CandleSnapshotDto` that includes `NumTrades` — or the ingestion service uses a separate internal method. The plan uses a new `CandleSnapshotDto` to preserve the clean layering

## Dependencies

- F1 — Candle Data Persistence (`Candle` entity, `ICandleRepository`, `TradePilotDbContext`)
- Microsoft.Extensions.Options (already in `TradePilot.Application.csproj`)
- MediatR 14.1.0 (already in `TradePilot.Application.csproj`)
- Polly resilience pipeline (already configured in `Program.cs`)

## Success Criteria

- `POST /api/candles/ingest` with valid symbol and intervals returns 200 OK with `IngestionResult`
- Incremental sync works: re-running ingestion only fetches new candles
- Concurrent requests return 409 Conflict
- Invalid symbol/interval returns 400 Bad Request
- Rate limiting enforces configurable delay between batch API calls
- All unit and integration tests pass
- Solution builds without errors or warnings

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-03-27T20:28:30Z | 2026-03-27T20:41:01Z |
| Plan Reviewer | plan-reviewed | 2026-03-27T20:41:35Z | 2026-03-27T20:52:20Z |
| Plan Implementer | implemented | 2026-03-27T21:00:00Z | 2026-03-27T21:10:00Z |
| Implementation Reviewer | in-review | 2026-03-27T22:00:00Z | |
