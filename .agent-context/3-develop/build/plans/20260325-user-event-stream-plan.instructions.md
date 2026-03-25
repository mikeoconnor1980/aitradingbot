---
applyTo: ".agent-context/3-develop/build/changes/20260325-user-event-stream-changes.md"
currentAgent: "None"
agentStartedAt: "2026-03-25T12:07:35Z"
status: "planned"
lastUpdated: "2026-03-25T13:00:00Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: F7 — User Event Stream (WebSocket)

## Overview

Subscribe to per-wallet WebSocket events from Hyperliquid for real-time fill and order update notifications, relayed to the Angular dashboard via SignalR with a live activity feed.

## PBI Details

**PBI ID:** Draft
**Status:** Draft
**Implementation Phase:** 7
**Risk Level:** Medium
**Depends On:** F1, F4, F5

Subscribe to per-wallet WebSocket events from Hyperliquid to receive real-time fill and order update notifications, relayed to the Angular dashboard via SignalR with a live activity feed. When active, this becomes the primary update mechanism for positions and orders, with F2 polling retained as a fallback.

### Acceptance Criteria

- [ ] Given the backend starts with a valid wallet address from F1 configuration, When the worker initialises, Then it subscribes to the Hyperliquid `userEvents` WebSocket stream for that wallet address
- [ ] Given a market order is placed via F5 and fills on the exchange, When Hyperliquid sends a fill event, Then the event appears in the activity feed within 2 seconds showing timestamp, "Fill", asset, side, size, and price
- [ ] Given a limit order is placed and partially fills, When Hyperliquid sends an order update event, Then the event appears in the activity feed showing timestamp, order ID, asset, new status, filled size, and remaining size
- [ ] Given a fill event is received via SignalR, When the shared state service processes it, Then the positions table in the F2 dashboard updates automatically without manual refresh
- [ ] Given an order update event is received via SignalR, When the shared state service processes it, Then the orders table in the F2 dashboard reflects the new status and sizes without manual refresh
- [ ] Given the activity feed contains 100 events, When a new event arrives, Then the oldest event is discarded and the new event appears at the top
- [ ] Given the user event WebSocket disconnects, When the reconnection process starts, Then the global connection status indicator shows a degraded state and the backend retries with exponential backoff (1s initial, 60s max)
- [ ] Given the user event WebSocket reconnects successfully, When the connection is re-established, Then the backend automatically resubscribes to `userEvents` and the global status indicator returns to "Connected"
- [ ] Given reconnection retries are exhausted (20 attempts), When the final retry fails, Then the global status indicator shows "Disconnected" with an error detail message
- [ ] Given the backend receives an event with an unexpected format, When deserialization fails, Then the event is skipped, a warning is logged via Serilog, and the activity feed remains unaffected

## Objectives

- Prove per-wallet WebSocket subscription capability from .NET to Hyperliquid
- Relay user events (fills, order updates) to Angular via SignalR in real-time
- Display events in a live activity feed tab on the F2 dashboard
- Reactively update positions and orders tables via shared Angular state service
- Extend F4 connection status indicator to aggregate user event stream status
- Implement exponential backoff reconnection matching F4 parameters (1s/60s/20 retries)

### Discovery References

**Key Design Decisions:**
- **Hosting**: UserEventStreamService runs as a BackgroundService in TradingApp.Api (follows F4 MarketDataStreamService pattern; IHubContext is only available in the API process)
- **WebSocket**: New IHyperliquidUserEventClient with separate WebSocket connection (does not modify existing market data client)
- **SignalR**: Extends existing MarketDataHub with new push methods; uses Clients.All for POC (single-user)
- **Angular State**: New AccountStateService with BehaviorSubject replaces polling for positions/orders; SignalRService extended for new events
- **Connection Status**: Existing navbar indicator aggregates both market data and user event stream status

**Hyperliquid userEvents Subscription:**
```json
{ "method": "subscribe", "subscription": { "type": "userEvents", "user": "0x...wallet_address" } }
```
> Note: Exact response message format must be verified against Hyperliquid docs during implementation.

**Pattern Precedent:**
- MarketDataStreamService → UserEventStreamService (same reconnection loop, exponential backoff, IHubContext broadcast)
- HyperliquidWebSocketClient → HyperliquidUserEventClient (same ClientWebSocket lifecycle, but for userEvents channel)
- SignalRService.priceUpdate$ → new fillEvent$/orderUpdate$ observables (same Subject→Observable pattern)
- HealthService BehaviorSubject → AccountStateService BehaviorSubject (same state management pattern)

