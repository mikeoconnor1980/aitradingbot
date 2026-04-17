<!-- markdownlint-disable-file -->
# Release Changes: Binance USD-M Futures Data Ingestion

**Related Plan**: 20260328-binance-futures-data-ingestion-plan.instructions.md
**Implementation Date**: 2026-03-28

## Summary

Implementation complete: Binance USD-M Futures historical data ingestion now supports trade candles, funding rates, and optional mark-price klines with source-aware persistence and API endpoints.

## Changes

### Added

<!-- Phase 1: Domain & Persistence Foundation (Source Column) -->
- src/TradePilot.Persistence/Migrations/20260328092306_AddSourceToCandles.cs: EF Core migration adding the Candle Source column and replacing the unique index.
- src/TradePilot.Persistence/Migrations/20260328092306_AddSourceToCandles.Designer.cs: Auto-generated EF Core designer metadata for the new migration.

<!-- Phase 2: Binance REST Client & Ingestion Infrastructure -->
- src/TradePilot.Application/Abstractions/Configuration/BinanceIngestionOptions.cs: Added typed Binance ingestion options with defaults and validation attributes.
- src/TradePilot.Application/Abstractions/Services/IBinanceFuturesRestClient.cs: Added the Binance Futures REST client contract for paged kline retrieval.
- src/TradePilot.Application/Abstractions/Services/IBinanceCandleIngestionService.cs: Added the Binance candle ingestion service contract.
- src/TradePilot.Infrastructure/Binance/BinanceAssetMapper.cs: Added Binance symbol and interval mapping helpers with validation.
- src/TradePilot.Infrastructure/Binance/Models/BinanceKline.cs: Added array-based Binance kline wire model parsing and normalization.
- src/TradePilot.Infrastructure/Services/BinanceFuturesRestClient.cs: Added typed HttpClient-based Binance Futures kline client with error mapping.
- src/TradePilot.Infrastructure/Services/BinanceCandleIngestionService.cs: Added Binance candle ingestion pipeline with pagination, retry, timeout, gap search, and source tagging.
- tests/TradePilot.Infrastructure.Tests/Services/BinanceAssetMapperTests.cs: Added mapper coverage for supported symbols, intervals, and invalid input handling.
- tests/TradePilot.Api.Tests/Services/BinanceFuturesRestClientTests.cs: Added REST client tests for response mapping and Binance-specific error handling.
- tests/TradePilot.Api.Tests/Services/BinanceCandleIngestionServiceTests.cs: Added ingestion service tests for resume, pagination, retry, timeout, gap search, and concurrency guard behavior.

<!-- Phase 3: Binance Kline API Endpoint -->
- src/TradePilot.Application/Candles/Commands/IngestBinanceCandlesCommand.cs: Added the Binance ingestion MediatR command and co-located handler delegating to the Binance ingestion service.

