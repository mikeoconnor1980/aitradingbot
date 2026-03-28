---
applyTo: ".agent-context/3-develop/build/changes/20260328-binance-futures-data-ingestion-changes.md"
currentAgent: "None"
agentStartedAt: "2026-03-28T10:38:56Z"
status: "complete"
lastUpdated: "2026-03-28T11:15:00Z"
---

<!-- markdownlint-disable-file -->

Add Binance USDⓈ-M Futures as a historical market data source for backtesting, including kline ingestion, funding rate history, and mark price klines — providing 6+ years of perpetual futures data versus Hyperliquid's ~60-day retention.

## PBI Details

**PBI ID:** Draft
**Status:** Draft
**Depends On:** F2 (Candle Ingestion Service — Hyperliquid, completed)
**PRD:** binance-futures-data-ingestion.md

> As a **strategy developer**, I want to **ingest historical Binance USDⓈ-M Futures data (candles, funding rates, mark prices)** so that **I can backtest grid strategies against years of real perpetual futures data that matches the product I plan to trade**.

### Acceptance Criteria

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

## Objectives

- Add `Source` property (string) to `Candle` entity with updated unique index `(Source, Symbol, Interval, Timestamp)`
- Create `IBinanceFuturesRestClient` / `BinanceFuturesRestClient` typed HttpClient targeting `https://fapi.binance.com`
- Create `BinanceAssetMapper` for symbol (`BTC` → `BTCUSDT`) and interval mapping
- Create `BinanceCandleIngestionService` with forward-pagination and binary-search gap-finding
- Create `FundingRate` domain entity with dedicated table and ingestion pipeline
- Support mark price kline ingestion via interval prefix convention (`mark-15m`)
- Expose `POST /api/candles/ingest/binance` and `POST /api/funding/ingest` API endpoints
- Respect Binance rate limits with configurable delay-based throttling

### Discovery References

- Existing Hyperliquid ingestion pipeline (F2) is the exact template: Controller → MediatR Command → `ICandleIngestionService` → `IHyperliquidRestClient` → `ICandleRepository`
- `CandleIngestionService` uses `static SemaphoreSlim Guard` for single-run concurrency guard — Binance gets its own separate guard
- `ICandleRepository.GetLatestTimestampAsync` and `GetCandlesAsync` do not filter by Source — must add `source` parameter so Binance ingestion can resume from its own latest timestamp independently of Hyperliquid data
- `IngestionAlreadyRunningException` only has a parameterless constructor with hardcoded message — must add a `(string message)` constructor for Binance/FundingRate-specific messages
- `RateLimitException` extends `HyperliquidApiException` — using for Binance 429 responses is functionally correct but semantically incorrect (tech debt: consider extracting a base `ExchangeApiException`)
- `CandleRepository.BulkInsertAsync` uses raw SQL `INSERT OR IGNORE` with 9 hardcoded columns — must be updated to 10 for `Source`
- Unique index `(Symbol, Interval, Timestamp)` must become `(Source, Symbol, Interval, Timestamp)` to allow both sources for overlapping time ranges
- `CandleSnapshotDto` is a source-agnostic normalized bridge format — reusable for Binance klines
- `IngestionRequest` / `IngestionResult` / `IntervalResult` models are source-agnostic — reusable
- Polly resilience handler on typed HttpClient registered in `Program.cs` (not Infrastructure) — same pattern for Binance
- `HyperliquidAssetMapper` only supports `5m`, `15m`, `1h`, `4h` intervals — Binance adds `1d` support
- All public Binance Futures API endpoints — no API key required
- Source stored as string (not enum) for extensibility
- Symbols stored as display names (`BTC`) not native (`BTCUSDT`) for consistency
- Auto-detect start date via binary search for tokens not available from 2019

### Project Patterns

