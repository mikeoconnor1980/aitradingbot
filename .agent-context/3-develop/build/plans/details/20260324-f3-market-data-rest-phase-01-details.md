<!-- markdownlint-disable-file -->

# Task Details: F3 — Market Data (REST)

## Phase 1: Backend — Application Layer, MediatR Infrastructure, Market Data API

## Standards and Knowledge References

- **api-controllers.instructions.md**: ApiController base class with IMediator + IdentityService; `[ApiController]`, `[Route("api/...")]`, `[Produces("application/json")]`, `[ProducesResponseType]` on every action; Envelope for error responses; HttpGlobalExceptionFilter maps exceptions to HTTP status codes
- **dotnet-architecture.instructions.md**: Bounded context folder structure (Queries/, Models/, MappingProfiles/); CQRS queries with `Query<T>` base; QueryHandler in same file as Query; DTOs in Models/; AutoMapper profiles; Infrastructure services with interface in Application/Abstractions/Services/
- **csharp.instructions.md**: `sealed` classes; `_camelCase` private fields; `Async` suffix; `CancellationToken` everywhere; `IOptions<T>` for config; handlers in same file as query/command
- **testing.instructions.md**: MSTest only; Moq; FluentAssertions ≤v6; Given_When_Then naming; BaseControllerTests<Startup>; command/query handlers tested only via controller tests
- **02-hyperliquid-integration.md**: Hyperliquid REST API is POST /info with JSON body; market data is public (no auth); `{"type": "metaAndAssetCtxs"}` returns all asset contexts; `{"type": "candleSnapshot", "req": {...}}` returns candle data

## Design References

- **Hyperliquid Info API**: All read operations use `POST https://api.hyperliquid-testnet.xyz/info` with a `type` field in the JSON body. Market metadata uses `{"type": "metaAndAssetCtxs"}` which returns the full asset universe (meta info + per-asset contexts). Candle data uses `{"type": "candleSnapshot", "req": {"coin": "BTC", "interval": "15m", "startTime": <unix_ms>, "endTime": <unix_ms>}}`.
- **Asset naming**: Hyperliquid uses short names like "BTC", "ETH", "SOL" (not "BTC-PERP"). The UI display name "BTC-PERP" maps to coin "BTC" when calling the exchange API.

---

### Task 1.1: Create Application project and add MediatR infrastructure {#task-11-create-application-project-and-add-mediatr-infrastructure}

Create the TradingApp.Application project with MediatR base types (Query, QueryHandler, Command, CommandHandler) and wire it into the solution.

- **Complexity**: Medium
- **Risk Factors**: First introduction of MediatR — must set up base types correctly for all subsequent features
- **Files**:
  - `src/TradingApp.Application/TradingApp.Application.csproj` — New project with MediatR + AutoMapper dependencies
  - `src/TradingApp.Application/Abstractions/Queries/Query.cs` — Base query record
  - `src/TradingApp.Application/Abstractions/Queries/QueryHandler.cs` — Base query handler
  - `src/TradingApp.Application/Abstractions/Commands/Command.cs` — Base command records
  - `src/TradingApp.Application/Abstractions/Commands/CommandHandler.cs` — Base command handler
  - `src/TradingApp.Application/Abstractions/Services/IHyperliquidRestClient.cs` — Move interface here from Infrastructure
  - `TradingApp.sln` — Add Application project reference
  - `src/TradingApp.Api/TradingApp.Api.csproj` — Add reference to Application project
  - `src/TradingApp.Infrastructure/TradingApp.Infrastructure.csproj` — Add reference to Application project
- **Success**:
  - Application project builds successfully
  - Solution builds with all project references intact
  - MediatR base types compile and are usable
- **Dependencies**:
  - F1 must have created the solution and Api/Infrastructure projects

#### Implementation Details

```xml
<!-- src/TradingApp.Application/TradingApp.Application.csproj — new file -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="AutoMapper" Version="13.*" />
    <PackageReference Include="MediatR" Version="12.*" />
  </ItemGroup>
</Project>
```

```csharp
// src/TradingApp.Application/Abstractions/Queries/Query.cs — new file
using MediatR;

namespace TradingApp.Application.Abstractions.Queries;

public abstract record Query<TResult> : IRequest<TResult>;
```

```csharp
// src/TradingApp.Application/Abstractions/Queries/QueryHandler.cs — new file
using MediatR;

namespace TradingApp.Application.Abstractions.Queries;

public abstract class QueryHandler<TQuery, TResult> : IRequestHandler<TQuery, TResult>
    where TQuery : Query<TResult>
{
    public abstract Task<TResult> Handle(TQuery request, CancellationToken cancellationToken);
}
```

```csharp
// src/TradingApp.Application/Abstractions/Commands/Command.cs — new file
using MediatR;

namespace TradingApp.Application.Abstractions.Commands;

public abstract record Command : IRequest<Unit>;
public abstract record Command<T> : IRequest<T>;
public abstract record CreateCommand : IRequest<Guid>;
```

```csharp
// src/TradingApp.Application/Abstractions/Commands/CommandHandler.cs — new file
using MediatR;

namespace TradingApp.Application.Abstractions.Commands;

public abstract class CommandHandler<TCommand> : IRequestHandler<TCommand, Unit>
    where TCommand : Command
{
    public abstract Task<Unit> Handle(TCommand request, CancellationToken cancellationToken);
}

public abstract class CommandHandler<TCommand, TResult> : IRequestHandler<TCommand, TResult>
    where TCommand : Command<TResult>
{
    public abstract Task<TResult> Handle(TCommand request, CancellationToken cancellationToken);
}

public abstract class CreateCommandHandler<TCommand> : IRequestHandler<TCommand, Guid>
    where TCommand : CreateCommand
{
    public abstract Task<Guid> Handle(TCommand request, CancellationToken cancellationToken);
}
```

