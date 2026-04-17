<!-- markdownlint-disable-file -->
# Release Changes: F4 — Market Data (WebSocket)

**Related Plan**: 20260324-f4-market-data-websocket-plan.instructions.md
**Implementation Date**: 2026-03-24

## Summary

Establishes a persistent WebSocket connection to Hyperliquid, subscribes to BTC-PERP trades stream, aggregates updates at 500ms intervals, relays to Angular UI via SignalR, and displays a live price ticker with rolling 15-minute chart using Lightweight Charts.

## Changes

### Added

<!-- Phase 1: Backend — WebSocket Client & Models -->
- src/TradePilot.Infrastructure/Hyperliquid/Models/HyperliquidWebSocketMessage.cs: Added base WebSocket envelope model for incoming channel/data messages.
- src/TradePilot.Infrastructure/Hyperliquid/Models/HyperliquidSubscribeRequest.cs: Added subscribe request and subscription payload models for trades stream.
- src/TradePilot.Infrastructure/Hyperliquid/Models/HyperliquidTrade.cs: Added individual trade message model.
- src/TradePilot.Infrastructure/Hyperliquid/Models/HyperliquidTradesMessage.cs: Added trades channel envelope model containing trade list.
- src/TradePilot.Application/Abstractions/Services/IHyperliquidWebSocketClient.cs: Added WebSocket client contract and connection state enum.
- src/TradePilot.Infrastructure/Services/HyperliquidWebSocketClient.cs: Implemented WebSocket connect/disconnect, subscribe, receive loop, message parsing, and callback dispatch.
- src/TradePilot.Application/MarketData/Models/TradeTickDto.cs: Added internal trade tick DTO used by WebSocket callback pipeline.
- src/TradePilot.Application/MarketData/Models/PriceUpdateDto.cs: Added aggregated price update DTO for SignalR payloads.
- src/TradePilot.Application/MarketData/Models/ConnectionStatusDto.cs: Added connection status DTO for SignalR payloads.
- tests/TradePilot.Infrastructure.Tests/Services/HyperliquidWebSocketClientTests.cs: Added unit tests for baseline client behavior and subscription precondition validation.

<!-- Phase 2: Backend — SignalR Hub & Stream Service -->
- src/TradePilot.Api/Hubs/MarketDataHub.cs: Added SignalR hub for market data client connection lifecycle logging.
- src/TradePilot.Api/Services/MarketDataStreamService.cs: Added hosted background service for REST seeding, WebSocket subscription, 500ms aggregation, SignalR broadcasting, and reconnect backoff.
- tests/TradePilot.Api.Tests/Hubs/MarketDataHubTests.cs: Added integration test validating SignalR hub connectivity.
- tests/TradePilot.Api.Tests/Services/MarketDataStreamServiceTests.cs: Added service tests for REST seeding, subscription behavior, trade broadcast, and retry behavior.

<!-- Phase 3: Frontend — SignalR Client, Price Ticker & Connection Status -->
- frontend/trading-ui/src/app/core/models/price-update.model.ts: Added frontend model for live price update payload.
- frontend/trading-ui/src/app/core/models/connection-status.model.ts: Added frontend model/type for connection status payloads.
- frontend/trading-ui/src/app/core/services/signalr.service.ts: Added root SignalR client service with reconnect lifecycle and merged status output.
- frontend/trading-ui/src/app/features/market-data/price-ticker/price-ticker.component.ts: Added standalone live ticker component logic.
- frontend/trading-ui/src/app/features/market-data/price-ticker/price-ticker.component.html: Added ticker UI for last price, 24h high/low, and volume.
- frontend/trading-ui/src/app/features/market-data/price-ticker/price-ticker.component.scss: Added ticker styling using existing theme tokens.

<!-- Phase 4: Frontend — Rolling 15-Minute Price Chart -->
- frontend/trading-ui/src/app/features/market-data/price-chart/price-chart.component.ts: Added standalone real-time chart component using Lightweight Charts, SignalR subscription, 15-minute rolling window logic, responsive resize handling, and cleanup.
- frontend/trading-ui/src/app/features/market-data/price-chart/price-chart.component.html: Added chart container and section title markup.
- frontend/trading-ui/src/app/features/market-data/price-chart/price-chart.component.scss: Added chart section/title/container styles.

