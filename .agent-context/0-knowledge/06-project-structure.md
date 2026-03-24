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
│   ├── Identity/          # AppIdentity (UserId, Email; static System identity)
│   └── Services/          # Application service interfaces
└── {Feature}/             # Feature folder, e.g. Health/
    ├── Models/            # DTOs returned by queries
    └── Queries/           # Query record + Handler in same file
```

MediatR is registered in the Api host to scan the Application assembly.

---

## Api Layer

```
src/TradingApp.Api/
├── Controllers/           # Feature controllers, inherit ApiController
├── Infrastructure/
│   ├── ApiController.cs          # Base: protected Mediator + IdentityService
│   ├── Envelope.cs               # Error response { ErrorMessage, Timestamp }
│   ├── CreatedResultEnvelope.cs  # 201 response { Id (Guid) }
│   └── IdentityService.cs        # Dev stub returning hardcoded AppIdentity
└── Program.cs             # DI composition root and startup configuration
```

---

## Infrastructure Layer

```
src/TradingApp.Infrastructure/
└── Services/
    ├── HyperliquidSigner.cs       # Derives wallet address from private key (Nethereum)
    └── HyperliquidRestClient.cs   # Typed HttpClient targeting Hyperliquid REST API
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

`BaseControllerTests` creates a `WebApplicationFactory<Program>` with service replacement via `ConfigureTestServices()`. It defines `HttpResponseExtensions` (`ReadAndAssertSuccessAsync<T>`, `AssertStatusCodeAsync`).

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
    │   └── services/           # Root-scoped injectable services (polling, HTTP)
    ├── features/               # Feature components grouped by domain area
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