# PBI Specification: F7 — User Event Stream (WebSocket)

**Date:** 2026-03-24  
**Author:** PRD Agent  
**Status:** Draft  
**PRD:** [hyperliquid-poc-prd.md](../../prd-approved/hyperliquid-poc-prd.md)  
**Implementation Phase:** 7  
**Risk Level:** Medium  
**Depends On:** F1, F4, F5

---

## Summary

Subscribe to per-wallet WebSocket events from Hyperliquid to receive real-time fill and order update notifications, relay them to Angular via SignalR, and display them in a live activity feed.

### User Story

> As a **developer**, I want to **receive real-time notifications when my orders fill or positions change** so that **I can validate the per-wallet WebSocket subscription and real-time event relay**.

### Business Value

Proves per-wallet WebSocket subscriptions work, which is critical for the production system where each subscriber needs their own event stream. Also validates that the UI can reactively update positions and orders tables from events rather than polling alone.

---

## Requirements

### Functional Requirements

- [ ] Subscribe to user-specific WebSocket events (fills, order updates) using wallet address
- [ ] Backend relays fill and order update events to Angular via SignalR
- [ ] Angular UI shows a live activity feed / event log
- [ ] Fill events update the positions table automatically
- [ ] Order status changes reflected in the orders table without manual refresh

### Non-Functional Requirements

- [ ] Activity feed shows newest events at the top
- [ ] Feed retains events for the current session only (in-memory, not persisted)

---

## User Flow

### Happy Path — Fill Event

1. Developer places a market order (F5) or a limit order fills
2. Hyperliquid sends a fill event via WebSocket
3. Backend receives the event and relays it to Angular via SignalR
4. Activity feed shows a new entry: timestamp, "Fill", asset, side, size, price
5. Positions table in the dashboard (F2) updates automatically

### Happy Path — Order Update Event

1. Developer places a limit order; it appears in orders table
2. Order partially fills or is modified externally
3. Hyperliquid sends an order update event via WebSocket
4. Activity feed shows the update
5. Orders table reflects the new status/size

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| User WebSocket disconnects | Status indicator shows issue; reconnect with backoff; resubscribe |
| Fill event for unknown order | Event logged in activity feed; no crash |
| Event with unexpected format | Backend logs warning; event skipped; feed unaffected |
| SignalR connection drops | Angular reconnects independently; missed events not replayed (poll catches up) |

---

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

> **Note:** Exact message format must be verified against Hyperliquid documentation during implementation.

### Key Components

| Component | Action |
|-----------|--------|
| `HyperliquidWebSocketClient` | Manages per-wallet WebSocket subscription (may be same or separate connection from market data) |
| `MarketDataStreamService` | Extends to manage user event subscriptions alongside market data |
| `MarketDataHub` | Additional SignalR methods for fill and order update events |
| `signalr.service.ts` | Angular service handling fill and order update events |
| Activity Feed feature component | Renders timestamped event log |
| Dashboard component | Reactively updates positions/orders tables from events |

### Connection Management Question

The PRD assumption (A-4) states that shared market data and per-wallet events may or may not work on the same WebSocket connection. Implementation should:
1. Try shared connection first
2. Fall back to separate connections if required
3. Document which approach works

---

## Out of Scope

- Event persistence / history beyond current session
- Event filtering or search in the activity feed
- Push notifications or alerts
- Multiple wallet subscriptions

---

## Open Questions

*None at this time.*

---

## Acceptance Criteria

- [ ] Backend subscribes to per-wallet WebSocket events using the configured wallet address
- [ ] Fill events are relayed to Angular and appear in the activity feed in real-time
- [ ] Order update events are relayed to Angular and appear in the activity feed
- [ ] Fill events automatically update the positions table in the dashboard
- [ ] Order status changes automatically update the orders table in the dashboard
- [ ] Activity feed shows newest events at the top with timestamp, type, and details
- [ ] Connection issues with the user event stream are visible in the UI

---

## Related Features

- **F4** — Market data WebSocket proves the connection management; this feature adds per-wallet subscriptions
- **F5** — Orders placed in F5 generate the fill events consumed here
- **F2** — Dashboard tables updated reactively from events
- **F8** — Resilience hardening applies to this WebSocket connection
