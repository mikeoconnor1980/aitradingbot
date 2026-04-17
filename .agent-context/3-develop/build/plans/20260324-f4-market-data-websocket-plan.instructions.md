---
applyTo: ".agent-context/3-develop/build/changes/20260324-f4-market-data-websocket-changes.md"
currentAgent: "3-Develop: 3 Reviewer"
agentStartedAt: "2026-03-25T00:19:00Z"
status: "complete"
lastUpdated: "2026-03-25T00:21:25Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: F4 — Market Data (WebSocket)

## Overview

Establish a persistent WebSocket connection to Hyperliquid, subscribe to the BTC-PERP trades stream, aggregate updates at a 500ms interval, relay them to the Angular UI via SignalR, and display a live price ticker with a rolling 15-minute price chart using Lightweight Charts. Includes automatic reconnection with exponential backoff and a global connection status indicator.

## PBI Details

**PBI ID:** Draft
**Status:** Draft
**Implementation Phase:** 6
**Risk Level:** Medium
**Depends On:** F1, F3

### User Story

> As a **developer**, I want to **see real-time price updates streamed to the UI** so that **I can validate the full WebSocket → .NET → SignalR → Angular real-time data pipeline**.

### Acceptance Criteria

- [ ] **Given** the backend is running, **When** the `MarketDataStreamService` starts, **Then** it fetches 24h stats via REST and connects to the Hyperliquid WebSocket subscribing to BTC-PERP trades
- [ ] **Given** trades are streaming from Hyperliquid, **When** 500ms has elapsed since the last push, **Then** an aggregated price update (last price, 24h high/low/volume) is pushed to all SignalR clients
- [ ] **Given** no trades are received in a 500ms interval, **When** the interval elapses, **Then** no SignalR message is sent
- [ ] **Given** the Angular UI is open, **When** a `ReceivePriceUpdate` arrives, **Then** the live ticker displays the last price, 24h high, 24h low, and 24h volume
- [ ] **Given** the Angular UI is open, **When** price updates arrive, **Then** a rolling 15-minute line chart updates in real-time using Lightweight Charts
- [ ] **Given** the Angular app shell, **When** the WebSocket connection state changes, **Then** a global navbar indicator shows Connected (green), Reconnecting (amber), or Disconnected (red)
- [ ] **Given** the WebSocket disconnects, **When** reconnection begins, **Then** exponential backoff is applied (1s initial, 60s max) up to 20 retry attempts
- [ ] **Given** 20 reconnection attempts have been exhausted, **When** the last attempt fails, **Then** the status shows "Disconnected" and no further retries occur
- [ ] **Given** a successful reconnect, **When** the WebSocket is re-established, **Then** the trades stream subscription is automatically restored and data resumes
- [ ] **Given** a malformed message is received from Hyperliquid, **When** the backend attempts to parse it, **Then** it logs a warning via Serilog and continues processing without crashing
- [ ] **Given** WebSocket lifecycle events occur (connect, disconnect, reconnect, subscribe, error), **When** each event happens, **Then** it is logged with structured logging via Serilog

## Objectives

- Prove the full WebSocket → .NET → SignalR → Angular real-time streaming pipeline
- Implement automatic reconnection with exponential backoff (1s–60s, 20 retries max)
- Display a live price ticker and rolling 15-minute chart in the Angular UI
- Establish connection management patterns reusable by future features (F7, F8)
- Add structured logging (Serilog) for all WebSocket lifecycle events

### Discovery References

**Architecture Decision — BackgroundService + SignalR co-located in TradePilot.Api:**
- Worker project is an empty stub; cross-process SignalR (IHubContext) would require Redis backplane
- For POC, co-locating BackgroundService in TradePilot.Api is the pragmatic choice
- Production migration to Worker + Redis backplane deferred to F8/scaling phase

**Key Constraints (from `.agent-context/0-knowledge/`):**
- Market data WebSocket streams are shared and unauthenticated (02-hyperliquid-integration.md)
- Scheduling architecture expects WebSocket → MarketStateStore → CandleClock pipeline (19-scheduling-architecture.md)
- F4 establishes the WebSocket entry point; CandleClock integration is separate
- ADR 14: bypass MediatR for raw exchange reads — WebSocket data flows directly, not through CQRS

**CORS Requirement:** Current CORS policy missing `AllowCredentials()` required by SignalR. Must update.

**Angular Pattern Conflict:** Instructions file says `standalone: false`, but actual codebase uses `standalone: true` everywhere with `inject()` function DI. Plan follows actual codebase patterns.

### Project Patterns

