# Project Structure

## Solution Overview

Solution entry point: `TradingApp.sln`

| Project | Role |
|---------|------|
| `TradingApp.Domain` | Core entities and domain rules |
| `TradingApp.Application` | CQRS handlers, strategy pipeline abstractions, scheduling, macro calendar, optimization, and feature contracts |
| `TradingApp.Infrastructure` | Auth, Hyperliquid, Binance, signing, SignalR publishing, and external integration implementations |
| `TradingApp.Persistence` | EF Core context, migrations, and repository implementations |
| `TradingApp.Api` | ASP.NET Core control plane host |
| `TradingApp.AI` | LLM interpretation and review services |
| `TradingApp.Indicators` | Standalone indicator calculation library used by Application services |
| `TradingApp.Worker` | Builds the `TradingApp.ExecutionAgent` Windows Service for client-side live execution |

The tenant boundary is enforced in the Domain, Application, Persistence, and API layers through `UserId`-scoped entities and repository access.

## Indicators Project

`src/TradingApp.Indicators/` is now a first-class project in the solution.

It contains reusable technical indicator implementations including:

- `AtrCalculator`
- `BollingerBandsCalculator`
- `EmaCalculator`
- `MacdCalculator`
- `RsiCalculator`
- `SupportResistanceCalculator`
- incremental helpers in `Incremental/` such as `IncrementalAtr`, `IncrementalEma`, `IncrementalMacd`, `IncrementalRsi`, and `IncrementalSma`

The project file currently has no external NuGet dependencies and is consumed as a lightweight shared library.

## Application Layer

`src/TradingApp.Application/` is organized by feature and by cross-cutting abstractions.

### Core folders

| Folder | Purpose |
|--------|---------|
| `Abstractions/Commands` | Base command records and handler patterns |
| `Abstractions/Queries` | Base query records and handlers |
| `Abstractions/Repositories` | Repository contracts owned by the Application layer |
| `Abstractions/Services` | Trading, exchange, streaming, and orchestration interfaces |
| `Abstractions/Auth` | JWT, Google auth, refresh token, and password hashing contracts and options |
| `Abstractions/Configuration` | Typed options such as exchange configuration |

### Feature folders

| Folder | Purpose |
|--------|---------|
| `Agent/` | Agent command store, heartbeat models, update metadata, and control-plane coordination |
| `Backtesting/` | Backtest commands, queries, models, metrics, and replay services |
| `Candles/` | Candle ingestion commands, coverage queries, and response models |
| `FundingRates/` | Funding rate ingestion commands and models |
| `Health/` | Health queries and DTOs |
| `Help/` | Help chat queries and models |
| `LlmContextSnapshots/` | Queries and DTOs for market-context history and current snapshots |
| `MacroCalendar/` | Macro event models, options, query services, ingestion contracts, and trade-gating services |
| `MarketData/` | Market info and candle queries used by the UI |
| `Optimization/` | Run and cancel commands, history queries, fitness models, job queues, sweep runner, evolutionary runner, and response mapping |
| `Scheduling/` | `CandleClock`, `StrategyScheduler`, and candle-close event models |
| `StrategyAuthoring/` | Strategy CRUD, AI review, validation, serialization, and typed strategy schema models |
| `Subscriptions/` | Free-tier subscription command and status query |
| `Trading/` | Trading models and runtime service contracts |

## AI Layer

`src/TradingApp.AI/` contains the LLM-facing implementation used by the control plane.

| Area | Purpose |
|------|---------|
| `Models/` | OpenAI-compatible request and response models |
| `Prompts/` | Prompt templates for strategy interpretation and review |
| `Services/OpenAiCompatibleLlmClient.cs` | General strategy interpretation client |
| `Services/ReviewLlmClient.cs` | Independent review client |
| `Services/StrategyInterpreter.cs` | Natural-language to strategy intent translation |
| `Services/StrategyReviewer.cs` | Markdown review generation for strategy revisions |
| `AiServiceExtensions.cs` | DI composition for AI services |

## Infrastructure Layer

`src/TradingApp.Infrastructure/` holds concrete implementations that integrate the system with external APIs and platform services.