<!-- Phase 4: FundingRate Entity & Ingestion -->
- src/TradePilot.Domain/Entities/FundingRate.cs: Added the funding rate domain entity with factory creation and symbol validation.
- src/TradePilot.Application/Abstractions/Repositories/IFundingRateRepository.cs: Added the funding rate repository contract.
- src/TradePilot.Persistence/Repositories/FundingRateRepository.cs: Added idempotent funding rate persistence with bulk insert and latest timestamp lookup.
- src/TradePilot.Application/FundingRates/Models/FundingRateDto.cs: Added the normalized funding rate DTO returned by the Binance client.
- src/TradePilot.Infrastructure/Binance/Models/BinanceFundingRate.cs: Added the Binance wire model for funding rate responses.
- src/TradePilot.Application/FundingRates/Models/FundingRateIngestionRequest.cs: Added the funding ingestion request model.
- src/TradePilot.Application/FundingRates/Models/FundingRateIngestionResult.cs: Added the funding ingestion result model.
- src/TradePilot.Application/Abstractions/Services/IFundingRateIngestionService.cs: Added the funding ingestion service contract.
- src/TradePilot.Infrastructure/Services/FundingRateIngestionService.cs: Added the funding ingestion pipeline with pagination, timeout handling, and concurrency guard.
- src/TradePilot.Application/FundingRates/Commands/IngestFundingRatesCommand.cs: Added the MediatR command and handler for funding ingestion.
- src/TradePilot.Api/Models/IngestFundingRatesRequest.cs: Added the API request model for funding ingestion.
- src/TradePilot.Api/Controllers/FundingRatesController.cs: Added the /api/funding/ingest endpoint with Binance symbol validation.
- src/TradePilot.Persistence/Migrations/20260328093941_AddFundingRates.cs: Added the EF Core migration creating the FundingRates table and unique index.
- src/TradePilot.Persistence/Migrations/20260328093941_AddFundingRates.Designer.cs: Added the generated EF Core migration designer metadata.
- tests/TradePilot.Domain.Tests/Entities/FundingRateTests.cs: Added domain tests for FundingRate creation and validation.
- tests/TradePilot.Persistence.Tests/Repositories/FundingRateRepositoryTests.cs: Added repository tests for inserts, deduplication, latest timestamp lookup, and decimal persistence.
- tests/TradePilot.Api.Tests/Services/FundingRateIngestionServiceTests.cs: Added ingestion service tests for resume, pagination, timeout, and concurrency behavior.
- tests/TradePilot.Api.Tests/Controllers/FundingRatesControllerTests.cs: Added controller tests for success, validation failure, and ingestion conflict cases.

### Modified

<!-- Phase 1: Domain & Persistence Foundation (Source Column) -->
- src/TradePilot.Domain/Entities/Candle.cs: Added Source with validation and preserved backward compatibility via a defaulted factory overload.
- src/TradePilot.Persistence/TradePilotDbContext.cs: Configured Source as required with a default value and updated the unique index to include Source.
- src/TradePilot.Application/Abstractions/Repositories/ICandleRepository.cs: Added optional source filtering parameters to candle query methods.
- src/TradePilot.Persistence/Repositories/CandleRepository.cs: Updated bulk insert SQL to persist Source and added optional source filtering to query methods.
- src/TradePilot.Infrastructure/Services/CandleIngestionService.cs: Passed Hyperliquid explicitly for source-aware resume and persisted candle creation.
- src/TradePilot.Application/Abstractions/Exceptions/IngestionAlreadyRunningException.cs: Added a message overload for later source-specific ingestion services.
- tests/TradePilot.Domain.Tests/Entities/CandleTests.cs: Added Source assertions and invalid-source validation coverage.
- tests/TradePilot.Persistence.Tests/Repositories/CandleRepositoryTests.cs: Added coverage for Source persistence, duplicate handling by source, and source-filtered queries.
- tests/TradePilot.Api.Tests/Services/CandleIngestionServiceTests.cs: Updated mocks and assertions for source-aware repository calls and persisted candle data.
- src/TradePilot.Persistence/Migrations/TradePilotDbContextModelSnapshot.cs: Updated EF Core snapshot to reflect the new Candle schema.

<!-- Phase 3: Binance Kline API Endpoint -->
- src/TradePilot.Api/Controllers/CandlesController.cs: Added the Binance ingestion endpoint with Binance-specific symbol and interval validation and MediatR dispatch.
- src/TradePilot.Api/Program.cs: Registered Binance options, typed HttpClient resilience pipeline, and Binance candle ingestion service.
- src/TradePilot.Api/appsettings.json: Added the BinanceIngestion configuration section with default API and ingestion settings.
- tests/TradePilot.Api.Tests/Controllers/CandlesControllerTests.cs: Added integration tests for Binance ingestion success, validation failures, conflict handling, and required-field validation.