- `src/TradePilot.Infrastructure/Services/HyperliquidRestClient.cs` — Typed HttpClient pattern for Hyperliquid REST API; model for WebSocket client structure
- `src/TradePilot.Application/Abstractions/Services/IHyperliquidRestClient.cs` — Interface-in-Application pattern for infrastructure contracts
- `src/TradePilot.Application/Abstractions/Configuration/HyperliquidOptions.cs` — Options pattern with startup validation
- `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidAssetMapper.cs` — Coin/timeframe mapping utility
- `src/TradePilot.Infrastructure/Hyperliquid/Models/HyperliquidCandle.cs` — Hyperliquid JSON model with `[JsonPropertyName]` attributes
- `src/TradePilot.Application/MarketData/Models/MarketInfoDto.cs` — Application-layer DTO pattern for market data
- `src/TradePilot.Api/Program.cs` — DI composition root, CORS config, middleware pipeline
- `src/TradePilot.Api/Infrastructure/ApiController.cs` — Base controller with MediatR
- `tests/TradePilot.Infrastructure.Tests/Services/HyperliquidSignerTests.cs` — Unit test pattern (MSTest + Moq + FluentAssertions)
- `tests/TradePilot.Api.Tests/Infrastructure/BaseControllerTests.cs` — Integration test base with WebApplicationFactory
- `tests/TradePilot.Api.Tests/Controllers/MarketDataControllerTests.cs` — Controller integration test pattern
- `frontend/trading-ui/src/app/core/services/market-data.service.ts` — Angular service pattern with ApiRestClient
- `frontend/trading-ui/src/app/features/market-data/market-data.component.ts` — Feature component with BehaviorSubject, takeUntilDestroyed
- `frontend/trading-ui/src/app/app.component.html` — App shell navbar layout
- `frontend/trading-ui/src/app/app.config.ts` — Root providers configuration
- `frontend/trading-ui/proxy.conf.json` — Dev server proxy config

### [x] Phase 1: Backend — WebSocket Client & Models

**Complexity**: High | **Risk**: Medium

- [x] Task 1.1: Add `WsBaseUrl` to `HyperliquidOptions`
  - Details: .agent-context/3-develop/build/plans/details/20260324-f4-market-data-websocket-phase-01-details.md#task-11-add-wsbaseurl-to-hyperliquidoptions

- [x] Task 1.2: Create WebSocket message models
  - Details: .agent-context/3-develop/build/plans/details/20260324-f4-market-data-websocket-phase-01-details.md#task-12-create-websocket-message-models

- [x] Task 1.3: Create `IHyperliquidWebSocketClient` interface
  - Details: .agent-context/3-develop/build/plans/details/20260324-f4-market-data-websocket-phase-01-details.md#task-13-create-ihyperliquidwebsocketclient-interface

- [x] Task 1.4: Create `HyperliquidWebSocketClient` implementation
  - Details: .agent-context/3-develop/build/plans/details/20260324-f4-market-data-websocket-phase-01-details.md#task-14-create-hyperliquidwebsocketclient-implementation

- [x] Task 1.5: Create `PriceUpdateDto` and `ConnectionStatusDto`
  - Details: .agent-context/3-develop/build/plans/details/20260324-f4-market-data-websocket-phase-01-details.md#task-15-create-priceupdatedto-and-connectionstatusdto

- [x] Task 1.6: Update configuration files
  - Details: .agent-context/3-develop/build/plans/details/20260324-f4-market-data-websocket-phase-01-details.md#task-16-update-configuration-files

- [x] Task 1.7: Unit tests for `HyperliquidWebSocketClient`
  - Details: .agent-context/3-develop/build/plans/details/20260324-f4-market-data-websocket-phase-01-details.md#task-17-unit-tests-for-hyperliquidwebsocketclient

### [x] Phase 2: Backend — SignalR Hub & Stream Service

**Complexity**: High | **Risk**: Medium

- [x] Task 2.1: Register SignalR and update CORS in `Program.cs`
  - Details: .agent-context/3-develop/build/plans/details/20260324-f4-market-data-websocket-phase-02-details.md#task-21-register-signalr-and-update-cors

- [x] Task 2.2: Create `MarketDataHub`
  - Details: .agent-context/3-develop/build/plans/details/20260324-f4-market-data-websocket-phase-02-details.md#task-22-create-marketdatahub

- [x] Task 2.3: Create `MarketDataStreamService` BackgroundService
  - Details: .agent-context/3-develop/build/plans/details/20260324-f4-market-data-websocket-phase-02-details.md#task-23-create-marketdatastreamservice-backgroundservice

- [x] Task 2.4: Integration tests for SignalR and stream service
  - Details: .agent-context/3-develop/build/plans/details/20260324-f4-market-data-websocket-phase-02-details.md#task-24-integration-tests-for-signalr-and-stream-service

### [x] Phase 3: Frontend — SignalR Client, Price Ticker & Connection Status

**Complexity**: Medium | **Risk**: Low

