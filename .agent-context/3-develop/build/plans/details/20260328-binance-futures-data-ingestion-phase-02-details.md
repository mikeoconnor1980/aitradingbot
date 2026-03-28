<!-- markdownlint-disable-file -->

# Task Details: Binance USDⓈ-M Futures Data Ingestion

## Phase 2: Binance REST Client & Ingestion Infrastructure

## Standards and Knowledge References

- **csharp.instructions.md**: Sealed classes, CancellationToken threading, async/await with `Async` suffix
- **testing.instructions.md**: MSTest, Moq with `MockBehavior.Strict`, FluentAssertions ≤ v6, `Given_When_Then` naming
- **dotnet-architecture.instructions.md**: Service interfaces in `Application/Abstractions/Services/`, implementations in `Infrastructure/Services/`
- **02-hyperliquid-integration.md**: Existing REST client pattern for exchange integrations

## Design References

- Binance USDⓈ-M Futures API: `GET /fapi/v1/klines` returns array of arrays (not JSON objects)
- Kline response: `[openTime, open, high, low, close, volume, closeTime, quoteVolume, trades, takerBuyBaseVol, takerBuyQuoteVol, ignore]`
- Max 1500 candles per request, weight = 5 per request
- Rate limit: 1200 weight/minute

---

### Task 2.1: Create `BinanceIngestionOptions` configuration class {#task-21-create-binanceingestionoptions}

Create a typed options class for Binance ingestion configuration following the existing `CandleIngestionOptions` pattern.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.Application/Abstractions/Configuration/BinanceIngestionOptions.cs` — New file
- **Success**:
  - Class follows `SectionName` constant + DataAnnotations pattern
  - Default values match PBI specification
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Application/Abstractions/Configuration/BinanceIngestionOptions.cs — new file
using System.ComponentModel.DataAnnotations;

namespace TradingApp.Application.Abstractions.Configuration;

public sealed class BinanceIngestionOptions
{
    public const string SectionName = "BinanceIngestion";

    [Range(0, 60000)]
    public int BatchDelayMs { get; set; } = 250;

    [Range(1, 10)]
    public int MaxRetries { get; set; } = 3;

    [Range(60000, 28800000)]
    public int MaxIngestionTimeoutMs { get; set; } = 7200000;

    [Required]
    public DateTime DefaultStartDate { get; set; } = new DateTime(2019, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    [Range(100, 1500)]
    public int PageSize { get; set; } = 1500;

    [Required]
    public string BaseUrl { get; set; } = "https://fapi.binance.com";
}
```

##### Pattern References

- `src/TradingApp.Application/Abstractions/Configuration/CandleIngestionOptions.cs` — existing options class

---

### Task 2.2: Create `IBinanceFuturesRestClient` interface {#task-22-create-ibinancefuturesrestclient-interface}

Create the Binance REST client interface in the Application abstractions layer, defining methods for klines, funding rates, and mark price klines.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.Application/Abstractions/Services/IBinanceFuturesRestClient.cs` — New file
- **Success**:
  - Interface defines `GetKlinesAsync` method returning `IReadOnlyList<CandleSnapshotDto>`
  - Method signatures support forward pagination with `startTime`, `endTime`, `limit`
  - `CancellationToken` on all methods
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Application/Abstractions/Services/IBinanceFuturesRestClient.cs — new file
using TradingApp.Application.MarketData.Models;

namespace TradingApp.Application.Abstractions.Services;

public interface IBinanceFuturesRestClient
{
    Task<IReadOnlyList<CandleSnapshotDto>> GetKlinesAsync(
        string futuresSymbol,
        string interval,
        long startTime,
        long? endTime = null,
        int limit = 1500,
        CancellationToken cancellationToken = default);
}
```

Note: `GetFundingRatesAsync` and `GetMarkPriceKlinesAsync` will be added in Phases 4 and 5.

##### Pattern References

- `src/TradingApp.Application/Abstractions/Services/IHyperliquidRestClient.cs` — existing REST client interface placement

---

### Task 2.3: Create `BinanceAssetMapper` static class {#task-23-create-binanceassetmapper}

Create a static asset mapper that converts display symbols to Binance Futures symbols and validates intervals.

- **Complexity**: Medium
- **Risk Factors**: Must cover all 8 supported symbols and 5 intervals including `1d`
- **Files**:
  - `src/TradingApp.Infrastructure/Binance/BinanceAssetMapper.cs` — New file
