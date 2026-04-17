<!-- markdownlint-disable-file -->
# Release Changes: F2 — Candle Ingestion Service

**Related Plan**: 20260327-f2-candle-ingestion-service-plan.instructions.md
**Implementation Date**: 2026-03-27

## Summary

Implements the Candle Ingestion Service: a new `GetCandlesAsync` overload on `IHyperliquidRestClient`, `CandleIngestionOptions` configuration, `ICandleIngestionService` with batch pagination/rate limiting/upsert logic, a MediatR `IngestCandlesCommand` handler, and a `POST /api/candles/ingest` endpoint with concurrency guard and validation.

## Changes

### Added

<!-- Phase 1: REST Client Overload & Configuration -->
- src/TradePilot.Application/MarketData/Models/CandleSnapshotDto.cs: Added rich candle snapshot DTO including NumTrades for ingestion pagination use cases.
- src/TradePilot.Application/Abstractions/Configuration/CandleIngestionOptions.cs: Added typed ingestion options with defaults and data annotation validation.
- tests/TradePilot.Api.Tests/Services/HyperliquidRestClientCandleSnapshotTests.cs: Added MSTest coverage for request range payload, full mapping behavior, and invalid timeframe validation.

<!-- Phase 2: Ingestion Service Implementation -->
- src/TradePilot.Application/Abstractions/Services/ICandleIngestionService.cs: Added the ingestion service contract exposed from the Application layer.
- src/TradePilot.Application/Candles/Models/IngestionRequest.cs: Added the request DTO for symbol, intervals, and optional time bounds.
- src/TradePilot.Application/Candles/Models/IngestionResult.cs: Added the aggregate ingestion result DTO with totals and elapsed time.
- src/TradePilot.Application/Candles/Models/IntervalResult.cs: Added the per-interval result DTO with fetched/inserted/skipped counts and error text.
- src/TradePilot.Application/Abstractions/Exceptions/IngestionAlreadyRunningException.cs: Added the custom exception used by the static concurrency guard.
- src/TradePilot.Infrastructure/Services/CandleIngestionService.cs: Implemented batching, retry/backoff, timeout handling, ordering, mapping, persistence, logging, and concurrency protection.
- tests/TradePilot.Api.Tests/Services/CandleIngestionServiceTests.cs: Added unit tests for default start behavior, incremental sync, pagination, delay, timeout, interval isolation, and concurrency.

<!-- Phase 3: API Endpoint & Exception Handling -->
- src/TradePilot.Application/Candles/Commands/IngestCandlesCommand.cs: Adds the ingestion command and thin handler delegating to ICandleIngestionService.
- src/TradePilot.Api/Models/IngestCandlesRequest.cs: Adds the API request model with required symbol and interval validation attributes.
- src/TradePilot.Api/Controllers/CandlesController.cs: Adds the ingestion endpoint with symbol and interval validation before dispatching the MediatR command.
- tests/TradePilot.Api.Tests/Controllers/CandlesControllerTests.cs: Adds integration coverage for success, validation failures, unknown symbols, and ingestion conflict handling.

### Modified

<!-- Phase 1: REST Client Overload & Configuration -->
- src/TradePilot.Application/Abstractions/Services/IHyperliquidRestClient.cs: Added GetCandleSnapshotsAsync overload signature with explicit start/end time bounds.
- src/TradePilot.Infrastructure/Services/HyperliquidRestClient.cs: Implemented GetCandleSnapshotsAsync using candleSnapshot request payload and CandleSnapshotDto mapping including NumTrades.
- src/TradePilot.Api/appsettings.json: Added CandleIngestion configuration section with phase-specified defaults.

<!-- Phase 2: Ingestion Service Implementation -->
- src/TradePilot.Api/Program.cs: Bound CandleIngestionOptions with startup validation and registered ICandleIngestionService as scoped.

<!-- Phase 3: API Endpoint & Exception Handling -->
- src/TradePilot.Api/Infrastructure/Filters/HttpGlobalExceptionFilter.cs: Maps IngestionAlreadyRunningException to 409 Conflict with the ingestion_conflict error code.
- src/TradePilot.Infrastructure/Hyperliquid/HyperliquidAssetMapper.cs: Adds IsValidCoin so the API can validate supported raw coin symbols after stripping any -PERP suffix.

### Removed

## Test Results

