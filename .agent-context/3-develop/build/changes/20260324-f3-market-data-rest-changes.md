<!-- markdownlint-disable-file -->
# Release Changes: F3 — Market Data (REST)

**Related Plan**: 20260324-f3-market-data-rest-plan.instructions.md
**Implementation Date**: 2026-03-24

## Summary

Frontend Angular market data page for F3 — asset selector (BTC-PERP default), market info card with 10s auto-poll, timeframe selector (15m default), candle table (50 rows), manual refresh, error/empty states. Angular Material UI. `ng build` and `ng lint` both pass.

## Changes

### Added

<!-- Phase 1: Backend — Application Layer, MediatR Infrastructure, Market Data API -->
- src/TradePilot.Application/Abstractions/Exceptions/DomainException.cs: Application-level domain validation exception type.
- src/TradePilot.Application/Abstractions/Exceptions/NotFoundException.cs: Application-level not-found exception type.
- src/TradePilot.Application/MarketData/Models/MarketInfoDto.cs: Market info DTO for API responses.
- src/TradePilot.Application/MarketData/Models/CandleDto.cs: Candle DTO for OHLCV API responses.
- src/TradePilot.Application/MarketData/Queries/GetMarketInfoQuery.cs: MediatR query and handler for market info fetch.
- src/TradePilot.Application/MarketData/Queries/GetCandlesQuery.cs: MediatR query and handler for candle data fetch.
- src/TradePilot.Infrastructure/Hyperliquid/HyperliquidAssetMapper.cs: Asset and timeframe mapping/validation helpers for exchange translation.
- src/TradePilot.Infrastructure/Hyperliquid/Models/HyperliquidInfoRequest.cs: Hyperliquid POST /info request payload models.
- src/TradePilot.Infrastructure/Hyperliquid/Models/HyperliquidMetaAndAssetCtxsResponse.cs: Hyperliquid meta and asset context response models.
- src/TradePilot.Infrastructure/Hyperliquid/Models/HyperliquidCandleSnapshotResponse.cs: Hyperliquid candle snapshot response model.
- src/TradePilot.Api/Infrastructure/Filters/HttpGlobalExceptionFilter.cs: Global exception-to-Envelope HTTP mapping filter.
- src/TradePilot.Api/Controllers/MarketDataController.cs: Market data API controller — `GET /api/market/info` and `GET /api/market/candles` endpoints.
- tests/TradePilot.Api.Tests/Controllers/MarketDataControllerTests.cs: API integration tests for market endpoints and error flows.
- tests/TradePilot.Application.Tests/MarketData/Queries/MarketDataQueryHandlersTests.cs: Unit tests for market data query handlers.

<!-- Phase 2: Frontend — Angular Market Data Page -->
- frontend/trading-ui/src/environments/environment.ts: Development environment config with `apiBaseUrl: "/api"` (proxy-based).
- frontend/trading-ui/src/environments/environment.prod.ts: Production environment config with `apiBaseUrl: "/api"`.
- frontend/trading-ui/src/app/core/services/api-rest-client.service.ts: Reusable typed HTTP wrapper (get/post/put/delete) using `HttpClient` and `environment.apiBaseUrl`.
- frontend/trading-ui/src/app/core/models/market-info.model.ts: `MarketInfo` interface matching backend DTO (asset, midPrice, markPrice, indexPrice, fundingRate, volume24h, openInterest, priceChange24hPercent).
- frontend/trading-ui/src/app/core/models/candle.model.ts: `Candle` interface for OHLCV rows (timestamp as Unix ms, open, high, low, close, volume).
- frontend/trading-ui/src/app/core/services/market-data.service.ts: Angular service calling `GET market/info?asset=` and `GET market/candles?asset=&timeframe=` via `ApiRestClient`.
- frontend/trading-ui/src/app/features/market-data/market-data.component.ts: Standalone component with `BehaviorSubject`-driven 10s polling (switchMap + catchError), manual refresh via `Subject`, candle reload on change.
- frontend/trading-ui/src/app/features/market-data/market-data.component.html: Angular Material UI — asset `mat-select`, `mat-card` market info, timeframe `mat-select`, `mat-table` candle grid, refresh `mat-button`, error banners, empty states.
- frontend/trading-ui/src/app/features/market-data/market-data.component.scss: Feature styles — responsive info grid, candle table full-width, error/empty banners.

### Modified

