<!-- markdownlint-disable-file -->
# Release Changes: F7 — User Event Stream (WebSocket)

**Related Plan**: 20260325-user-event-stream-plan.instructions.md
**Implementation Date**: 2026-03-26

## Summary

Subscribe to per-wallet WebSocket events from Hyperliquid for real-time fill and order update notifications, relayed to the Angular dashboard via SignalR with a live activity feed.

## Changes

### Added

<!-- Phase 1: Backend — User Event WebSocket Client & Models -->
- src/TradePilot.Infrastructure/Hyperliquid/Models/HyperliquidUserEventSubscription.cs: userEvents subscription request type with `user` field
- src/TradePilot.Infrastructure/Hyperliquid/Models/HyperliquidUserEventSubscribeRequest.cs: subscription request envelope (method + subscription)
- src/TradePilot.Infrastructure/Hyperliquid/Models/HyperliquidUserEventsMessage.cs: inbound user events message envelope with channel/data
- src/TradePilot.Infrastructure/Hyperliquid/Models/HyperliquidUserEventsData.cs: data payload with fills and orderUpdates arrays
- src/TradePilot.Infrastructure/Hyperliquid/Models/HyperliquidUserFill.cs: single fill record from WebSocket (coin, px, sz, side, time, fee, oid, hash, closedPnl, dir)
- src/TradePilot.Infrastructure/Hyperliquid/Models/HyperliquidOrderUpdate.cs: single order update record (order, status, statusTimestamp)
- src/TradePilot.Infrastructure/Hyperliquid/Models/HyperliquidOrderInfo.cs: order detail within an order update (coin, side, limitPx, sz, oid, timestamp, origSz)
- src/TradePilot.Application/MarketData/Models/FillEventDto.cs: SignalR payload DTO for fill events (Timestamp, Asset, Side, Size, Price, Fee, OrderId)
- src/TradePilot.Application/MarketData/Models/OrderUpdateDto.cs: SignalR payload DTO for order updates (Timestamp, OrderId, Asset, Status, FilledSize, RemainingSize)
- src/TradePilot.Application/Abstractions/Services/IHyperliquidUserEventClient.cs: interface for user event WebSocket client with connect/subscribe/receive/handler registration
- src/TradePilot.Infrastructure/Services/HyperliquidUserEventClient.cs: WebSocket client implementation managing its own connection, subscribe, receive loop, fill/order update dispatch
- tests/TradePilot.Infrastructure.Tests/Services/HyperliquidUserEventClientTests.cs: unit tests covering initial state, handler registration, and subscribe pre-condition validation

<!-- Phase 2: Backend — Stream Service & SignalR Relay -->
- src/TradePilot.Api/Services/UserEventStreamService.cs: BackgroundService with exponential backoff reconnection (1s/60s/20 retries), SignalR broadcast for fills, order updates, and connection status

<!-- Phase 3: Frontend — Shared State Service & SignalR Integration -->
- frontend/trading-ui/src/app/core/models/fill-event.model.ts: TypeScript interface for fill event SignalR payload
- frontend/trading-ui/src/app/core/models/order-update.model.ts: TypeScript interface for order update SignalR payload
- frontend/trading-ui/src/app/core/models/user-event.model.ts: Discriminated union type for activity feed events (Fill | OrderUpdate)
- frontend/trading-ui/src/app/core/services/account-state.service.ts: Shared Angular service with BehaviorSubject state for positions, orders, and events (100-event cap)

<!-- Phase 4: Frontend — Activity Feed & Dashboard Integration -->
- frontend/trading-ui/src/app/features/dashboard/activity-feed/activity-feed.component.ts: Standalone Angular component rendering live activity feed with type guards and display helpers
- frontend/trading-ui/src/app/features/dashboard/activity-feed/activity-feed.component.html: Template with @for loop, type badges, empty state, and date pipe formatting
- frontend/trading-ui/src/app/features/dashboard/activity-feed/activity-feed.component.scss: BEM-scoped styles for activity feed table, badges (fill/order), and empty state

### Modified

<!-- Phase 2: Backend — Stream Service & SignalR Relay -->
- src/TradePilot.Api/Program.cs: Added `IHyperliquidUserEventClient` singleton and `UserEventStreamService` hosted service registrations
- tests/TradePilot.Api.Tests/Hubs/MarketDataHubTests.cs: Added `IHyperliquidUserEventClient` mock to existing test; added new hub integration test `GivenSignalRHub_WhenUserEventStreamRegistered_ThenConnectionStillSucceeds`