- [x] Task 3.1: Install `@microsoft/signalr` and update proxy config
  - Details: .agent-context/3-develop/build/plans/details/20260324-f4-market-data-websocket-phase-03-details.md#task-31-install-signalr-and-update-proxy-config

- [x] Task 3.2: Create SignalR service
  - Details: .agent-context/3-develop/build/plans/details/20260324-f4-market-data-websocket-phase-03-details.md#task-32-create-signalr-service

- [x] Task 3.3: Create price update and connection status models
  - Details: .agent-context/3-develop/build/plans/details/20260324-f4-market-data-websocket-phase-03-details.md#task-33-create-price-update-and-connection-status-models

- [x] Task 3.4: Create price ticker component
  - Details: .agent-context/3-develop/build/plans/details/20260324-f4-market-data-websocket-phase-03-details.md#task-34-create-price-ticker-component

- [x] Task 3.5: Add connection status indicator to app navbar
  - Details: .agent-context/3-develop/build/plans/details/20260324-f4-market-data-websocket-phase-03-details.md#task-35-add-connection-status-indicator-to-app-navbar

- [x] Task 3.6: Frontend build and lint verification
  - Details: .agent-context/3-develop/build/plans/details/20260324-f4-market-data-websocket-phase-03-details.md#task-36-frontend-build-and-lint-verification

### [x] Phase 4: Frontend — Rolling 15-Minute Price Chart

**Complexity**: Medium | **Risk**: Medium

- [x] Task 4.1: Install `lightweight-charts` and create chart component
  - Details: .agent-context/3-develop/build/plans/details/20260324-f4-market-data-websocket-phase-04-details.md#task-41-install-lightweight-charts-and-create-chart-component

- [x] Task 4.2: Integrate chart into market data feature
  - Details: .agent-context/3-develop/build/plans/details/20260324-f4-market-data-websocket-phase-04-details.md#task-42-integrate-chart-into-market-data-feature

- [x] Task 4.3: Frontend build and lint verification
  - Details: .agent-context/3-develop/build/plans/details/20260324-f4-market-data-websocket-phase-04-details.md#task-43-frontend-build-and-lint-verification

## Scoping Summary

| Phase | Complexity | Risk |
|-------|-----------|------|
| Phase 1: Backend — WebSocket Client & Models | High | Medium |
| Phase 2: Backend — SignalR Hub & Stream Service | High | Medium |
| Phase 3: Frontend — SignalR Client, Price Ticker & Connection Status | Medium | Low |
| Phase 4: Frontend — Rolling 15-Minute Price Chart | Medium | Medium |
| **Total** | **High** | **Medium** |

### Scoping Notes

- BackgroundService co-located in TradePilot.Api for POC simplicity; migration to Worker with Redis backplane deferred
- Serilog structured logging assumed available via `ILogger<T>` (Microsoft.Extensions.Logging abstractions already in use)
- Hyperliquid testnet WebSocket endpoint assumed as `wss://api.hyperliquid-testnet.xyz/ws` (inferred from REST base URL pattern)
- Exact Hyperliquid trade message format should be verified during implementation; models based on documented API
- Angular follows actual codebase patterns (`standalone: true`, `inject()` function) not instruction file's `standalone: false`
- No domain entities created in TradePilot.Domain — all DTOs live in Application layer (consistent with existing patterns)
- 24h stats seeded from existing REST endpoint (`IHyperliquidRestClient.GetMarketInfoAsync`) at startup

## Dependencies

- `@microsoft/signalr` npm package (Angular SignalR client)
- `lightweight-charts` npm package (TradingView charting library)
- ASP.NET Core SignalR (bundled in `Microsoft.NET.Sdk.Web`, no extra NuGet)
- Existing `IHyperliquidRestClient` for 24h stats seeding
- Existing `HyperliquidAssetMapper` for coin name normalization

## Success Criteria

- Real-time BTC-PERP price updates flow from Hyperliquid WebSocket → Backend → SignalR → Angular UI
- Price ticker displays last price, 24h high, 24h low, and 24h volume with ~500ms update latency
- Rolling 15-minute line chart renders and updates in real-time
- Global connection status indicator shows Connected/Reconnecting/Disconnected states
- WebSocket automatically reconnects with exponential backoff (1s–60s, 20 retry max)
- All lifecycle events are logged via structured logging
- All backend tests pass (`dotnet test`)
- Angular build and lint pass (`ng build`, `ng lint`)

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-03-24T10:00:00Z | 2026-03-24T10:45:00Z |
| Plan Reviewer | plan-reviewed | 2026-03-24T23:18:27Z | 2026-03-24T23:23:13Z |
| 3-Develop: 2 Implementer | implemented | 2026-03-24T23:23:13Z | 2026-03-24T23:23:13Z |
| 3-Develop: 3 Reviewer | complete | 2026-03-25T00:19:00Z | 2026-03-25T00:21:25Z |