- `src/TradingApp.Domain/Entities/Candle.cs` — Domain entity with sealed class, private ctor, static `Create` factory
- `src/TradingApp.Application/Abstractions/Services/IHyperliquidRestClient.cs` — REST client interface pattern
- `src/TradingApp.Application/Abstractions/Services/ICandleIngestionService.cs` — Ingestion service interface
- `src/TradingApp.Application/Abstractions/Configuration/CandleIngestionOptions.cs` — Options class with SectionName + DataAnnotations
- `src/TradingApp.Application/Abstractions/Repositories/ICandleRepository.cs` — Repository interface
- `src/TradingApp.Application/Candles/Commands/IngestCandlesCommand.cs` — MediatR command + handler co-location
- `src/TradingApp.Application/Candles/Models/IngestionResult.cs` — Result DTO pattern
- `src/TradingApp.Infrastructure/Services/CandleIngestionService.cs` — Ingestion engine with pagination, retry, gap detection
- `src/TradingApp.Infrastructure/Services/HyperliquidRestClient.cs` — Typed HttpClient implementation
- `src/TradingApp.Infrastructure/Hyperliquid/HyperliquidAssetMapper.cs` — Static asset/interval mapper
- `src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidCandle.cs` — Wire model with JsonPropertyName
- `src/TradingApp.Persistence/Repositories/CandleRepository.cs` — Raw SQL BulkInsertAsync pattern
- `src/TradingApp.Persistence/TradingAppDbContext.cs` — EF Core DbContext with decimal→double conversion
- `src/TradingApp.Api/Controllers/CandlesController.cs` — MediatR-dispatching controller on ApiController base
- `src/TradingApp.Api/Infrastructure/Filters/HttpGlobalExceptionFilter.cs` — Exception→HTTP status mapping
- `src/TradingApp.Api/Program.cs` — DI composition root with options binding and Polly pipeline
- `tests/TradingApp.Api.Tests/Services/CandleIngestionServiceTests.cs` — Service test pattern with Moq strict mocks
- `tests/TradingApp.Api.Tests/Controllers/CandlesControllerTests.cs` — Controller integration tests via WebApplicationFactory
- `tests/TradingApp.Persistence.Tests/Repositories/CandleRepositoryTests.cs` — In-memory SQLite repository tests
- `tests/TradingApp.Domain.Tests/Entities/CandleTests.cs` — Entity factory method tests

### [x] Phase 1: Domain & Persistence Foundation (Source Column)

**Complexity**: Medium | **Risk**: Medium

- [x] Task 1.1: Add `Source` property to `Candle` entity and update `Create` factory
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-01-details.md#task-11-add-source-property-to-candle-entity

- [x] Task 1.2: Update `TradingAppDbContext` with Source column configuration and new unique index
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-01-details.md#task-12-update-dbcontext-configuration

- [x] Task 1.3: Create EF migration `AddSourceToCandles`
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-01-details.md#task-13-create-ef-migration

- [x] Task 1.4: Update `CandleRepository` for Source column (BulkInsertAsync + query methods)
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-01-details.md#task-14-update-candlerepository-bulkinsertasync

- [x] Task 1.5: Update `CandleIngestionService` to pass `Source = "Hyperliquid"`
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-01-details.md#task-15-update-candleingestionservice-for-source

- [x] Task 1.6: Update existing tests for Source column changes
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-01-details.md#task-16-update-existing-tests

- [x] Task 1.7: Build and run all test projects
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-01-details.md#task-17-build-and-run-tests

### [x] Phase 2: Binance REST Client & Ingestion Infrastructure

**Complexity**: High | **Risk**: Medium

- [x] Task 2.1: Create `BinanceIngestionOptions` configuration class
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-02-details.md#task-21-create-binanceingestionoptions

- [x] Task 2.2: Create `IBinanceFuturesRestClient` interface
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-02-details.md#task-22-create-ibinancefuturesrestclient-interface

- [x] Task 2.3: Create `BinanceAssetMapper` static class
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-02-details.md#task-23-create-binanceassetmapper

- [x] Task 2.4: Create Binance wire models
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-02-details.md#task-24-create-binance-wire-models

- [x] Task 2.5: Implement `BinanceFuturesRestClient`
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-02-details.md#task-25-implement-binancefuturesrestclient

- [x] Task 2.6: Create `IBinanceCandleIngestionService` and implement `BinanceCandleIngestionService`
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-02-details.md#task-26-create-binancecandleingestionservice

- [x] Task 2.7: Write unit tests for all new components
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-02-details.md#task-27-write-unit-tests

- [x] Task 2.8: Build and run tests
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-02-details.md#task-28-build-and-run-tests

### [x] Phase 3: Binance Kline API Endpoint

**Complexity**: Medium | **Risk**: Low

- [x] Task 3.1: Create `IngestBinanceCandlesCommand` and handler
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-03-details.md#task-31-create-ingestbinancecandlescommand

- [x] Task 3.2: Add `POST /api/candles/ingest/binance` endpoint to `CandlesController`
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-03-details.md#task-32-add-binance-ingest-endpoint

- [x] Task 3.3: Wire up Binance DI registrations in `Program.cs`
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-03-details.md#task-33-wire-up-binance-di

- [x] Task 3.4: Add `BinanceIngestion` configuration to `appsettings.json`
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-03-details.md#task-34-add-appsettings-configuration

- [x] Task 3.5: Create controller tests for Binance ingestion endpoint
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-03-details.md#task-35-create-controller-tests