```csharp
// src/TradingApp.Application/Abstractions/Services/IHyperliquidRestClient.cs — new file
// Interface for the Hyperliquid REST client. Implementation stays in Infrastructure.
// Market data methods (GetMarketInfoAsync, GetCandlesAsync) are added in Task 1.3 after DTOs are created.
namespace TradingApp.Application.Abstractions.Services;

public interface IHyperliquidRestClient
{
    Task<bool> CheckConnectivityAsync(CancellationToken cancellationToken);
}
```

> **Note**: The `CheckConnectivityAsync` method is from F1 — it should already exist if F1 used an interface, or it needs to be reconciled. Market data methods (`GetMarketInfoAsync`, `GetCandlesAsync`) are added to this interface in Task 1.3 after the DTOs are defined, to avoid forward references that prevent compilation.

##### Pattern References

- `dotnet-architecture.instructions.md` — Application/Abstractions/Services/ pattern for infrastructure service interfaces
- `dotnet-architecture.instructions.md` — CQRS Command/Query base types
- `api-controllers.instructions.md` — MediatR dispatch pattern from controllers

---

### Task 1.2: Create ApiController base class, Envelope, and HttpGlobalExceptionFilter {#task-12-create-apicontroller-base-class-envelope-and-httpglobalexceptionfilter}

Create the shared API infrastructure types that all controllers, including the existing HealthController from F1, will use.

- **Complexity**: Medium
- **Risk Factors**: Must integrate with existing F1 HealthController without breaking it; exception filter must handle all expected exception types
- **Files**:
  - `src/TradingApp.Api/Infrastructure/ApiController.cs` — Base controller with IMediator and IdentityService
  - `src/TradingApp.Api/Infrastructure/Envelope.cs` — Standard error response envelope
  - `src/TradingApp.Api/Infrastructure/CreatedResultEnvelope.cs` — Created response envelope
  - `src/TradingApp.Api/Infrastructure/Filters/HttpGlobalExceptionFilter.cs` — Global exception to HTTP status mapping
  - `src/TradingApp.Api/Infrastructure/Services/IdentityService.cs` — Identity service (stub for POC - no auth)
  - `src/TradingApp.Application/Abstractions/Exceptions/NotFoundException.cs` — Not found exception
  - `src/TradingApp.Application/Abstractions/Exceptions/DomainException.cs` — Domain validation exception
- **Success**:
  - ApiController base class compiles and provides Mediator/IdentityService
  - HttpGlobalExceptionFilter correctly maps exceptions to HTTP status codes
  - Envelope serializes to expected JSON shape
  - Existing HealthController (F1) continues to work (may need migration to new base class or can remain independent)
- **Dependencies**:
  - Task 1.1 (Application project exists with MediatR)

#### Implementation Details

```csharp
// src/TradingApp.Api/Infrastructure/ApiController.cs — new file
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TradingApp.Api.Infrastructure.Services;

namespace TradingApp.Api.Infrastructure;

[ApiController]
[Produces("application/json")]
public abstract class ApiController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IdentityService _identityService;

    protected ApiController(IMediator mediator, IdentityService identityService)
    {
        _mediator = mediator;
        _identityService = identityService;
    }

    protected IMediator Mediator => _mediator;
    protected IdentityService IdentityService => _identityService;
}
```

```csharp
// src/TradingApp.Api/Infrastructure/Envelope.cs — new file
namespace TradingApp.Api.Infrastructure;

public sealed class Envelope
{
    public string ErrorMessage { get; }
    public string? Detail { get; }

    public Envelope(string errorMessage, string? detail = null)
    {
        ErrorMessage = errorMessage;
        Detail = detail;
    }
}
```

```csharp
// src/TradingApp.Api/Infrastructure/CreatedResultEnvelope.cs — new file
namespace TradingApp.Api.Infrastructure;

public sealed class CreatedResultEnvelope
{
    public Guid Id { get; }

    public CreatedResultEnvelope(Guid id)
    {
        Id = id;
    }
}
```

```csharp
// src/TradingApp.Api/Infrastructure/Filters/HttpGlobalExceptionFilter.cs — new file
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TradingApp.Application.Abstractions.Exceptions;

namespace TradingApp.Api.Infrastructure.Filters;

public sealed class HttpGlobalExceptionFilter : IExceptionFilter
{
    private readonly ILogger<HttpGlobalExceptionFilter> _logger;

    public HttpGlobalExceptionFilter(ILogger<HttpGlobalExceptionFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        _logger.LogError(context.Exception, "Unhandled exception occurred");

        var (statusCode, envelope) = context.Exception switch
        {
            DomainException ex => (StatusCodes.Status400BadRequest, new Envelope(ex.Message)),
            NotFoundException ex => (StatusCodes.Status404NotFound, new Envelope(ex.Message)),
            UnauthorizedAccessException ex => (StatusCodes.Status403Forbidden, new Envelope(ex.Message)),
            HttpRequestException ex => (StatusCodes.Status503ServiceUnavailable, new Envelope("External service unavailable", ex.Message)),
            _ => (StatusCodes.Status500InternalServerError, new Envelope("An unexpected error occurred", context.Exception.Message))
        };

        context.Result = new ObjectResult(envelope) { StatusCode = statusCode };
        context.ExceptionHandled = true;
    }
}
```

