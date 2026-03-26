# Hyperliquid Integration

The platform interacts with Hyperliquid using:

- REST API
- WebSocket streams

Capabilities:

- order placement (per-user)
- order cancellation (per-user)
- position monitoring (per-user)
- market data streaming (shared)

---

# Authentication

Hyperliquid uses wallet-based signing.

Each request includes:

action  
nonce  
signature

Since the platform is multi-tenant, each subscriber provides their own wallet private key.

Private keys are encrypted at rest and stored per-user in the database.
In the Azure phase, keys are stored in Azure Key Vault.

The platform signs trading actions on behalf of each subscriber using their key.

---

# Multi-Tenant Connection Model

Market data streams (trades, candles, orderbook) are shared across all users.
These do not require per-user authentication.

User-specific streams (fills, order updates, position changes) require
per-user WebSocket subscriptions or polling.

The worker must manage connections for all active subscribers.

---

# WebSocket Streams

Shared streams:

- trades
- candles
- orderbook

Per-user streams:

- fills
- order updates
- position changes

---

# Reconnection

The worker must support:

automatic reconnect  
state recovery

After reconnect:

sync open orders (per-user)  
sync positions (per-user)

---

# Rate Limiting

With multiple subscribers, the platform must respect Hyperliquid API rate limits.

Order submissions should be queued and throttled to stay within limits.
Market data streams are shared and do not multiply with user count.

---

# Current Implementation

## Key Components

| Component | Location |
|-----------|----------|
| `HyperliquidOptions` | `src/TradingApp.Application/Abstractions/Configuration/HyperliquidOptions.cs` |
| `IHyperliquidSigner` | `src/TradingApp.Application/Abstractions/Services/IHyperliquidSigner.cs` |
| `IHyperliquidRestClient` | `src/TradingApp.Application/Abstractions/Services/IHyperliquidRestClient.cs` |
| `HyperliquidSigner` | `src/TradingApp.Infrastructure/Services/HyperliquidSigner.cs` |
| `HyperliquidRestClient` | `src/TradingApp.Infrastructure/Services/HyperliquidRestClient.cs` |
| `HyperliquidAssetMapper` | `src/TradingApp.Infrastructure/Hyperliquid/HyperliquidAssetMapper.cs` |
| `IHyperliquidAccountService` | `src/TradingApp.Api/Services/IHyperliquidAccountService.cs` |
| `HyperliquidAccountService` | `src/TradingApp.Api/Services/HyperliquidAccountService.cs` |
| `IHyperliquidWebSocketClient` | `src/TradingApp.Application/Abstractions/Services/IHyperliquidWebSocketClient.cs` |
| `HyperliquidWebSocketClient` | `src/TradingApp.Infrastructure/Services/HyperliquidWebSocketClient.cs` |
| `WebSocketConnectionState` | `src/TradingApp.Application/Abstractions/Services/WebSocketConnectionState.cs` |

`HyperliquidAssetMapper` is a static helper that maps display asset names (e.g., `BTC-PERP`) to Hyperliquid coin symbols (`BTC`) and resolves timeframe strings (e.g., `15m`, `1h`, `4h`) to interval milliseconds. It throws `NotFoundException` for unknown assets and `DomainException` for invalid timeframes.

Hyperliquid API request/response shapes live in `src/TradingApp.Infrastructure/Hyperliquid/Models/`.

## REST Info API

Hyperliquid read operations use a single POST `/info` endpoint with a `type` field discriminator.
`IHyperliquidRestClient.PostInfoAsync<TResponse>(request)` handles all typed info reads.

## WebSocket Client

`IHyperliquidWebSocketClient` manages a persistent WebSocket connection to Hyperliquid. Key operations:

| Method | Description |
|--------|-------------|
| `ConnectAsync` | Opens the `ClientWebSocket` and notifies state callbacks |
| `SubscribeToTradesAsync(coin)` | Sends the Hyperliquid trades subscription JSON frame |
| `ReceiveLoopAsync` | Reads messages in a loop and dispatches to the registered trade handler |
| `OnTradeReceived(handler)` | Registers a callback invoked per trade tick |
| `OnConnectionStateChanged(handler)` | Registers a callback invoked on state transitions |

