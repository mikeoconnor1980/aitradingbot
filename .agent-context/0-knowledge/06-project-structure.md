# Project Structure

Solution file: `TradingApp.sln`

| Project | Role |
|---------|------|
| `TradingApp.Domain` | Core domain entities; entities use a `static Create` factory with validation guards and private setters |
| `TradingApp.Application` | CQRS commands/queries/handlers, DTOs, interfaces, config options |
| `TradingApp.Infrastructure` | External service implementations (Hyperliquid client, signing) |
| `TradingApp.Persistence` | EF Core context (`TradingAppDbContext`), repository implementations, and `PersistenceServiceExtensions` for DI and auto-migration |
| `TradingApp.Api` | ASP.NET Core Web API host, controllers, DI composition root |
| `TradingApp.AI` | LLM integration services (OpenAI-compatible client, strategy interpreter) |
| `TradingApp.Worker` | .NET Worker Service host for background strategy execution |

Domain, Application, and Persistence are tenant-aware. All data access is scoped by `UserId`.

---

## Application Layer

```
src/TradingApp.Application/
├── Abstractions/
│   ├── Commands/          # Command, Command<T>, CreateCommand base records + handler bases
│   ├── Queries/           # Query<T> base record + QueryHandler base class
│   ├── Configuration/     # Typed options (e.g., HyperliquidOptions)
│   ├── Exceptions/        # DomainException (→400), NotFoundException (→404), DuplicateStrategyNameException (→409); mapped by HttpGlobalExceptionFilter
│   ├── Identity/          # AppIdentity (UserId, Email; static System identity)
│   ├── Repositories/      # Repository interfaces (ICandleRepository, IFundingRateRepository, IBacktestRunRepository, IStrategyRepository)
│   └── Services/          # Pipeline interfaces (IStrategyEngine, IGridController, IRiskEngine, IPositionManager,
│                          #   IMarketContextBuilder, IExecutionEngine, IBacktestRunner, IStrategyInterpreter) + infrastructure client contracts
├── Backtesting/           # Backtest CQRS handlers + engine
│   ├── Models/            # Engine models: BacktestConfig, BacktestResult, BacktestTrade, FeeModel, SimulatedFill/Order/Position, ReplayData
│   │                      # Response DTOs: BacktestRunResponse, BacktestRunSummary, BacktestTradeResponse,
│   │                      #   CandleCoverageResponse, IntervalCoverage
│   ├── Services/          # BacktestRunner, CandleReplayEngine, SimulatedExecutionEngine, BacktestMetricsCalculator
│   ├── RunBacktestCommand.cs        # CQRS command + handler
│   ├── GetBacktestResultQuery.cs    # CQRS query + handler
│   ├── GetBacktestListQuery.cs      # CQRS query + handler
│   ├── GetCandleCoverageQuery.cs    # CQRS query + handler
│   └── BacktestRunResponseMapper.cs # Internal: entity ↔ response DTO + JSON helpers
├── Scheduling/            # Shared between live and backtest
│   ├── CandleClock.cs              # Emits CandleClosedEvent once per closed candle
│   ├── StrategyScheduler.cs        # Drives strategy pipeline on trigger timeframe
│   └── Models/CandleClosedEvent.cs
├── Trading/               # Trading pipeline models (no handlers — consumed by pipeline services)
│   └── Models/            # MarketContext, StrategyEvaluation, IndicatorSnapshot,
│                          #   GridState, GridLifecycle, PositionState, TradingSignal,
│                          #   OrderRequest, OrderSide, OrderType, TradeType
├── StrategyAuthoring/     # Strategy CRUD and schema — models, CQRS, serialization, validation
│   ├── Commands/          # CreateStrategyCommand, UpdateStrategyCommand, DeleteStrategyCommand (+ handlers)
│   ├── Queries/           # GetStrategiesQuery (→ List<StrategySummaryDto>), GetStrategyByIdQuery (→ StrategyDto)
│   ├── Models/            # StrategyConfig (implements IStrategyConfig), GridConfig, ExitConfig, RiskConfig,
│   │                      #   TrendFilterConfig, EntryConditionConfig, typed params (RsiParams, PriceVsEmaParams,
│   │                      #   MacdParams), enums (StrategyMode, EntryConditionType, Direction, EntryLogic, etc.)
│   │                      #   DTOs: StrategyDto (full config + metadata), StrategySummaryDto (list view)
│   ├── Serialization/     # StrategyJsonOptions (shared JsonSerializerOptions),
│   │                      #   EntryConditionConfigConverter, EntryConditionParamsConverter (polymorphic)
│   └── Validation/        # IStrategyValidator, CompositeStrategyValidator (chains 3 levels),
│                          #   SchemaValidator, BusinessRuleValidator, CrossFieldValidator,
│                          #   ValidationResult, ValidationError, ValidationSeverity
└── {Feature}/             # CQRS feature folder, e.g. Health/, MarketData/
    ├── Models/            # DTOs returned by queries
    └── Queries/           # Query record + Handler in same file
```