### Service implementations

| File | Purpose |
|------|---------|
| `AspNetPasswordHasher.cs` | Password hashing implementation for auth flows |
| `AzureSignalRPublisher.cs` | Production `ISignalRPublisher` using Azure SignalR management APIs |
| `BinanceCandleIngestionService.cs` | Historical Binance candle ingestion |
| `BinanceFuturesRestClient.cs` | Typed Binance REST client |
| `CandleIngestionService.cs` | Hyperliquid candle ingestion |
| `FundingRateIngestionService.cs` | Binance funding rate ingestion |
| `GoogleTokenValidator.cs` | Google OAuth token validation |
| `HyperliquidAccountService.cs` | Exchange account and position reads used by API and worker flows |
| `HyperliquidRestClient.cs` | Typed Hyperliquid REST client |
| `HyperliquidSigner.cs` | Wallet address derivation from private key material |
| `HyperliquidUserEventClient.cs` | Hyperliquid user-event WebSocket client |
| `HyperliquidWebSocketClient.cs` | Shared market-data WebSocket client |
| `JwtTokenService.cs` | JWT access and refresh token generation and validation |
| `LiveExecutionEngine.cs` | Live order execution engine used by the worker |
| `MutableSignerProvider.cs` | Runtime-configurable signer and key holder on the execution agent |
| `NonceProvider.cs` | Nonce generation for outbound order flows |

### Other folders

| Folder | Purpose |
|--------|---------|
| `Hyperliquid/` | Hyperliquid mappers and request/response models |
| `Binance/` | Binance mappers and models |
| `Providers/MacroCalendar/` | Macro calendar provider implementations such as `StubMacroCalendarProvider` |

## API Layer

`src/TradingApp.Api/` is the browser-facing control plane.

### Controllers

| Controller | Purpose |
|-----------|---------|
| `AccountController` | Account summary, positions, and order state |
| `AgentController` | Agent heartbeat, command polling, and update metadata |
| `AuthController` | Registration, login, refresh token, and Google sign-in |
| `BacktestsController` | Backtest submission and result retrieval |
| `CandlesController` | Candle ingestion and coverage endpoints |
| `FundingRatesController` | Funding rate ingestion endpoints |
| `HealthController` | API and dependency health checks |
| `HelpController` | Help and assistant queries |
| `LiveTradingController` | Live trading session control |
| `MacroCalendarController` | Macro event query and sync endpoints |
| `MarketContextController` | LLM context snapshot queries |
| `MarketDataController` | Market info and candle data |
| `OptimizationsController` | Optimization run, cancel, list, and detail endpoints |
| `OrdersController` | Order placement and order management operations |
| `ProfileController` | User profile management |
| `ReferenceDataController` | Supported assets, timeframes, and reference metadata |
| `RiskController` | Risk and protective order operations |
| `StrategiesController` | Strategy CRUD, validation, and review flows |
| `SubscriptionController` | Subscription activation and status |
| `TradingController` | Agent-directed trading commands |
| `WalletAddressController` | Wallet address persistence |
| `WalletController` | Wallet-related endpoints |

### API infrastructure

| File | Purpose |
|------|---------|
| `Infrastructure/ApiController.cs` | Base controller for MediatR-backed endpoints |
| `Infrastructure/CorrelationIdMiddleware.cs` | Request correlation middleware |
| `Infrastructure/CreatedResultEnvelope.cs` | Standard created response envelope |
| `Infrastructure/Envelope.cs` | Standard error envelope |
| `Infrastructure/IdentityService.cs` | Resolves `AppIdentity` from JWT claims with a dev fallback |
| `Infrastructure/NetworkRoutingHandler.cs` | Per-request Hyperliquid base URL rewriting |
| `Infrastructure/UserNetworkProvider.cs` | Resolves the current user's preferred mainnet or testnet setting |
| `Infrastructure/Filters/` | HTTP exception mapping and global filters |

### API services

