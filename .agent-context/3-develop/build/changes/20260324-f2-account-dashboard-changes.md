<!-- markdownlint-disable-file -->
# Release Changes: F2 — Account Dashboard

**Related Plan**: 20260324-f2-account-dashboard-plan.instructions.md
**Implementation Date**: 2026-03-24

## Summary

Implements the F2 Account Dashboard feature: backend DTOs, service layer, and API endpoints for account summary, positions, and orders; Angular Material frontend dashboard with tabbed navigation, 2-second polling, staleness detection, and error handling.

## Changes

### Added

<!-- Phase 1: Backend — DTOs, Service Layer, and API Endpoints -->
- src/TradePilot.Api/Models/AccountSummaryDto.cs: Added account summary response DTO with equity/margin/PnL fields.
- src/TradePilot.Api/Models/PositionDto.cs: Added position response DTO for asset/size/pricing/PnL/liquidation data.
- src/TradePilot.Api/Models/OpenOrderDto.cs: Added open order response DTO for order list output.
- src/TradePilot.Api/Services/IHyperliquidAccountService.cs: Added account service abstraction with async methods for summary, positions, and orders.
- src/TradePilot.Api/Services/HyperliquidAccountService.cs: Added service implementation calling Hyperliquid POST /info and mapping JSON responses to DTOs.
- src/TradePilot.Api/Controllers/AccountController.cs: Added API controller exposing GET /api/account, /api/positions, and /api/orders with 503 handling.
- tests/TradePilot.Api.Tests/Controllers/AccountControllerTests.cs: Added 6 integration tests for happy paths, empty orders, and upstream error behavior.

<!-- Phase 2: Frontend — Angular Material Dashboard with Polling -->
- frontend/trading-ui/src/app/core/models/account-summary.model.ts: Added account summary model interface for dashboard binding.
- frontend/trading-ui/src/app/core/models/position.model.ts: Added position model interface with PnL and liquidation fields.
- frontend/trading-ui/src/app/core/models/open-order.model.ts: Added open order model interface for orders tab rendering.
- frontend/trading-ui/src/app/core/services/hyperliquid-api.service.ts: Added API service with health, account, positions, and orders GET methods.
- frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts: Added dashboard orchestration component with polling, manual refresh, stale detection, and error handling.
- frontend/trading-ui/src/app/features/dashboard/dashboard.component.html: Added dashboard layout with summary section, tabs, spinner, refresh controls, and error banner.
- frontend/trading-ui/src/app/features/dashboard/dashboard.component.scss: Added dashboard styling including stale dimming and banner visuals.
- frontend/trading-ui/src/app/features/dashboard/account-summary/account-summary.component.ts: Added summary card component with input binding and PnL class selection.
- frontend/trading-ui/src/app/features/dashboard/account-summary/account-summary.component.html: Added metric card template showing all required account fields.
- frontend/trading-ui/src/app/features/dashboard/account-summary/account-summary.component.scss: Added summary card styles and profit/loss color classes.
- frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts: Added positions table component with empty-state and PnL styling logic.
- frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.html: Added positions table template with required columns and PnL percentage display.
- frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.scss: Added positions table styling and long/short coloring.
- frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.ts: Added orders table component with side-to-class mapping.
- frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.html: Added orders table template with required columns and empty-state.
- frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.scss: Added orders table styling and buy/sell coloring.
- frontend/trading-ui/src/app/app.routes.ts: Added app routes including dashboard route, connection route, default redirect, and wildcard redirect.

### Modified

<!-- Phase 1: Backend — DTOs, Service Layer, and API Endpoints -->
- src/TradePilot.Application/Abstractions/Services/IHyperliquidRestClient.cs: Extended interface with generic PostInfoAsync method for typed POST /info reads.
- src/TradePilot.Infrastructure/Services/HyperliquidRestClient.cs: Implemented PostInfoAsync with ensure-success and typed JSON deserialization.
- src/TradePilot.Api/Program.cs: Registered IHyperliquidAccountService -> HyperliquidAccountService in DI.

