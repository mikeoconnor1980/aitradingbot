# PRD: Hyperliquid POC — Exchange Integration Proof of Concept

**Status:** Ready for Review  
**Author:** PRD Agent  
**Date:** 2026-03-24  
**Version:** 1.0  
**Source Specification:** [hyperlink-poc.md](..\\3-develop\\backlog\\hyperlink-poc.md)

---

## 1. Executive Summary

This PRD defines the scope and requirements for a Proof of Concept (POC) validating end-to-end integration between a .NET backend, an Angular frontend, and the Hyperliquid decentralised exchange (testnet only).

The POC targets the highest-risk technical assumptions that must hold before the full trading engine can be built: EIP-712 wallet-based signing from .NET, REST and WebSocket API integration, order lifecycle management, and the frontend–backend data contract.

This is **not** a product release. It is a technical validation exercise with no formal time-box, conducted by a solo developer and one PM. No real funds are at risk. No multi-tenant, persistence, or security hardening work is in scope.

This POC is sequenced **before** the strategy-focused POC (backtesting, paper trading). Exchange integration is the first priority; strategy runtime work is paused until this POC completes.

---

## 2. Background & Context

### Problem Statement

The AI Grid Trading System (see [00-project-overview.md](..\\0-knowledge\\00-project-overview.md)) depends on reliable, low-latency integration with the Hyperliquid exchange. Hyperliquid uses a non-standard authentication model — **EIP-712 typed data signing** rather than traditional API keys — and exposes both REST and WebSocket interfaces with specific protocol requirements.

These integration points have not been validated in the project's chosen tech stack (C# / .NET + Nethereum). If signing fails, WebSocket behaviour differs from documentation, or order lifecycle transitions are unreliable, the entire platform is blocked.

### Current State

- The project has a defined architecture ([03-infrastructure-architecture.md](..\\0-knowledge\\03-infrastructure-architecture.md)) and a separate POC epic plan ([proof-of-concept-epics.md](..\\3-develop\\backlog\\proof-of-concept-epics.md)) focused on strategy replay and paper trading.
- The Hyperliquid integration layer described in [02-hyperliquid-integration.md](..\\0-knowledge\\02-hyperliquid-integration.md) specifies multi-tenant connection management, per-user signing, and shared market data — but none of this has been implemented or tested.
- No code currently exists for Hyperliquid REST calls, WebSocket streaming, or EIP-712 signing in .NET.

### Opportunity

A focused POC that isolates exchange integration from strategy logic allows the team to:

1. **De-risk the highest-uncertainty component** — signing and order placement — before investing in the full trading engine.
2. **Establish the API contract** between Angular and .NET for market data, account state, and order management.
3. **Document testnet quirks and rate-limit behaviour** so the production integration can be designed accurately.
4. **Validate the real-time data pipeline** (Hyperliquid WebSocket → .NET → SignalR → Angular).

### Relationship to Other POC Work

This Hyperliquid POC is sequenced **before** the strategy-focused POC defined in [proof-of-concept-epics.md](..\\3-develop\\backlog\\proof-of-concept-epics.md). The strategy POC (backtesting, paper trading, grid controller) is paused until this exchange integration POC completes.

This POC validates the exchange integration layer that the strategy POC's paper-trading mode (Epic 5) will eventually depend on. Completing it first de-risks the most uncertain component.

### Team & Working Model

- **1 developer, 1 PM** — no sprint cadence or formal time-box
- Internal audience only — the Angular UI needs to be functional, not polished
- Testnet vs. mainnet discrepancies will be captured as informal notes, not a formal comparison document

---

## 3. Goals & Objectives

### Business Goals

| ID | Goal | Measure of Success |
|----|------|--------------------|
| BG-1 | Validate that the Hyperliquid exchange can be integrated from the chosen .NET tech stack | EIP-712 signing accepted by Hyperliquid testnet; orders placed and confirmed |
| BG-2 | Reduce technical risk before committing to full engine development | All 7 risk areas in the POC spec validated with documented results |
| BG-3 | Establish the frontend–backend API contract for trading features | Angular UI successfully renders account, market, and order data from .NET API |

