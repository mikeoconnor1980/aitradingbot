# Binance USD-M Futures Integration — Implementation Prompt

## Objective

Integrate Binance USD-M Futures as a second live-execution exchange alongside Hyperliquid in TradePilot. The system is currently Hyperliquid-only for live trading; Binance is read-only (candle/funding-rate ingestion). This work promotes Binance to a fully tradeable venue — initially scoped to **BTC and ETH perpetual futures only**.

The exchange abstraction layer documented in `.agent-context/0-knowledge/38-exchange-abstraction-architecture.md` already defines the seams (`IExchangeAccountClient`, `IExchangeMarketMetadataProvider`, `IExchangeHistoricalDataClient`, `IExchangeCapabilities`, `IExchangeSymbolMapper`). Several Hyperliquid implementations exist. This plan fills in the Binance side and wires exchange selection end-to-end.

---

## Scope

### In Scope

1. **Binance API credentials storage** — API Key + Secret per user (HMAC SHA256 authentication)
2. **Multi-exchange wallet/credential model** — A user can hold both a Hyperliquid wallet address AND Binance API credentials simultaneously
3. **Account balance retrieval** — `GET /fapi/v3/balance` for USDT balance, unrealised PnL, available margin
4. **Dashboard balance display** — Show Binance balance on the dashboard alongside or instead of Hyperliquid, driven by active exchange context
5. **Place orders** — Long/Short via `POST /fapi/v1/order` (MARKET and LIMIT), for BTCUSDT and ETHUSDT only
6. **Change leverage** — `POST /fapi/v1/leverage` per symbol
7. **Stop-Loss / Take-Profit** — Companion STOP_MARKET and TAKE_PROFIT_MARKET trigger orders
8. **Order routing through the worker** — Binance orders follow the same control-plane → agent → execution engine pipeline as Hyperliquid
9. **Exchange-aware UI** — Clear indication of which exchange the user is operating against, with the ability to switch
10. **Exchange abstraction adapter implementations** for Binance

### Out of Scope (Deferred)

- Binance WebSocket user-data streams (fill detection via polling initially)
- Binance WebSocket market-data streams for candle assembly
- Grid strategy automation on Binance (manual trading only for now)
- Assets beyond BTC and ETH
- Binance Spot or Coin-M futures
- Persistence-wide canonical symbol migration
- Binance testnet integration (use demo-fapi.binance.com later)

---

## Background & Current State

### Exchange Abstraction (knowledge file: `38-exchange-abstraction-architecture.md`)

The abstraction layer exists with these contracts in `src/TradePilot.Application/Abstractions/Services/`:

| Contract | Hyperliquid Impl | Binance Impl |
|---|---|---|
| `IExchangeSymbolMapper` | `HyperliquidAssetMapper` | `BinanceAssetMapper` (exists — maps BTC→BTCUSDT) |
| `IExchangeMarketMetadataProvider` | `HyperliquidMarketMetadataProvider` | **Not implemented** |
| `IExchangeHistoricalDataClient` | `HyperliquidHistoricalDataClient` | **Not implemented** |
| `IExchangeAccountClient` | `HyperliquidAccountAdapter` | **Not implemented** |
| `IExchangeCapabilities` | `HyperliquidCapabilities` | **Not implemented** |

### Binance REST Client (knowledge file: `23-binance-integration.md`)

`IBinanceFuturesRestClient` / `BinanceFuturesRestClient` already exists for read-only ingestion (`/fapi/v1/klines`, `/fapi/v1/fundingRate`). It does NOT currently support authenticated endpoints (no HMAC signing). It will need to be extended or a new authenticated client created.

### Authentication Model Difference

| Concern | Hyperliquid | Binance |
|---|---|---|
| Identity | Ethereum wallet address (0x...) | API Key + Secret Key |
| Signing | EIP-712 typed data, ECDSA secp256k1 | HMAC SHA256 over query string params |
| Request auth | Signature in POST body | `X-MBX-APIKEY` header + `signature` query param |
| Key custody | Private key on Worker only | API Key + Secret needed wherever orders are placed |
| Testnet | mainnet/testnet via URL routing | `https://demo-fapi.binance.com` base URL swap |

### Wallet/Credential Model

`UserWalletAddress` currently stores one active record per user with `Exchange` as a string field. The repository `IUserWalletAddressRepository.GetActiveByUserIdAsync(userId)` returns a single record — it does NOT filter by exchange. This needs to change to support multiple active credentials per user (one per exchange).

