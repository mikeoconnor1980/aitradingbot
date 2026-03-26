# Project Structure

Solution file: `TradingApp.sln`

| Project | Role |
|---------|------|
| `TradingApp.Domain` | Core domain entities, value objects (scaffolded — no entities yet) |
| `TradingApp.Application` | CQRS commands/queries/handlers, DTOs, interfaces, config options |
| `TradingApp.Infrastructure` | External service implementations (Hyperliquid client, signing) |
| `TradingApp.Persistence` | EF Core context and repositories (scaffolded — no DbContext yet) |
| `TradingApp.Api` | ASP.NET Core Web API host, controllers, DI composition root |
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
│   ├── Exceptions/        # DomainException (→400), NotFoundException (→404); mapped by HttpGlobalExceptionFilter
│   ├── Identity/          # AppIdentity (UserId, Email; static System identity)
│   └── Services/          # Application service interfaces
└── {Feature}/             # Feature folder, e.g. Health/, MarketData/
    ├── Models/            # DTOs returned by queries
    └── Queries/           # Query record + Handler in same file
```

MediatR is registered in the Api host to scan the Application assembly.

---

## Api Layer

```
src/TradingApp.Api/
├── Controllers/           # Feature controllers (inherit ApiController for MediatR features; ControllerBase for direct-service features)
├── Hubs/
│   └── MarketDataHub.cs          # SignalR hub for real-time market data relay; thin hub — all pushes come from IHubContext<MarketDataHub>
├── Infrastructure/
│   ├── ApiController.cs          # Base: protected Mediator + IdentityService
│   ├── Envelope.cs               # Error response { ErrorMessage, Timestamp }
│   ├── CreatedResultEnvelope.cs  # 201 response { Id (Guid) }
│   ├── IdentityService.cs        # Dev stub returning hardcoded AppIdentity
│   └── Filters/
│       └── HttpGlobalExceptionFilter.cs  # Global IExceptionFilter: DomainException→400, NotFoundException→404, HttpRequestException→503, unhandled→500
├── Models/                # DTOs for responses served directly by the Api layer (no Application-layer handler)
├── Services/              # Api-layer services; includes MarketDataStreamService and UserEventStreamService (both BackgroundService — WebSocket → SignalR relay with exponential-backoff reconnect)
└── Program.cs             # DI composition root and startup configuration
```

---

## Infrastructure Layer

```
src/TradingApp.Infrastructure/
├── Hyperliquid/
│   ├── HyperliquidAssetMapper.cs  # Maps display names (BTC-PERP → BTC) and timeframes to interval ms; validates against supported assets/timeframes
│   └── Models/                    # Hyperliquid API request/response shapes (HyperliquidMeta, HyperliquidAssetCtx, HyperliquidCandle, etc.)
└── Services/
    ├── HyperliquidSigner.cs           # Derives wallet address from private key (Nethereum)
    ├── HyperliquidRestClient.cs       # Typed HttpClient targeting Hyperliquid REST API
    ├── HyperliquidWebSocketClient.cs  # Persistent WebSocket client; implements IHyperliquidWebSocketClient (singleton); shared market data stream
    └── HyperliquidUserEventClient.cs  # Per-wallet WebSocket client; implements IHyperliquidUserEventClient (singleton); user fills and order updates
```

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
└── TradingApp.Infrastructure.Tests/
    └── Services/                  # Infrastructure unit tests
```

`BaseControllerTests` creates a `WebApplicationFactory<Program>` with web host configuration via `ConfigureWebHost()` and service replacement via `ConfigureTestServices()`. It defines `HttpResponseExtensions` (`ReadAndAssertSuccessAsync<T>`, `AssertStatusCode`).

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
        #                   signalr.service.ts — SignalR hub connection; exposes priceUpdate$ and connectionStatus$; merges SignalR + market data stream + user event stream statuses; routes fill/order events to AccountStateService
        #                   account-state.service.ts — shared reactive state layer; BehaviorSubject for positions$, orders$, events$; 100-event cap for activity feed
    ├── features/               # Feature components grouped by domain area
    │   ├── dashboard/          # Main dashboard; contains sub-component folders (account-summary/, positions-table/, orders-table/, activity-feed/)
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