| File | Purpose |
|------|---------|
| `BacktestProcessorService.cs` | Hosted service that processes queued backtests |
| `HubContextSignalRPublisher.cs` | Local SignalR publisher implementation |
| `HyperliquidAssetMetadataCache.cs` | Hyperliquid asset metadata caching |
| `HyperliquidExecutionEngine.cs` | API-hosted execution adapter for live order flows |
| `HyperliquidOrderService.cs` | Order placement, modification, and cancellation orchestration |
| `MacroCalendarSyncWorker.cs` | Hosted macro calendar synchronization |
| `MarketDataStreamService.cs` | Local market data streaming service when Azure SignalR is absent |
| `OptimizationProcessorService.cs` | Hosted optimization job processor |
| `UnavailableBacktestRunner.cs` | Placeholder `IBacktestRunner` for unsupported host compositions |
| `UserEventStreamService.cs` | Local user-event streaming service when Azure SignalR is absent |

## Worker Host

`src/TradingApp.Worker/` is not a generic backend worker. It produces the subscriber-side execution agent.

Important characteristics from `TradingApp.Worker.csproj`:

- `AssemblyName` is `TradingApp.ExecutionAgent`
- Release publishes are single-file, self-contained, and `win-x64`
- the service is intended to run as a Windows Service and is distributed through the installer pipeline

Key worker services live under `src/TradingApp.Worker/Services/` and include:

- `AgentCheckInService`
- `MarketDataStreamService`
- `UserEventStreamService`
- `HealthMonitorService`
- `TradingSession`
- `TradingHealthProvider`
- `UpdateCheckerService`

## Persistence Layer

`src/TradingApp.Persistence/` contains:

- `TradingAppDbContext`
- repository implementations
- EF Core migrations
- `PersistenceServiceExtensions` for registration and startup migration
- `DesignTimeDbContextFactory` for tooling

Repository contracts remain in `src/TradingApp.Application/Abstractions/Repositories/`, while implementations live in `src/TradingApp.Persistence/Repositories/`.

## Frontend

`frontend/trading-ui/` is an Angular standalone application.

### Core folders

| Folder | Purpose |
|--------|---------|
| `core/components/` | Shared UI building blocks |
| `core/guards/` | Route guards such as auth, mobile redirect, and subscription gating |
| `core/interceptors/` | HTTP interceptors for auth and request handling |
| `core/models/` | Shared TypeScript models aligned with API responses |
| `core/pipes/` | Shared formatting pipes |
| `core/services/` | Root-scoped REST, SignalR, auth, and domain services |
| `core/utils/` | Shared helper utilities |

### Feature folders

| Folder | Purpose |
|--------|---------|
| `features/agents/` | Agent health, update, and kill-switch UI |
| `features/auth/` | Login and registration pages |
| `features/backtesting/` | Backtest submission, result visualization, comparisons, and coverage views |
| `features/candle-management/` | Candle ingestion and coverage management |
| `features/connection/` | Connectivity and health status UI |
| `features/dashboard/` | Live account overview, positions, orders, and related dialogs |
| `features/macro-calendar/` | Economic event calendar and risk windows |
| `features/market-data/` | Market info, live ticker, and charting |
| `features/optimizer/` | Optimization setup, history, detail, and results views |
| `features/order-entry/` | Manual order-entry workflows |
| `features/profile/` | Profile and preference management |
| `features/strategy-builder/` | Strategy list, builder, AI review, diff view, and multi-step wizard |

The route map in `src/app/app.routes.ts` confirms that auth, strategy authoring, optimizer, macro calendar, agent management, profile, order entry, backtesting, and dashboard flows are all part of the current shipped UI.

## Future Recommendations

- Add a short ownership map showing which layer owns each major business capability.
- Add a repository-level note for generated artifacts and deployment output directories.
- Add a dedicated section for cross-project conventions such as DI composition roots, options binding, and tenant scoping.
- Add a small dependency diagram showing how `TradingApp.Indicators`, `TradingApp.AI`, and the worker relate to the core application layers.
| Angular component | `frontend/trading-ui/src/app/features/market-data/market-data.component.ts` |
| API integration test | `tests/TradingApp.Api.Tests/Controllers/MarketDataControllerTests.cs` |