MediatR is registered in the Api host to scan the Application assembly.

---

## AI Layer

```
src/TradingApp.AI/
├── Models/                 # LLM request/response shapes (ChatMessage, ChatCompletionRequest/Response)
├── Prompts/
│   └── StrategyInterpreterPrompt.cs  # System prompt template for NL→StrategyConfig interpretation
├── Services/
│   ├── OpenAiCompatibleLlmClient.cs  # ILlmClient implementation; works with Gemini, Ollama, or any OpenAI-compatible endpoint
│   └── StrategyInterpreter.cs        # IStrategyInterpreter implementation; calls LLM, parses response, returns StrategyIntentDto
└── AiServiceExtensions.cs            # DI registration (AddAI); binds LlmOptions, registers typed HttpClient and services
```

Registered via `AiServiceExtensions.AddAI()` in the Api host DI composition root.

---

## Api Layer

```
src/TradingApp.Api/
├── Controllers/           # Feature controllers (inherit ApiController for MediatR features; ControllerBase for direct-service features)
│                          # StrategiesController: full CRUD (GET, POST, PUT, DELETE /api/strategies) via MediatR + POST /api/strategies/validate (direct IStrategyValidator)
│                          # ReferenceDataController: GET /api/reference-data/markets — returns supported markets and timeframes from HyperliquidAssetMapper
├── Hubs/
│   └── MarketDataHub.cs          # SignalR hub for real-time market data relay; thin hub — all pushes come from IHubContext<MarketDataHub>
├── Infrastructure/
│   ├── ApiController.cs          # Base: protected Mediator + IdentityService
│   ├── Envelope.cs               # Error response { ErrorMessage, Timestamp }
│   ├── CreatedResultEnvelope.cs  # 201 response { Id (Guid) }
│   ├── IdentityService.cs        # Dev stub returning hardcoded AppIdentity
│   └── Filters/
│       └── HttpGlobalExceptionFilter.cs  # Global IExceptionFilter: DomainException→400, NotFoundException→404, OperationCanceledException→408, HttpRequestException→503, unhandled→500
├── Models/                # DTOs for responses served directly by the Api layer (no Application-layer handler)
├── Services/              # Api-layer services; includes MarketDataStreamService (BackgroundService — WebSocket aggregation + SignalR broadcast),
│                          #   UnavailableBacktestRunner (IBacktestRunner placeholder — throws until full pipeline is composed in API host)
└── Program.cs             # DI composition root and startup configuration
```

---

## Infrastructure Layer

```
src/TradingApp.Infrastructure/
├── Hyperliquid/
│   ├── HyperliquidAssetMapper.cs  # Maps display names (BTC-PERP → BTC) and timeframes to interval ms; validates against supported assets/timeframes
│   └── Models/                    # Hyperliquid API request/response shapes (HyperliquidMeta, HyperliquidAssetCtx, HyperliquidCandle, etc.)
├── Binance/
│   ├── BinanceAssetMapper.cs      # Maps display symbols (BTC → BTCUSDT) and intervals to ms; handles mark-price interval prefix (mark-15m)
│   └── Models/                    # Binance API response shapes (BinanceKline, BinanceFundingRate)
└── Services/
    ├── HyperliquidSigner.cs            # Derives wallet address from private key (Nethereum)
    ├── HyperliquidRestClient.cs        # Typed HttpClient targeting Hyperliquid REST API
    ├── HyperliquidWebSocketClient.cs   # Persistent WebSocket client; implements IHyperliquidWebSocketClient (singleton)
    ├── BinanceFuturesRestClient.cs     # Typed HttpClient targeting Binance USDⓈ-M Futures REST API (/fapi/v1)
    ├── BinanceCandleIngestionService.cs  # Paginates kline + mark-price kline history; writes to ICandleRepository
    ├── CandleIngestionService.cs       # Paginates Hyperliquid candleSnapshot history; writes to ICandleRepository
    └── FundingRateIngestionService.cs  # Paginates Binance funding rate history; writes to IFundingRateRepository
```