<!-- Phase 3: Frontend — Shared State Service & SignalR Integration -->
- frontend/trading-ui/src/app/core/services/signalr.service.ts: Added ReceiveFillEvent, ReceiveOrderUpdate, ReceiveUserConnectionStatus handlers; updated _emitConnectionStatus() to aggregate user event stream status

<!-- Phase 4: Frontend — Activity Feed & Dashboard Integration -->
- frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts: Added ActivityFeedComponent import, AccountStateService injection; polling now writes through AccountStateService; ngOnInit subscribes to shared state positions$/orders$
- frontend/trading-ui/src/app/features/dashboard/dashboard.component.html: Added Activity tab with app-activity-feed to mat-tab-group

### Removed

## Test Results

<!-- Phase 1: Backend — User Event WebSocket Client & Models -->
- HyperliquidUserEventClientTests: 5/5 passed (new)
- TradePilot.Infrastructure.Tests: 30/30 passed
- TradePilot.Api.Tests: 41/41 passed

<!-- Phase 2: Backend — Stream Service & SignalR Relay -->
- UserEventStreamServiceTests: 3/3 passed (new)
- MarketDataHubTests.GivenSignalRHub_WhenUserEventStreamRegistered_ThenConnectionStillSucceeds: 1/1 passed (new)
- TradePilot.Api.Tests: 45/45 passed

<!-- Phase 3: Frontend — Shared State Service & SignalR Integration -->
- Angular build: succeeded (budget warning pre-existing, not introduced by this feature)
- Angular lint: All files pass linting

<!-- Phase 4: Frontend — Activity Feed & Dashboard Integration -->
- Angular build: succeeded (budget warning pre-existing, not introduced by this feature)
- Angular lint: All files pass linting

## Issues

<!-- Phase 1: Backend — User Event WebSocket Client & Models -->
- None

<!-- Phase 2: Backend — Stream Service & SignalR Relay -->
- None

<!-- Phase 3: Frontend — Shared State Service & SignalR Integration -->
- None

## Design Decisions

<!-- Phase 1: Backend — User Event WebSocket Client & Models -->
- ProcessMessageAsync routes on both "user" and "userEvents" channel names since exact Hyperliquid channel name is unverified at POC stage

<!-- Phase 2: Backend — Stream Service & SignalR Relay -->
- None — implemented exactly as specified

<!-- Phase 3: Frontend — Shared State Service & SignalR Integration -->
- None — implemented exactly as specified

<!-- Phase 4: Frontend — Activity Feed & Dashboard Integration -->
- None — implemented exactly as specified

## Review Hints

- HyperliquidUserEventsMessage channel routing checks both "user" and "userEvents" — needs verification against live Hyperliquid API once available

## Release Summary

F7 — User Event Stream (WebSocket) fully implemented across 4 phases:

**Backend** (Phases 1–2):
- New `HyperliquidUserEventClient` manages a dedicated WebSocket connection to Hyperliquid `userEvents`, separate from the market data connection
- 7 infrastructure models handle wire-format deserialization for fill and order update events
- `UserEventStreamService` (BackgroundService) connects on startup, subscribes using the configured wallet address, relays events to SignalR, and reconnects with exponential backoff (1s initial, 60s max, 20 retry cap)
- `IHyperliquidUserEventClient` and `UserEventStreamService` registered in DI

**Frontend** (Phases 3–4):
- `AccountStateService` is a new shared reactive state layer (BehaviorSubject) for positions, orders, and activity events (100-event cap, newest first)
- `SignalRService` extended with handlers for `ReceiveFillEvent`, `ReceiveOrderUpdate`, `ReceiveUserConnectionStatus`; connection status indicator now aggregates all three sources (SignalR transport + market data stream + user event stream)
- New `ActivityFeedComponent` displays live event feed as a third dashboard tab
- Dashboard refactored to write polling results through `AccountStateService` and subscribe reactively; pending order/position guards preserved

**Test coverage**: 8 new backend tests (5 unit + 3 service + 1 integration), 30/30 Infrastructure, 45/45 API tests