<!-- Phase 2: Frontend — Angular Material Dashboard with Polling -->
- frontend/trading-ui/package.json: Added Angular Material, CDK, and animations dependencies.
- frontend/trading-ui/package-lock.json: Updated lockfile for new Angular Material dependencies.
- frontend/trading-ui/src/styles.scss: Added Angular Material dark theme and global body styling.
- frontend/trading-ui/src/app/app.config.ts: Added router and animations providers.
- frontend/trading-ui/src/app/app.component.ts: Switched root component to routed shell with navigation directives.
- frontend/trading-ui/src/app/app.component.html: Replaced static status card with navigation header and router outlet.
- frontend/trading-ui/src/app/app.component.scss: Updated app shell styles for routed dashboard layout and responsive nav.
- frontend/trading-ui/src/app/app.component.spec.ts: Updated title assertions and added router provider for standalone routing directives.

### Removed

## Test Results

<!-- Phase 1: Backend — DTOs, Service Layer, and API Endpoints -->
- AccountControllerTests: 6/6 passed
- TradePilot.Api.Tests (full project): 9/9 passed
- TradePilot.Infrastructure.Tests (full project): 6/6 passed

<!-- Phase 2: Frontend — Angular Material Dashboard with Polling -->
- Frontend Build (ng build --configuration=development): PASSED
- Frontend Lint (ng lint): PASSED

## Issues

<!-- Phase 1: Backend — DTOs, Service Layer, and API Endpoints -->
- Initial dotnet build TradePilot.sln failed restore with 401 from private Azure Artifacts feed. Resolved by running build/test with --no-restore and --no-build using already-restored local packages.

<!-- Phase 2: Frontend — Angular Material Dashboard with Polling -->
- Initial build failed due to strict nullability on forkJoin results in dashboard data assignment; resolved by using null-safe fallback assignments for positions/orders.
- Initial lint failed on constructor injection rule in dashboard component; resolved by migrating to inject() pattern for DI.

## Design Decisions

<!-- Phase 1: Backend — DTOs, Service Layer, and API Endpoints -->
- Added PostInfoAsync<T> on the existing Hyperliquid REST client abstraction rather than introducing direct HttpClient usage in API services, to keep Hyperliquid transport access centralized.
- Kept the F2 account feature in the API layer using direct service injection (no MediatR) to match phase requirements and POC simplification guidance.
- Implemented defensive JSON parsing for Hyperliquid response shape variance (string/number fields, nested position payloads, and order type object/string forms).

<!-- Phase 2: Frontend — Angular Material Dashboard with Polling -->
- Created hyperliquid-api.service.ts as a new file because the referenced F1 service did not exist in the current workspace; includes getHealth plus all account endpoints.
- Added a connection route to preserve access to the existing status card while making dashboard the default landing route.

## Review Hints

- Review mapping assumptions in src/TradePilot.Api/Services/HyperliquidAccountService.cs against live Hyperliquid testnet payloads, especially cross margin ratio and order type/status fields.
- Review the polling reset behavior in frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts to confirm the timer reset semantics match expected UX under repeated manual refresh clicks.
- Review API error UX thresholds in frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts to confirm 3 consecutive failures is the intended banner trigger.

## Release Summary

F2 Account Dashboard is fully implemented. The backend exposes three new endpoints (`GET /api/account`, `GET /api/positions`, `GET /api/orders`) via `AccountController`, backed by `HyperliquidAccountService` which calls Hyperliquid's POST `/info` API and maps responses to strongly-typed DTOs. The `IHyperliquidRestClient` abstraction was extended with a generic `PostInfoAsync<T>` method. Six integration tests cover all endpoints including error scenarios.

The Angular frontend adds an Angular Material dashboard with tabbed navigation (Summary, Positions, Orders). `DashboardComponent` polls every 2 seconds using RxJS `timer` + `forkJoin`, detects staleness after 10 seconds, and handles errors with a toast for transient failures and an inline banner after 3 consecutive failures. `AccountSummaryComponent`, `PositionsTableComponent`, and `OrdersTableComponent` use `MatTable`, `MatTab`, and `MatCard` with PnL color-coding (green/red) and empty-state messaging. The app shell was updated with a navigation header and router outlet; dashboard is the default route.
