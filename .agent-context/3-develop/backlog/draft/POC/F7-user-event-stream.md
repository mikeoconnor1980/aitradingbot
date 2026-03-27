# PBI Specification: F7 — User Event Stream (WebSocket)

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-24
**PRD:** [hyperliquid-poc-prd.md](../../prd-approved/hyperliquid-poc-prd.md)
**Implementation Phase:** 7
**Risk Level:** Medium
**Depends On:** F1, F4, F5

---

## Summary

Subscribe to per-wallet WebSocket events from Hyperliquid to receive real-time fill and order update notifications, relayed to the Angular dashboard via SignalR with a live activity feed. When active, this becomes the primary update mechanism for positions and orders, with F2 polling retained as a fallback.

### User Story

> As a **developer**, I want to **receive real-time notifications when my orders fill or positions change** so that **I can validate the per-wallet WebSocket subscription and real-time event relay**.

### Business Value

Proves the per-wallet WebSocket subscription capability that the production trading engine depends on. Validates that Hyperliquid's user event streams work from .NET and can reactively update the Angular UI.

---

## Problem Statement

The production trading system requires per-wallet WebSocket subscriptions so each subscriber receives their own fill and order update events. This capability must be proven in the POC to confirm that Hyperliquid's per-user event streams work from .NET and that events can be relayed to the Angular UI reactively.

> **Note:** When F7 is active, the F2 dashboard polling (2-second interval) is retained as a fallback for missed events (e.g. during SignalR reconnection) but is no longer the primary update path for positions and orders.

## Requirements

### Functional Requirements

1. Subscribe to user-specific WebSocket events (fills, order updates) using the wallet address from F1 configuration
2. Backend relays fill and order update events to Angular via SignalR **immediately** as they arrive (no throttling — user events are low-frequency)
3. Angular UI shows a live **activity feed** as a dedicated tab in the F2 dashboard, displaying timestamped event entries
4. Activity feed shows newest events at the top; capped at **100 events** (oldest discarded when limit reached)
5. Fill events automatically update the positions table in the F2 dashboard via a shared Angular state service (`BehaviorSubject`)
6. Order status changes automatically update the orders table in the F2 dashboard via the same shared state service
7. The F4 global connection status indicator in the navbar is **extended** to include user event stream status (aggregated view of all WebSocket connections)
8. Automatic reconnection on user event WebSocket disconnect with **exponential backoff** (1s initial, 60s max, 20 retry cap) — same parameters as F4
9. After successful reconnect, automatically resubscribe to the `userEvents` stream
10. All user event WebSocket lifecycle events logged with **structured logging (Serilog)**: connect, disconnect, reconnect, subscribe, error

### Non-Functional Requirements

- Feed retains events for the current session only (in-memory, not persisted)
- User events relayed without throttling; SignalR hub reconnection handled independently of Hyperliquid WebSocket
- Reconnection backoff: initial 1s, exponential increase, max 60s, cap at 20 retries

## User Flow

### Happy Path — Fill Event

1. Developer places a market order (F5) or a limit order fills
2. Hyperliquid sends a fill event via WebSocket
3. Backend receives the event and relays it to Angular via SignalR immediately
4. Activity feed shows a new entry: timestamp, "Fill", asset, side, size, price
5. Positions table in the dashboard (F2) updates automatically via shared state service

### Happy Path — Order Update Event

1. Developer places a limit order; it appears in orders table
2. Order partially fills or is modified externally
3. Hyperliquid sends an order update event via WebSocket
4. Activity feed shows the update
5. Orders table reflects the new status/size via shared state service

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| User WebSocket disconnects | Global status indicator shows degraded state; reconnect with exponential backoff (1s→60s, 20 retries); resubscribe on success |
| Reconnection retries exhausted | Global status indicator shows "Disconnected" with error detail; manual refresh or page reload required |
| Fill event for unknown order | Event logged in activity feed; no crash |
| Event with unexpected format | Backend logs warning via Serilog; event skipped; feed unaffected |
| SignalR connection drops | Angular reconnects independently; missed events not replayed (poll catches up via F2 REST endpoints) |

## Technical Considerations

### SignalR Hub Methods

| Method | Direction | Payload |
|--------|-----------|---------|
| `ReceiveFillEvent` | Server → Client | `{ timestamp, asset, side, size, price, fee, orderId }` |
| `ReceiveOrderUpdate` | Server → Client | `{ timestamp, orderId, asset, status, filledSize, remainingSize }` |
| `ReceiveUserConnectionStatus` | Server → Client | `{ source, status, detail }` |