### User Goals

| ID | Goal | Measure of Success |
|----|------|--------------------|
| UG-1 | Developer can configure a testnet wallet and verify connectivity | Health check endpoint returns success; UI shows connected status and wallet address |
| UG-2 | Developer can view account state (balances, positions, orders) in a browser | Account dashboard renders correct data from Hyperliquid testnet |
| UG-3 | Developer can place, modify, and cancel orders from the UI | Full order lifecycle (place → view → modify → cancel) works end-to-end |
| UG-4 | Developer can observe real-time price and fill data | WebSocket → SignalR → Angular pipeline delivers live updates |

### Success Metrics

| Metric | Target |
|--------|--------|
| EIP-712 signing acceptance rate | 100% of correctly formed requests accepted by testnet |
| Order round-trip latency (place → confirmed) | Measured and documented (no specific target for POC) |
| WebSocket reconnection | Automatic recovery within 30 seconds of disconnect |
| Rate-limit violations | Zero 429 responses during normal operation with throttled queue |
| Feature coverage | All 8 features (F1–F8) demonstrated and acceptance criteria met |

### Non-Goals

| ID | Non-Goal | Rationale |
|----|----------|-----------|
| NG-1 | Multi-tenant / multi-user support | Single developer wallet only; multi-tenancy is a production concern |
| NG-2 | Database persistence | In-memory / local state only; no SQLite or EF Core in this POC |
| NG-3 | Key encryption or Key Vault | Private key stored in plaintext config; acceptable for testnet |
| NG-4 | Strategy execution | No grid strategy, signals, or risk engine — pure exchange integration |
| NG-5 | Docker / deployment | `dotnet run` + `ng serve` only |
| NG-6 | Authentication / authorization | No user login; single-user local development |
| NG-7 | Subscription / billing | Not applicable to a technical POC |
| NG-8 | Production readiness or mainnet connectivity | All operations target Hyperliquid testnet only |

---

## 4. Scope

### In Scope

| Area | Detail |
|------|--------|
| **Exchange** | Hyperliquid **testnet** only (`https://api.hyperliquid-testnet.xyz`) |
| **Asset** | BTC-PERP only |
| **Authentication** | EIP-712 typed data signing via Nethereum; single wallet; private key in `appsettings.json` or environment variable |
| **REST API** | Account state (balance, positions, orders), market metadata, candle data (15m, 1H, 4H), mid/mark price, funding rate |
| **WebSocket** | Trades/candle stream for BTC-PERP (shared); user fills and order updates (per-wallet) |
| **Order operations** | Place market order, place limit order, modify order (price/size), cancel single order, cancel all orders |
| **Real-time pipeline** | Hyperliquid WebSocket → .NET background service → SignalR → Angular |
| **Frontend** | Angular 19 standalone app with: connection status, account dashboard, market info, live price ticker, order entry form, order management, activity feed |
| **Error handling** | API error surfacing, invalid signature diagnosis, rate-limit backoff/retry, WebSocket reconnection with exponential backoff, state resync after reconnect |
| **Logging** | Structured logging via Serilog |
| **Deliverables** | Working code, informal notes on testnet quirks / rate limits / signing findings |

### Out of Scope

| Area | Rationale |
|------|-----------|
| Multi-tenant / multi-user | Single wallet; multi-tenancy is a production concern |
| Database persistence | In-memory / local state only |
| Key encryption / Key Vault | Plaintext config is acceptable for testnet with no real funds |
| Grid strategy / signals / risk engine | This POC validates exchange plumbing, not trading logic |
| Backtesting | Covered by the separate strategy POC |
| Docker / deployment | Local `dotnet run` + `ng serve` only |
| Authentication / authorization | No user login; single-user local dev |
| Subscription / billing | Not applicable |
| Mainnet connectivity | Testnet only; no production endpoints |
| Polished UI / UX design | Functional prototype for internal use only |
| Assets other than BTC-PERP | Scope locked to one pair to avoid over-engineering |
| Automated tests | Manual verification is sufficient for a POC of this size |

