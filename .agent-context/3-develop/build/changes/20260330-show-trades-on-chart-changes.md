<!-- markdownlint-disable-file -->
# Release Changes: Show Trades on Main Chart

**Related Plan**: 20260330-show-trades-on-chart-plan.instructions.md
**Implementation Date**: 2026-03-30

## Summary

Implemented live trade markers on the market data price chart with backend asset filtering, SignalR-driven updates, a marker visibility toggle, and hover tooltips backed by automated backend and frontend validation.

## Changes

### Added

<!-- Phase 2: Frontend Chart Integration -->
- frontend/trading-ui/src/app/features/market-data/price-chart/price-chart.component.spec.ts: Added chart marker coverage for initial marker rendering, toggle clearing, fill appending, and empty-fill cleanup.

### Modified

<!-- Phase 1: Backend API Extension -->
- src/TradePilot.Api/Controllers/AccountController.cs: Added optional asset query parameter support to the fills endpoint and forwarded it to the account service.
- src/TradePilot.Api/Services/IHyperliquidAccountService.cs: Updated the fills service contract to accept an optional asset filter.
- src/TradePilot.Api/Services/HyperliquidAccountService.cs: Implemented optional asset filtering with coin mapping and switched asset-filtered requests to all-time fill retrieval.
- src/TradePilot.Infrastructure/Services/HyperliquidRestClient.cs: Removed the hardcoded Take(50) limit from user fills retrieval.
- tests/TradePilot.Api.Tests/Controllers/AccountControllerTests.cs: Added asset-filter coverage and updated fills endpoint mocks for the new service signature.

<!-- Phase 2: Frontend Chart Integration -->
- frontend/trading-ui/src/app/core/services/signalr.service.ts: Exposed a new fillEvent$ observable and emitted fill events alongside the existing account-state update.
- frontend/trading-ui/src/app/core/services/hyperliquid-api.service.ts: Added optional asset query-string support to getRecentFills().
- frontend/trading-ui/src/app/features/market-data/price-chart/price-chart.component.ts: Added fill inputs, marker plugin lifecycle, candle-bucket marker mapping, tooltip state, crosshair hover handling, and real-time fill appending.
- frontend/trading-ui/src/app/features/market-data/price-chart/price-chart.component.html: Added the tooltip overlay markup and chart surface wrapper.
- frontend/trading-ui/src/app/features/market-data/price-chart/price-chart.component.scss: Added tooltip and chart-surface styling using existing theme tokens.
- frontend/trading-ui/src/app/features/market-data/market-data.component.ts: Added fill loading, SignalR fill subscription, duplicate protection, asset filtering, and trade-marker toggle orchestration.
- frontend/trading-ui/src/app/features/market-data/market-data.component.html: Added the Trades toggle button and passed fills and showTradeMarkers into the price chart.
- frontend/trading-ui/src/app/features/market-data/market-data.component.scss: Added active styling for the Trades toggle button.

### Removed

## Test Results

<!-- Phase 1: Backend API Extension -->
- AccountControllerTests: 10/10 passed
- TradePilot.Api.Tests: 144/144 passed
- Solution Build: PASSED
- Architecture Tests: Not run - not required by the phase details

<!-- Phase 2: Frontend Chart Integration -->
- Angular Unit Tests: 103/103 passed
- Frontend Build: PASSED
- Frontend Lint: PASSED
- Architecture Tests: Not run - not required by the phase details

## Issues

<!-- Phase 1: Backend API Extension -->
- Initial targeted test run failed because a running TradePilot.Api process had backend DLLs locked during test build; the tests passed after the lock cleared and the run was retried.
- dotnet build and dotnet test reported pre-existing NU1903 warnings for AutoMapper 12.0.1; these warnings were unrelated to this phase and did not block compilation or tests.

<!-- Phase 2: Frontend Chart Integration -->
- The initial new spec failed under strict TypeScript because spy call metadata was accessed through a non-spy function type; this was fixed by capturing the spy return and reading calls from that spy.
- The initial chart tests hit Angular's ExpressionChangedAfterItHasBeenCheckedError because seeded candle state was being populated in ngAfterViewInit; this was fixed by moving seed-to-state application into the earlier input-change path so derived title data is stable on first detection.
- The frontend build completed with existing bundle and style budget warnings; these warnings did not block the build and were not introduced by this phase.

## Design Decisions

<!-- Phase 1: Backend API Extension -->
- Treated whitespace-only asset values as omitted in HyperliquidAccountService to preserve backward compatibility for empty query-string values.
- Kept asset filtering in the API-layer account service to stay aligned with ADR 14 for direct exchange-read controllers and services.

<!-- Phase 2: Frontend Chart Integration -->
- Snapped fill markers to the selected candle bucket time instead of raw fill seconds so markers render reliably on the candlestick series and tooltip grouping aligns with candle-based hovering.
- Deduplicated real-time fills in MarketDataComponent before appending them to avoid duplicate markers when historical refetches and SignalR events overlap.
- Kept tooltip rendering template-bound in Angular rather than generating HTML strings so sanitization and component state stay straightforward.

## Review Hints

<!-- Phase 1: Backend API Extension -->
- Confirm that GET /api/account/fills?asset=... returning all historical fills for the requested asset, while GET /api/account/fills remains limited to the last 24 hours, matches the intended product behavior.

<!-- Phase 2: Frontend Chart Integration -->
- Review the marker bucketing behavior in PriceChartComponent, especially for multiple fills inside the same candle, because tooltips intentionally group those fills by candle time.
- Review tooltip placement near chart edges to confirm the current clamping heuristics are acceptable across expected chart sizes and mobile widths.

## Release Summary

Completed both implementation phases for live trade markers on the main market-data chart.

- Backend: Added optional asset filtering to GET /api/account/fills, preserved backward compatibility for empty asset values, and removed the premature Take(50) truncation from Hyperliquid fill retrieval.
- Frontend: Added marker rendering, tooltip hover details, a Trades visibility toggle, SignalR fill streaming, per-asset fill orchestration, and dedicated PriceChartComponent test coverage.
- Validation: Backend solution build passed, TradePilot.Api.Tests passed 144/144, Angular unit tests passed 103/103, frontend build passed, and frontend lint passed.