`WebSocketConnectionState` enum: `Disconnected` → `Connecting` → `Connected` → `Reconnecting`

Registered as a **singleton** — one shared connection for all market data streams, since streams are not per-user authenticated.

Reconnection with exponential backoff (1 s–60 s, 20 retries max) is managed by the consuming `BackgroundService`, not the client itself.

Established request types:

| Request Type | Description | Auth Required |
|---|---|---|
| `clearinghouseState` | Account equity, margin, positions | Yes (wallet address) |
| `openOrders` | Active open orders | Yes (wallet address) |
| `meta` | Exchange metadata (used for connectivity check) | No |
| `metaAndAssetCtxs` | Full universe metadata + per-asset market context (price, funding rate, OI, 24h volume) | No |
| `candleSnapshot` | OHLCV candle data for a given coin, interval, and time range | No |

Requests requiring user identity include `"user": signerWalletAddress` in the body.

## Configuration

Config section: `Hyperliquid`

| Key | Description |
|-----|-------------|
| `Hyperliquid:BaseUrl` | REST API base URL (default: `https://api.hyperliquid-testnet.xyz`) |
| `Hyperliquid:WsBaseUrl` | WebSocket endpoint (default: `wss://api.hyperliquid-testnet.xyz/ws`) |
| `Hyperliquid:Network` | Network label — `"testnet"` or `"mainnet"` |
| `Hyperliquid:PrivateKey` | Wallet private key — set via `appsettings.Development.json` or env var `Hyperliquid__PrivateKey` |

`HyperliquidOptions` uses `[Required]` + `ValidateOnStart()`. `PrivateKey` is read directly from `IConfiguration` at startup and is NOT stored in `HyperliquidOptions` to avoid holding it in DI.

## DI Registration Pattern

`HyperliquidSigner` is constructed via its static factory (`HyperliquidSigner.Create(privateKey)`) at startup and registered as `IHyperliquidSigner` singleton. The raw private key is not retained in the DI container.

`HyperliquidRestClient` is registered as a typed `HttpClient<IHyperliquidRestClient, HyperliquidRestClient>` with 5-second timeout and `BaseUrl` from config.

## Extending

To add a new Hyperliquid read:
1. **Simple raw reads with no domain logic** (e.g., POC account state) — use `PostInfoAsync<TResponse>` directly inside an Api-layer service (see `IHyperliquidAccountService` and ADR 14). No new `IHyperliquidRestClient` method needed.
2. **Application-layer features with mapping/transformation** (e.g., asset name resolution, response parsing, candle batching) — add a typed method to `IHyperliquidRestClient` (e.g., `GetMarketInfoAsync`, `GetCandlesAsync`) and implement it in `HyperliquidRestClient` using `PostInfoAsync` internally. Consume via a MediatR query handler in `TradingApp.Application/{Feature}/Queries/`.
3. **New non-info endpoints** (e.g., WebSocket subscriptions, exchange order actions) — add a method to `IHyperliquidRestClient` and implement in `HyperliquidRestClient`.

---

# Testnet Characteristics

The Hyperliquid testnet (`https://api.hyperliquid-testnet.xyz`) behaves differently from mainnet in several important ways:

## Thin Order Book / Low Liquidity

- Testnet has very few real participants, so order books are thin with wide bid-ask spreads.
- Price swings on 15-minute candles can be 5–10x larger than mainnet (e.g., $1,500–$3,000 BTC range per 15m vs $50–$200 on mainnet).
- Consecutive candles sometimes show a uniform ~$1,600 range, caused by market-maker bots oscillating within a fixed band.

## Candle Data Anomalies

- Some candles have a range of exactly $0 during inactive periods (no trades).
- Large wicks and tall candle bodies are normal testnet artifacts, not indicative of data corruption.
- OHLC data integrity is correct (low never exceeds high, timestamps are contiguous with no gaps).

## Implications for Development

- Do not tune strategy parameters (e.g., grid spacing, stop-loss thresholds) against testnet price action — the volatility profile is unrealistic.
- Candle chart visualisations will show exaggerated candle heights compared to real markets. This is expected.
- Backtesting against testnet historical data will produce unreliable results. Use mainnet historical data or synthetic data for strategy validation.
- Slippage and fill behaviour on testnet do not represent mainnet conditions.