<!-- Phase 4: FundingRate Entity & Ingestion -->
- src/TradePilot.Persistence/TradePilotDbContext.cs: Added the FundingRates DbSet and EF Core configuration for the new entity.
- src/TradePilot.Persistence/PersistenceServiceExtensions.cs: Registered IFundingRateRepository in persistence DI.
- src/TradePilot.Application/Abstractions/Services/IBinanceFuturesRestClient.cs: Extended the Binance client contract with funding rate retrieval.
- src/TradePilot.Infrastructure/Services/BinanceFuturesRestClient.cs: Implemented Binance funding rate API calls and DTO mapping.
- src/TradePilot.Api/Program.cs: Registered IFundingRateIngestionService in the API composition root.
- src/TradePilot.Persistence/Migrations/TradePilotDbContextModelSnapshot.cs: Updated the model snapshot to include FundingRate.
- tests/TradePilot.Api.Tests/Services/FundingRateIngestionServiceTests.cs: Corrected the multi-page test to use a full first page and validate real pagination behavior.

<!-- Phase 5: Mark Price Klines -->
- src/TradePilot.Application/Abstractions/Services/IBinanceFuturesRestClient.cs: Added the mark price kline client contract.
- src/TradePilot.Infrastructure/Services/BinanceFuturesRestClient.cs: Implemented Binance mark price kline retrieval and factored shared kline deserialization.
- src/TradePilot.Infrastructure/Binance/BinanceAssetMapper.cs: Allowed interval millisecond resolution for mark-prefixed intervals.
- src/TradePilot.Application/Candles/Models/IngestionRequest.cs: Added the IncludeMarkPrice flag to the ingestion request model.
- src/TradePilot.Api/Models/IngestCandlesRequest.cs: Added the IncludeMarkPrice API request property.
- src/TradePilot.Api/Controllers/CandlesController.cs: Passed the IncludeMarkPrice flag into Binance ingestion requests.
- src/TradePilot.Infrastructure/Services/BinanceCandleIngestionService.cs: Reused the existing Binance ingestion pipeline for mark price klines stored under mark-prefixed intervals.
- tests/TradePilot.Api.Tests/Services/BinanceFuturesRestClientTests.cs: Added coverage for the mark price kline endpoint and response mapping.
- tests/TradePilot.Api.Tests/Services/BinanceCandleIngestionServiceTests.cs: Added coverage for IncludeMarkPrice true and false ingestion behavior.
- tests/TradePilot.Api.Tests/Controllers/CandlesControllerTests.cs: Added controller coverage for passing IncludeMarkPrice through the Binance ingest endpoint.

### Removed

<!-- Phase 1: Domain & Persistence Foundation (Source Column) -->
- None.

## Test Results

<!-- Phase 1: Domain & Persistence Foundation (Source Column) -->
- TradePilot.Domain.Tests: 11/11 passed
- TradePilot.Persistence.Tests: 11/11 passed
- TradePilot.Api.Tests: 66/66 passed
- Architecture Tests: Not applicable — no architecture test project exists in the workspace

<!-- Phase 2: Binance REST Client & Ingestion Infrastructure -->
- BinanceFuturesRestClientTests: 3/3 passed
- BinanceCandleIngestionServiceTests: 6/6 passed
- BinanceAssetMapperTests: 21/21 passed
- API Binance-filtered test run: 9/9 passed
- Infrastructure Binance-filtered test run: 21/21 passed
- Architecture Tests: Not applicable — no architecture test project exists in the workspace

<!-- Phase 3: Binance Kline API Endpoint -->
- TradePilot.Api.Tests build: PASSED
- TradePilot.Api.Tests: 81/81 passed
- Architecture Tests: Not applicable — no architecture test project exists in the workspace

<!-- Phase 4: FundingRate Entity & Ingestion -->
- TradePilot.Domain.Tests: 15/15 passed
- TradePilot.Persistence.Tests: 16/16 passed
- TradePilot.Api.Tests: 89/89 passed
- Architecture Tests: Not applicable — no architecture test project exists in the workspace

<!-- Phase 5: Mark Price Klines -->
- TradePilot.Api.Tests build: PASSED
- TradePilot.Api.Tests: 93/93 passed
- Architecture Tests: Not applicable — no architecture test project exists in the workspace

## Issues

