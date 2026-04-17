applyTo: ".agent-context/3-develop/build/changes/20260330-show-trades-on-chart-changes.md"
currentAgent: "Plan Implementer"
agentStartedAt: "2026-03-30T21:07:37Z"
status: "in-progress"
lastUpdated: "2026-03-30T21:07:37Z"
---
<!-- markdownlint-disable-file -->
currentAgent: "None"
# Task Checklist: Show Trades on Main Chart
status: "implemented"
lastUpdated: "2026-03-30T21:22:21Z"

Display fill events as arrow markers on the main price chart, matching the backtest cycle chart marker style, with real-time SignalR updates, asset filtering, visibility toggle, and hover tooltips.

## PBI Details

As a trader, I want to see my live fills displayed as markers on the main price chart so that I can visually correlate my entries and exits with price action.

### Acceptance Criteria

- [ ] **Given** the Market Data page is loaded with a selected asset, **When** fill history exists for that asset, **Then** arrow markers appear on the chart at the correct candle times and prices matching the backtest chart style
- [ ] **Given** a new fill event arrives via SignalR for the selected asset, **When** the chart is visible, **Then** a new marker is added to the chart in real-time without requiring a page refresh
- [ ] **Given** the user switches the selected asset, **When** fills are re-fetched, **Then** only fills for the newly selected asset are shown
- [ ] **Given** the trade markers toggle is off, **When** the chart renders, **Then** no fill markers are displayed even if fills exist
- [ ] **Given** the trade markers toggle is on and the user hovers over a marker, **When** the tooltip appears, **Then** it shows side, price, size, fee, and closed PnL
- [ ] **Given** no fills exist for the selected asset, **When** the chart loads, **Then** the chart renders cleanly with no markers and no error messages
- [ ] **Given** fills exist that are outside the loaded candle time range, **When** markers are rendered, **Then** those out-of-range fills are silently excluded
- [ ] **Given** the `GET /api/account/fills` endpoint, **When** called with an `asset` query parameter, **Then** it returns only fills matching that asset

## Objectives

- Extend `GET /api/account/fills` with optional `asset` query parameter for per-asset filtering
- Add fill markers to `PriceChartComponent` using `createSeriesMarkers` plugin (matching `CycleChartComponent` style)
- Stream real-time fill events to the chart via existing SignalR `ReceiveFillEvent` pipeline
- Add toggle button on Market Data page to show/hide trade markers
- Add `subscribeCrosshairMove`-based tooltip overlay showing side, price, size, fee, and closed PnL

### Discovery References

- Fills are NOT persisted — sourced live from Hyperliquid REST API and WebSocket
- `FillEventDto` is shared between REST responses and SignalR broadcasts
- ADR 14: `AccountController` uses direct service injection (no MediatR)
- Asset naming mismatch: `FillEventDto.Asset` = coin symbol (e.g. "BTC"), UI `selectedAsset` = display form (e.g. "BTC-PERP"); `HyperliquidAssetMapper.ToCoin()` handles conversion
- `HyperliquidRestClient.GetUserFillsAsync` supports both `userFillsByTime` (with startTime) and `userFills` (all-time) via nullable `startTimeMs` parameter
- Current `Take(50)` cap in `GetUserFillsAsync` must be repositioned after asset filter to avoid silently dropping asset-specific fills
- `createSeriesMarkers` plugin already used in `CycleChartComponent` and `EquityChartComponent` — exact pattern to replicate
- `subscribeCrosshairMove` is not used anywhere in the codebase — this is a new pattern for rich tooltips
- `SignalRService` already handles `ReceiveFillEvent` but routes only to `AccountStateService.addFillEvent()` — need to also expose `fillEvent$: Subject<FillEvent>`

### Project Patterns

- src/TradePilot.Api/Controllers/AccountController.cs - fills endpoint (no asset param yet)
- src/TradePilot.Api/Services/IHyperliquidAccountService.cs - service interface
- src/TradePilot.Api/Services/HyperliquidAccountService.cs - 24h lookback, delegates to rest client
- src/TradePilot.Infrastructure/Services/HyperliquidRestClient.cs - GetUserFillsAsync with Take(50)
- src/TradePilot.Infrastructure/Hyperliquid/HyperliquidAssetMapper.cs - ToCoin() helper
- src/TradePilot.Application/MarketData/Models/FillEventDto.cs - fill DTO
- src/TradePilot.Api/Services/UserEventStreamService.cs - SignalR fill broadcast
- frontend/trading-ui/src/app/features/backtesting/cycle-chart/cycle-chart.component.ts - canonical createSeriesMarkers pattern
- frontend/trading-ui/src/app/features/market-data/price-chart/price-chart.component.ts - main chart (no markers yet)
- frontend/trading-ui/src/app/features/market-data/market-data.component.ts - host page
- frontend/trading-ui/src/app/features/market-data/market-data.component.html - template with controls
- frontend/trading-ui/src/app/core/services/signalr.service.ts - ReceiveFillEvent handler
- frontend/trading-ui/src/app/core/services/hyperliquid-api.service.ts - getRecentFills() (no asset param)
- frontend/trading-ui/src/app/core/models/fill-event.model.ts - FillEvent interface
- tests/TradePilot.Api.Tests/Controllers/AccountControllerTests.cs - fill endpoint tests
- tests/TradePilot.Api.Tests/Infrastructure/BaseControllerTests.cs - test base class
- frontend/trading-ui/src/app/features/backtesting/equity-chart/equity-chart.component.spec.ts - chart test pattern