<!-- Phase 1: REST Client Overload & Configuration -->
- HyperliquidRestClientCandleSnapshotTests: 2/2 passed
- TradePilot.Domain.Tests: 7/7 passed
- TradePilot.Application.Tests: 5/5 passed
- TradePilot.Persistence.Tests: 8/8 passed
- TradePilot.Infrastructure.Tests: 30/30 passed
- TradePilot.Api.Tests: 51/51 passed

<!-- Phase 2: Ingestion Service Implementation -->
- CandleIngestionServiceTests: 7/7 passed
- TradePilot.Api.Tests (cumulative): 58/58 passed

<!-- Phase 3: API Endpoint & Exception Handling -->
- CandlesControllerTests: 6/6 passed
- TradePilot.Api.Tests (final): 64/64 passed
- All projects (final full run): PASS

## Issues

<!-- Phase 1: REST Client Overload & Configuration -->
- Pre-existing NU1903 vulnerability advisory for AutoMapper 12.0.1 (not introduced by this phase).

<!-- Phase 2: Ingestion Service Implementation -->
- Initial build failed: CandleIngestionService was missing CandleSnapshotDto namespace import. Resolved.
- Two new tests failed initially: Moq Returns callbacks didn't match five-parameter GetCandleSnapshotsAsync. Resolved by fixing signatures.

<!-- Phase 3: API Endpoint & Exception Handling -->
- None — all tasks completed cleanly with pre-existing NU1903 advisory only.

## Design Decisions

<!-- Phase 1: REST Client Overload & Configuration -->
- Implemented as GetCandleSnapshotsAsync (not an overload of GetCandlesAsync) to avoid changing behavior of existing consumers.
- New overload intentionally does not apply Take(500) or descending sort, preserving full response payload for forward pagination.
- Added CandleSnapshotDto instead of extending CandleDto to keep existing contracts stable while adding NumTrades.

<!-- Phase 2: Ingestion Service Implementation -->
- Sorted each fetched candle batch by timestamp before persistence and cursor advancement to normalize ordering from API.
- Used Candle.Create(...) factory pattern for entity mapping due to private setters on the domain entity.
- Added IngestionAlreadyRunningException in Phase 2 (not Phase 3) as the service contract and tests require it for concurrency behaviour.

<!-- Phase 3: API Endpoint & Exception Handling -->
- IngestionAlreadyRunningException left unchanged (already created in Phase 2); only HTTP mapping added in this phase.
- Request validation kept at controller boundary, matching existing API pattern; MediatR handler is a thin orchestration layer.

## Review Hints

- Validate whether downstream ingestion logic expects snapshot ordering from API as-is or requires explicit ascending ordering before persistence.
- Confirm Program.cs options binding/ValidateOnStart wiring for CandleIngestionOptions is correct.
- Review inserted/skipped counts in CandleIngestionService — inserted tracks attempted inserts since BulkInsertAsync does not return affected-row counts.
- Review static semaphore behavior in CandleIngestionService together with scoped registration in Program.cs to confirm cross-request concurrency model.
- Review whether timeout result shape should remain as partial-success response with interval error text.
- Review whether framework-generated model validation responses for missing/empty request fields should be normalized to the Envelope shape for consistency.
- Review whether raw lowercase coin inputs should be normalized to canonical uppercase symbols before persistence.

## Release Summary

All 3 phases and 16 tasks implemented successfully. The Candle Ingestion Service is now complete:

- `GetCandleSnapshotsAsync` overload added to `IHyperliquidRestClient` and implemented in `HyperliquidRestClient` with `CandleSnapshotDto` (including `NumTrades`)
- `CandleIngestionOptions` typed configuration class with defaults bound at startup with `ValidateOnStart`
- `ICandleIngestionService` interface with `IngestionRequest`/`IngestionResult`/`IntervalResult` DTOs
- `CandleIngestionService` with forward batch pagination, per-interval retry/backoff, configurable timeout, rate limiting (between-batch delay), incremental sync via `GetLatestTimestampAsync`, and `SemaphoreSlim` concurrency guard
- `IngestionAlreadyRunningException` mapped to 409 Conflict in `HttpGlobalExceptionFilter`
- `IngestCandlesCommand` + handler (thin MediatR orchestration)
- `IsValidCoin()` helper added to `HyperliquidAssetMapper`
- `CandlesController` with `POST /api/candles/ingest`: symbol + interval validation → 400, concurrency → 409, success → 200 with `IngestionResult`
- 15 new tests; all 64 tests across the solution pass

**Files created**: 12 | **Files modified**: 6 | **Tests passing**: 64/64