### Modified

<!-- Phase 1: Backend — WebSocket Client & Models -->
- src/TradePilot.Application/Abstractions/Configuration/HyperliquidOptions.cs: Added WsBaseUrl option with required validation.
- src/TradePilot.Api/appsettings.json: Added Hyperliquid WsBaseUrl default setting.
- src/TradePilot.Api/appsettings.Development.json: Added Hyperliquid BaseUrl, WsBaseUrl, and Network development settings.
- src/TradePilot.Infrastructure/Hyperliquid/HyperliquidAssetMapper.cs: Added reverse coin-to-display mapping via ToDisplayName.

<!-- Phase 2: Backend — SignalR Hub & Stream Service -->
- src/TradePilot.Api/Program.cs: Registered SignalR, WebSocket client singleton, hosted stream service, CORS credentials, and mapped `/hubs/marketdata`.
- tests/TradePilot.Api.Tests/TradePilot.Api.Tests.csproj: Added `Microsoft.AspNetCore.SignalR.Client` package for hub integration testing.
- tests/TradePilot.Api.Tests/Infrastructure/FakeHttpMessageHandler.cs: Updated fake handler to return fresh response instances per request to prevent disposed-content failures after hosted service startup calls.

<!-- Phase 3: Frontend — SignalR Client, Price Ticker & Connection Status -->
- frontend/trading-ui/package.json: Added @microsoft/signalr dependency.
- frontend/trading-ui/package-lock.json: Updated lockfile after installing @microsoft/signalr.
- frontend/trading-ui/proxy.conf.json: Added /hubs proxy route with ws=true for SignalR WebSocket upgrade.
- frontend/trading-ui/src/environments/environment.ts: Added hubBaseUrl for dev.
- frontend/trading-ui/src/environments/environment.prod.ts: Added hubBaseUrl for prod.
- frontend/trading-ui/src/app/features/market-data/market-data.component.ts: Imported and registered PriceTickerComponent.
- frontend/trading-ui/src/app/features/market-data/market-data.component.html: Rendered price ticker in market data page layout.
- frontend/trading-ui/src/app/app.component.ts: Added SignalR status subscription and computed status class.
- frontend/trading-ui/src/app/app.component.html: Added global navbar connection status indicator.
- frontend/trading-ui/src/app/app.component.scss: Added Connected/Reconnecting/Disconnected status styles and mobile adjustment.

<!-- Phase 4: Frontend — Rolling 15-Minute Price Chart -->
- frontend/trading-ui/package.json: Added `lightweight-charts` dependency.
- frontend/trading-ui/package-lock.json: Updated lockfile after installing `lightweight-charts`.
- frontend/trading-ui/src/app/features/market-data/market-data.component.ts: Imported and registered PriceChartComponent in standalone component imports.
- frontend/trading-ui/src/app/features/market-data/market-data.component.html: Added realtime section rendering ticker and chart above existing market info/candles.
- frontend/trading-ui/src/app/features/market-data/market-data.component.scss: Added realtime layout styles for ticker/chart spacing.

### Removed

## Test Results

<!-- Phase 1: Backend — WebSocket Client & Models -->
- HyperliquidWebSocketClientTests: 6/6 passed
- TradePilot.Infrastructure.Tests: 10/10 passed

<!-- Phase 2: Backend — SignalR Hub & Stream Service -->
- TradePilot.Api.Tests: 19/19 passed
- MarketDataHubTests: 1/1 passed
- MarketDataStreamServiceTests: 5/5 passed

<!-- Phase 3: Frontend — SignalR Client, Price Ticker & Connection Status -->
- Angular Build (development): PASSED
- Angular Lint: PASSED

<!-- Phase 4: Frontend — Rolling 15-Minute Price Chart -->
- Angular Build (development): PASSED
- Angular Lint: PASSED

## Issues

<!-- Phase 1: Backend — WebSocket Client & Models -->
- Existing NU1903 warning for AutoMapper 12.0.1 vulnerability in TradePilot.Application.csproj; no build/test failures occurred.

<!-- Phase 2: Backend — SignalR Hub & Stream Service -->
- `runTests` tool did not discover tests when given the `.csproj` path; switched to `dotnet test` execution.
- Existing health API tests initially failed due to reused disposable `HttpResponseMessage` in `FakeHttpMessageHandler` after introducing hosted startup calls; resolved by returning a fresh response per request.