```csharp
// src/TradingApp.Api/Infrastructure/Services/IdentityService.cs — new file
// Stub for POC — no authentication. Returns a default identity.
namespace TradingApp.Api.Infrastructure.Services;

public sealed class IdentityService
{
    // POC: No auth. Placeholder for when JWT identity is added.
}
```

```csharp
// src/TradingApp.Application/Abstractions/Exceptions/NotFoundException.cs — new file
namespace TradingApp.Application.Abstractions.Exceptions;

public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
    public NotFoundException(string name, object key) : base($"{name} with key '{key}' was not found.") { }
}
```

```csharp
// src/TradingApp.Application/Abstractions/Exceptions/DomainException.cs — new file
namespace TradingApp.Application.Abstractions.Exceptions;

public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
```

##### Pattern References

- `api-controllers.instructions.md` — ApiController with IMediator + IdentityService, Envelope error type, HttpGlobalExceptionFilter exception mapping
- `csharp.instructions.md` — Sealed classes, constructor injection

---

### Task 1.3: Create MarketData DTOs and Hyperliquid response models {#task-13-create-marketdata-dtos-and-hyperliquid-response-models}

Create the DTO models returned by the API and the raw Hyperliquid JSON response models used for deserialization.

- **Complexity**: Medium
- **Risk Factors**: Hyperliquid response JSON structure must be verified against exchange docs; property naming must match for System.Text.Json deserialization
- **Files**:
  - `src/TradingApp.Application/MarketData/Models/MarketInfoDto.cs` — API response DTO for market info
  - `src/TradingApp.Application/MarketData/Models/CandleDto.cs` — API response DTO for candles
  - `src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidMetaAndAssetCtxsResponse.cs` — Raw exchange response model
  - `src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidCandleSnapshotResponse.cs` — Raw candle response model
  - `src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidInfoRequest.cs` — Request body models for POST /info
- **Success**:
  - DTOs contain all fields from the PBI (mid price, mark price, index price, funding rate, 24h volume, open interest, 24h change %)
  - Hyperliquid response models can deserialize exchange JSON
  - CandleDto has timestamp, open, high, low, close, volume
- **Dependencies**:
  - Task 1.1 (Application project exists)

#### Implementation Details

```csharp
// src/TradingApp.Application/MarketData/Models/MarketInfoDto.cs — new file
namespace TradingApp.Application.MarketData.Models;

public sealed class MarketInfoDto
{
    public string Asset { get; init; } = string.Empty;
    public decimal MidPrice { get; init; }
    public decimal MarkPrice { get; init; }
    public decimal IndexPrice { get; init; }
    public decimal FundingRate { get; init; }
    public decimal Volume24h { get; init; }
    public decimal OpenInterest { get; init; }
    public decimal PriceChange24hPercent { get; init; }
}
```

```csharp
// src/TradingApp.Application/MarketData/Models/CandleDto.cs — new file
namespace TradingApp.Application.MarketData.Models;

public sealed class CandleDto
{
    public long Timestamp { get; init; }  // Unix milliseconds
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal Close { get; init; }
    public decimal Volume { get; init; }
}
```

```csharp
// src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidInfoRequest.cs — new file
using System.Text.Json.Serialization;

namespace TradingApp.Infrastructure.Hyperliquid.Models;

public sealed class HyperliquidInfoRequest
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;
}

public sealed class HyperliquidCandleSnapshotRequest
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "candleSnapshot";

    [JsonPropertyName("req")]
    public CandleSnapshotPayload Req { get; init; } = new();
}

public sealed class CandleSnapshotPayload
{
    [JsonPropertyName("coin")]
    public string Coin { get; init; } = string.Empty;

    [JsonPropertyName("interval")]
    public string Interval { get; init; } = string.Empty;

    [JsonPropertyName("startTime")]
    public long StartTime { get; init; }

    [JsonPropertyName("endTime")]
    public long EndTime { get; init; }
}
```

```csharp
// src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidMetaAndAssetCtxsResponse.cs — new file
// Models the response from POST /info {"type": "metaAndAssetCtxs"}
// Response is a JSON array: [meta, [assetCtx0, assetCtx1, ...]]
using System.Text.Json.Serialization;

namespace TradingApp.Infrastructure.Hyperliquid.Models;

public sealed class HyperliquidAssetMeta
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("szDecimals")]
    public int SzDecimals { get; set; }
}

public sealed class HyperliquidMeta
{
    [JsonPropertyName("universe")]
    public List<HyperliquidAssetMeta> Universe { get; set; } = new();
}

public sealed class HyperliquidAssetCtx
{
    [JsonPropertyName("funding")]
    public string Funding { get; set; } = "0";

    [JsonPropertyName("openInterest")]
    public string OpenInterest { get; set; } = "0";

    [JsonPropertyName("prevDayPx")]
    public string PrevDayPx { get; set; } = "0";

    [JsonPropertyName("dayNtlVlm")]
    public string DayNtlVlm { get; set; } = "0";

    [JsonPropertyName("markPx")]
    public string MarkPx { get; set; } = "0";

    [JsonPropertyName("midPx")]
    public string MidPx { get; set; } = "0";

    [JsonPropertyName("oraclePx")]
    public string OraclePx { get; set; } = "0";
}
```

