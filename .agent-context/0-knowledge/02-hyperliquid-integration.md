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

## Configuration

Config section: `Hyperliquid`

| Key | Description |
|-----|-------------|
| `Hyperliquid:BaseUrl` | REST API base URL (default: `https://api.hyperliquid-testnet.xyz`) |
| `Hyperliquid:Network` | Network label — `"testnet"` or `"mainnet"` |
| `Hyperliquid:PrivateKey` | Wallet private key — set via `appsettings.Development.json` or env var `Hyperliquid__PrivateKey` |

`HyperliquidOptions` uses `[Required]` + `ValidateOnStart()`. `PrivateKey` is read directly from `IConfiguration` at startup and is NOT stored in `HyperliquidOptions` to avoid holding it in DI.

## DI Registration Pattern

`HyperliquidSigner` is constructed via its static factory (`HyperliquidSigner.Create(privateKey)`) at startup and registered as `IHyperliquidSigner` singleton. The raw private key is not retained in the DI container.

`HyperliquidRestClient` is registered as a typed `HttpClient<IHyperliquidRestClient, HyperliquidRestClient>` with 5-second timeout and `BaseUrl` from config.

## Extending

To add a new Hyperliquid REST endpoint:
1. Add method to `IHyperliquidRestClient`
2. Implement in `HyperliquidRestClient`
3. Inject `IHyperliquidRestClient` into the relevant query/command handler