### Future Considerations

Items that are explicitly deferred but will be informed by POC findings:

| Item | How the POC Informs It |
|------|----------------------|
| **Multi-tenant connection manager** | POC proves the single-wallet flow; production needs per-subscriber connection pooling and key retrieval from Key Vault |
| **Rate-limit strategy for N subscribers** | POC documents observed rate limits and queue behaviour; production must scale the throttling approach |
| **Mainnet differences** | Any testnet quirks noted during the POC will feed into mainnet integration planning |
| **Signing library confidence** | If Nethereum signing is validated, the same `HyperliquidSigner` can be promoted into the production codebase |
| **SignalR contract** | The hub contract established in the POC becomes the baseline for the production real-time API |
| **Strategy POC integration** | Epic 5 (Live Market Data & Paper Trading) in the strategy POC will consume the market data and WebSocket services proven here |

---

## 5. Technical Considerations

### Architecture

The POC uses a minimal two-tier architecture with no database:

```
Angular 19 (standalone)
  ↕ HTTP (REST) + SignalR (WebSocket)
.NET 8 Web API + Background Worker
  ↕ REST + WebSocket
Hyperliquid Testnet API
```

The .NET process serves both the API controllers and a hosted background service for WebSocket stream management. There is no separate worker process in this POC.

### Key Components

| Component | Responsibility | Technology |
|-----------|---------------|------------|
| `HyperliquidSigner` | EIP-712 typed data signing for all write operations | Nethereum `Eip712TypedDataSigner` |
| `HyperliquidRestClient` | REST API calls (account state, market data, order submission) | `HttpClient` |
| `HyperliquidWebSocketClient` | Persistent WebSocket connection management, subscription, reconnection | `ClientWebSocket` |
| `MarketDataStreamService` | Background service managing shared market data streams | .NET `BackgroundService` |
| `MarketDataHub` | SignalR hub pushing real-time data to Angular | ASP.NET Core SignalR |
| Angular services | HTTP client for REST, SignalR client for real-time data | Angular `HttpClient`, `@microsoft/signalr` |

### EIP-712 Signing (Highest Risk)

Hyperliquid requires typed data signatures for all write operations (place, modify, cancel). This is the most uncertain technical area.

**What must be validated:**

| Concern | Detail |
|---------|--------|
| Domain separator | Must match Hyperliquid's expected format exactly |
| Type hashes | Computed correctly for each action type (order, cancel, modify) |
| Nonce generation | Monotonically increasing; no collisions across rapid submissions |
| Signature format | `v`, `r`, `s` values in the encoding Hyperliquid expects |
| Nethereum compatibility | Confirm `Eip712TypedDataSigner` produces accepted signatures without patching |

**Assumption:** Nethereum's EIP-712 implementation is compatible with Hyperliquid's signature validation. If this assumption fails, the POC must identify the gap and determine whether a workaround exists.

### WebSocket Protocol

| Concern | Detail |
|---------|--------|
| Subscription format | JSON messages with specific channel/subscription keys |
| Heartbeat | Ping/pong or keep-alive mechanism to prevent server-side timeout |
| Multiple subscriptions | Shared stream (trades, candles) + per-wallet stream (fills, order updates) on same or separate connections |
| Reconnection | Automatic reconnect with exponential backoff; resubscribe on reconnect |
| State resync | After reconnect, re-fetch open orders and positions via REST to reconcile |

### Rate Limiting

| Concern | Detail |
|---------|--------|
| Observed limits | Document actual rate-limit thresholds encountered on testnet |
| Throttled queue | Order submissions pass through an in-process queue that enforces spacing |
| 429 handling | Backoff and retry; surface errors to the UI if retries are exhausted |

### Integration Points