- **Success**:
  - `ToFuturesSymbol("BTC")` returns `"BTCUSDT"`
  - `IsValidSymbol("BTC")` returns `true`, `IsValidSymbol("INVALID")` returns `false`
  - `IsValidInterval("1d")` returns `true`
  - `GetIntervalMs("15m")` returns `900000`
  - Invalid inputs throw `DomainException`
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Infrastructure/Binance/BinanceAssetMapper.cs — new file
using TradingApp.Application.Abstractions.Exceptions;

namespace TradingApp.Infrastructure.Binance;

public static class BinanceAssetMapper
{
    private static readonly Dictionary<string, string> SymbolToFuturesSymbol = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BTC"] = "BTCUSDT",
        ["ETH"] = "ETHUSDT",
        ["SOL"] = "SOLUSDT",
        ["DOGE"] = "DOGEUSDT",
        ["AVAX"] = "AVAXUSDT",
        ["ARB"] = "ARBUSDT",
        ["LINK"] = "LINKUSDT",
        ["OP"] = "OPUSDT"
    };

    private static readonly Dictionary<string, long> IntervalToMs = new(StringComparer.Ordinal)
    {
        ["5m"] = 300_000L,
        ["15m"] = 900_000L,
        ["1h"] = 3_600_000L,
        ["4h"] = 14_400_000L,
        ["1d"] = 86_400_000L
    };

    public static string ToFuturesSymbol(string displaySymbol)
    {
        if (SymbolToFuturesSymbol.TryGetValue(displaySymbol, out var futuresSymbol))
            return futuresSymbol;

        throw new DomainException($"Unsupported Binance symbol: '{displaySymbol}'. " +
            $"Valid symbols: {string.Join(", ", SymbolToFuturesSymbol.Keys)}");
    }

    public static bool IsValidSymbol(string displaySymbol)
        => SymbolToFuturesSymbol.ContainsKey(displaySymbol);

    public static bool IsValidInterval(string interval)
        => IntervalToMs.ContainsKey(interval);

    public static long GetIntervalMs(string interval)
    {
        if (IntervalToMs.TryGetValue(interval, out var ms))
            return ms;

        throw new DomainException($"Unsupported Binance interval: '{interval}'. " +
            $"Valid intervals: {string.Join(", ", IntervalToMs.Keys)}");
    }

    public static IReadOnlyCollection<string> ValidSymbols => SymbolToFuturesSymbol.Keys;
    public static IReadOnlyCollection<string> ValidIntervals => IntervalToMs.Keys;
}
```

##### Pattern References

- `src/TradingApp.Infrastructure/Hyperliquid/HyperliquidAssetMapper.cs` — existing static mapper pattern

---

### Task 2.4: Create Binance wire models {#task-24-create-binance-wire-models}

Create the wire model for deserializing Binance kline API responses. Binance returns arrays of arrays (not JSON objects), requiring custom deserialization.

- **Complexity**: Medium
- **Risk Factors**: Binance klines are `JsonArray` (array of arrays), not objects — requires index-based parsing
- **Files**:
  - `src/TradingApp.Infrastructure/Binance/Models/BinanceKline.cs` — New file
- **Success**:
  - `BinanceKline` correctly maps all 12 array positions
  - `ToCandleSnapshotDto()` converts to the normalized `CandleSnapshotDto`
  - String-to-decimal parsing handles Binance's string-encoded numbers
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Infrastructure/Binance/Models/BinanceKline.cs — new file
using System.Globalization;
using System.Text.Json;
using TradingApp.Application.MarketData.Models;

namespace TradingApp.Infrastructure.Binance.Models;

/// <summary>
/// Binance kline response is an array of arrays:
/// [openTime, open, high, low, close, volume, closeTime, quoteVolume, trades, takerBuyBaseVol, takerBuyQuoteVol, ignore]
/// All numeric values except openTime/closeTime/trades are returned as strings.
/// </summary>
public sealed class BinanceKline
{
    public long OpenTime { get; init; }
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal Close { get; init; }
    public decimal Volume { get; init; }
    public long CloseTime { get; init; }
    public int NumberOfTrades { get; init; }

    public static BinanceKline FromJsonArray(JsonElement element)
    {
        return new BinanceKline
        {
            OpenTime = element[0].GetInt64(),
            Open = decimal.Parse(element[1].GetString()!, CultureInfo.InvariantCulture),
            High = decimal.Parse(element[2].GetString()!, CultureInfo.InvariantCulture),
            Low = decimal.Parse(element[3].GetString()!, CultureInfo.InvariantCulture),
            Close = decimal.Parse(element[4].GetString()!, CultureInfo.InvariantCulture),
            Volume = decimal.Parse(element[5].GetString()!, CultureInfo.InvariantCulture),
            CloseTime = element[6].GetInt64(),
            NumberOfTrades = element[8].GetInt32()
        };
    }

    public CandleSnapshotDto ToCandleSnapshotDto() => new()
    {
        Timestamp = OpenTime,
        Open = Open,
        High = High,
        Low = Low,
        Close = Close,
        Volume = Volume,
        NumTrades = NumberOfTrades
    };
}
```