---

## Persistence Layer

```
src/TradingApp.Persistence/
├── Repositories/                   # EF Core repository implementations (interfaces in Application.Abstractions.Repositories)
├── TradingAppDbContext.cs          # EF Core DbContext; one DbSet per persisted entity; configures column mappings and indexes
├── PersistenceServiceExtensions.cs # AddPersistence() registers DbContext + all repositories; MigrateDatabaseAsync() runs EF migrations on startup
├── DesignTimeDbContextFactory.cs   # Design-time factory for EF migration tooling
└── Migrations/                     # EF Core auto-generated migration files
```

**Key conventions:**
- Repository interfaces live in `src/TradingApp.Application/Abstractions/Repositories/` (Application layer owns the contract)
- Repository implementations live in `src/TradingApp.Persistence/Repositories/`
- Call `AddPersistence()` from both the API and Worker host `Program.cs`
- Call `MigrateDatabaseAsync()` on startup in both hosts to auto-apply EF migrations
- Connection string key: `ConnectionStrings:DefaultConnection` in `appsettings.json`
- SQLite path convention: `Data Source=../../data/tradingapp.db` — shared database between API and Worker

---

## Test Projects

```
tests/
├── TradingApp.Api.Tests/
│   ├── Controllers/               # Controller integration tests
│   └── Infrastructure/
│       ├── BaseControllerTests.cs      # WebApplicationFactory<Program> base + HttpResponseExtensions
│       └── FakeHttpMessageHandler.cs   # Configurable HttpMessageHandler stub
├── TradingApp.Application.Tests/  # Handler unit tests
├── TradingApp.Domain.Tests/       # Domain entity unit tests
├── TradingApp.Infrastructure.Tests/
│   └── Services/                  # Infrastructure unit tests
└── TradingApp.Persistence.Tests/
    └── Repositories/              # Persistence integration tests using in-memory SQLite
```

`BaseControllerTests` creates a `WebApplicationFactory<Program>` with web host configuration via `ConfigureWebHost()` and service replacement via `ConfigureTestServices()`. It defines `HttpResponseExtensions` (`ReadAndAssertSuccessAsync<T>`, `AssertStatusCode`).

`TradingApp.Persistence.Tests` uses an in-memory SQLite connection (`SqliteConnection("Data Source=:memory:")`) kept open for the test lifetime. Each test creates its own `TradingAppDbContext` from shared `DbContextOptions`. The connection is disposed in `[TestCleanup]`. See `tests/TradingApp.Persistence.Tests/Repositories/CandleRepositoryTests.cs` for the reference pattern.

`FakeHttpMessageHandler` accepts a preset `HttpResponseMessage` or `Exception` and returns/throws it for every request.

---

## Strategy Plugins

Strategies are implemented as plugins in `src/TradingApp.Application/Strategies/`.
Future strategies are added here without modifying the worker.

---

## Frontend

```
frontend/trading-ui/           # Angular 19 standalone application
└── src/app/
    ├── core/
    │   ├── models/             # TypeScript interfaces matching API response shapes
    └── services/           # Root-scoped injectable services
        #                   api-rest-client.service.ts — generic HTTP wrapper (get/post/put/delete) over Angular HttpClient
        #                   {feature}.service.ts — domain-specific service using ApiRestClient (e.g., market-data.service.ts, health.service.ts)
        #                   hyperliquid-api.service.ts — legacy direct-call service (Account/positions/orders; pre-ApiRestClient pattern)
        #                   signalr.service.ts — SignalR hub connection; exposes priceUpdate$ and connectionStatus$; merges SignalR + backend connection states
    ├── features/               # Feature components grouped by domain area
    │   ├── dashboard/          # Main dashboard; contains sub-component folders (account-summary/, positions-table/, orders-table/)
    │   ├── connection/         # Exchange connectivity / health check view
    └── market-data/        # Market info (10s polling), candle table, live price ticker (SignalR), and 15-min rolling chart
    │   ├── price-ticker/   # PriceTickerComponent — live BTC-PERP price fed from SignalRService.priceUpdate$
    │   └── price-chart/    # PriceChartComponent — Lightweight Charts line series; rolling 15-min window; seeded from REST candles
    ├── app.routes.ts           # Lazy-loaded routes (loadComponent)
    └── app.config.ts           # Root providers (provideHttpClient, etc.)
```