| Integration | Protocol | Direction | Auth Required |
|-------------|----------|-----------|---------------|
| Hyperliquid REST API | HTTPS | Backend → Exchange | EIP-712 signature (write ops); none (read ops) |
| Hyperliquid WebSocket | WSS | Exchange → Backend | None (market data); wallet address (user events) |
| SignalR Hub | WSS | Backend → Frontend | None (POC) |
| Angular HTTP → .NET API | HTTP | Frontend → Backend | None (POC) |

### Constraints

| Constraint | Impact |
|------------|--------|
| **Testnet only** | Endpoint: `https://api.hyperliquid-testnet.xyz`. No mainnet URLs in config. |
| **No database** | All state is in-memory. Restarting the backend loses order/position history. |
| **Single process** | API + background worker run in one `dotnet run` process. No horizontal scaling. |
| **Private key in plaintext** | Acceptable only because testnet wallet holds no real funds. Must not leak into git. |
| **BTC-PERP only** | Market data endpoints, WebSocket subscriptions, and order forms are hardcoded to one asset. |

### Project Structure

```
src/
  TradingApp.HyperliquidPoc.Api/        # .NET Web API + SignalR hub
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

## 6. Use Cases

### Personas

| Persona | Description |
|---------|-------------|
| **Developer** | Solo developer building the trading platform. Uses the POC to validate exchange integration, debug signing, and establish the API contract. Runs both backend and frontend locally. |
| **PM** | Product manager reviewing POC output to confirm technical feasibility and decide whether to proceed to the strategy POC and V1 development. |

### Features & User Stories

#### F1 — Configuration & Connectivity

**As a developer, I want to configure my Hyperliquid testnet wallet and verify connectivity.**

| # | User Story | Acceptance Criteria |
|---|-----------|-------------------|
| F1.1 | As a developer, I want to load my private key from local config so I don't hardcode secrets | Private key loaded from `appsettings.json` or environment variable; wallet address derived automatically |
| F1.2 | As a developer, I want a health check endpoint so I can verify the backend can reach Hyperliquid | `GET /api/health` pings Hyperliquid API and returns connectivity status |
| F1.3 | As a developer, I want the UI to show connection status so I can confirm the full pipeline works | Angular UI displays connected / disconnected / error state and truncated wallet address |

---

#### F2 — Account Dashboard

**As a developer, I want to see my testnet account state at a glance.**

| # | User Story | Acceptance Criteria |
|---|-----------|-------------------|
| F2.1 | As a developer, I want to view my account balance so I can confirm authenticated reads work | Displays equity, available margin, and cross-margin details |
| F2.2 | As a developer, I want to see open positions so I can verify position data from the exchange | Positions table shows asset, size, entry price, unrealised PnL, liquidation price |
| F2.3 | As a developer, I want to see open orders so I can verify order state from the exchange | Orders table shows asset, side, price, size, order type, status |
| F2.4 | As a developer, I want the dashboard to auto-refresh so I see current state without manual reload | Polling interval (e.g. 5s) refreshes account data automatically |

---

#### F3 — Market Data (REST)

**As a developer, I want to view current market information for BTC-PERP.**

| # | User Story | Acceptance Criteria |
|---|-----------|-------------------|
| F3.1 | As a developer, I want to fetch BTC-PERP market metadata so I can confirm the REST API works | Displays mid price, mark price, funding rate, 24h volume |
| F3.2 | As a developer, I want to view recent candle data so I can confirm historical data retrieval | Fetches candles for 15m, 1H, 4H timeframes; renders in a simple table/list |

---

#### F4 — Market Data (WebSocket)

**As a developer, I want to see real-time price updates streamed to the UI.**

| # | User Story | Acceptance Criteria |
|---|-----------|-------------------|
| F4.1 | As a developer, I want the backend to stream BTC-PERP trades via WebSocket so I can validate the streaming pipeline | Backend establishes WSS connection, subscribes to trades/candle stream for BTC-PERP |
| F4.2 | As a developer, I want live prices pushed to the Angular UI so I can confirm the SignalR relay works | SignalR hub pushes updates; Angular shows live price ticker updating in real-time |
| F4.3 | As a developer, I want to see WebSocket connection status so I know if the stream is healthy | UI shows connected / reconnecting indicator |
| F4.4 | As a developer, I want automatic reconnection so the stream recovers from drops | Reconnects with exponential backoff; resubscribes after reconnect |

---

#### F5 — Order Placement

**As a developer, I want to place orders on testnet to prove the signing and submission flow.**

| # | User Story | Acceptance Criteria |
|---|-----------|-------------------|
| F5.1 | As a developer, I want to place a market order so I can prove basic order submission works | Buy/sell BTC-PERP market order with specified size; EIP-712 signature accepted |
| F5.2 | As a developer, I want to place a limit order so I can prove price-specific orders work | Buy/sell BTC-PERP limit order with specified price and size |
| F5.3 | As a developer, I want an order entry form in the UI so I can submit orders without curl | Angular form with side, type, price (for limit), and size fields |
| F5.4 | As a developer, I want success/error feedback so I know if the order was accepted | UI displays confirmation or error message after submission |
| F5.5 | As a developer, I want nonce management to be reliable so rapid orders don't collide | Monotonically increasing nonces; no collisions under normal usage |

---

#### F6 — Order Management

**As a developer, I want to cancel and modify existing orders.**

| # | User Story | Acceptance Criteria |
|---|-----------|-------------------|
| F6.1 | As a developer, I want to cancel a single order so I can prove cancel signing works | Cancel by order ID; order removed from open orders table |
| F6.2 | As a developer, I want to cancel all open orders so I can quickly clear the book | "Cancel All" button clears all open orders |
| F6.3 | As a developer, I want to modify an existing order so I can prove modify signing works | Change price or size of an existing order |
| F6.4 | As a developer, I want a confirmation dialog before destructive actions so I don't cancel by accident | Confirmation prompt before cancel/cancel-all |
| F6.5 | As a developer, I want the orders table to update after cancel/modify so I see the result | Orders table reflects changes without manual refresh |

---

#### F7 — User Event Stream (WebSocket)

**As a developer, I want real-time notifications when my orders fill or positions change.**

| # | User Story | Acceptance Criteria |
|---|-----------|-------------------|
| F7.1 | As a developer, I want to subscribe to per-wallet WebSocket events so I receive fills and order updates | Backend subscribes to user-specific channels using wallet address |
| F7.2 | As a developer, I want fill events relayed to the UI so I can see trades as they happen | SignalR pushes fill events; Angular activity feed shows them in real-time |
| F7.3 | As a developer, I want fill events to update the positions table so data stays consistent | Positions table auto-updates when fills are received |
| F7.4 | As a developer, I want order status changes reflected in the orders table without refresh | Order updates from WebSocket propagate to the UI automatically |

---

#### F8 — Error Handling & Resilience

**As a developer, I want to understand how the integration behaves under failure conditions.**

| # | User Story | Acceptance Criteria |
|---|-----------|-------------------|
| F8.1 | As a developer, I want API errors surfaced to the UI so I can diagnose issues | 4xx/5xx errors caught and displayed with meaningful messages |
| F8.2 | As a developer, I want invalid signature errors clearly identified so I can debug signing | Signature-related errors distinguished from other API errors |
| F8.3 | As a developer, I want rate-limit responses to trigger backoff so the system self-recovers | 429 responses trigger exponential backoff and retry |
| F8.4 | As a developer, I want WebSocket disconnects to trigger reconnection so streams resume | Automatic reconnect with exponential backoff |
| F8.5 | As a developer, I want state resynced after reconnect so data is consistent | Open orders and positions re-fetched via REST after WebSocket recovery |
| F8.6 | As a developer, I want structured logging so I can trace issues through the backend | All errors logged with Serilog; structured fields for correlation |

---

### Key Scenarios

| # | Scenario | Steps | Expected Outcome |
|---|----------|-------|-----------------|
| S1 | **First-run setup** | Configure private key → start backend → start frontend → open browser | UI shows "Connected", displays wallet address and account balance |
| S2 | **Place and fill a market order** | Open order form → select Market Buy → enter size → submit | Order submitted, fill event appears in activity feed, position appears in dashboard |
| S3 | **Place and cancel a limit order** | Place a limit order far from market price → see it in orders table → click cancel | Order appears, then disappears from orders table after cancel |
| S4 | **Observe live price updates** | Navigate to market data view → wait | Price ticker updates continuously without user interaction |
| S5 | **Recover from WebSocket drop** | Simulate network interruption (disconnect WiFi briefly) → reconnect | Connection status shows "Reconnecting" → "Connected"; streams resume; state resynced |
| S6 | **Trigger a signing error** | Intentionally misconfigure the signing (e.g. wrong chain ID) → attempt order | Clear error message identifying signature rejection; no crash |

---

## 7. Design & UX

### Approach

The Angular UI is a **functional prototype** for internal use only (developer + PM). It does not need visual polish but should be navigable and clearly present the data needed to validate each feature.

Wireframes will be generated using the **HTML Wireframe Generator** agent ([html-wireframe-generator.agent.md](../../.github/agents/html-wireframe-generator.agent.md)), which produces self-contained HTML files viewable in any browser. This is the same tooling used for the full product wireframes.

### Existing Wireframes (Full Product)

The following wireframes already exist in `.agent-context/1-discover/wireframes/` for the full product. They can serve as **visual references** but will need POC-specific versions that strip out multi-tenant, auth, and strategy features:

| Existing Wireframe | Relevance to POC |
|-------------------|-----------------|
| `mockup_dashboard.html` | Partial — account summary, positions, orders layout is reusable; remove strategy and subscription elements |
| `mockup_dashboard_empty.html` | Partial — empty state for when no positions/orders exist |
| `mockup_positions.html` | High — positions table layout directly applicable |
| `mockup_order_history.html` | High — orders table layout directly applicable |
| `mockup_exchange_connection.html` | Partial — connection status concept reusable; strip multi-tenant key management |

### POC Screens Required

| Screen | Maps to Feature | Description |
|--------|----------------|-------------|
| **POC Dashboard** | F1, F2 | Single-page overview: connection status badge, wallet address, account balance summary, positions table, open orders table. Auto-refreshes. |
| **Market Data** | F3, F4 | BTC-PERP market info card (mid/mark price, funding rate, volume). Live price ticker updating via SignalR. Simple candle data table for 15m/1H/4H. |
| **Order Entry** | F5 | Form: side (buy/sell), type (market/limit), price (limit only), size. Submit button. Success/error feedback area. |
| **Order Management** | F6 | Integrated into dashboard orders table: cancel button per row, "Cancel All" button, modify action (inline or modal). Confirmation dialog for destructive actions. |
| **Activity Feed** | F7 | Live event log showing fills, order updates, and connection events. Newest at top. Timestamp, event type, and details per row. |
| **Error Panel** | F8 | Inline error display (toast or banner) for API errors, signing failures, and rate-limit events. Could be a shared component across screens. |

### Layout

A simple single-page app with tab navigation or a sidebar:

```
┌──────────────────────────────────────────────┐
│  [Connected ●]  0x1a2b...3c4d   BTC-PERP    │  ← Header: status, wallet, asset
├──────────┬───────────────────────────────────┤
│          │                                    │
│ Dashboard│  Account / Positions / Orders      │
│ Market   │  (content area)                    │
│ Orders   │                                    │
│ Activity │                                    │
│          │                                    │
├──────────┴───────────────────────────────────┤
│  [Error/notification bar]                     │  ← Errors, toasts
└──────────────────────────────────────────────┘
```

### Wireframe Generation Plan

POC wireframes should be generated **before implementation** of each feature to confirm the UI contract between frontend and backend. Suggested approach:

1. Generate wireframes for the Dashboard + Market Data screens first (covers F1–F4)
2. Generate wireframes for Order Entry + Order Management (covers F5–F6)
3. Generate wireframes for Activity Feed (covers F7)
4. Error handling is a cross-cutting concern — annotate on existing wireframes rather than a separate screen

Wireframes will be stored in `.agent-context/1-discover/wireframes/` with a `poc_` prefix to distinguish them from full-product wireframes.

---

## 8. Implementation Order

The feature spec defines a deliberate implementation sequence that builds from low-risk reads to high-risk writes, then adds streaming and hardening. This is the recommended order:

| Phase | Feature | Risk Level | Rationale |
|-------|---------|-----------|-----------|
| 1 | **F1 — Configuration & Connectivity** | Low | Foundation. Proves config loading, key derivation, and basic API reachability. |
| 2 | **F3 — Market Data (REST)** | Low | Read-only, no signing. Proves REST client works against Hyperliquid. |
| 3 | **F2 — Account Dashboard** | Medium | Proves authenticated reads (account state requires wallet context). |
| 4 | **F5 — Order Placement** | **High** | Proves EIP-712 signing — the single riskiest item in the POC. |
| 5 | **F6 — Order Management** | Medium | Extends F5 with cancel/modify signing. Lower risk once F5 is proven. |
| 6 | **F4 — Market Data (WebSocket)** | Medium | Proves WebSocket connection, SignalR relay, and reconnection. |
| 7 | **F7 — User Event Stream** | Medium | Proves per-wallet WebSocket subscriptions and real-time event relay. |
| 8 | **F8 — Error Handling & Resilience** | Low | Hardening pass across all features. |

Wireframe generation (Section 7) should precede or run in parallel with implementation of each phase.

---

## 9. Definition of Done

The POC is complete when:

| # | Criterion |
|---|-----------|
| 1 | A developer can configure a testnet wallet and see their account state in the Angular UI |
| 2 | Real-time BTC-PERP price data streams from Hyperliquid → .NET → Angular |
| 3 | Orders can be placed, modified, and cancelled from the UI |
| 4 | Fill events appear in the activity feed in real-time |
| 5 | The EIP-712 signing implementation is validated and findings documented (informal notes) |
| 6 | Known limitations, testnet quirks, and rate-limit observations are documented (informal notes) |
| 7 | The API contract between Angular and .NET is established and working |

---

## 10. Open Questions (Consolidated)

### Resolved

| # | Question | Resolution |
|---|----------|------------|
| OQ-1 | Is this POC intended to run before, after, or in parallel with the strategy-focused POC? | **Before.** This POC is first priority. Strategy POC is paused. |
| OQ-2 | Should the POC target BTC-PERP only, or prove connectivity for any asset? | **BTC-PERP only.** |
| OQ-3 | Is there a time-box for the POC? | **No formal time-box.** Solo developer + one PM, no sprint cadence. |
| OQ-4 | Who is the primary audience for the POC output? | **Internal only** (developer + PM). Functional UI, not polished. |
| OQ-5 | Should testnet vs. mainnet discrepancies be formally documented? | **Informal notes only**, captured as encountered. |
| OQ-6 | Should automated tests be included? | **No.** Manual verification is sufficient for a POC of this size. |

### Assumptions

| # | Assumption | Impact if Wrong |
|---|-----------|----------------|
| A-1 | Nethereum's `Eip712TypedDataSigner` is compatible with Hyperliquid's signature validation | POC blocked on signing; would need to find an alternative library or manual EIP-712 implementation |
| A-2 | Hyperliquid testnet API is stable and available | POC cannot progress during outages; no mitigation other than waiting |
| A-3 | Hyperliquid testnet behaviour is representative of mainnet for the operations tested | Production integration may require changes; mitigated by informal notes during POC |
| A-4 | A single WebSocket connection can carry both shared (market data) and per-wallet (user events) subscriptions | If separate connections are required, the `HyperliquidWebSocketClient` needs connection-per-stream management |
| A-5 | Testnet wallet can be funded with sufficient test tokens to exercise order placement | If testnet faucet is unavailable or limited, order-related features (F5–F7) cannot be fully validated |

### Unresolved

*None at this time. New questions will be added here as they arise during implementation.*
