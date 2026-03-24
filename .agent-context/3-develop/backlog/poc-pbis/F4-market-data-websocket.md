# PBI Specification: F4 — Market Data (WebSocket)

**Date:** 2026-03-24  
**Author:** PRD Agent  
**Status:** Draft  
**PRD:** [hyperliquid-poc-prd.md](../../prd-approved/hyperliquid-poc-prd.md)  
**Implementation Phase:** 6  
**Risk Level:** Medium  
**Depends On:** F1, F3

---

## Summary

Establish a persistent WebSocket connection to Hyperliquid, stream real-time BTC-PERP price data, relay it to the Angular UI via SignalR, and handle reconnection automatically.

### User Story

> As a **developer**, I want to **see real-time price updates streamed to the UI** so that **I can validate the full WebSocket → .NET → SignalR → Angular real-time data pipeline**.

### Business Value

Proves the WebSocket streaming infrastructure that the production trading engine will depend on. Validates connection management, subscription protocol, SignalR relay, and reconnection — all critical for live trading.

---

## Requirements

### Functional Requirements

- [ ] Backend establishes WebSocket connection to Hyperliquid testnet
- [ ] Subscribe to trades stream and/or candle stream for BTC-PERP
- [ ] Backend pushes price updates to Angular via SignalR hub
- [ ] Angular UI shows live price ticker that updates in real-time
- [ ] Connection status indicator shows WebSocket state (connected / reconnecting)
- [ ] Automatic reconnection on disconnect with exponential backoff
- [ ] After reconnect, resubscribe to all active streams

### Non-Functional Requirements

- [ ] Reconnection backoff: initial 1s, max 30s, exponential increase
- [ ] SignalR hub reconnection handled independently of Hyperliquid WebSocket

---

## User Flow

### Happy Path

1. Developer starts the backend — background service opens WebSocket to Hyperliquid
2. Developer opens Angular UI — SignalR client connects to backend hub
3. Live price ticker begins updating with BTC-PERP trade/candle data
4. Connection indicator shows green "Connected" for WebSocket stream
5. Data continues flowing indefinitely until manually stopped

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| Hyperliquid WebSocket drops | Status shows "Reconnecting"; auto-reconnect with backoff; resubscribe on success |
| SignalR connection drops | Angular client reconnects independently; resumes receiving data |
| Both connections drop | Each reconnects independently; data resumes when both are restored |
| Hyperliquid sends unexpected message format | Backend logs warning; does not crash; skips malformed message |

---

## Technical Considerations

### Key Components

| Component | Action |
|-----------|--------|
| `HyperliquidWebSocketClient` | Manages WebSocket connection lifecycle, subscription, reconnection |
| `MarketDataStreamService` | .NET `BackgroundService` that starts/manages the WebSocket client |
| `MarketDataHub` | SignalR hub that broadcasts price updates to connected Angular clients |
| `signalr.service.ts` | Angular SignalR client service |
| Market Data feature component | Renders live price ticker and connection status |

### SignalR Hub Methods

| Method | Direction | Payload |
|--------|-----------|---------|
| `ReceivePriceUpdate` | Server → Client | `{ asset, price, timestamp }` |
| `ReceiveConnectionStatus` | Server → Client | `{ source, status, detail }` |

### WebSocket Subscription Message (Hyperliquid)

```json
{
  "method": "subscribe",
  "subscription": {
    "type": "trades",
    "coin": "BTC"
  }
}
```

> **Note:** Exact message format must be verified against Hyperliquid documentation during implementation. The above is an approximation.

### Reconnection Strategy

1. WebSocket disconnects → log event, set status to "Reconnecting"
2. Wait backoff interval (1s, 2s, 4s, 8s, ... max 30s)
3. Attempt reconnect
4. On success → resubscribe to all channels, set status to "Connected"
5. On failure → increment backoff, retry

---

## Out of Scope

- Orderbook streaming
- Multiple asset subscriptions
- Historical backfill after reconnect (REST resync for account data is F8)
- WebSocket message persistence or replay

---

## Open Questions

*None at this time.*

---

## Acceptance Criteria

- [ ] Backend connects to Hyperliquid WebSocket and subscribes to BTC-PERP trades/candles
- [ ] Price updates are pushed to Angular via SignalR and displayed in a live ticker
- [ ] WebSocket connection status is visible in the UI (connected / reconnecting)
- [ ] Automatic reconnection occurs within 30 seconds of a disconnect
- [ ] After reconnection, subscriptions are re-established automatically
- [ ] Malformed messages are logged but do not crash the service

---

## Related Features

- **F1** — Connectivity established before WebSocket can connect
- **F3** — REST market data proves the data model; WebSocket adds real-time
- **F7** — Per-wallet WebSocket (F7) builds on the connection management proven here
- **F8** — Error handling and resilience hardening applied to WebSocket reconnection