### WebSocket Subscription (Per-Wallet)

```json
{
  "method": "subscribe",
  "subscription": {
    "type": "userEvents",
    "user": "0x1a2b...wallet_address"
  }
}
```

> **Note:** Exact message format must be verified against Hyperliquid documentation during implementation. The wallet address is obtained from the F1 configuration (user's connected wallet).

### Key Components

| Component | Action |
|-----------|--------|
| `HyperliquidWebSocketClient` | Manages per-wallet WebSocket subscription (may be same or separate connection from market data) |
| `MarketDataStreamService` | Extends to manage user event subscriptions alongside market data |
| `MarketDataHub` | Additional SignalR methods for fill and order update events |
| `signalr.service.ts` | Angular service handling fill and order update events |
| Shared state service (Angular) | Holds positions and orders state as `BehaviorSubject`; updated from SignalR events; consumed by dashboard components |
| Activity Feed component | Dashboard tab rendering timestamped event log (newest first, 100-event cap) |
| Connection status indicator (F4) | Extended to aggregate user event stream status alongside market data status |

### Connection Management

The PRD assumption (A-4) states that shared market data and per-wallet events may or may not work on the same WebSocket connection. Implementation should:
1. Try shared connection first
2. Fall back to separate connections if required
3. Document which approach works

### Jobs

The user event WebSocket listener runs as a background task within the `TradingApp.Worker` project, managed alongside the existing market data WebSocket from F4.

---

## Out of Scope

- Event persistence / history beyond current session
- Event filtering or search in the activity feed
- Push notifications or alerts
- Multiple wallet subscriptions
- Event replay for missed events during SignalR disconnection

---

## Open Questions

*None at this time.*

---

## Acceptance Criteria

- [ ] **Given** the backend starts with a valid wallet address from F1 configuration, **When** the worker initialises, **Then** it subscribes to the Hyperliquid `userEvents` WebSocket stream for that wallet address
- [ ] **Given** a market order is placed via F5 and fills on the exchange, **When** Hyperliquid sends a fill event, **Then** the event appears in the activity feed within 2 seconds showing timestamp, "Fill", asset, side, size, and price
- [ ] **Given** a limit order is placed and partially fills, **When** Hyperliquid sends an order update event, **Then** the event appears in the activity feed showing timestamp, order ID, asset, new status, filled size, and remaining size
- [ ] **Given** a fill event is received via SignalR, **When** the shared state service processes it, **Then** the positions table in the F2 dashboard updates automatically without manual refresh
- [ ] **Given** an order update event is received via SignalR, **When** the shared state service processes it, **Then** the orders table in the F2 dashboard reflects the new status and sizes without manual refresh
- [ ] **Given** the activity feed contains 100 events, **When** a new event arrives, **Then** the oldest event is discarded and the new event appears at the top
- [ ] **Given** the user event WebSocket disconnects, **When** the reconnection process starts, **Then** the global connection status indicator shows a degraded state and the backend retries with exponential backoff (1s initial, 60s max)
- [ ] **Given** the user event WebSocket reconnects successfully, **When** the connection is re-established, **Then** the backend automatically resubscribes to `userEvents` and the global status indicator returns to "Connected"
- [ ] **Given** reconnection retries are exhausted (20 attempts), **When** the final retry fails, **Then** the global status indicator shows "Disconnected" with an error detail message
- [ ] **Given** the backend receives an event with an unexpected format, **When** deserialization fails, **Then** the event is skipped, a warning is logged via Serilog, and the activity feed remains unaffected

### Release Notes Information

- **Heading**: Real-Time User Event Stream
- **Release note type**: Feature
- **Release Note Summary**: Subscribe to per-wallet WebSocket events from Hyperliquid to receive real-time fill and order update notifications, relayed to the Angular dashboard via SignalR with a live activity feed.
- **Release Notes Audience**: Product
- **Breaking Change**: No

---

## Related Features

- **F4** — Market data WebSocket proves the connection management; this feature adds per-wallet subscriptions and extends the global status indicator
- **F5** — Orders placed in F5 generate the fill events consumed here
- **F2** — Dashboard tables updated reactively from events via shared state service; activity feed added as new dashboard tab
- **F8** — Resilience hardening applies to this WebSocket connection
