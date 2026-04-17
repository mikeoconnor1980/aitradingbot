<!-- markdownlint-disable-file -->
# Release Changes: Historical Candles from Local Database on Price Chart

**Related Plan**: 20260328-historical-candles-chart-plan.instructions.md
**Implementation Date**: 2026-03-28

## Summary

Completed both phases for serving price chart history from the local candle database, including the new paged backend endpoint, Angular history-first loading with fallback, and smoke validation against the live Market Data page.

## Changes

### Added

<!-- Phase 1: Backend — New CQRS Query & API Endpoint -->
- src/TradePilot.Application/MarketData/Queries/GetHistoricalCandlesQuery.cs: Added the historical-candles CQRS query and handler that maps asset symbols, validates timeframe and limit, calculates the requested window, queries ICandleRepository, and returns CandleDto results.
- tests/TradePilot.Application.Tests/MarketData/Queries/GetHistoricalCandlesQueryHandlerTests.cs: Added focused handler unit tests covering mapping, validation, limit enforcement, time-range calculation, and empty-result behavior.

### Modified

<!-- Phase 1: Backend — New CQRS Query & API Endpoint -->
- src/TradePilot.Api/Controllers/MarketDataController.cs: Added GET /api/market/candles/history to dispatch the new query with asset, timeframe, endTime, and limit parameters.
- tests/TradePilot.Api.Tests/Controllers/MarketDataControllerTests.cs: Added controller integration tests for the new history endpoint and registered a mocked ICandleRepository for the MediatR-backed path.

<!-- Phase 2: Frontend — Wire Chart to Local DB Endpoint -->
- frontend/trading-ui/src/app/core/services/market-data.service.ts: Added getHistoricalCandles() for the new /api/market/candles/history endpoint.
- frontend/trading-ui/src/app/features/market-data/market-data.component.ts: Switched initial chart loading and scrollback to prefer historical candles and added fallback to the existing Hyperliquid candles endpoint when history is empty or unavailable.

### Removed

<!-- Phase 1: Backend — New CQRS Query & API Endpoint -->
- None.

## Test Results

<!-- Phase 1: Backend — New CQRS Query & API Endpoint -->
- GetHistoricalCandlesQueryHandlerTests: 12/12 passed
- MarketDataControllerTests: 10/10 passed
- TradePilot.Application.Tests: 49/49 passed
- TradePilot.Domain.Tests: 15/15 passed
- TradePilot.Infrastructure.Tests: 51/51 passed
- TradePilot.Persistence.Tests: 18/18 passed
- TradePilot.Api.Tests: 122/122 passed
- Architecture Tests: Not run separately; no dedicated architecture test task was defined in this phase

<!-- Phase 2: Frontend — Wire Chart to Local DB Endpoint -->
- Angular lint: PASSED
- Angular build: PASSED
- Manual Smoke Test: PASSED
- Browser route validation: Market Data page loaded successfully in Chrome on http://127.0.0.1:4200/market-data
- Historical candle load validation: API log showed SQLite Candles table queries for symbol, interval, and time range immediately after Market Data page load
- Live update path validation: MarketDataHub SignalR client connection was established from the browser session
- Architecture Tests: Not run separately; no architecture-test task was defined for this phase

## Issues

<!-- Phase 1: Backend — New CQRS Query & API Endpoint -->
- The running Start API task held Debug build outputs open, which caused targeted Debug test and build attempts to fail with file-lock errors. Resolved by running validation in Release configuration.
- The phase details requested handler unit tests while the broader testing guidance prefers CQRS coverage through controllers. Resolved by implementing the explicitly requested handler tests while keeping controller integration tests as the primary API-path verification.

<!-- Phase 2: Frontend — Wire Chart to Local DB Endpoint -->
- Angular lint initially flagged the new limit parameter as a redundant inferred type. Resolved by removing the explicit number annotation.
- The original Start UI task was not actually running during smoke validation even though earlier workspace state indicated it was active. Resolved by restarting ng serve manually on 127.0.0.1:4200 before browser validation.
- Headless browser mode, DevTools remote debugging, and direct HTTP probes were restricted by local policy. Resolved by using a visible Chrome session plus backend log correlation to complete smoke validation.

## Design Decisions

<!-- Phase 1: Backend — New CQRS Query & API Endpoint -->
- Added the historical query in the Application layer against ICandleRepository instead of introducing new repository methods, which kept the change aligned with the plan and existing CQRS boundaries.
- Used DomainException for invalid timeframe and non-positive limit so API callers receive a validation-style 400 response instead of an unhandled 500.
- Used MSTest and Moq for the new unit tests to match the existing test-project conventions, even though the phase details mentioned NSubstitute.

<!-- Phase 2: Frontend — Wire Chart to Local DB Endpoint -->
- Added a frontend fallback from /api/market/candles/history to the existing /api/market/candles path for both initial load and load-more so chart behavior remains intact when local history is unavailable or sparse.
- Left market-info polling, SignalR live updates, and chart/table binding unchanged so the frontend change stayed scoped to candle sourcing only.
- Treated the smoke test as satisfied once the route loaded, the backend accepted a SignalR Market Data connection, and the backend executed historical candle reads from the local SQLite Candles table for the Market Data page.

## Review Hints

<!-- Phase 1: Backend — New CQRS Query & API Endpoint -->
- Review the time-window calculation in src/TradePilot.Application/MarketData/Queries/GetHistoricalCandlesQuery.cs to confirm the inclusive range behavior matches the frontend paging expectations.
- There is an existing AutoMapper 12.0.1 package warning during builds; it did not block this phase, but it remains an out-of-scope dependency hygiene item.

<!-- Phase 2: Frontend — Wire Chart to Local DB Endpoint -->
- Confirm whether the backend should keep defaulting historical requests to endTime = now, or whether it should anchor the initial window to the latest locally stored candle when the DB is not fully current.
- If you want stronger human verification, manually exercise chart scrollback, timeframe switching, and asset switching on the Market Data page; those interactions were not fully scripted because local browser automation policy is restrictive.

## Release Summary

The implementation now serves historical chart candles from the local SQLite candle store through a paged backend endpoint and updates the Angular Market Data flow to prefer database-backed history while preserving the existing Hyperliquid fallback path and live SignalR updates.

Backend work added a new Application-layer historical candles query, exposed it through MarketDataController, and covered it with handler and controller tests. Frontend work added a dedicated history API call, switched initial chart seeding and backward pagination to use the history endpoint first, and validated the end-to-end route against the live Market Data page with backend log confirmation.