<!-- Phase 1: Backend — Application Layer, MediatR Infrastructure, Market Data API -->
- src/TradePilot.Application/TradePilot.Application.csproj: Added AutoMapper package dependency.
- src/TradePilot.Application/Abstractions/Services/IHyperliquidRestClient.cs: Extended interface with `GetMarketInfoAsync` and `GetCandlesAsync` methods.
- src/TradePilot.Infrastructure/Services/HyperliquidRestClient.cs: Implemented `GetMarketInfoAsync` and `GetCandlesAsync` with mapping/validation.
- src/TradePilot.Api/Program.cs: Registered AutoMapper assembly scanning and global exception filter.
- src/TradePilot.Api/TradePilot.Api.csproj: Added `AutoMapper.Extensions.Microsoft.DependencyInjection` package.

<!-- Phase 2: Frontend — Angular Market Data Page -->
- frontend/trading-ui/src/app/app.routes.ts: Added lazy-loaded `/market-data` route pointing to `MarketDataComponent`.
- frontend/trading-ui/src/app/app.component.html: Added navigation link to Market Data page.

### Removed

## Test Results

<!-- Phase 1: Backend — Application Layer, MediatR Infrastructure, Market Data API -->
- TradePilot.Application.Tests: 3/3 passed
- TradePilot.Api.Tests: 13/13 passed
- TradePilot.Infrastructure.Tests: 6/6 passed

<!-- Phase 2: Frontend — Angular Market Data Page -->
- Angular Build (`npx ng build --configuration=development`): PASSED
- Angular Lint (`npx ng lint`): PASSED

## Issues

<!-- Phase 1: Backend — Application Layer, MediatR Infrastructure, Market Data API -->
- NuGet restore initially failed due to an unauthorized Azure Artifacts feed; resolved by using `RestoreSources=https://api.nuget.org/v3/index.json` override.
- NU1903 vulnerability warning for AutoMapper 12.0.1 noted; not a compile/test failure. Consider upgrading or pinning when integrating into CI.

<!-- Phase 2: Frontend — Angular Market Data Page -->
- None

## Design Decisions

<!-- Phase 1: Backend — Application Layer, MediatR Infrastructure, Market Data API -->
- Kept existing `PostInfoAsync` generic method on `IHyperliquidRestClient` and added explicit `GetMarketInfoAsync`/`GetCandlesAsync` methods to preserve account-service usage.
- Centralised exchange asset/timeframe translation in `HyperliquidAssetMapper` (Infrastructure layer) to keep CQRS handlers exchange-agnostic.
- NotFoundException and DomainException map to 404/400 respectively in `HttpGlobalExceptionFilter`.

<!-- Phase 2: Frontend — Angular Market Data Page -->
- Used `/api` for development `apiBaseUrl` because `proxy.conf.json` defines an `/api` proxy — no hardcoded port needed.
- Polling uses `BehaviorSubject<string>` (selected asset) + outer `switchMap` + inner `merge(interval(10s), manualRefresh$)` + inner `switchMap` + `catchError` — asset change cancels old subscription, errors do not break polling.
- Candle data is one-shot subscribe only (no auto-poll), triggered on asset change, timeframe change, or manual refresh.

## Review Hints

- Verify Hyperliquid response field mappings in `src/TradePilot.Infrastructure/Services/HyperliquidRestClient.cs` against live API payloads (field names, array indexing).
- Validate `HttpGlobalExceptionFilter` exception-to-status-code mappings match the Envelope error contract.
- Evaluate AutoMapper 12.0.1 NU1903 vulnerability warning — consider upgrading or confirming acceptable risk.
- Verify backend response shapes match Angular `MarketInfo` and `Candle` interfaces exactly (camelCase property names).
- Integration-test with backend running: BTC-PERP default, 15m timeframe, 10s polling, manual refresh.

## Release Summary

F3 Market Data (REST) is fully implemented across both phases:

**Phase 1 (Backend)**: Application layer with MediatR CQRS queries/handlers for market info and candle data; `IHyperliquidRestClient` extended with `GetMarketInfoAsync`/`GetCandlesAsync`; `MarketDataController` with `GET /api/market/info` and `GET /api/market/candles`; `HttpGlobalExceptionFilter` mapping domain exceptions to Envelope responses; `HyperliquidAssetMapper` for exchange translation; 22 backend tests passing (3 Application, 13 Api, 6 Infrastructure).

**Phase 2 (Frontend)**: Angular 19 standalone `MarketDataComponent` with Angular Material UI — asset `mat-select` (BTC-PERP default), market info `mat-card` with 10s `BehaviorSubject`-driven polling, timeframe `mat-select` (15m default), `mat-table` candle grid (50 rows), manual refresh button. `ApiRestClient` wrapper, `MarketDataService`, `MarketInfo`/`Candle` models created. `ng build` and `ng lint` pass cleanly.
