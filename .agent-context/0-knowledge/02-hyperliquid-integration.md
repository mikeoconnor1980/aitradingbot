# Hyperliquid Integration

This document describes the implemented Hyperliquid integration across the control-plane API and the execution-agent Worker. The platform uses shared REST and market-data clients, per-user wallet routing, and runtime-configurable signing so that the server can manage user wallet addresses without taking custody of trading keys.

## Overview

The integration is split across two runtime contexts:

| Host | Role |
|------|------|
| `src/TradingApp.Api` | Control plane for account inspection, order endpoints, asset metadata, and user-profile driven network routing |
| `src/TradingApp.Worker` | Execution agent that owns private-key signing, live strategy execution, and per-wallet user event streams |

Core capabilities:

- Shared REST reads through `IHyperliquidRestClient`
- Shared public WebSocket market data through `IHyperliquidWebSocketClient`
- Per-user wallet event streams through `IHyperliquidUserEventClient`
- Per-user mainnet/testnet routing through `INetworkProvider` and `NetworkRoutingHandler`
- Runtime-swappable signing through `ISignerProvider` and `MutableSignerProvider`
- Separate execution engines for API-hosted direct actions and Worker-hosted live trading

## Authentication And Key Custody

Hyperliquid uses wallet-based signing for exchange actions. The implemented system does not store private keys in the control plane.

| Concern | Implemented behavior |
|---------|----------------------|
| Wallet identity | The server stores wallet addresses per user via the user profile and `UserWalletAddress` records |
| Private key custody | Private keys are expected to live on the Worker or be supplied via environment/runtime configuration |
| Signing contract | `IHyperliquidSigner` exposes only `WalletAddress` and `SignHash(byte[])`; typed-data signing is only available on the concrete signer implementation |
| Runtime key management | `ISignerProvider` extends the signer with `IsConfigured`, `Configure(key)`, and `Clear()` |

`MutableSignerProvider` in `src/TradingApp.Infrastructure/Services/MutableSignerProvider.cs` is the key runtime pattern. It is thread-safe, can be reconfigured without restart, and is registered as both `ISignerProvider` and `IHyperliquidSigner`.

The API host creates the signer provider without a key at startup and logs a warning if no runtime key is configured. The Worker follows the same provider pattern and can optionally bootstrap from `Hyperliquid__PrivateKey`, but the provider remains mutable so the key can be swapped later.

## Network Routing

Hyperliquid reads and writes are routed per user rather than through a single global network setting.

| Component | Location | Purpose |
|-----------|----------|---------|
| `INetworkProvider` | `src/TradingApp.Application/Abstractions/Services/INetworkProvider.cs` | Abstraction for resolving the effective network |
| `UserNetworkProvider` | `src/TradingApp.Api/Infrastructure/UserNetworkProvider.cs` | Resolves mainnet vs testnet from the current request context |
| `NetworkRoutingHandler` | `src/TradingApp.Api/Infrastructure/NetworkRoutingHandler.cs` | `DelegatingHandler` that rewrites outgoing requests to the user-specific base URL |

This lets the API expose account and order features for users on different Hyperliquid networks without duplicating service registrations.

## Key Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `HyperliquidOptions` | `src/TradingApp.Application/Abstractions/Configuration/HyperliquidOptions.cs` | Base REST/WS configuration |
| `IHyperliquidSigner` | `src/TradingApp.Application/Abstractions/Services/IHyperliquidSigner.cs` | Minimal signing interface: wallet address plus hash signing |
| `ISignerProvider` | `src/TradingApp.Application/Abstractions/Services/ISignerProvider.cs` | Runtime-configurable signer abstraction |
| `MutableSignerProvider` | `src/TradingApp.Infrastructure/Services/MutableSignerProvider.cs` | Thread-safe runtime-swappable signer implementation |
| `IHyperliquidRestClient` | `src/TradingApp.Application/Abstractions/Services/IHyperliquidRestClient.cs` | Typed Hyperliquid REST boundary |
| `HyperliquidRestClient` | `src/TradingApp.Infrastructure/Services/HyperliquidRestClient.cs` | Handles `/info` and `/exchange` calls, typed parsing, and API error translation |
| `IHyperliquidAccountService` | `src/TradingApp.Application/Abstractions/Services/IHyperliquidAccountService.cs` | Account/position abstraction used by API and Worker |
| `HyperliquidAccountService` | `src/TradingApp.Infrastructure/Services/HyperliquidAccountService.cs` | Account-state implementation backed by REST calls |
| `IHyperliquidOrderService` | `src/TradingApp.Api/Services/IHyperliquidOrderService.cs` | API surface for direct order actions |
| `HyperliquidOrderService` | `src/TradingApp.Api/Services/HyperliquidOrderService.cs` | Builds signed exchange actions and companion trigger orders |
| `IHyperliquidWebSocketClient` | `src/TradingApp.Application/Abstractions/Services/IHyperliquidWebSocketClient.cs` | Shared public market-data WebSocket client |
| `HyperliquidWebSocketClient` | `src/TradingApp.Infrastructure/Services/HyperliquidWebSocketClient.cs` | Public WebSocket implementation |
| `IHyperliquidUserEventClient` | `src/TradingApp.Application/Abstractions/Services/IHyperliquidUserEventClient.cs` | Per-wallet user event stream abstraction |
| `HyperliquidUserEventClient` | `src/TradingApp.Infrastructure/Services/HyperliquidUserEventClient.cs` | User WebSocket for fills and order updates |
| `IHyperliquidAssetMetadataCache` | `src/TradingApp.Api/Services/HyperliquidAssetMetadataCache.cs` | Lazy-loaded exchange metadata cache |
| `HyperliquidAssetMetadataCache` | `src/TradingApp.Api/Services/HyperliquidAssetMetadataCache.cs` | 30-minute TTL cache of asset index, size decimals, and leverage |
| `HyperliquidExecutionEngine` | `src/TradingApp.Api/Services/HyperliquidExecutionEngine.cs` | API-side `IExecutionEngine` wrapper over the order service |
| `LiveExecutionEngine` | `src/TradingApp.Infrastructure/Services/LiveExecutionEngine.cs` | Worker-side execution engine that signs and submits live orders |