```csharp
// src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidCandleSnapshotResponse.cs — new file
// Each candle is returned as: {"t": timestamp, "T": closeTime, "s": symbol, "i": interval, "o": open, "h": high, "l": low, "c": close, "v": volume, "n": numTrades}
using System.Text.Json.Serialization;

namespace TradingApp.Infrastructure.Hyperliquid.Models;

public sealed class HyperliquidCandle
{
    [JsonPropertyName("t")]
    public long OpenTime { get; set; }

    [JsonPropertyName("T")]
    public long CloseTime { get; set; }

    [JsonPropertyName("s")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("i")]
    public string Interval { get; set; } = string.Empty;

    [JsonPropertyName("o")]
    public string Open { get; set; } = "0";

    [JsonPropertyName("h")]
    public string High { get; set; } = "0";

    [JsonPropertyName("l")]
    public string Low { get; set; } = "0";

    [JsonPropertyName("c")]
    public string Close { get; set; } = "0";

    [JsonPropertyName("v")]
    public string Volume { get; set; } = "0";

    [JsonPropertyName("n")]
    public int NumTrades { get; set; }
}
```

> **Note**: Hyperliquid returns numeric values as strings in many fields. The HyperliquidRestClient (Task 1.4) must parse these to decimal when mapping to DTOs. Verify exact field names against https://hyperliquid.gitbook.io/hyperliquid-docs/for-developers/api/info-endpoint during implementation.

**Extend IHyperliquidRestClient interface**: Add the market data methods now that DTOs are defined:

```csharp
// src/TradingApp.Application/Abstractions/Services/IHyperliquidRestClient.cs — modification
// Add these methods to the interface created in Task 1.1:
using TradingApp.Application.MarketData.Models;

namespace TradingApp.Application.Abstractions.Services;

public interface IHyperliquidRestClient
{
    Task<bool> CheckConnectivityAsync(CancellationToken cancellationToken);
    Task<MarketInfoDto?> GetMarketInfoAsync(string asset, CancellationToken cancellationToken);
    Task<List<CandleDto>> GetCandlesAsync(string asset, string timeframe, CancellationToken cancellationToken);
}
```

##### Pattern References

- `dotnet-architecture.instructions.md` — DTOs in Application/{BoundedContext}/Models/
- `csharp.instructions.md` — Sealed classes, init-only properties

---

### Task 1.4: Extend HyperliquidRestClient with market info and candle methods {#task-14-extend-hyperliquidrestclient-with-market-info-and-candle-methods}

Add `GetMarketInfoAsync` and `GetCandlesAsync` methods to the existing HyperliquidRestClient. Update the class to implement the `IHyperliquidRestClient` interface from the Application layer.

- **Complexity**: High
- **Risk Factors**: Hyperliquid POST /info API with JSON body — must correctly construct requests and parse responses; asset name mapping (BTC-PERP → BTC); timeframe validation; candle time window calculation
- **Files**:
  - `src/TradingApp.Infrastructure/Hyperliquid/HyperliquidRestClient.cs` — modification: add market info and candle methods, implement IHyperliquidRestClient interface
  - `src/TradingApp.Infrastructure/Hyperliquid/HyperliquidAssetMapper.cs` — New: maps display names to exchange coin names
- **Success**:
  - `GetMarketInfoAsync("BTC-PERP")` calls POST /info with `{"type": "metaAndAssetCtxs"}`, finds BTC in the response, and returns a populated MarketInfoDto
  - `GetCandlesAsync("BTC-PERP", "15m")` calls POST /info with candleSnapshot type and returns up to 50 CandleDtos sorted newest first
  - Returns null MarketInfoDto if asset not found; throws NotFoundException for invalid assets
  - Invalid timeframe throws DomainException
- **Dependencies**:
  - Task 1.1 (IHyperliquidRestClient interface)
  - Task 1.3 (DTOs and Hyperliquid response models)

#### Implementation Details

```csharp
// src/TradingApp.Infrastructure/Hyperliquid/HyperliquidAssetMapper.cs — new file
namespace TradingApp.Infrastructure.Hyperliquid;

/// <summary>
/// Maps display asset names (e.g., "BTC-PERP") to Hyperliquid coin names (e.g., "BTC").
/// </summary>
public static class HyperliquidAssetMapper
{
    private static readonly Dictionary<string, string> _displayToCoin = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BTC-PERP"] = "BTC",
        ["ETH-PERP"] = "ETH",
        ["SOL-PERP"] = "SOL",
        ["DOGE-PERP"] = "DOGE",
        ["AVAX-PERP"] = "AVAX",
        ["ARB-PERP"] = "ARB",
        ["LINK-PERP"] = "LINK",
        ["OP-PERP"] = "OP",
    };

    private static readonly HashSet<string> _validTimeframes = new(StringComparer.OrdinalIgnoreCase)
    {
        "15m", "1h", "4h"
    };

    public static string ToCoin(string displayName)
    {
        return _displayToCoin.TryGetValue(displayName, out var coin)
            ? coin
            : throw new Application.Abstractions.Exceptions.NotFoundException("Asset", displayName);
    }

    public static bool IsValidTimeframe(string timeframe)
    {
        return _validTimeframes.Contains(timeframe);
    }

    public static IReadOnlyList<string> SupportedAssets => _displayToCoin.Keys.ToList();
}
```