---

## Canonical Feature Example: Health

The Health feature is the reference implementation for adding new features end-to-end:

| Layer | File |
|-------|------|
| DTO | `src/TradingApp.Application/Health/Models/HealthDto.cs` |
| Query + Handler | `src/TradingApp.Application/Health/Queries/GetHealthQuery.cs` |
| Controller | `src/TradingApp.Api/Controllers/HealthController.cs` |
| Infrastructure | `src/TradingApp.Infrastructure/Services/HyperliquidRestClient.cs` |
| Angular model | `frontend/trading-ui/src/app/core/models/health-response.model.ts` |
| Angular service | `frontend/trading-ui/src/app/core/services/health.service.ts` |
| Angular component | `frontend/trading-ui/src/app/features/connection/status-card.component.ts` |
| API integration test | `tests/TradingApp.Api.Tests/Controllers/HealthControllerTests.cs` |
| Infrastructure unit test | `tests/TradingApp.Infrastructure.Tests/Services/HyperliquidSignerTests.cs` |

## Canonical Feature Example: Account Dashboard

The Account Dashboard is the reference implementation for read-only exchange data features that bypass MediatR (see ADR 14):

| Layer | File |
|-------|------|
| API-layer DTO | `src/TradingApp.Api/Models/AccountSummaryDto.cs` |
| API-layer service interface | `src/TradingApp.Api/Services/IHyperliquidAccountService.cs` |
| API-layer service implementation | `src/TradingApp.Api/Services/HyperliquidAccountService.cs` |
| Controller | `src/TradingApp.Api/Controllers/AccountController.cs` |
| Angular models | `frontend/trading-ui/src/app/core/models/account-summary.model.ts` etc. |
| Angular API service | `frontend/trading-ui/src/app/core/services/hyperliquid-api.service.ts` |
| Angular feature | `frontend/trading-ui/src/app/features/dashboard/` |
| API integration test | `tests/TradingApp.Api.Tests/Controllers/AccountControllerTests.cs` |

## Canonical Feature Example: Market Data

The Market Data feature is the reference for Application-layer CQRS features that add typed methods to `IHyperliquidRestClient` (see [Hyperliquid Integration](02-hyperliquid-integration.md) — Extending, rule 2):

| Layer | File |
|-------|------|
| Application DTOs | `src/TradingApp.Application/MarketData/Models/MarketInfoDto.cs`, `CandleDto.cs` |
| Query + Handler | `src/TradingApp.Application/MarketData/Queries/GetMarketInfoQuery.cs` |
| Query + Handler | `src/TradingApp.Application/MarketData/Queries/GetCandlesQuery.cs` |
| Rest client interface | `src/TradingApp.Application/Abstractions/Services/IHyperliquidRestClient.cs` |
| Rest client impl + asset mapper | `src/TradingApp.Infrastructure/Services/HyperliquidRestClient.cs` |
| Asset/timeframe mapping | `src/TradingApp.Infrastructure/Hyperliquid/HyperliquidAssetMapper.cs` |
| Controller | `src/TradingApp.Api/Controllers/MarketDataController.cs` |
| Angular models | `frontend/trading-ui/src/app/core/models/market-info.model.ts`, `candle.model.ts` |
| Angular service | `frontend/trading-ui/src/app/core/services/market-data.service.ts` |
| Angular component | `frontend/trading-ui/src/app/features/market-data/market-data.component.ts` |
| API integration test | `tests/TradingApp.Api.Tests/Controllers/MarketDataControllerTests.cs` |