## REST Client Behavior

`HyperliquidRestClient` is the shared boundary for Hyperliquid HTTP operations.

Implemented request types include:

| Method | Purpose |
|--------|---------|
| `PostInfoAsync<T>` | Generic typed `/info` reads |
| `PostExchangeAsync<T>` | Signed `/exchange` writes |
| `GetMarketInfoAsync` | `metaAndAssetCtxs` mapping to market info |
| `GetCandlesAsync` | Last 500 candles for a timeframe |
| `GetCandleSnapshotsAsync` | Range-based candle snapshots returning `List<CandleSnapshotDto>` including `NumTrades` |
| `GetUserFillsAsync` | User fill history via `userFills` or `userFillsByTime` |

### Retry And Timeout Policy

The current registration uses:

- `HttpClient.Timeout = 30s` as the outer cap for the overall request lifecycle
- Polly-based resilience with up to 5 exponential-backoff retries
- Initial retry delay of 1 second, max delay 60 seconds, jitter enabled
- Per-attempt timeout of 5 seconds
- Retry handling for HTTP 429 and 5xx responses

This is registered in both API and Worker hosts. The retry pipeline is attached when `IHyperliquidRestClient` is registered.

## Asset Mapping And Metadata

`HyperliquidAssetMapper` in `src/TradingApp.Infrastructure/Hyperliquid/HyperliquidAssetMapper.cs` is intentionally lenient.

| Method | Implemented behavior |
|--------|----------------------|
| `ToCoin` | Strips `-PERP` or `-USD` suffixes when present, otherwise returns the input unchanged |
| `ToDisplayName` | Maps known coins back to `*-PERP`, otherwise formats `COIN-PERP` |
| `GetIntervalMs` | Converts supported timeframe strings to milliseconds and throws only for invalid timeframes |
| `IsValidTimeframe` | Checks supported timeframe set |
| `IsValidCoin` | Checks known display-mapped coin set |
| `GetSupportedCoins` | Returns supported mapped coin list |
| `GetSupportedTimeframes` | Returns supported timeframe list |

Unknown coin failures are not raised by `ToCoin`. The lookup that can throw `NotFoundException` is `HyperliquidAssetMetadataCache.GetAsync`, because that is where exchange metadata is validated against the current universe.

`HyperliquidAssetMetadataCache` is API-hosted and lazy-loads `meta` into a 30-minute cache of:

- `Index`
- `SzDecimals`
- `MaxLeverage`

## WebSocket Model

The implemented WebSocket model separates shared market data from authenticated user events.

### Shared Market Data

`IHyperliquidWebSocketClient` is registered as a singleton and is used for public streams such as:

- trades
- candles
- order book updates

Reconnection behavior is handled by the consuming hosted services rather than by the client owning its own infinite retry loop.

### Per-User Event Streams

`IHyperliquidUserEventClient` supports wallet-scoped user events, including:

- fill updates
- order updates

The current consumer pattern lives in the API and Worker `UserEventStreamService` and `TradingSession` services. This is a one-client-per-session model rather than a centralized multi-tenant socket manager.

## Execution Engines

Two concrete `IExecutionEngine` implementations exist today:

| Engine | Host | Purpose |
|--------|------|---------|
| `HyperliquidExecutionEngine` | API | Uses API services for direct order placement flows initiated through the control plane |
| `LiveExecutionEngine` | Worker | Owns direct live execution during strategy sessions, including leverage updates and exchange submission |

This split keeps API-hosted direct operations separate from the Worker's always-on trading loop.

## Trigger Orders And Account Enrichment

The API order flow supports exchange-native trigger orders for stop loss and take profit.

- Trigger orders are reduce-only and market-triggered on activation
- Companion triggers can be placed after a successful entry order
- Trigger order failures are treated as warnings, not as hard failures for the parent order
- Account position reads enrich position DTOs by correlating live reduce-only trigger orders back onto positions

This enrichment logic is implemented in `HyperliquidAccountService`, which now lives in Infrastructure rather than Api.

## Extending The Integration

Use these rules when adding functionality:

1. New `/info` reads with lightweight mapping belong on `IHyperliquidRestClient` when they are reused across layers.
2. Account- or order-specific orchestration belongs in the API or Worker service layer, not inside the raw REST client.
3. New exchange actions should build payloads through the existing signing flow and reuse `PostExchangeAsync`.
4. Network-aware calls in the API should flow through `INetworkProvider` so per-user routing is preserved.
5. New authenticated stream consumers should use `IHyperliquidUserEventClient` rather than overloading the shared public WebSocket client.

## Related Knowledge

- [03-infrastructure-architecture.md](03-infrastructure-architecture.md)
- [19-scheduling-architecture.md](19-scheduling-architecture.md)
- [30-worker-execution-pipeline.md](30-worker-execution-pipeline.md)
- [33-risk-management-and-trade-sizing.md](33-risk-management-and-trade-sizing.md)

## Future Recommendations

- Add a true multi-tenant user-event WebSocket connection manager so the platform can multiplex many wallet streams more efficiently.
- Extend `HyperliquidAssetMapper` for HIP-3 and other non-standard symbol families instead of relying only on simple suffix stripping.
- Move reconnection backoff, replay, and resubscription policy into a reusable consumer layer shared by API and Worker user-event services.