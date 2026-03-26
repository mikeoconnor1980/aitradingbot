# Infrastructure Architecture

The system uses a phased deployment model.
The platform is multi-tenant — multiple subscribers each with their own Hyperliquid keys.

---

# Phase 1 — VPS (POC)

Single-node deployment for proving the trading engine and strategy logic.

Main components:

Angular UI  
C# API  
C# Worker  
SQLite database

Deployment:

Docker containers (api, worker, ui) managed with docker compose.

Data storage:

/data/sqlite/tradingapp.db  
/data/logs  
/data/backups  
/data/snapshots

In POC phase, subscriber count is small (personal use / early testers).
SQLite is sufficient.

---

# Phase 2 — Azure Cloud (Production)

Once proven on VPS, the system moves to Azure for production multi-tenant operation.

Changes from Phase 1:

SQLite → Azure SQL  
Docker Compose → Azure Container Apps (or similar)  
Local disk storage → Azure Blob Storage  
Environment variables → Azure Key Vault for secrets and user API keys  

Additional production concerns:

Authentication → Azure AD B2C or similar identity provider  
Subscription billing → Stripe or similar payment integration  
Worker scaling → must handle N active subscribers concurrently  
Key security → per-user Hyperliquid keys encrypted at rest (Key Vault)  

The trading pipeline and application architecture remain identical across phases.

---

# Execution Flow

Browser  
↓  
Angular UI (authenticated per-user)  
↓  
API (tenant-scoped)  
↓  
Trading Worker (executes per-user strategies)  
↓  
Hyperliquid (using subscriber's keys)

This flow is the same in both VPS and cloud deployments.
The difference is scale and infrastructure services.

---

# Real-Time Market Data Streaming

Market data is streamed from Hyperliquid via a persistent WebSocket and relayed to browser clients via SignalR:

```
Hyperliquid WebSocket
↓ trades stream (shared, unauthenticated)
MarketDataStreamService  (BackgroundService co-located in TradingApp.Api)
↓ 500ms aggregation, exponential-backoff reconnect
IHubContext<MarketDataHub>  (SignalR, mapped at /hubs/marketdata)
↓
Angular SignalRService  (browser)
```

**POC note:** `MarketDataStreamService` is hosted inside `TradingApp.Api` for simplicity. In production it should migrate to `TradingApp.Worker` with a Redis backplane for cross-process SignalR.

**CORS requirement:** SignalR requires `AllowCredentials()` on the CORS policy. The allowed origins are configured via `Cors:AllowedOrigins` in `appsettings.json`.

---

# Real-Time User Event Streaming

Per-wallet user events (fills and order updates) are streamed via a dedicated WebSocket separate from market data:

```
Hyperliquid WebSocket (per-wallet, requires wallet address)
↓ userEvents subscription — dual channel routing: "user" or "userEvents" (to be verified against live API)
UserEventStreamService  (BackgroundService co-located in TradingApp.Api)
↓ zero-latency relay, same exponential-backoff reconnect (1s/60s/20 retries)
IHubContext<MarketDataHub>  (SignalR — same hub; methods: ReceiveFillEvent, ReceiveOrderUpdate, ReceiveUserConnectionStatus)
↓
Angular SignalRService → AccountStateService → positions$, orders$, events$
```

`UserEventStreamService` derives the wallet address from `IHyperliquidSigner.WalletAddress` (configured via F1). On SignalR disconnect and reconnect, the backend automatically resubscribes to `userEvents`.

The `AccountStateService` is a shared Angular state layer (BehaviorSubject) that decouples SignalR events from UI components. The dashboard subscribes to `positions$` and `orders$` reactively; the `ActivityFeedComponent` subscribes to `events$` (capped at 100, newest first).

The connection status indicator in the navbar aggregates three sources: SignalR transport + market data stream + user event stream, showing the most severe state.