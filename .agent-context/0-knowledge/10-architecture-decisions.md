# Architecture Decisions

ADR 1 — Backend Language

C# (.NET) chosen for performance and maintainability.

ADR 2 — Frontend

Angular chosen for structured enterprise architecture.

ADR 3 — Database

SQLite chosen for POC phase due to simplicity and single-node deployment.

When the system moves to Azure cloud hosting, the database will migrate to Azure SQL.

The application uses EF Core, so the migration path is straightforward —
only the database provider and connection string change.

ADR 4 — Strategy Architecture

Strategies implemented as C# plugins.

Initial plugin:

GridStrategy

Future plugins:

TrendBreakoutStrategy  
MeanReversionStrategy

ADR 5 — Strategy Configuration

Users configure strategies using JSON configuration.

The JSON is stored in the database and interpreted by the strategy engine.

ADR 6 — Multi-Tenancy

The platform is multi-tenant. All data is tenant-scoped by UserId.

Shared database with tenant isolation (not database-per-tenant).
All queries filter by UserId to enforce data isolation.

ADR 7 — Authentication

User authentication via an external identity provider.

Azure AD B2C or Auth0 considered for production.
JWT-based API authentication.

ADR 8 — Subscriber Key Storage

Subscribers provide Hyperliquid wallet private keys.

Keys are encrypted at rest in the database (POC phase).
In Azure phase, keys are stored in Azure Key Vault.

The platform signs trading actions on behalf of each subscriber.

ADR 9 — Subscription Billing

Stripe or similar payment provider for subscription management.

Trading is paused if subscription lapses.

ADR 10 — Worker Scaling

The worker must execute strategies for all active subscribers on each candle close.

POC phase: single worker iterates over all active users.
Production phase: worker scales horizontally or partitions users across instances.

ADR 11 — CQRS Bus

MediatR chosen as the in-process CQRS pipeline.

Commands and queries are dispatched via `IMediator.Send()`.
Base types (`Command`, `Command<T>`, `CreateCommand`, `Query<T>`) and handler base classes
are defined in `TradingApp.Application.Abstractions`.
MediatR scans the Application assembly for all handler registrations.

ADR 12 — Ethereum Key Library

Nethereum chosen for wallet key derivation.

`HyperliquidSigner` uses Nethereum's `EthECKey` to derive the wallet public address
from a raw Ethereum-compatible private key. Hyperliquid uses EVM wallet addresses
for order authentication.

ADR 13 — Identity Stub

Real authentication (JWT/OIDC) is deferred until a later phase.

`IdentityService` in the Api layer returns a hardcoded dev `AppIdentity` during
development. All handlers receive identity as an `AppIdentity` parameter,
so the auth source can be swapped without changing handler code.

ADR 14 — Direct Service Injection in Api Layer (POC)

For POC-phase controllers that read raw exchange state (account, positions, orders),
the MediatR pipeline is bypassed. Controllers inject Api-layer services directly,
and those services use `IHyperliquidRestClient` to call Hyperliquid.

This avoids Application-layer ceremony for operations with no domain logic.
When business rules or domain entities are involved, use the standard MediatR path (ADR 11).

Api-layer services live in `TradingApp.Api/Services/`, not `TradingApp.Infrastructure`.
DTOs for these responses live in `TradingApp.Api/Models/`.

ADR 15 — Historical Market Data Source

Binance USDⓈ-M Futures is used as the primary source for historical candle and funding rate data.

Rationale:

- Hyperliquid's `candleSnapshot` API covers only from its own launch (late 2022). Binance data extends back to 2019, providing a much larger backtest window.
- Binance USDⓈ-M Futures is the most liquid perpetual futures venue. Its price and funding history is a reliable training dataset.
- Binance is read-only — no authentication or key management is required.

Scope:

- Binance is used for data ingestion only. Live trading always uses Hyperliquid.
- Candles ingested from Binance are stored with `Source = "Binance"` to distinguish them from Hyperliquid candles in the shared `Candles` table.