##### Pattern References

- `src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidCandle.cs` — existing exchange wire model pattern
- `src/TradingApp.Application/MarketData/Models/CandleSnapshotDto.cs` — normalized DTO target

---

### Task 2.5: Implement `BinanceFuturesRestClient` {#task-25-implement-binancefuturesrestclient}

Create a typed `HttpClient` implementation for the Binance Futures API. Handles `GET /fapi/v1/klines` with query parameters, response deserialization, and error mapping.

- **Complexity**: High
- **Risk Factors**: Binance uses GET with query params (not POST like Hyperliquid), array-of-arrays JSON format
- **Files**:
  - `src/TradingApp.Infrastructure/Services/BinanceFuturesRestClient.cs` — New file
- **Success**:
  - `GetKlinesAsync` calls `GET /fapi/v1/klines?symbol=X&interval=Y&startTime=Z&limit=1500`
  - Response deserialized from array-of-arrays to `BinanceKline` then mapped to `CandleSnapshotDto`
  - HTTP 429 → `RateLimitException`, HTTP 451 → `DomainException` with IP ban message
  - HTTP 4xx/5xx → appropriate exception mapping
- **Dependencies**: Tasks 2.2, 2.4

#### Implementation Details

```csharp
// src/TradingApp.Infrastructure/Services/BinanceFuturesRestClient.cs — new file
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.MarketData.Models;
using TradingApp.Infrastructure.Binance.Models;

namespace TradingApp.Infrastructure.Services;

public sealed class BinanceFuturesRestClient : IBinanceFuturesRestClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BinanceFuturesRestClient> _logger;

    public BinanceFuturesRestClient(HttpClient httpClient, ILogger<BinanceFuturesRestClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CandleSnapshotDto>> GetKlinesAsync(
        string futuresSymbol,
        string interval,
        long startTime,
        long? endTime = null,
        int limit = 1500,
        CancellationToken cancellationToken = default)
    {
        var url = $"/fapi/v1/klines?symbol={futuresSymbol}&interval={interval}&startTime={startTime}&limit={limit}";
        if (endTime.HasValue)
            url += $"&endTime={endTime.Value}";

        using var response = await _httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            MapErrorResponse(response.StatusCode, body);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var klines = new List<CandleSnapshotDto>();
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var kline = BinanceKline.FromJsonArray(element);
            klines.Add(kline.ToCandleSnapshotDto());
        }

        _logger.LogDebug("Binance klines: {Symbol} {Interval} from {StartTime} — {Count} candles",
            futuresSymbol, interval, startTime, klines.Count);

        return klines;
    }

    private static void MapErrorResponse(HttpStatusCode statusCode, string body)
    {
        throw statusCode switch
        {
            HttpStatusCode.TooManyRequests => new RateLimitException(
                $"Binance rate limit exceeded: {body}"),
            (HttpStatusCode)451 => new DomainException(
                $"Binance IP banned (451). Response: {body}"),
            _ when (int)statusCode >= 400 && (int)statusCode < 500 => new DomainException(
                $"Binance API error {(int)statusCode}: {body}"),
            _ => new DomainException(
                $"Binance API server error {(int)statusCode}: {body}")
        };
    }
}
```

##### Pattern References

- `src/TradingApp.Infrastructure/Services/HyperliquidRestClient.cs` — existing typed HttpClient with error mapping

---

### Task 2.6: Create `IBinanceCandleIngestionService` and implement `BinanceCandleIngestionService` {#task-26-create-binancecandleingestionservice}

Create the Binance-specific candle ingestion service with forward-pagination, binary-search gap-finding, and concurrent ingestion guard. Follows the same architecture as the existing `CandleIngestionService`.

- **Complexity**: High
- **Risk Factors**: Complex pagination logic, binary-search gap detection, timeout handling
- **Files**:
  - `src/TradingApp.Application/Abstractions/Services/IBinanceCandleIngestionService.cs` — New interface
  - `src/TradingApp.Infrastructure/Services/BinanceCandleIngestionService.cs` — New implementation
- **Success**:
  - Paginates forward from `DefaultStartDate` (2019-09-01) or last stored candle
  - Binary search finds next data boundary on consecutive empty batches
  - Concurrent ingestion guard throws `IngestionAlreadyRunningException`
  - Rate limiting via configurable `BatchDelayMs` between requests
  - Timeout via `MaxIngestionTimeoutMs` with graceful cancellation
  - Per-interval retry with exponential backoff
  - Creates `Candle` entities with `Source = "Binance"`
