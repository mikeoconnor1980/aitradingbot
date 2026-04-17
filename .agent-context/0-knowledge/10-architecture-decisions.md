# Architecture Decisions

## ADR 1 - Backend Language

C# on .NET is the primary backend platform for API, worker, domain, application, persistence, and integration code. The choice favors long-term maintainability, strong typing, and a consistent host model across HTTP APIs, background services, and tooling.

## ADR 2 - Frontend

Angular is the UI platform for the control plane. The current codebase uses standalone components, route guards, interceptors, and feature-folder organization to keep larger product surfaces manageable.

## ADR 3 - Database Strategy

SQLite remains the default local-development database because it keeps setup friction low and works well for local experimentation. The Azure deployment path provisions Azure SQL through Bicep, and the EF Core-based persistence layer keeps the provider switch isolated to infrastructure and configuration.

## ADR 4 - Strategy Architecture

Trading strategies are implemented through application-layer strategy services and configuration-driven evaluation rather than hardcoded controller logic. The current implementation centers on grid-style strategy execution, but the pipeline is intentionally structured so additional strategy families can be introduced later.

## ADR 5 - Strategy Configuration Format

Strategy definitions are stored as JSON-backed configuration objects. This allows the strategy builder, AI interpreter, validator chain, backtester, and optimizer to share the same config contract.

## ADR 6 - Multi-Tenancy

The system is multi-tenant with shared infrastructure and tenant-scoped data. Repository access and most domain entities are filtered by `UserId` rather than using database-per-tenant isolation.

## ADR 7 - Authentication Is Custom JWT Plus Google OAuth

Authentication is implemented, not deferred.

Current design:

- email and password registration and login
- JWT access tokens and refresh tokens
- Google sign-in via Google Identity Services token validation
- auth contracts under `TradingApp.Application/Abstractions/Auth/`
- concrete implementations such as `JwtTokenService`, `AspNetPasswordHasher`, and `GoogleTokenValidator`

Rationale:

- the current auth surface is modest and fully under application control
- the team needed a working auth flow without introducing Azure AD B2C or Auth0 complexity
- JWT claims map cleanly into the existing `IdentityService` abstraction

## ADR 8 - Private Keys Stay on the Execution Agent

The system no longer stores subscriber private keys in the platform database.

Current design under Option C:

- the platform stores wallet addresses in `UserWalletAddress`
- the execution agent holds the private key through local config or environment variables
- `MutableSignerProvider` manages runtime signer state on the agent
- the server never signs orders on behalf of users

This is the core security boundary of the split architecture.

## ADR 9 - Billing Is Deferred

Subscription billing is still planned around Stripe or an equivalent provider, but the billing system is not yet implemented. The current subscription feature set is limited compared with the original business-model ambitions.

## ADR 10 - Worker Scaling Strategy Changed with Option C

The original "single worker iterates all subscribers" idea no longer describes the live execution model. Execution is pushed to subscriber-side agents, while the control plane coordinates strategy state, commands, and monitoring. Future scaling work is therefore split between control-plane scalability and agent fleet management, not just server-side worker concurrency.

## ADR 11 - CQRS Bus

MediatR remains the in-process CQRS mechanism for most application features. Commands and queries are defined in `TradingApp.Application`, and the API registers handlers by scanning the Application assembly.

## ADR 12 - Ethereum-Compatible Signing Library

Nethereum is used for wallet-address derivation and EVM-compatible signing primitives. This is aligned with Hyperliquid's wallet model and keeps signing behavior inside a well-known ecosystem.

## ADR 13 - Identity Comes from ClaimsPrincipal with a Dev Fallback

`IdentityService` now resolves identity from `HttpContext.User` and extracts `NameIdentifier` and `Email` claims into `AppIdentity`.

There is still a `dev-user` fallback for unauthenticated local scenarios, but production access is expected to be protected by `[Authorize]` and JWT authentication. Identity is therefore no longer a hardcoded stub architecture.