- [x] Task 3.6: Build and run tests
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-03-details.md#task-36-build-and-run-tests

### [x] Phase 4: FundingRate Entity & Ingestion

**Complexity**: High | **Risk**: Medium

- [x] Task 4.1: Create `FundingRate` domain entity
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-04-details.md#task-41-create-fundingrate-entity

- [x] Task 4.2: Update `TradingAppDbContext` and create EF migration for `FundingRates` table
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-04-details.md#task-42-update-dbcontext-and-create-migration

- [x] Task 4.3: Create `IFundingRateRepository` and `FundingRateRepository`
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-04-details.md#task-43-create-funding-rate-repository

- [x] Task 4.4: Add `GetFundingRatesAsync` to `IBinanceFuturesRestClient` and implementation
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-04-details.md#task-44-add-getfundingratesasync-to-rest-client

- [x] Task 4.5: Create `IFundingRateIngestionService` and `FundingRateIngestionService`
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-04-details.md#task-45-create-funding-rate-ingestion-service

- [x] Task 4.6: Create `IngestFundingRatesCommand`, handler, and `FundingRatesController`
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-04-details.md#task-46-create-api-layer

- [x] Task 4.7: Wire up DI and configuration
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-04-details.md#task-47-wire-up-di-and-configuration

- [x] Task 4.8: Write tests for all FundingRate components
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-04-details.md#task-48-write-tests

- [x] Task 4.9: Build and run tests
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-04-details.md#task-49-build-and-run-tests

### [x] Phase 5: Mark Price Klines

**Complexity**: Low | **Risk**: Low

- [x] Task 5.1: Add `GetMarkPriceKlinesAsync` to `IBinanceFuturesRestClient` and implementation
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-05-details.md#task-51-add-mark-price-klines-to-rest-client

- [x] Task 5.2: Extend `BinanceCandleIngestionService` for mark price kline ingestion
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-05-details.md#task-52-extend-ingestion-service-for-mark-price

- [x] Task 5.3: Add `IncludeMarkPrice` parameter to command and API endpoint
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-05-details.md#task-53-add-mark-price-api-parameter

- [x] Task 5.4: Write tests for mark price functionality
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-05-details.md#task-54-write-mark-price-tests

- [x] Task 5.5: Build and run tests
  - Details: .agent-context/3-develop/build/plans/details/20260328-binance-futures-data-ingestion-phase-05-details.md#task-55-build-and-run-tests

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: Domain & Persistence Foundation | Medium | Medium |
| Phase 2: Binance REST Client & Ingestion | High | Medium |
| Phase 3: Binance Kline API Endpoint | Medium | Low |
| Phase 4: FundingRate Entity & Ingestion | High | Medium |
| Phase 5: Mark Price Klines | Low | Low |
| **Total** | **High** | **Medium** |

### Scoping Notes

- All Binance Futures API endpoints are public (no API key required), reducing integration risk
- The existing Hyperliquid ingestion pipeline is a proven template — Binance follows the same architecture
- `Source` column as string (not enum) allows adding future exchanges without code changes
- Symbols stored as display names (`BTC`) consistent with existing Hyperliquid data
- Mark price klines use interval prefix convention (`mark-15m`) to avoid schema changes
- Binary search gap-finding reused from Hyperliquid service for auto-detecting token start dates
- Each Binance ingestion service gets its own `SemaphoreSlim` guard — independent of Hyperliquid

## Dependencies

- .NET 9 / ASP.NET Core (existing)
- Microsoft.Extensions.Http.Resilience (existing, Polly pipeline)
- Entity Framework Core with SQLite provider (existing)
- MediatR (existing)
- Binance USDⓈ-M Futures public API (`https://fapi.binance.com`)

## Success Criteria

- `POST /api/candles/ingest/binance` returns 50,000+ BTC candles at 15m interval dating to 2019
- `POST /api/funding/ingest` stores BTC funding rate history from 2019 to present
- Existing Hyperliquid ingestion (`POST /api/candles/ingest`) continues to work unchanged
- All candles have correct `Source` discriminator (`Hyperliquid` or `Binance`)
- Re-ingestion is idempotent (INSERT OR IGNORE with Source in unique index)
- Rate limiting respected — no Binance 429 errors during normal ingestion
- All new and existing unit tests pass
- Binary search efficiently skips empty time ranges

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-03-28T08:25:58Z | 2026-03-28T08:45:00Z |
| Plan Reviewer | plan-reviewed | 2026-03-28T08:57:45Z | 2026-03-28T09:04:26Z |
| Plan Implementer | implemented | 2026-03-28T09:05:00Z | 2026-03-28T09:50:00Z |