```csharp
// src/TradingApp.Infrastructure/Hyperliquid/HyperliquidRestClient.cs — modification
// Add these methods to the existing class. The class should implement IHyperliquidRestClient.

using System.Net.Http.Json;
using System.Text.Json;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.MarketData.Models;
using TradingApp.Infrastructure.Hyperliquid.Models;

// ... existing class declaration, updated to:
public sealed class HyperliquidRestClient : IHyperliquidRestClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HyperliquidRestClient> _logger;

    public HyperliquidRestClient(HttpClient httpClient, ILogger<HyperliquidRestClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    // ... existing CheckConnectivityAsync from F1 ...

    public async Task<MarketInfoDto?> GetMarketInfoAsync(string asset, CancellationToken cancellationToken)
    {
        var coin = HyperliquidAssetMapper.ToCoin(asset);

        var request = new HyperliquidInfoRequest { Type = "metaAndAssetCtxs" };
        var response = await _httpClient.PostAsJsonAsync("/info", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        // Response is [meta, [assetCtx0, assetCtx1, ...]]
        using var doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);

        var root = doc.RootElement;
        var meta = JsonSerializer.Deserialize<HyperliquidMeta>(root[0].GetRawText());
        var assetCtxs = root[1];

        // Find the index of the requested coin in the universe
        var assetIndex = -1;
        for (var i = 0; i < meta!.Universe.Count; i++)
        {
            if (string.Equals(meta.Universe[i].Name, coin, StringComparison.OrdinalIgnoreCase))
            {
                assetIndex = i;
                break;
            }
        }

        if (assetIndex < 0 || assetIndex >= assetCtxs.GetArrayLength())
        {
            _logger.LogWarning("Asset {Asset} (coin: {Coin}) not found in Hyperliquid universe", asset, coin);
            return null;
        }

        var ctx = JsonSerializer.Deserialize<HyperliquidAssetCtx>(assetCtxs[assetIndex].GetRawText())!;

        var midPrice = decimal.Parse(ctx.MidPx, CultureInfo.InvariantCulture);
        var markPrice = decimal.Parse(ctx.MarkPx, CultureInfo.InvariantCulture);
        var prevDayPrice = decimal.Parse(ctx.PrevDayPx, CultureInfo.InvariantCulture);
        var priceChange = prevDayPrice != 0
            ? ((midPrice - prevDayPrice) / prevDayPrice) * 100
            : 0;

        return new MarketInfoDto
        {
            Asset = asset,
            MidPrice = midPrice,
            MarkPrice = markPrice,
            IndexPrice = decimal.Parse(ctx.OraclePx, CultureInfo.InvariantCulture),
            FundingRate = decimal.Parse(ctx.Funding, CultureInfo.InvariantCulture),
            Volume24h = decimal.Parse(ctx.DayNtlVlm, CultureInfo.InvariantCulture),
            OpenInterest = decimal.Parse(ctx.OpenInterest, CultureInfo.InvariantCulture),
            PriceChange24hPercent = Math.Round(priceChange, 2)
        };
    }

    public async Task<List<CandleDto>> GetCandlesAsync(string asset, string timeframe, CancellationToken cancellationToken)
    {
        var coin = HyperliquidAssetMapper.ToCoin(asset);

        if (!HyperliquidAssetMapper.IsValidTimeframe(timeframe))
        {
            throw new DomainException($"Invalid timeframe '{timeframe}'. Supported: 15m, 1h, 4h");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var intervalMs = timeframe.ToLowerInvariant() switch
        {
            "15m" => 15L * 60 * 1000,
            "1h" => 60L * 60 * 1000,
            "4h" => 4L * 60 * 60 * 1000,
            _ => throw new DomainException($"Unsupported timeframe: {timeframe}")
        };
        var startTime = now - (50 * intervalMs);

        var request = new HyperliquidCandleSnapshotRequest
        {
            Req = new CandleSnapshotPayload
            {
                Coin = coin,
                Interval = timeframe.ToLowerInvariant(),
                StartTime = startTime,
                EndTime = now
            }
        };

        var response = await _httpClient.PostAsJsonAsync("/info", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var candles = await response.Content.ReadFromJsonAsync<List<HyperliquidCandle>>(cancellationToken: cancellationToken);

        return (candles ?? new List<HyperliquidCandle>())
            .Select(c => new CandleDto
            {
                Timestamp = c.OpenTime,
                Open = decimal.Parse(c.Open, CultureInfo.InvariantCulture),
                High = decimal.Parse(c.High, CultureInfo.InvariantCulture),
                Low = decimal.Parse(c.Low, CultureInfo.InvariantCulture),
                Close = decimal.Parse(c.Close, CultureInfo.InvariantCulture),
                Volume = decimal.Parse(c.Volume, CultureInfo.InvariantCulture)
            })
            .OrderByDescending(c => c.Timestamp)
            .Take(50)
            .ToList();
    }
}
```

##### Pattern References

- `dotnet-architecture.instructions.md` — Infrastructure service implementing Application interface
- `02-hyperliquid-integration.md` — POST /info endpoint, public market data, no auth required
- `csharp.instructions.md` — Sealed class, CancellationToken, Async suffix, structured logging

---

### Task 1.5: Create MediatR queries and handlers for market data {#task-15-create-mediatr-queries-and-handlers-for-market-data}

Create the CQRS queries and handlers that sit between the controller and the HyperliquidRestClient.

- **Complexity**: Medium
- **Risk Factors**: Handler must correctly invoke IHyperliquidRestClient and handle null responses (asset not found)
- **Files**:
  - `src/TradingApp.Application/MarketData/Queries/GetMarketInfoQuery.cs` — Query record + handler
  - `src/TradingApp.Application/MarketData/Queries/GetCandlesQuery.cs` — Query record + handler