## Design Decisions

<!-- Phase 1: Backend — WebSocket Client & Models -->
- Kept WsBaseUrl validation as Required (without Url attribute) to avoid potential rejection of wss scheme during startup validation.
- Implemented HyperliquidWebSocketClient as public sealed in Infrastructure to match existing service pattern and allow direct unit test construction.
- Reused existing mapper strategy by adding ToDisplayName reverse mapping with fallback format coin-PERP.

<!-- Phase 2: Backend — SignalR Hub & Stream Service -->
- Kept `MarketDataStreamService` registered as a hosted service in API startup as required by phase scope.
- Added a resilient fake HTTP handler implementation in tests to preserve existing integration test behavior with additional startup-time REST calls introduced by this phase.

<!-- Phase 3: Frontend — SignalR Client, Price Ticker & Connection Status -->
- Implemented connection status as a worst-of merge between SignalR client state and backend Hyperliquid state in the SignalR service, so navbar status reflects the most severe current condition.
- Integrated the new ticker directly into the existing market data feature page to satisfy live display behavior without changing route structure.

<!-- Phase 4: Frontend — Rolling 15-Minute Price Chart -->
- Used `CrosshairMode.Normal` enum rather than numeric literal for clarity and type safety.
- Stored chart points as `LineData<UTCTimestamp>[]` to keep time comparisons type-safe and avoid union-time issues.
- Kept integration minimal by adding the chart into the existing market-data realtime section without changing existing polling/table behavior.

<!-- Phase 3: Frontend — SignalR Client, Price Ticker & Connection Status -->
- None

<!-- Phase 4: Frontend — Rolling 15-Minute Price Chart -->
- Build initially failed with `TS2365` in the chart component when comparing `Time` to `UTCTimestamp` in rolling-window filtering. Resolved by strongly typing chart data points as `LineData<UTCTimestamp>[]`.

## Review Hints

- Review message parsing path in src/TradePilot.Infrastructure/Services/HyperliquidWebSocketClient.cs for expected behavior on non-trades channel/control messages.
- Review whether DI registration for IHyperliquidWebSocketClient should be added in the next phase where the stream service is introduced.
- Review reconnect timing behavior in `MarketDataStreamService` to confirm expected production tolerance for cancellation during backoff delays.
- Review whether additional assertions for `ReceiveConnectionStatus` payloads are desired in `MarketDataStreamServiceTests` for stricter status-contract coverage.
- Review frontend/trading-ui/src/app/core/services/signalr.service.ts to confirm the worst-of status precedence aligns with expected UX priority.
- Review frontend/trading-ui/src/app/features/market-data/price-ticker/price-ticker.component.ts and frontend/trading-ui/src/app/features/market-data/market-data.component.html to confirm real-time ticker placement and behavior are as intended.
- Validate that incoming `PriceUpdate.timestamp` is always Unix milliseconds (chart logic converts with `/ 1000`).
- Review whether current chart title and fixed `BTC-PERP` label should be made dynamic if multi-asset realtime charting is required later.

## Release Summary

All 4 phases implemented successfully. The full WebSocket → .NET → SignalR → Angular real-time data pipeline is operational for F4 (Market Data WebSocket).

**Backend (Phases 1–2)**:
- `HyperliquidWebSocketClient` connects to the Hyperliquid trades stream with exponential backoff reconnection (1s–60s, 20 retries max)
- `MarketDataStreamService` hosted service aggregates trades at 500ms intervals and broadcasts via SignalR `MarketDataHub`
- `PriceUpdateDto` and `ConnectionStatusDto` define the SignalR message contracts
- CORS policy updated with `AllowCredentials()` for SignalR compatibility

**Frontend (Phases 3–4)**:
- `SignalrService` connects to `/hubs/marketdata`, merges SignalR transport state with backend Hyperliquid status
- `PriceTickerComponent` displays live last price, 24h high/low, and volume updating at ~500ms
- `PriceChartComponent` renders a rolling 15-minute Lightweight Charts line chart updating in real-time
- Global navbar shows Connected (green) / Reconnecting (amber) / Disconnected (red) indicator

**Quality**:
- 25+ backend tests pass across Infrastructure and API test projects
- Angular build and lint pass clean