<!-- Phase 1: Domain & Persistence Foundation (Source Column) -->
- `dotnet ef migrations add` failed with `src/TradePilot.Api` as the startup project because that project does not reference `Microsoft.EntityFrameworkCore.Design`. Resolved by generating the migration with `src/TradePilot.Persistence` as both project and startup, using the existing design-time factory.
- `dotnet ef database update` initially failed because the design-time factory connection string pointed at a non-resolvable relative SQLite path. Resolved by re-running with an explicit connection string to `c:/Projects/Personal/aitradingbot/data/TradePilot.db`.
- Builds emitted existing `NU1903` warnings for `AutoMapper` 12.0.1 in `src/TradePilot.Application/TradePilot.Application.csproj`. These warnings were not introduced by this phase.

<!-- Phase 2: Binance REST Client & Ingestion Infrastructure -->
- Builds emitted existing NU1903 warnings for AutoMapper 12.0.1 in src/TradePilot.Application/TradePilot.Application.csproj. These warnings were pre-existing and not introduced by this phase.

<!-- Phase 3: Binance Kline API Endpoint -->
- A duplicate using directive was introduced in tests/TradePilot.Api.Tests/Controllers/CandlesControllerTests.cs during the initial edit. Removed it and re-ran the build.
- The build still emits the existing NU1903 AutoMapper advisory from src/TradePilot.Application/TradePilot.Application.csproj. This was pre-existing and not introduced by Phase 3.

<!-- Phase 4: FundingRate Entity & Ingestion -->
- The initial API test run failed in the new funding ingestion pagination test because the test expected a second page after a short first page. The service behavior was correct, so the test was updated to use a 1000-row first page, and the API test project then passed.
- Builds for persistence and API test projects emitted the existing NU1903 AutoMapper 12.0.1 vulnerability warning from src/TradePilot.Application/TradePilot.Application.csproj. This warning was pre-existing and not introduced by Phase 4.

<!-- Phase 5: Mark Price Klines -->
- An initial duplicate Interval assignment was introduced in the BinanceCandleIngestionService interval result initializer during the first edit pass. It was removed before verification.
- Build and test output still includes the pre-existing NU1903 AutoMapper 12.0.1 vulnerability warning from src/TradePilot.Application/TradePilot.Application.csproj. This was not introduced by Phase 5.

## Design Decisions

<!-- Phase 1: Domain & Persistence Foundation (Source Column) -->
- Added a new `Create(string source, ...)` overload in `src/TradePilot.Domain/Entities/Candle.cs` and kept the existing call shape with an optional trailing `source = "Hyperliquid"` parameter. This preserves existing callers while allowing future explicit source-first construction if needed.
- Kept repository source filtering optional so existing read paths remain backward-compatible while new ingestion paths can resume independently per source.
- Did not modify EF tooling package references in the API project because that was outside the phase scope and unnecessary once the persistence design-time path was used.

<!-- Phase 2: Binance REST Client & Ingestion Infrastructure -->
- Persisted candles with the display symbol from the ingestion request, while mapping only the outbound Binance REST call to the native futures symbol such as BTCUSDT. This keeps Binance data aligned with the existing candle storage conventions.
- Kept the Binance ingestion flow intentionally close to CandleIngestionService so later API wiring and future funding and mark-price extensions can reuse the same operational behavior.
- Used a separate static ingestion guard and source discriminator of Binance so Binance runs remain isolated from Hyperliquid ingestion state.

<!-- Phase 3: Binance Kline API Endpoint -->
- Used the existing candle ingestion API shape and validation flow so the new Binance endpoint stays parallel to the Hyperliquid endpoint while keeping Binance-specific validation isolated to BinanceAssetMapper.
- Kept the Binance command payload as IngestionRequest, matching the phase details and allowing the handler to remain a thin delegation layer to the Binance ingestion service.
- Registered the Binance HttpClient resilience policy in the API composition root beside the existing Hyperliquid registration to preserve the current DI pattern already established in the host.