- **Success**:
  - GetMarketInfoQuery dispatches to handler which calls IHyperliquidRestClient.GetMarketInfoAsync
  - GetCandlesQuery dispatches to handler which calls IHyperliquidRestClient.GetCandlesAsync
  - Handler throws NotFoundException if market info returns null
- **Dependencies**:
  - Task 1.1 (Query base types)
  - Task 1.3 (DTOs)
  - Task 1.4 (IHyperliquidRestClient methods)

#### Implementation Details

```csharp
// src/TradingApp.Application/MarketData/Queries/GetMarketInfoQuery.cs — new file
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Queries;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.MarketData.Models;

namespace TradingApp.Application.MarketData.Queries;

public sealed record GetMarketInfoQuery(string Asset) : Query<MarketInfoDto>;

public sealed class GetMarketInfoQueryHandler : QueryHandler<GetMarketInfoQuery, MarketInfoDto>
{
    private readonly IHyperliquidRestClient _restClient;

    public GetMarketInfoQueryHandler(IHyperliquidRestClient restClient)
    {
        _restClient = restClient;
    }

    public override async Task<MarketInfoDto> Handle(GetMarketInfoQuery request, CancellationToken cancellationToken)
    {
        var result = await _restClient.GetMarketInfoAsync(request.Asset, cancellationToken);

        if (result is null)
        {
            throw new NotFoundException("Asset", request.Asset);
        }

        return result;
    }
}
```

```csharp
// src/TradingApp.Application/MarketData/Queries/GetCandlesQuery.cs — new file
using TradingApp.Application.Abstractions.Queries;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.MarketData.Models;

namespace TradingApp.Application.MarketData.Queries;

public sealed record GetCandlesQuery(string Asset, string Timeframe) : Query<List<CandleDto>>;

public sealed class GetCandlesQueryHandler : QueryHandler<GetCandlesQuery, List<CandleDto>>
{
    private readonly IHyperliquidRestClient _restClient;

    public GetCandlesQueryHandler(IHyperliquidRestClient restClient)
    {
        _restClient = restClient;
    }

    public override async Task<List<CandleDto>> Handle(GetCandlesQuery request, CancellationToken cancellationToken)
    {
        return await _restClient.GetCandlesAsync(request.Asset, request.Timeframe, cancellationToken);
    }
}
```

##### Pattern References

- `dotnet-architecture.instructions.md` — CQRS Query + QueryHandler in same file
- `csharp.instructions.md` — Sealed record for queries, sealed class for handlers, CancellationToken

---

### Task 1.6: Create MarketDataController with GET endpoints {#task-16-create-marketdatacontroller-with-get-endpoints}

Create the MarketDataController with two GET endpoints that dispatch MediatR queries.

- **Complexity**: Low
- **Risk Factors**: Must follow ApiController base pattern exactly; ProducesResponseType must match actual responses
- **Files**:
  - `src/TradingApp.Api/Controllers/MarketDataController.cs` — New controller with GET info and GET candles
- **Success**:
  - `GET /api/market/info?asset=BTC-PERP` dispatches GetMarketInfoQuery and returns MarketInfoDto
  - `GET /api/market/candles?asset=BTC-PERP&timeframe=15m` dispatches GetCandlesQuery and returns List<CandleDto>
  - Missing asset parameter returns 400 (model binding)
  - Invalid asset returns 404 via exception filter
  - Invalid timeframe returns 400 via exception filter
- **Dependencies**:
  - Task 1.2 (ApiController base, Envelope)
  - Task 1.5 (MediatR queries)

#### Implementation Details

```csharp
// src/TradingApp.Api/Controllers/MarketDataController.cs — new file
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TradingApp.Api.Infrastructure;
using TradingApp.Api.Infrastructure.Services;
using TradingApp.Application.MarketData.Models;
using TradingApp.Application.MarketData.Queries;

namespace TradingApp.Api.Controllers;

[Route("api/market")]
public sealed class MarketDataController : ApiController
{
    public MarketDataController(IMediator mediator, IdentityService identityService)
        : base(mediator, identityService)
    {
    }

    [HttpGet("info")]
    [ProducesResponseType(typeof(MarketInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetMarketInfo([FromQuery] string asset, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetMarketInfoQuery(asset), cancellationToken);
        return Ok(result);
    }

    [HttpGet("candles")]
    [ProducesResponseType(typeof(List<CandleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetCandles([FromQuery] string asset, [FromQuery] string timeframe, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetCandlesQuery(asset, timeframe), cancellationToken);
        return Ok(result);
    }
}
```

##### Pattern References

- `api-controllers.instructions.md` — ApiController base, Route, ProducesResponseType, Mediator.Send dispatch
- `csharp.instructions.md` — Sealed class

---

### Task 1.7: Update Program.cs with MediatR, AutoMapper, and exception filter registration {#task-17-update-programcs-with-mediatr-automapper-and-exception-filter-registration}

Update the existing Program.cs (from F1) to register MediatR, AutoMapper, the exception filter, and IdentityService.

- **Complexity**: Low
- **Risk Factors**: Must not break existing F1 registrations; MediatR assembly scanning must find the Application assembly
- **Files**:
  - `src/TradingApp.Api/Program.cs` — modification: add MediatR, AutoMapper, exception filter, IdentityService
  - `src/TradingApp.Api/TradingApp.Api.csproj` — may need new package references