### [x] Phase 1: Backend API Extension

**Complexity**: Low | **Risk**: Low

- [x] Task 1.1: Add optional asset parameter to fills endpoint and service interface
  - Details: .agent-context/3-develop/build/plans/details/20260330-show-trades-on-chart-phase-01-details.md#task-11-add-optional-asset-parameter-to-fills-endpoint-and-service-interface

- [x] Task 1.2: Implement asset filtering in HyperliquidAccountService
  - Details: .agent-context/3-develop/build/plans/details/20260330-show-trades-on-chart-phase-01-details.md#task-12-implement-asset-filtering-in-hyperliquidaccountservice

- [x] Task 1.3: Remove Take(50) cap from HyperliquidRestClient.GetUserFillsAsync
  - Details: .agent-context/3-develop/build/plans/details/20260330-show-trades-on-chart-phase-01-details.md#task-13-remove-take50-cap-from-hyperliquidrestclientgetuserfillsasync

- [x] Task 1.4: Update AccountController tests for asset filtering
  - Details: .agent-context/3-develop/build/plans/details/20260330-show-trades-on-chart-phase-01-details.md#task-14-update-accountcontroller-tests-for-asset-filtering

- [x] Task 1.5: Build and run backend tests
  - Details: .agent-context/3-develop/build/plans/details/20260330-show-trades-on-chart-phase-01-details.md#task-15-build-and-run-backend-tests

### [x] Phase 2: Frontend Chart Integration

**Complexity**: High | **Risk**: Medium

- [x] Task 2.1: Expose fillEvent$ observable from SignalRService
  - Details: .agent-context/3-develop/build/plans/details/20260330-show-trades-on-chart-phase-02-details.md#task-21-expose-fillevent-observable-from-signalrservice

- [x] Task 2.2: Update HyperliquidApiService with asset parameter
  - Details: .agent-context/3-develop/build/plans/details/20260330-show-trades-on-chart-phase-02-details.md#task-22-update-hyperliquidapiservice-with-asset-parameter

- [x] Task 2.3: Add trade markers to PriceChartComponent
  - Details: .agent-context/3-develop/build/plans/details/20260330-show-trades-on-chart-phase-02-details.md#task-23-add-trade-markers-to-pricechartcomponent

- [x] Task 2.4: Add crosshairMove tooltip overlay to PriceChartComponent
  - Details: .agent-context/3-develop/build/plans/details/20260330-show-trades-on-chart-phase-02-details.md#task-24-add-crosshairmove-tooltip-overlay-to-pricechartcomponent

- [x] Task 2.5: Add toggle button and fill orchestration to MarketDataComponent
  - Details: .agent-context/3-develop/build/plans/details/20260330-show-trades-on-chart-phase-02-details.md#task-25-add-toggle-button-and-fill-orchestration-to-marketdatacomponent

- [x] Task 2.6: Add PriceChartComponent spec for marker rendering
  - Details: .agent-context/3-develop/build/plans/details/20260330-show-trades-on-chart-phase-02-details.md#task-26-add-pricechartcomponent-spec-for-marker-rendering

- [x] Task 2.7: Build and lint frontend
  - Details: .agent-context/3-develop/build/plans/details/20260330-show-trades-on-chart-phase-02-details.md#task-27-build-and-lint-frontend

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: Backend API Extension | Low | Low |
| Phase 2: Frontend Chart Integration | High | Medium |
| **Total** | **Medium** | **Medium** |

### Scoping Notes

- Backend changes are straightforward: thread an optional parameter through controller → service → LINQ filter
- Frontend marker rendering reuses the exact `CycleChartComponent` pattern — proven and tested
- The `subscribeCrosshairMove` tooltip is a new pattern in the codebase, adding moderate complexity
- `Take(50)` removal is safe because fills are naturally bounded by trading volume
- No new NuGet/npm packages required — all dependencies already installed

## Dependencies

- `lightweight-charts` npm package (already installed) — `createSeriesMarkers` plugin API
- Hyperliquid REST API `userFills` / `userFillsByTime` endpoints (already integrated)
- SignalR `ReceiveFillEvent` pipeline (already functional)
- `HyperliquidAssetMapper.ToCoin()` for coin↔display name conversion (already exists)

## Success Criteria

- Arrow markers (green arrowUp for buys, amber arrowDown for sells) appear on the price chart for fills matching the selected asset
- Real-time fills arrive via SignalR and render as markers without page refresh
- Toggle button shows/hides all trade markers
- Hovering near a marker shows a tooltip with side, price, size, fee, and closed PnL
- Asset switch re-fetches and re-renders markers for the new asset
- `GET /api/account/fills?asset=BTC-PERP` returns only BTC fills
- All backend tests pass, frontend builds and lints cleanly

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-03-30T20:12:37Z | 2026-03-30T20:56:50Z |
| Plan Reviewer | plan-reviewed | 2026-03-30T20:57:43Z | 2026-03-30T21:04:57Z |
| Plan Implementer | implemented | 2026-03-30T21:07:37Z | 2026-03-30T21:22:21Z |