<!-- Phase 4: FundingRate Entity & Ingestion -->
- Reused the existing Binance ingestion options and operational patterns rather than introducing a separate funding-specific options type, keeping throttling and timeout behavior aligned across Binance ingestion flows.
- Stored funding rates using the display symbol such as BTC and mapped only outbound REST calls to Binance futures symbols such as BTCUSDT, matching the established candle persistence convention.
- Returned human-readable UTC timestamps from FundingRateIngestionService using the same style as candle ingestion instead of ISO 8601, to keep ingestion result formatting consistent across exchange ingestion features.
- Generated the migration using src/TradePilot.Persistence as both project and startup project, following the known-good design-time factory path already established in earlier phases.

<!-- Phase 5: Mark Price Klines -->
- Mark price candles are persisted in the existing Candle table using the existing interval-prefix convention such as mark-15m, which keeps the change additive and avoids schema changes.
- The Binance candle ingestion service uses the same interval ingestion path for both trade and mark price candles, switching only the REST fetch method and stored interval name. This keeps pagination, retry, timeout, and gap-search behavior consistent.
- BinanceAssetMapper.GetIntervalMs was extended to normalize mark-prefixed intervals so existing end-time and pagination calculations continue to work without introducing a parallel mapping API.

## Review Hints

<!-- Phase 1: Domain & Persistence Foundation (Source Column) -->
- Review the design-time EF tooling setup around `src/TradePilot.Persistence/DesignTimeDbContextFactory.cs` and `src/TradePilot.Api/TradePilot.Api.csproj`, since future migration workflows may hit the same startup-project limitation unless the repo standardizes one tooling path.

<!-- Phase 2: Binance REST Client & Ingestion Infrastructure -->
- Review the gap-search behavior in BinanceCandleIngestionService, especially the binary-search handoff after three empty batches, because it deliberately mirrors the Hyperliquid ingestion strategy while operating against Binance's GET-based pagination model.

<!-- Phase 3: Binance Kline API Endpoint -->
- Review the validation messages produced by src/TradePilot.Api/Controllers/CandlesController.cs to confirm the exact Binance symbol and interval lists are the desired API contract for clients.
- Review the Binance DI registration in src/TradePilot.Api/Program.cs to confirm the retry and timeout behavior should intentionally mirror the current Hyperliquid host-level resilience setup.

<!-- Phase 4: FundingRate Entity & Ingestion -->
- Review FundingRateIngestionService pagination semantics, especially the stop condition on batches smaller than 1000, because funding ingestion now assumes Binance's funding history API uses full pages to signal more data.
- Review the inserted and skipped counts returned by funding ingestion, since the repository uses INSERT OR IGNORE and the current service follows the existing candle ingestion pattern of treating attempted inserts as inserted counts.

<!-- Phase 5: Mark Price Klines -->
- Review src/TradePilot.Infrastructure/Services/BinanceCandleIngestionService.cs for the decision to ingest regular klines first and mark price klines second under the same request, since downstream consumers will now see both interval names in the ingestion result when IncludeMarkPrice is enabled.
- Review src/TradePilot.Api/Controllers/CandlesController.cs and src/TradePilot.Api/Models/IngestCandlesRequest.cs to confirm the API contract should remain opt-in with includeMarkPrice defaulting to false.

## Release Summary

Implemented all 5 planned phases for Binance USD-M Futures historical ingestion.

- Added source-aware candle persistence so Binance and Hyperliquid data can coexist for the same symbol, interval, and timestamp.
- Added Binance Futures historical candle ingestion infrastructure with symbol and interval mapping, rate-aware pagination, gap detection, and API exposure.
- Added funding-rate persistence, ingestion services, and a dedicated ingestion endpoint.
- Added optional mark-price kline ingestion using the existing candle storage model with mark-prefixed intervals.
- Verified implementation with focused and full project test runs reported across the affected test projects.

Residual risks and follow-up review focus:

- The EF migration workflow still depends on using the persistence project as the startup project for tooling.
- Existing AutoMapper NU1903 advisories remain in the solution and were not addressed by this implementation.