- **Success**:
  - MediatR resolves queries from TradingApp.Application assembly
  - AutoMapper scans Application assembly for profiles
  - HttpGlobalExceptionFilter is registered and active
  - IdentityService is registered as singleton/scoped
  - Existing F1 functionality (health check, HyperliquidRestClient, config validation) still works
- **Dependencies**:
  - Task 1.2 (exception filter, IdentityService)
  - Task 1.5 (queries to register)

#### Implementation Details

```csharp
// src/TradingApp.Api/Program.cs — modification
// Add these registrations to the existing Program.cs from F1:

// ... existing builder setup ...

// MediatR — scan Application assembly for handlers
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(TradingApp.Application.MarketData.Queries.GetMarketInfoQuery).Assembly));

// AutoMapper — scan Application assembly for profiles
builder.Services.AddAutoMapper(typeof(TradingApp.Application.MarketData.Queries.GetMarketInfoQuery).Assembly);

// IdentityService (POC stub)
builder.Services.AddScoped<TradingApp.Api.Infrastructure.Services.IdentityService>();

// Exception filter
builder.Services.AddControllers(options =>
{
    options.Filters.Add<TradingApp.Api.Infrastructure.Filters.HttpGlobalExceptionFilter>();
});

// ... existing app configuration ...
```

##### Pattern References

- `dotnet-architecture.instructions.md` — MediatR and AutoMapper assembly scanning registration
- `api-controllers.instructions.md` — HttpGlobalExceptionFilter added via MVC options

---

### Task 1.8: Create test infrastructure and write backend tests {#task-18-create-test-infrastructure-and-write-backend-tests}

Create the API test project with BaseControllerTests and write controller tests for the MarketDataController.

- **Complexity**: High
- **Risk Factors**: BaseControllerTests requires WebApplicationFactory setup with mocked IHyperliquidRestClient; must correctly configure test DI container
- **Files**:
  - `tests/TradingApp.Api.Tests/TradingApp.Api.Tests.csproj` — New test project
  - `tests/TradingApp.Api.Tests/Usings.cs` — Global usings
  - `tests/TradingApp.Api.Tests/Infrastructure/BaseControllerTests.cs` — Base test class with WebApplicationFactory
  - `tests/TradingApp.Api.Tests/Controllers/MarketDataControllerTests.cs` — Controller tests
- **Success**:
  - Test project builds successfully
  - BaseControllerTests provides a test HTTP client with mocked dependencies
  - Tests cover: get market info success, get market info not found, get candles success, get candles invalid timeframe
  - All tests pass
- **Dependencies**:
  - Tasks 1.1–1.7 (full backend implementation)

#### Implementation Details

```xml
<!-- tests/TradingApp.Api.Tests/TradingApp.Api.Tests.csproj — new file -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="FluentAssertions" Version="6.*" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.*" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="Moq" Version="4.*" />
    <PackageReference Include="MSTest.TestAdapter" Version="3.*" />
    <PackageReference Include="MSTest.TestFramework" Version="3.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\TradingApp.Api\TradingApp.Api.csproj" />
    <ProjectReference Include="..\..\src\TradingApp.Application\TradingApp.Application.csproj" />
    <ProjectReference Include="..\..\src\TradingApp.Infrastructure\TradingApp.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

```csharp
// tests/TradingApp.Api.Tests/Usings.cs — new file
global using FluentAssertions;
global using Microsoft.VisualStudio.TestTools.UnitTesting;
global using Moq;
global using System.Net;
global using System.Net.Http.Json;
```

```csharp
// tests/TradingApp.Api.Tests/Infrastructure/BaseControllerTests.cs — new file
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TradingApp.Application.Abstractions.Services;

namespace TradingApp.Api.Tests.Infrastructure;

public abstract class BaseControllerTests
{
    private WebApplicationFactory<Program>? _factory;
    protected Mock<IHyperliquidRestClient> RestClientMock { get; } = new();

    protected HttpClient GetTestClient()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove the real HyperliquidRestClient registration
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(IHyperliquidRestClient));
                    if (descriptor is not null)
                        services.Remove(descriptor);

                    // Register mock
                    services.AddSingleton(RestClientMock.Object);
                });
            });

        return _factory.CreateClient();
    }

    protected async Task<T?> ReadSuccessAsync<T>(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<T>();
    }

    protected async Task AssertStatusCodeAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        response.StatusCode.Should().Be(expected);
    }
}
```

```csharp
// tests/TradingApp.Api.Tests/Controllers/MarketDataControllerTests.cs — new file
using TradingApp.Api.Tests.Infrastructure;
using TradingApp.Application.MarketData.Models;

namespace TradingApp.Api.Tests.Controllers;

[TestClass]
public class MarketDataControllerTests : BaseControllerTests
{
    private const string BaseUrl = "api/market";

