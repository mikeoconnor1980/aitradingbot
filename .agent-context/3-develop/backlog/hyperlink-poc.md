# Hyperliquid POC — Feature Specification

## Goal

Prove end-to-end integration with the Hyperliquid exchange from a .NET backend
with an Angular frontend. Validate the riskiest technical assumptions before
building the full trading engine.

All operations target Hyperliquid **testnet** to avoid real funds.

Private key is stored locally (appsettings / environment variable) — no Key Vault
or database encryption in this phase.

---

## What This POC Proves

| # | Risk Area | What We Validate |
|---|-----------|-----------------|
| 1 | **EIP-712 Signing in .NET** | Hyperliquid uses wallet-based typed data signing, not API keys. Prove Nethereum can produce valid signatures accepted by the exchange. |
| 2 | **REST API Integration** | Can we read account state, positions, open orders, and market metadata reliably? |
| 3 | **WebSocket Streaming** | Can we maintain a persistent connection for real-time candle data, orderbook, and user events (fills, order updates)? |
| 4 | **Order Lifecycle** | Place, modify, and cancel limit/market orders on testnet. Confirm round-trip latency and order state transitions. |
| 5 | **Rate Limiting** | Understand and respect Hyperliquid's rate limits. Prove a throttled queue works. |
| 6 | **Reconnection** | WebSocket drops, API timeouts — does the client recover gracefully and resync state? |
| 7 | **Frontend–Backend Contract** | Validate the Angular ↔ .NET API shape for market data, account info, and order management. |

---

## Tech Stack (POC)

- **Backend**: C# / .NET 8 Web API + Background Worker
- **Frontend**: Angular 19 standalone
- **Signing**: Nethereum (EIP-712 typed data signing)
- **Database**: None (in-memory / local state only)
- **Config**: appsettings.json + environment variables for private key
- **Deployment**: `dotnet run` + `ng serve` (no Docker for POC)

---

## Features

### F1 — Configuration & Connectivity

**As a developer, I want to configure my Hyperliquid testnet wallet and verify connectivity.**

Acceptance criteria:
- [ ] Private key loaded from local config (appsettings / env var)
- [ ] Wallet address derived from private key
- [ ] Health check endpoint that pings Hyperliquid API and returns connectivity status
- [ ] Angular UI shows connection status (connected / disconnected / error)
- [ ] UI displays wallet address (truncated)

---

### F2 — Account Dashboard

**As a user, I want to see my testnet account state at a glance.**

Acceptance criteria:
- [ ] Fetch account balance (equity, available margin, cross-margin details)
- [ ] Display open positions (asset, size, entry price, unrealised PnL, liquidation price)
- [ ] Display open orders (asset, side, price, size, order type, status)
- [ ] Auto-refresh on a polling interval (e.g. 5s)
- [ ] Angular UI renders account summary, positions table, and orders table

---

### F3 — Market Data (REST)

**As a user, I want to view current market information for BTC-PERP.**

Acceptance criteria:
- [ ] Fetch available markets / asset metadata from Hyperliquid
- [ ] Display mid price, mark price, funding rate, 24h volume
- [ ] Fetch recent candle data (15m, 1H, 4H) via REST
- [ ] Angular UI shows market info card and a simple candle table/list

---

### F4 — Market Data (WebSocket)

**As a user, I want to see real-time price updates streamed to the UI.**

Acceptance criteria:
- [ ] Backend establishes WebSocket connection to Hyperliquid
- [ ] Subscribe to trades stream and/or candle stream for BTC-PERP
- [ ] Backend pushes updates to Angular via SignalR
- [ ] Angular UI shows live price ticker that updates in real-time
- [ ] Connection status indicator (WebSocket connected / reconnecting)
- [ ] Automatic reconnection on disconnect with backoff

---

### F5 — Order Placement

**As a user, I want to place orders on testnet to prove the signing and submission flow.**

Acceptance criteria:
- [ ] Place a **market order** (buy/sell BTC-PERP, specified size)
- [ ] Place a **limit order** (buy/sell BTC-PERP, specified price and size)
- [ ] EIP-712 typed data signature generated correctly in .NET
- [ ] Nonce management (monotonically increasing, no collisions)
- [ ] Angular UI with order entry form (side, type, price, size)
- [ ] Success/error feedback shown in UI
- [ ] New order appears in the open orders table (F2)

---

### F6 — Order Management

**As a user, I want to cancel and modify existing orders.**