- **Dependencies**: Tasks 2.1, 2.3, 2.5

#### Implementation Details

```csharp
// src/TradingApp.Application/Abstractions/Services/IBinanceCandleIngestionService.cs — new file
using TradingApp.Application.Candles.Models;

namespace TradingApp.Application.Abstractions.Services;

public interface IBinanceCandleIngestionService
{
    Task<IngestionResult> IngestAsync(IngestionRequest request, CancellationToken cancellationToken = default);
}
```

```csharp
// src/TradingApp.Infrastructure/Services/BinanceCandleIngestionService.cs — new file
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Candles.Models;
using TradingApp.Domain.Entities;
using TradingApp.Infrastructure.Binance;

namespace TradingApp.Infrastructure.Services;

public sealed class BinanceCandleIngestionService : IBinanceCandleIngestionService
{
    private static readonly SemaphoreSlim Guard = new(1, 1);

    private readonly IBinanceFuturesRestClient _restClient;
    private readonly ICandleRepository _repository;
    private readonly BinanceIngestionOptions _options;
    private readonly ILogger<BinanceCandleIngestionService> _logger;

    public BinanceCandleIngestionService(
        IBinanceFuturesRestClient restClient,
        ICandleRepository repository,
        IOptions<BinanceIngestionOptions> options,
        ILogger<BinanceCandleIngestionService> logger)
    {
        _restClient = restClient;
        _repository = repository;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IngestionResult> IngestAsync(IngestionRequest request, CancellationToken cancellationToken = default)
    {
        if (!Guard.Wait(0))
            throw new IngestionAlreadyRunningException("Binance candle ingestion is already running.");

        try
        {
            // Implementation follows same pattern as CandleIngestionService:
            // 1. Resolve Binance futures symbol via BinanceAssetMapper.ToFuturesSymbol()
            // 2. For each interval: paginate forward, binary-search gaps, create Candle entities with Source="Binance"
            // 3. BulkInsert via ICandleRepository
            // 4. Return IngestionResult with per-interval breakdown
            // ... (full pagination loop logic matching CandleIngestionService pattern)
        }
        finally
        {
            Guard.Release();
        }
    }

    // Private methods: IngestIntervalAsync, FindNextDataStartAsync, GetEffectiveEndTime
    // All follow exact same patterns as CandleIngestionService but using:
    // - BinanceAssetMapper.GetIntervalMs() instead of HyperliquidAssetMapper
    // - _restClient.GetKlinesAsync() instead of GetCandleSnapshotsAsync()
    // - Candle.Create(..., source: "Binance") instead of default source
    // - _options.PageSize (1500) instead of hardcoded 500
}
```

The implementation should match the existing `CandleIngestionService` structure closely, replacing:
- `HyperliquidAssetMapper` → `BinanceAssetMapper`
- `_restClient.GetCandleSnapshotsAsync()` → `_restClient.GetKlinesAsync()`
- Page size 500 → `_options.PageSize` (1500)
- `Candle.Create(...)`  → `Candle.Create(..., source: "Binance")`
- Default start date from `BinanceIngestionOptions.DefaultStartDate`- `GetLatestTimestampAsync(symbol, interval)` → `GetLatestTimestampAsync(symbol, interval, source: "Binance")` to resume from Binance-specific data only
##### Pattern References

- `src/TradingApp.Infrastructure/Services/CandleIngestionService.cs` — exact template for pagination, retry, gap detection, concurrency guard

---

### Task 2.7: Write unit tests for all new components {#task-27-write-unit-tests}

Write comprehensive unit tests for `BinanceAssetMapper`, `BinanceFuturesRestClient`, and `BinanceCandleIngestionService`.

- **Complexity**: High
- **Risk Factors**: Multiple test classes needed; REST client tests require `FakeHttpMessageHandler`
- **Files**:
  - `tests/TradingApp.Infrastructure.Tests/Services/BinanceAssetMapperTests.cs` — New file
  - `tests/TradingApp.Api.Tests/Services/BinanceFuturesRestClientTests.cs` — New file
  - `tests/TradingApp.Api.Tests/Services/BinanceCandleIngestionServiceTests.cs` — New file
- **Success**:
  - Mapper tests: all 8 symbols, 5 intervals, invalid inputs throw `DomainException`
  - REST client tests: successful response deserialization, 429 → `RateLimitException`, 451 → `DomainException`
  - Ingestion service tests: single batch, multi-batch pagination, empty batches with binary search, concurrent guard → `IngestionAlreadyRunningException`, timeout cancellation, retry with backoff