## ADR 14 - Direct Service Injection for Some API Features

The project still allows direct Api-layer service injection for exchange-facing operations such as account, order, and risk endpoints where MediatR would add ceremony without domain value. CQRS remains the default for domain-shaped operations, while thin exchange operations can stay in the API layer.

## ADR 15 - Historical Market Data Source

Binance USD-M Futures is used as the primary source for historical candles and funding rates. Hyperliquid remains the live-trading venue, but Binance offers a deeper historical dataset for backtesting and optimization.

## ADR 16 - Trigger Order State Is Exchange-Authoritative

Trigger orders such as stop loss and take profit orders are not persisted as first-class local entities. The system treats Hyperliquid as the source of truth and enriches position views from live exchange state instead of mirroring every trigger order in the database.

## ADR 17 - Business Model Option C Is the Chosen Deployment Model

TradingApp adopted the split architecture:

- control plane in the API and Angular UI
- execution on the subscriber machine through `TradingApp.ExecutionAgent`
- heartbeat and command polling between agent and control plane
- no cloud-side custody of private keys

This was chosen over:

- Option A, which would sacrifice centralized product control and observability
- Option B, which would require server-side custody of customer private keys

## ADR 18 - Per-User Network Routing

Hyperliquid network selection is resolved per request.

Implementation:

- `UserNetworkProvider` reads `User.PreferredNetwork`
- `NetworkRoutingHandler` rewrites outgoing Hyperliquid request URIs
- API services can support mainnet and testnet users concurrently

This avoids separate API deployments for different network targets.

## ADR 19 - Azure SignalR Is the Production Push Layer

Production real-time browser messaging uses Azure SignalR through `AzureSignalRPublisher` and `Microsoft.Azure.SignalR.Management`.

Rationale:

- managed service with simpler operations than self-managed Redis fan-out
- good fit for browser-centric real-time updates
- clean separation between local in-process SignalR and cloud publishing

Redis is not the current production backplane choice.

## ADR 20 - Three Independent LLM Clients

The system uses separate LLM client roles rather than a single shared AI client:

- `ILlmClient` for strategy interpretation
- `IReviewLlmClient` for revision review
- `ILlmContextClient` for live market context

This keeps prompts, temperatures, credentials, and failure handling isolated by use case.

## ADR 21 - Macro Calendar Acts as a Trade Gate

Economic events are not just informational UI data. Macro calendar data is part of the execution gate.

Implementation:

- macro events are synchronized into the application database
- `MacroEventRiskCheck` evaluates block windows around high-impact events
- `MacroCalendarOptions` control sync behavior and timing rules

The design intent is to reduce exposure around scheduled macro volatility.

## ADR 22 - Strategy Optimization Is a First-Class Feature

Optimization is implemented as a persisted workflow rather than an ad hoc script.

Current design includes:

- queued optimization runs
- parameter sweep execution
- evolutionary search
- walk-forward out-of-sample validation
- persisted `OptimizationRun` and `OptimizationResult` records
- fitness scoring based on performance and risk metrics

This supports repeatable research and comparison rather than one-off manual tuning.

## ADR 23 - Indicators Live in a Separate Project

`TradingApp.Indicators` is a standalone library for indicator calculations.

Rationale:

- separates numerical indicator logic from orchestration logic
- improves reuse between live trading, backtesting, and optimization
- keeps the indicator layer lightweight and dependency-minimal

## Future Recommendations

- Add ADRs for agent update distribution, installer packaging, and operational kill-switch behavior once those flows stabilize.
- Revisit ADR 9 after subscription billing is implemented so the knowledge base reflects an actual billing state instead of a planned one.
- Add an ADR for secret management once Azure Key Vault or another managed-secret pattern is adopted.
- Add an ADR for observability once tracing, metrics, and production diagnostics are standardized.