Binance requires storing an API Key and Secret Key — not just a wallet address. The current `UserWalletAddress` entity only has a `WalletAddress` field. Options:
- **Option A**: Extend `UserWalletAddress` with nullable `ApiKey` / `EncryptedApiSecret` fields
- **Option B**: Create a separate `UserExchangeCredential` entity for key-based exchanges
- **Recommended**: Option B — cleaner separation, different validation rules, different security posture (secrets must be encrypted at rest)

### Current Order Flow (knowledge files: `29-control-plane-agent-architecture.md`, `30-worker-execution-pipeline.md`)

```
Dashboard → OrdersController → IHyperliquidOrderService → HyperliquidRestClient → Hyperliquid
Dashboard → TradingController → AgentCommandStore → Worker heartbeat → TradingSession → LiveExecutionEngine → Hyperliquid
```

Key issue: `OrdersController` directly depends on `IHyperliquidOrderService`. `TradingController` dispatches strategy commands through the agent pipeline. For Binance, the same pattern should apply but the order execution path in the Worker needs a `BinanceLiveExecutionEngine`.

### Current DI Registration

**API** (`src/TradePilot.Api/Program.cs`):
- `IExecutionEngine` → `HyperliquidExecutionEngine` (single registration)
- `IHyperliquidOrderService` → `HyperliquidOrderService`
- `IExchangeAccountClient` → `HyperliquidAccountAdapter`
- `IExchangeMarketMetadataProvider` → `HyperliquidMarketMetadataProvider`

**Worker** (`src/TradePilot.Worker/Program.cs`):
- `IExecutionEngine` → `LiveExecutionEngine` (single registration)
- All exchange services are Hyperliquid-specific singletons

Both hosts need **keyed DI** or a **factory/resolver pattern** to select the correct exchange implementation at runtime.

### Current UI State

- **Dashboard**: `DashboardComponent` uses `AccountStateService` + `HyperliquidApiService` — hardcoded to Hyperliquid
- **Order Entry**: `OrderEntryComponent` calls `OrderService` → `ApiRestClient.post("orders", ...)` — exchange-agnostic at the HTTP level but the API controller is Hyperliquid-specific
- **Strategy Builder**: Has an Exchange dropdown but only offers "Hyperliquid" as an option
- **Wallet Settings**: `WalletAddressController` manages a single wallet — no multi-exchange awareness

---

## Binance USD-M Futures API Reference

Base URL: `https://fapi.binance.com`
Testnet: `https://demo-fapi.binance.com`
Auth: HMAC SHA256 — `X-MBX-APIKEY` header, `signature` query/body param, `timestamp` param required

### Endpoints Needed

| Action | Method | Endpoint | Auth | Weight |
|---|---|---|---|---|
| Account balance | GET | `/fapi/v3/balance` | SIGNED | 5 |
| Account info (positions) | GET | `/fapi/v2/account` | SIGNED | 5 |
| Place order | POST | `/fapi/v1/order` | SIGNED | 1 |
| Cancel order | DELETE | `/fapi/v1/order` | SIGNED | 1 |
| Cancel all orders | DELETE | `/fapi/v1/allOpenOrders` | SIGNED | 1 |
| Change leverage | POST | `/fapi/v1/leverage` | SIGNED | 1 |
| Open orders | GET | `/fapi/v1/openOrders` | SIGNED | 1/40 |
| Position info | GET | `/fapi/v2/positionRisk` | SIGNED | 5 |
| Exchange info | GET | `/fapi/v1/exchangeInfo` | NONE | 1 |

### Order Types for TP/SL