- **Dependencies**: Tasks 2.3, 2.5, 2.6

#### Implementation Details

```csharp
// tests/TradingApp.Api.Tests/Services/BinanceAssetMapperTests.cs — new file
[TestClass]
public sealed class BinanceAssetMapperTests
{
    [TestMethod]
    [DataRow("BTC", "BTCUSDT")]
    [DataRow("ETH", "ETHUSDT")]
    [DataRow("SOL", "SOLUSDT")]
    [DataRow("DOGE", "DOGEUSDT")]
    [DataRow("AVAX", "AVAXUSDT")]
    [DataRow("ARB", "ARBUSDT")]
    [DataRow("LINK", "LINKUSDT")]
    [DataRow("OP", "OPUSDT")]
    public void GivenValidSymbol_WhenToFuturesSymbol_ThenReturnsBinanceSymbol(string display, string expected)
    {
        BinanceAssetMapper.ToFuturesSymbol(display).Should().Be(expected);
    }

    [TestMethod]
    public void GivenInvalidSymbol_WhenToFuturesSymbol_ThenThrowsDomainException()
    {
        var act = () => BinanceAssetMapper.ToFuturesSymbol("INVALID");
        act.Should().Throw<DomainException>();
    }

    [TestMethod]
    [DataRow("5m", 300_000L)]
    [DataRow("15m", 900_000L)]
    [DataRow("1h", 3_600_000L)]
    [DataRow("4h", 14_400_000L)]
    [DataRow("1d", 86_400_000L)]
    public void GivenValidInterval_WhenGetIntervalMs_ThenReturnsMilliseconds(string interval, long expected)
    {
        BinanceAssetMapper.GetIntervalMs(interval).Should().Be(expected);
    }
}
```

```csharp
// tests/TradingApp.Api.Tests/Services/BinanceFuturesRestClientTests.cs — new file
// Pattern: Create HttpClient with FakeHttpMessageHandler, test JSON deserialization
[TestClass]
public sealed class BinanceFuturesRestClientTests
{
    // Test: GivenBinanceReturnsKlines_WhenGetKlinesAsync_ThenReturnsCandleSnapshotDtos
    // - Configure FakeHttpMessageHandler with Binance array-of-arrays JSON response
    // - Assert returned CandleSnapshotDto list matches expected values

    // Test: GivenBinanceReturns429_WhenGetKlinesAsync_ThenThrowsRateLimitException
    // Test: GivenBinanceReturns451_WhenGetKlinesAsync_ThenThrowsDomainException
}
```

```csharp
// tests/TradingApp.Api.Tests/Services/BinanceCandleIngestionServiceTests.cs — new file
// Pattern: Follow CandleIngestionServiceTests pattern exactly
[TestClass]
public sealed class BinanceCandleIngestionServiceTests
{
    private Mock<IBinanceFuturesRestClient> _restClientMock = default!;
    private Mock<ICandleRepository> _repositoryMock = default!;
    private Mock<ILogger<BinanceCandleIngestionService>> _loggerMock = default!;

    // CreateSut factory method with optional parameters
    // Tests: single batch, multi-batch, empty response, concurrent guard,
    //        timeout, retry on failure, source = "Binance" on created candles
}
```

##### Pattern References

- `tests/TradingApp.Api.Tests/Services/CandleIngestionServiceTests.cs` — ingestion service test pattern
- `tests/TradingApp.Api.Tests/Services/HyperliquidRestClientCandleSnapshotTests.cs` — REST client test with FakeHttpMessageHandler

---

### Task 2.8: Build and run tests {#task-28-build-and-run-tests}

Build and run tests to verify all Phase 2 components.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification step)
- **Success**:
  - `dotnet build tests/TradingApp.Api.Tests` — succeeds
  - `dotnet test tests/TradingApp.Api.Tests --filter "FullyQualifiedName~Binance"` — all tests pass
- **Dependencies**: Task 2.7

## Phase Success Criteria

- `BinanceIngestionOptions` created with correct defaults and DataAnnotations
- `IBinanceFuturesRestClient` interface defined with `GetKlinesAsync`
- `BinanceAssetMapper` maps all 8 symbols and 5 intervals (including `1d`)
- `BinanceKline` correctly deserializes Binance array-of-arrays response format
- `BinanceFuturesRestClient` calls Binance API, handles errors, maps to `CandleSnapshotDto`
- `BinanceCandleIngestionService` paginates, retries, detects gaps, respects rate limits
- All new unit tests pass with good coverage