Acceptance criteria:
- [ ] Cancel a single order by order ID
- [ ] Cancel all open orders
- [ ] Modify an existing order (change price or size)
- [ ] Angular UI shows cancel button per order row
- [ ] Angular UI shows "Cancel All" button
- [ ] Confirmation dialog before destructive actions
- [ ] Orders table updates after cancel/modify

---

### F7 — User Event Stream (WebSocket)

**As a user, I want to receive real-time notifications when my orders fill or positions change.**

Acceptance criteria:
- [ ] Subscribe to user-specific WebSocket events (fills, order updates)
- [ ] Backend relays events to Angular via SignalR
- [ ] Angular UI shows a live event log / activity feed
- [ ] Fill events update positions table automatically
- [ ] Order status changes reflected in orders table without manual refresh

---

### F8 — Error Handling & Resilience

**As a developer, I want to understand how the integration behaves under failure conditions.**

Acceptance criteria:
- [ ] API errors (4xx, 5xx) are caught and surfaced to the UI with meaningful messages
- [ ] Invalid signature errors are clearly identified (helps debug signing issues)
- [ ] Rate limit responses (429) trigger backoff and retry
- [ ] WebSocket disconnects trigger automatic reconnection with exponential backoff
- [ ] After reconnect, open orders and positions are resynced
- [ ] All errors logged with structured logging (Serilog)

---

## Out of Scope (for POC)

- Multi-tenant / multi-user support
- Database persistence
- Key encryption or Key Vault
- Full grid strategy execution
- Backtesting
- Docker / deployment
- Authentication / authorization
- Subscription / billing

---

## Project Structure (POC)

```
src/
  TradePilot.HyperliquidPoc.Api/        # .NET Web API + SignalR hub
    Controllers/
      AccountController.cs               # GET account, positions, orders
      MarketDataController.cs            # GET markets, candles, prices
      OrderController.cs                 # POST place, DELETE cancel, PUT modify
    Hubs/
      MarketDataHub.cs                   # SignalR hub for real-time data
    Services/
      HyperliquidRestClient.cs           # REST API wrapper
      HyperliquidWebSocketClient.cs      # WebSocket manager
      HyperliquidSigner.cs              # EIP-712 signing via Nethereum
      MarketDataStreamService.cs         # Background service for WS streams
    Configuration/
      HyperliquidOptions.cs              # Config model (wallet, endpoints)
    Program.cs

frontend/
  hyperliquid-poc/                       # Angular standalone app
    src/app/
      core/
        services/
          hyperliquid-api.service.ts     # HTTP calls to .NET API
          signalr.service.ts             # SignalR client
        models/
          account.model.ts
          order.model.ts
          position.model.ts
          market.model.ts
      features/
        dashboard/                       # Account overview, positions, orders
        market-data/                     # Price ticker, market info
        order-entry/                     # Order placement form
        activity-feed/                   # Live event log
      app.component.ts
      app.routes.ts
```

---

## Implementation Order

1. **F1** — Configuration & connectivity (foundation)
2. **F3** — Market data REST (read-only, low risk, proves API works)
3. **F2** — Account dashboard (proves authenticated reads)
4. **F5** — Order placement (proves EIP-712 signing — highest risk)
5. **F6** — Order management (extends F5)
6. **F4** — WebSocket market data (proves streaming)
7. **F7** — User event stream (proves per-user WS)
8. **F8** — Error handling & resilience (hardening)

---

## Key Technical Decisions to Validate

### EIP-712 Signing

Hyperliquid requires typed data signatures for all write operations.
The POC must prove that Nethereum's `Eip712TypedDataSigner` produces
signatures that Hyperliquid accepts.

Key points to verify:
- Domain separator matches Hyperliquid's expected format
- Type hashes are computed correctly
- Nonce generation is reliable
- Signature format (v, r, s) matches expectations

### WebSocket Protocol

Hyperliquid uses a JSON-based WebSocket protocol.
The POC must prove:
- Subscription messages are correctly formatted
- Heartbeat / ping-pong keeps connection alive
- Multiple subscriptions on one connection work
- Reconnection resumes subscriptions

### Testnet vs Mainnet

All POC work targets: `https://api.hyperliquid-testnet.xyz`

Testnet behaviour may differ from mainnet. Document any discrepancies found.

---

## Definition of Done

The POC is complete when:
1. A developer can configure a testnet wallet and see their account state in the Angular UI
2. Real-time price data streams from Hyperliquid → .NET → Angular
3. Orders can be placed, modified, and cancelled from the UI
4. Fill events appear in the activity feed in real-time
5. The signing implementation is validated and documented
6. Known limitations and testnet quirks are documented