- `STOP_MARKET` — stop-loss: triggers a market sell when `stopPrice` is hit
- `TAKE_PROFIT_MARKET` — take-profit: triggers a market sell when `stopPrice` is hit
- Both support `reduceOnly=true`, `closePosition=true`
- Use `positionSide=BOTH` for one-way mode (matches Hyperliquid's model)

### Rate Limits

- IP weight: 2400/min
- Order rate: 300/10s, 1200/min per account
- Retry on 429, back off on 418 (IP ban)

---

## Key Design Decisions Required

### 1. Exchange Resolution Strategy

How do we resolve the correct exchange implementation at runtime?

**Recommended approach**: .NET 8 keyed services
```csharp
builder.Services.AddKeyedScoped<IExchangeAccountClient, HyperliquidAccountAdapter>("Hyperliquid");
builder.Services.AddKeyedScoped<IExchangeAccountClient, BinanceAccountAdapter>("Binance");
```
With an `IExchangeResolver` that reads the user's active exchange context from the request.

### 2. Binance Credential Security

API Secret must be encrypted at rest. Options:
- AES-256-GCM with a data protection key from `IDataProtector`
- Azure Key Vault for production (deferred, use `IDataProtector` for POC)

### 3. Exchange Context in UI

How does a user indicate they're working with Binance vs Hyperliquid?

**Recommended**: Global exchange selector in the top navigation bar (similar to network selector pattern used for mainnet/testnet). The selected exchange becomes the "active exchange" for:
- Account balance display
- Order placement
- Position views
- Credential management

This selection should be persisted on the user profile (similar to `PreferredNetwork`).

### 4. Worker Exchange Awareness

The Worker currently creates a `TradingSession` with Hyperliquid-specific components. For Binance:
- The agent heartbeat should communicate which exchange to use
- `AgentCheckInService.CreateSession()` must compose Binance-specific or Hyperliquid-specific components based on the strategy config
- A `BinanceLiveExecutionEngine` implements `IExecutionEngine` and uses HMAC-signed REST calls instead of EIP-712

---

## Implementation Areas

### Area 1: Binance Credential Storage

**Domain changes:**
- New entity `UserExchangeCredential` with: `Id`, `UserId`, `Exchange` (enum), `ApiKey`, `EncryptedApiSecret`, `Label`, `CreatedAtUtc`, `IsActive`
- Or extend `UserWalletAddress` — but this mixes wallet addresses with API keys

**Repository changes:**
- `IUserWalletAddressRepository` needs: `GetActiveByUserIdAndExchangeAsync(userId, exchange)`
- New `IUserExchangeCredentialRepository` with: `GetActiveByUserIdAndExchangeAsync(userId, exchange)`, `AddAsync`, `SaveChangesAsync`

**API changes:**
- New endpoints or extend `WalletAddressController` to handle Binance credentials
- `POST /api/credentials/binance` — store API key + encrypted secret
- `GET /api/credentials` — list active credentials by exchange
- `DELETE /api/credentials/{id}` — revoke

**Security:**
- Encrypt secret at rest using `IDataProtector`
- Never return the full secret in API responses (mask it)
- Validate API key format

### Area 2: Binance Authenticated REST Client

**New components:**
- `IBinanceFuturesAuthClient` — authenticated Binance futures client (or extend `IBinanceFuturesRestClient`)
- `BinanceFuturesAuthClient` — implements HMAC SHA256 signing via `DelegatingHandler`
- `BinanceSigningHandler : DelegatingHandler` — intercepts outgoing requests, appends `timestamp` and `signature`

**HMAC Signing flow:**
1. Collect all query string + body params as `totalParams`
2. Compute `HMAC-SHA256(secretKey, totalParams)`
3. Append `&signature={hash}` to the request
4. Add `X-MBX-APIKEY: {apiKey}` header

**Resilience:**
- Polly retry for 429 (rate limit) and 5xx
- Respect `Retry-After` header
- Circuit breaker for sustained failures

### Area 3: Exchange Abstraction Implementations for Binance

Implement the remaining exchange abstraction contracts:

| Contract | Binance Implementation | Key Behaviour |
|---|---|---|
| `IExchangeAccountClient` | `BinanceAccountAdapter` | Calls `/fapi/v3/balance` and `/fapi/v2/account`, maps to `AccountSummaryDto` |
| `IExchangeMarketMetadataProvider` | `BinanceMarketMetadataProvider` | Calls `/fapi/v1/exchangeInfo`, provides leverage limits and tick/lot sizes for BTCUSDT/ETHUSDT |
| `IExchangeCapabilities` | `BinanceCapabilities` | Declares: Perp, supports leverage, supports trigger orders, supports reduce-only, no user-event stream (yet) |

### Area 4: Binance Execution Engine

**New `BinanceLiveExecutionEngine : IExecutionEngine`:**
- `PlaceOrderAsync` → `POST /fapi/v1/order` with HMAC signing
- `CancelOrderAsync` → `DELETE /fapi/v1/order`
- `CancelAllOrdersAsync` → `DELETE /fapi/v1/allOpenOrders`
- `SetLeverageAsync` → `POST /fapi/v1/leverage`
- `PlaceTriggerOrderAsync` → `POST /fapi/v1/order` with `type=STOP_MARKET` or `TAKE_PROFIT_MARKET`

**Symbol mapping:** Use `BinanceAssetMapper` (already exists) — `BTC` → `BTCUSDT`, `ETH` → `ETHUSDT`

**API-side execution engine:** `BinanceExecutionEngine` (mirrors `HyperliquidExecutionEngine`) wraps `IBinanceFuturesAuthClient` for direct order placement from the API host.

### Area 5: Exchange-Aware DI and Resolution

**Pattern: Keyed DI + Exchange Resolver**

```
IExchangeResolver.GetCurrentExchange(HttpContext) → Exchange enum
→ resolve keyed IExchangeAccountClient, IExecutionEngine, etc.
```

**API host changes:**
- Register Hyperliquid and Binance implementations as keyed services
- Create `IExchangeResolver` that reads the exchange from request header, query param, or user profile
- `AccountController` resolves the correct `IExchangeAccountClient` based on exchange context
- `OrdersController` must be refactored to use `IExecutionEngine` instead of `IHyperliquidOrderService` directly — or create a parallel `BinanceOrdersController`

**Worker host changes:**
- Strategy config already includes an `Exchange` field (from the strategy builder dropdown)
- `AgentCheckInService.CreateSession()` must compose the correct execution engine based on the strategy's exchange
- For manual Binance trading (not strategy-driven), the agent heartbeat command must carry exchange context

### Area 6: Dashboard and UI Changes

**Global exchange selector:**
- Add exchange selector to the top nav bar (Angular `ToolbarComponent` or `HeaderComponent`)
- Store selection in `UserPreferencesService` (localStorage + sync to user profile)
- All API calls from the dashboard include the active exchange as a header or query param

**Account summary:**
- `AccountController.GetAccountSummaryAsync` must resolve the correct `IExchangeAccountClient`
- The Angular `AccountSummaryComponent` / `DashboardComponent` should display exchange-contextual data
- Show the exchange name/logo next to the balance

**Order entry:**
- `OrderEntryComponent` works mostly unchanged — it posts to `/api/orders`
- The API must route to the correct execution engine based on exchange context
- Asset list should be filtered to the exchange's supported assets (BTC-PERP, ETH-PERP for Binance scope)

**Credentials page:**
- New settings page or extend wallet settings to manage Binance API Key + Secret
- Mask the secret, show validation status, allow revocation

**Strategy builder:**
- Exchange dropdown already exists but is locked to "Hyperliquid"
- Add "Binance" option
- Filter available markets based on selected exchange

### Area 7: Wallet/Credential Repository Enhancement

Current `IUserWalletAddressRepository.GetActiveByUserIdAsync` returns ONE record. Need:

- `GetActiveByUserIdAndExchangeAsync(userId, exchange)` — for exchange-specific lookup
- `GetAllActiveByUserIdAsync(userId)` — for listing all credentials across exchanges
- Keep backward compatibility: existing Hyperliquid wallet lookups must not break

---

## What Areas Are We Missing? (Gaps Analysis)

| Gap | Impact | Recommendation |
|---|---|---|
| **Fill detection for Binance** | Without user-data WebSocket, we won't know when orders fill in real-time | Poll `/fapi/v1/userTrades` or `/fapi/v1/order` on interval (every 5-10s). Add `IBinanceFillPoller` as a background task in the Worker |
| **Position sync for Binance** | Dashboard positions tab needs exchange-aware position data | `GET /fapi/v2/positionRisk` through `IExchangeAccountClient` — already covered by Area 3 |
| **Open orders for Binance** | Dashboard open orders tab | `GET /fapi/v1/openOrders` through `IExchangeAccountClient` |
| **Order tracking / LiveOrder persistence** | `LiveOrder` and `LiveFill` entities need an `Exchange` field | Add `Exchange` column to `LiveOrder` and `LiveFill` tables |
| **GridCycle exchange awareness** | Grid cycles are exchange-scoped | Add `Exchange` to `GridCycle` if grid trading is extended to Binance later |
| **Account controller refactoring** | `AccountController` directly uses `IHyperliquidAccountService` | Must be refactored to use `IExchangeAccountClient` resolved by exchange context |
| **Binance margin mode** | Binance supports Cross and Isolated margin | Need `POST /fapi/v1/marginType` endpoint support + UI toggle |
| **Error mapping** | Binance error codes differ from Hyperliquid | Create `BinanceApiException` and map common errors (insufficient margin, invalid symbol, etc.) to domain exceptions |
| **Binance exchange info caching** | Avoid hitting `/fapi/v1/exchangeInfo` on every request | Cache with 30-min TTL like `HyperliquidAssetMetadataCache` |
| **Health monitoring for Binance** | `HealthMonitorService` is Hyperliquid-specific | Extend or create Binance-specific health checks |
| **User preference for active exchange** | Need to persist which exchange the user is "looking at" | Add `PreferredExchange` to `User` entity (like `PreferredNetwork`) |
| **Binance API key validation** | User should know if their key works before trading | Add a "test connection" endpoint that calls `/fapi/v3/balance` to verify credentials |

---

## Phasing Recommendation

### Phase 1: Foundation (Credential Storage + Authenticated Client + Account Read)
- `UserExchangeCredential` entity, repository, migration
- `BinanceSigningHandler` (HMAC SHA256)
- `BinanceFuturesAuthClient` (authenticated REST)
- `BinanceAccountAdapter` (implements `IExchangeAccountClient`)
- `BinanceCapabilities` and `BinanceMarketMetadataProvider`
- Keyed DI registration pattern
- `IExchangeResolver` and exchange context middleware
- Credential management API endpoints
- Test connection endpoint
- Unit tests for signing, mapping, adapters

### Phase 2: Dashboard & Balance Display
- Refactor `AccountController` to use exchange-resolved `IExchangeAccountClient`
- UI global exchange selector in top nav
- `PreferredExchange` on User entity
- Dashboard shows Binance balance when Binance is selected
- Position and open-order display for Binance
- Credential management UI page
- Angular exchange context service

### Phase 3: Order Execution
- `BinanceLiveExecutionEngine` (Worker-side)
- `BinanceExecutionEngine` (API-side)
- Refactor `OrdersController` to be exchange-aware (or use `IExecutionEngine` with exchange resolution)
- Place MARKET/LIMIT orders for BTCUSDT/ETHUSDT
- Leverage change support
- TP/SL via STOP_MARKET and TAKE_PROFIT_MARKET companion orders
- Order entry UI asset filtering by exchange
- `LiveOrder`/`LiveFill` exchange column migration

### Phase 4: Worker Integration & Fill Detection
- `BinanceFillPoller` background service for the Worker
- Exchange-aware `AgentCheckInService.CreateSession()`
- Exchange context in heartbeat commands
- State recovery for Binance (poll open orders + recent fills on restart)
- Health monitoring extension

---

## Key Files to Read Before Planning

| File | Why |
|---|---|
| `.agent-context/0-knowledge/38-exchange-abstraction-architecture.md` | Exchange abstraction contracts and extension checklist |
| `.agent-context/0-knowledge/23-binance-integration.md` | Existing Binance read-only integration |
| `.agent-context/0-knowledge/02-hyperliquid-integration.md` | Hyperliquid integration patterns to mirror |
| `.agent-context/0-knowledge/29-control-plane-agent-architecture.md` | Agent command/heartbeat flow |
| `.agent-context/0-knowledge/30-worker-execution-pipeline.md` | Worker execution pipeline |
| `.agent-context/0-knowledge/04-domain-model.md` | Entity model, especially UserWalletAddress |
| `src/TradePilot.Domain/Entities/UserWalletAddress.cs` | Current wallet entity |
| `src/TradePilot.Application/Abstractions/Repositories/IUserWalletAddressRepository.cs` | Current repo — single-exchange |
| `src/TradePilot.Application/Abstractions/Services/IExecutionEngine.cs` | Execution boundary |
| `src/TradePilot.Infrastructure/Binance/BinanceAssetMapper.cs` | Existing Binance symbol mapper |
| `src/TradePilot.Infrastructure/Services/BinanceFuturesRestClient.cs` | Existing unauthenticated Binance client |
| `src/TradePilot.Api/Controllers/OrdersController.cs` | Current Hyperliquid-specific order controller |
| `src/TradePilot.Api/Controllers/AccountController.cs` | Current Hyperliquid-specific account controller |
| `src/TradePilot.Api/Program.cs` | API DI registration |
| `src/TradePilot.Worker/Program.cs` | Worker DI registration |
| `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` | Dashboard component |
| `frontend/trading-ui/src/app/features/order-entry/order-entry.component.ts` | Order entry component |
| `frontend/trading-ui/src/app/core/services/order.service.ts` | Angular order service |

---

## Constraints & Principles

- All orders must still route through `RiskEngine` — Binance orders are not exempt
- Never store Binance API Secret in plaintext — encrypt at rest
- Never return the full API Secret in any API response
- Use `BinanceAssetMapper` (via `IExchangeSymbolMapper`) for all symbol translation
- Follow the existing DI and service registration patterns
- Keep Hyperliquid as the default exchange — Binance is additive
- Scoped to BTC and ETH perpetuals only — do not add other assets
- Use one-way position mode (`positionSide=BOTH`) to match Hyperliquid's model
- Dashboard must make it visually obvious which exchange is active to prevent user confusion