### Project Patterns

- `src/TradingApp.Infrastructure/Services/HyperliquidWebSocketClient.cs` - WebSocket client with singleton pattern, callback handlers, channel message routing
- `src/TradingApp.Application/Abstractions/Services/IHyperliquidWebSocketClient.cs` - WebSocket client interface contract
- `src/TradingApp.Api/Services/MarketDataStreamService.cs` - BackgroundService with exponential backoff reconnection and SignalR broadcast
- `src/TradingApp.Api/Hubs/MarketDataHub.cs` - Thin SignalR hub with lifecycle logging
- `src/TradingApp.Api/Program.cs` - DI registration, SignalR setup, hub mapping
- `src/TradingApp.Application/MarketData/Models/ConnectionStatusDto.cs` - Connection status DTO (reusable for user event status)
- `src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidSubscribeRequest.cs` - Subscribe request serialization model
- `src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidWebSocketMessage.cs` - Generic WS message envelope
- `src/TradingApp.Infrastructure/Services/HyperliquidSigner.cs` - Derives WalletAddress from private key
- `frontend/trading-ui/src/app/core/services/signalr.service.ts` - SignalR hub connection, event registration, connection status
- `frontend/trading-ui/src/app/core/services/health.service.ts` - BehaviorSubject state management pattern
- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` - Dashboard with polling, mat-tab-group
- `frontend/trading-ui/src/app/app.component.ts` - Navbar connection status indicator
- `tests/TradingApp.Api.Tests/Services/MarketDataStreamServiceTests.cs` - Stream service unit test pattern (mock chain, callback capture)
- `tests/TradingApp.Api.Tests/Hubs/MarketDataHubTests.cs` - Hub integration test pattern (WebApplicationFactory, LongPolling)
- `tests/TradingApp.Infrastructure.Tests/Services/HyperliquidWebSocketClientTests.cs` - WebSocket client unit test pattern

### [ ] Phase 1: Backend — User Event WebSocket Client & Models

**Complexity**: Medium | **Risk**: Medium

- [ ] Task 1.1: Create Hyperliquid user event infrastructure models
  - Details: .agent-context/3-develop/build/plans/details/20260325-user-event-stream-phase-01-details.md#task-11-create-hyperliquid-user-event-infrastructure-models

- [ ] Task 1.2: Create application-layer DTOs for SignalR payloads
  - Details: .agent-context/3-develop/build/plans/details/20260325-user-event-stream-phase-01-details.md#task-12-create-application-layer-dtos-for-signalr-payloads

- [ ] Task 1.3: Create IHyperliquidUserEventClient interface
  - Details: .agent-context/3-develop/build/plans/details/20260325-user-event-stream-phase-01-details.md#task-13-create-ihyperliquidusereventclient-interface

- [ ] Task 1.4: Implement HyperliquidUserEventClient
  - Details: .agent-context/3-develop/build/plans/details/20260325-user-event-stream-phase-01-details.md#task-14-implement-hyperliquidusereventclient

- [ ] Task 1.5: Add unit tests for HyperliquidUserEventClient
  - Details: .agent-context/3-develop/build/plans/details/20260325-user-event-stream-phase-01-details.md#task-15-add-unit-tests-for-hyperliquidusereventclient

- [ ] Task 1.6: Run all backend tests and verify no regressions
  - Details: .agent-context/3-develop/build/plans/details/20260325-user-event-stream-phase-01-details.md#task-16-run-all-backend-tests-and-verify-no-regressions

### [ ] Phase 2: Backend — Stream Service & SignalR Relay

**Complexity**: Medium | **Risk**: Low

- [ ] Task 2.1: Create UserEventStreamService (BackgroundService)
  - Details: .agent-context/3-develop/build/plans/details/20260325-user-event-stream-phase-02-details.md#task-21-create-usereventstreamservice

- [ ] Task 2.2: Register DI and configuration in Program.cs
  - Details: .agent-context/3-develop/build/plans/details/20260325-user-event-stream-phase-02-details.md#task-22-register-di-and-configuration

- [ ] Task 2.3: Add unit tests for UserEventStreamService
  - Details: .agent-context/3-develop/build/plans/details/20260325-user-event-stream-phase-02-details.md#task-23-add-unit-tests-for-usereventstreamservice

- [ ] Task 2.4: Add SignalR hub integration test for user events
  - Details: .agent-context/3-develop/build/plans/details/20260325-user-event-stream-phase-02-details.md#task-24-add-signalr-hub-integration-test

- [ ] Task 2.5: Run all backend tests and verify no regressions
  - Details: .agent-context/3-develop/build/plans/details/20260325-user-event-stream-phase-02-details.md#task-25-run-all-backend-tests

### [ ] Phase 3: Frontend — Shared State Service & SignalR Integration

**Complexity**: Medium | **Risk**: Low

- [ ] Task 3.1: Create Angular models and DTOs for user events
  - Details: .agent-context/3-develop/build/plans/details/20260325-user-event-stream-phase-03-details.md#task-31-create-angular-models-and-dtos

- [ ] Task 3.2: Create AccountStateService with BehaviorSubject state
  - Details: .agent-context/3-develop/build/plans/details/20260325-user-event-stream-phase-03-details.md#task-32-create-accountstateservice

- [ ] Task 3.3: Extend SignalRService with user event handlers
  - Details: .agent-context/3-develop/build/plans/details/20260325-user-event-stream-phase-03-details.md#task-33-extend-signalrservice-with-user-event-handlers

- [ ] Task 3.4: Update connection status aggregation
  - Details: .agent-context/3-develop/build/plans/details/20260325-user-event-stream-phase-03-details.md#task-34-update-connection-status-aggregation

- [ ] Task 3.5: Run frontend build and lint
  - Details: .agent-context/3-develop/build/plans/details/20260325-user-event-stream-phase-03-details.md#task-35-run-frontend-build-and-lint

### [ ] Phase 4: Frontend — Activity Feed & Dashboard Integration

**Complexity**: Low | **Risk**: Low

- [ ] Task 4.1: Create ActivityFeedComponent
  - Details: .agent-context/3-develop/build/plans/details/20260325-user-event-stream-phase-04-details.md#task-41-create-activityfeedcomponent

- [ ] Task 4.2: Integrate activity feed and shared state into dashboard
  - Details: .agent-context/3-develop/build/plans/details/20260325-user-event-stream-phase-04-details.md#task-42-integrate-activity-feed-and-shared-state-into-dashboard

- [ ] Task 4.3: Run frontend build and lint
  - Details: .agent-context/3-develop/build/plans/details/20260325-user-event-stream-phase-04-details.md#task-43-run-frontend-build-and-lint

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: Backend — User Event WebSocket Client & Models | Medium | Medium |
| Phase 2: Backend — Stream Service & SignalR Relay | Medium | Low |
| Phase 3: Frontend — Shared State Service & SignalR Integration | Medium | Low |
| Phase 4: Frontend — Activity Feed & Dashboard Integration | Low | Low |
| **Overall** | **Medium** | **Low–Medium** |

### Scoping Notes

- Hyperliquid `userEvents` WebSocket message format must be verified against live API docs during Phase 1 implementation; models may need adjustment
- POC uses `Clients.All` for SignalR broadcast (single-user); per-user routing deferred to multi-tenancy phase
- Activity feed is session-only, in-memory (no persistence); 100-event cap with oldest-discarded strategy
- F2 polling (2s interval) retained as fallback alongside reactive SignalR updates
- Wallet address derived from `IHyperliquidSigner.WalletAddress` (already available from F1 configuration)

## Dependencies

- F1 (Wallet Connection) — provides `IHyperliquidSigner` with `WalletAddress` for subscription
- F4 (Market Data WebSocket) — provides existing `MarketDataStreamService`, `MarketDataHub`, `SignalRService` patterns to extend
- F5 (Order Management) — orders placed in F5 generate the fill events consumed here
- `@microsoft/signalr ^10.0.0` — already installed in frontend
- `System.Net.WebSockets.Client` — already used by `HyperliquidWebSocketClient`

## Success Criteria

- Backend subscribes to Hyperliquid `userEvents` stream on startup using configured wallet address
- Fill events relay through SignalR and appear in the activity feed within 2 seconds
- Order update events relay through SignalR and appear in the activity feed
- Positions table updates reactively from fill events (no manual refresh required)
- Orders table updates reactively from order update events (no manual refresh required)
- Activity feed caps at 100 events, newest first, oldest discarded
- Connection status indicator aggregates both market data and user event stream status
- Exponential backoff reconnection works with 1s initial, 60s max, 20 retry cap
- Auto-resubscribe on successful reconnect
- All lifecycle events logged with structured Serilog logging
- All backend tests pass; frontend builds and lints cleanly

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-03-25T12:07:35Z | 2026-03-25T12:35:20Z |
| Plan Reviewer | complete | 2026-03-25T12:36:07Z | 2026-03-25T13:00:00Z |