    [TestMethod]
    public async Task GivenController_WhenGetMarketInfoWithValidAsset_ThenReturnsOk()
    {
        // Arrange
        var expected = new MarketInfoDto
        {
            Asset = "BTC-PERP",
            MidPrice = 50000m,
            MarkPrice = 50001m,
            IndexPrice = 49999m,
            FundingRate = 0.0001m,
            Volume24h = 1000000m,
            OpenInterest = 500000m,
            PriceChange24hPercent = 2.5m
        };
        RestClientMock.Setup(c => c.GetMarketInfoAsync("BTC-PERP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var client = GetTestClient();

        // Act
        var response = await client.GetAsync($"{BaseUrl}/info?asset=BTC-PERP");

        // Assert
        var result = await ReadSuccessAsync<MarketInfoDto>(response);
        result.Should().NotBeNull();
        result!.Asset.Should().Be("BTC-PERP");
        result.MidPrice.Should().Be(50000m);
    }

    [TestMethod]
    public async Task GivenController_WhenGetMarketInfoWithUnknownAsset_ThenReturnsNotFound()
    {
        // Arrange
        // Note: "FAKE-PERP" is not in HyperliquidAssetMapper, so NotFoundException is thrown 
        // at the mapper level before the REST client is called. The mock is not invoked —
        // the 404 comes from the mapper via the exception filter.
        var client = GetTestClient();

        // Act
        var response = await client.GetAsync($"{BaseUrl}/info?asset=FAKE-PERP");

        // Assert
        await AssertStatusCodeAsync(response, HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task GivenController_WhenGetMarketInfoReturnsNull_ThenReturnsNotFound()
    {
        // Arrange — asset exists in mapper but exchange returns no data
        RestClientMock.Setup(c => c.GetMarketInfoAsync("BTC-PERP", It.IsAny<CancellationToken>()))
            .ReturnsAsync((MarketInfoDto?)null);

        var client = GetTestClient();

        // Act
        var response = await client.GetAsync($"{BaseUrl}/info?asset=BTC-PERP");

        // Assert — handler throws NotFoundException when result is null
        await AssertStatusCodeAsync(response, HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task GivenController_WhenGetCandlesWithValidParams_ThenReturnsOk()
    {
        // Arrange
        var expected = new List<CandleDto>
        {
            new() { Timestamp = 1700000000000, Open = 50000m, High = 50100m, Low = 49900m, Close = 50050m, Volume = 100m },
            new() { Timestamp = 1699999100000, Open = 49900m, High = 50000m, Low = 49800m, Close = 49950m, Volume = 90m }
        };
        RestClientMock.Setup(c => c.GetCandlesAsync("BTC-PERP", "15m", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var client = GetTestClient();

        // Act
        var response = await client.GetAsync($"{BaseUrl}/candles?asset=BTC-PERP&timeframe=15m");

        // Assert
        var result = await ReadSuccessAsync<List<CandleDto>>(response);
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task GivenController_WhenGetCandlesWithInvalidTimeframe_ThenReturnsBadRequest()
    {
        // Arrange
        RestClientMock.Setup(c => c.GetCandlesAsync("BTC-PERP", "invalid", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Application.Abstractions.Exceptions.DomainException("Invalid timeframe 'invalid'. Supported: 15m, 1h, 4h"));

        var client = GetTestClient();

        // Act
        var response = await client.GetAsync($"{BaseUrl}/candles?asset=BTC-PERP&timeframe=invalid");

        // Assert
        await AssertStatusCodeAsync(response, HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task GivenController_WhenGetCandlesReturnsEmpty_ThenReturnsOkWithEmptyList()
    {
        // Arrange
        RestClientMock.Setup(c => c.GetCandlesAsync("BTC-PERP", "4h", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CandleDto>());

        var client = GetTestClient();

        // Act
        var response = await client.GetAsync($"{BaseUrl}/candles?asset=BTC-PERP&timeframe=4h");

        // Assert
        var result = await ReadSuccessAsync<List<CandleDto>>(response);
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }
}
```

> **Note**: The `BaseControllerTests` pattern here uses `WebApplicationFactory` directly instead of the full `BaseControllerTests<Startup>` from the instruction file, since this POC doesn't have a `Startup` class (uses minimal API with `Program`). The mock registration replaces the real `IHyperliquidRestClient` with a Moq mock.

> **IMPORTANT**: The `Program` class must be visible to the test project. Add the following at the bottom of `src/TradingApp.Api/Program.cs`:
> ```csharp
> // Required for WebApplicationFactory in test projects
> public partial class Program { }
> ```
> Alternatively, add `<InternalsVisibleTo Include="TradingApp.Api.Tests" />` to the Api project's csproj.

##### Pattern References

- `testing.instructions.md` — MSTest, Moq, FluentAssertions ≤v6, BaseControllerTests pattern, Given_When_Then naming
- `testing.instructions.md` — Command/Query handlers tested only via controller tests
- `csharp.instructions.md` — Async test methods

---

### Task 1.9: Build solution and run all tests {#task-19-build-solution-and-run-all-tests}

Build the complete solution and run all test projects to verify everything compiles and passes.

- **Complexity**: Low
- **Risk Factors**: Compilation errors from project references; test failures from DI configuration
- **Files**: None (verification only)
- **Success**:
  - `dotnet build TradingApp.sln` succeeds with no errors
  - `dotnet test tests/TradingApp.Api.Tests/TradingApp.Api.Tests.csproj` — all tests pass
- **Dependencies**:
  - Tasks 1.1–1.8

## Phase Success Criteria

- Application project created with MediatR/AutoMapper and CQRS base types
- ApiController base class, Envelope, and HttpGlobalExceptionFilter registered and functional
- HyperliquidRestClient extended with GetMarketInfoAsync and GetCandlesAsync
- MarketDataController exposes GET /api/market/info and GET /api/market/candles
- MediatR queries dispatch correctly from controller to handler to REST client
- All backend tests pass (market info success/not-found, candles success/invalid-timeframe/empty)
- Solution builds cleanly
