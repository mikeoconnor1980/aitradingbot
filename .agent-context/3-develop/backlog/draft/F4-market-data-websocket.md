# PBI Specification: F4 — Market Data (WebSocket)

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-24
**PRD:** [hyperliquid-poc-prd.md](../../prd-approved/hyperliquid-poc-prd.md)
**Implementation Phase:** 6
**Risk Level:** Medium
**Depends On:** F1, F3

---

## Summary

Establish a persistent WebSocket connection to Hyperliquid, subscribe to the BTC-PERP trades stream, aggregate updates at a 500ms interval, relay them to the Angular UI via SignalR, and display a live price ticker with a rolling 15-minute price chart. Includes automatic reconnection with exponential backoff and a global connection status indicator.

### User Story

> As a **developer**, I want to **see real-time price updates streamed to the UI** so that **I can validate the full WebSocket → .NET → SignalR → Angular real-time data pipeline**.

### Business Value

Proves the WebSocket streaming infrastructure that the production trading engine will depend on. Validates connection management, subscription protocol, SignalR relay, throttled aggregation, and reconnection — all critical for live trading.

---

## Problem Statement

The production trading engine requires real-time price data via WebSocket for timely execution. This PBI proves the full streaming pipeline: Hyperliquid WebSocket → .NET aggregation → SignalR relay → Angular UI, including connection management and automatic reconnection.

---

## Requirements

### Functional Requirements

- [ ] Backend establishes WebSocket connection to Hyperliquid testnet
- [ ] Subscribe to **trades stream** for BTC-PERP (no candle stream in this PBI)
- [ ] Backend aggregates trade events and pushes updates to Angular via SignalR hub at a **500ms throttle interval**
- [ ] SignalR payload includes: last price, 24h high, 24h low, 24h volume
- [ ] **24h stats seeded** from REST API (F3) at startup, then updated incrementally from the trades stream
- [ ] Angular UI shows a **live price ticker** displaying last price, 24h high/low, and 24h volume
- [ ] Angular UI shows a **rolling 15-minute price chart** using Lightweight Charts (TradingView)
- [ ] **Global connection status indicator** in the app navbar showing WebSocket state (Connected / Reconnecting / Disconnected)
- [ ] Automatic reconnection on disconnect with **exponential backoff** (1s initial, 60s max)
- [ ] Reconnection capped at **20 retry attempts**; after exhaustion, status shows "Disconnected" with error detail
- [ ] After successful reconnect, resubscribe to the trades stream automatically
- [ ] All WebSocket lifecycle events logged with **structured logging (Serilog)**: connect, disconnect, reconnect, subscribe, error

### Non-Functional Requirements

- [ ] Reconnection backoff: initial 1s, exponential increase, max 60s, cap at 20 retries
- [ ] SignalR hub reconnection handled independently of Hyperliquid WebSocket
- [ ] 500ms throttle ensures the UI receives at most ~2 updates per second regardless of trade volume
- [ ] Chart must remain responsive with 15 minutes of accumulated trade data

---

## User Flow

### Happy Path

1. Developer starts the backend — `MarketDataStreamService` fetches 24h stats via REST (F3), then opens WebSocket to Hyperliquid and subscribes to BTC-PERP trades
2. Developer opens Angular UI — SignalR client connects to backend hub
3. Global navbar indicator turns green showing "Connected"
4. Live price ticker begins updating with last price, 24h high/low, and 24h volume
5. Rolling 15-minute price chart plots trade prices over time using Lightweight Charts
6. Data continues flowing indefinitely until manually stopped

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| Hyperliquid WebSocket drops | Global indicator shows "Reconnecting"; auto-reconnect with exponential backoff (1s–60s); resubscribe on success |
| 20 reconnect attempts exhausted | Global indicator shows "Disconnected" with error detail; no further automatic retries |
| SignalR connection drops | Angular client reconnects independently; resumes receiving data when reconnected |
| Both connections drop | Each reconnects independently; data resumes when both are restored |
| Hyperliquid sends unexpected message format | Backend logs warning via Serilog; does not crash; skips malformed message |
| REST 24h stats fetch fails at startup | Backend logs error; proceeds with zero-initialized stats; updates incrementally from trades |

---

## Technical Considerations

### Key Components

| Component | Action |
|-----------|--------|
| `HyperliquidWebSocketClient` | Manages WebSocket connection lifecycle, subscription, reconnection with backoff |
| `MarketDataStreamService` | .NET `BackgroundService` that seeds 24h stats via REST, starts/manages the WebSocket client, aggregates trades at 500ms interval |
| `MarketDataHub` | SignalR hub that broadcasts aggregated price updates to connected Angular clients |
| `signalr.service.ts` | Angular SignalR client service |
| Market Data feature component | Renders live price ticker, 15-min rolling chart (Lightweight Charts), and connection status |
| App shell / navbar component | Renders global connection status indicator |

### SignalR Hub Methods

| Method | Direction | Payload |
|--------|-----------|---------|
| `ReceivePriceUpdate` | Server → Client | `{ asset, lastPrice, high24h, low24h, volume24h, timestamp }` |
| `ReceiveConnectionStatus` | Server → Client | `{ source, status, detail, retryCount }` |

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

1. WebSocket disconnects → log event (Serilog), set status to "Reconnecting", broadcast status to UI
2. Wait backoff interval (1s, 2s, 4s, 8s, 16s, 32s, 60s max)
3. Attempt reconnect (increment retry counter)
4. On success → reset retry counter, resubscribe to trades stream, set status to "Connected"
5. On failure → if retryCount < 20, increment backoff and retry; if retryCount >= 20, set status to "Disconnected" and stop retrying

### Aggregation Strategy

1. Raw trade events arrive from WebSocket at high frequency
2. `MarketDataStreamService` buffers incoming trades
3. Every 500ms, compute: last price, update running 24h high/low/volume
4. Push single aggregated `ReceivePriceUpdate` message via SignalR
5. If no trades received in the interval, skip the push (no empty updates)

### Chart Requirements

- Library: **Lightweight Charts** (TradingView open-source)
- Chart type: Line series plotting last trade price over time
- Rolling window: 15 minutes (older data points dropped)
- Updates: New data point added on each SignalR `ReceivePriceUpdate` message
- Responsive: Chart resizes with container

---

## Out of Scope

- Orderbook streaming
- Candle stream subscription (trades stream only for this PBI)
- Multiple asset subscriptions (BTC-PERP only)
- Historical backfill after reconnect (REST resync for account data is F8)
- WebSocket message persistence or replay
- Manual reconnect button (fully automatic only)

---

## Open Questions

*None at this time.*

---

## Acceptance Criteria

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

### Release Notes Information

- **Heading**: Real-Time Market Data Streaming
- **Release note type**: Feature
- **Release Note Summary**: Live BTC-PERP price data streamed from Hyperliquid via WebSocket to the Angular UI, with a real-time ticker, rolling 15-minute price chart, and automatic reconnection.
- **Release Notes Audience**: Product
- **Breaking Change**: No

---

## Related Features

- **F1** — Connectivity established before WebSocket can connect
- **F3** — REST market data proves the data model and provides 24h stats seed data
- **F7** — Per-wallet WebSocket (F7) builds on the connection management proven here
- **F8** — Error handling and resilience hardening applied to WebSocket reconnection
