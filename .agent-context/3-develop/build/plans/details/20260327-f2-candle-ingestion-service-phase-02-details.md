<!-- markdownlint-disable-file -->

# Task Details: F2 — Candle Ingestion Service

## Phase 2: Ingestion Service Implementation

## Standards and Knowledge References

- **csharp.instructions.md** — `sealed` classes, `_field` naming, async/await with `CancellationToken`, `IOptions<T>` configuration
- **testing.instructions.md** — MSTest + Moq + FluentAssertions 6.x, `Given_When_Then` naming, service unit test pattern with `[TestInitialize]`
- **dotnet-architecture.instructions.md** — Interface in `Application/Abstractions/Services/`, implementation in `Infrastructure/Services/`, DTOs in `Application/<Feature>/Models/`
- **02-hyperliquid-integration.md** — Candle batch fetching semantics, Hyperliquid returns up to 5000 candles per request
- **F1 PBI spec** — `ICandleRepository.BulkInsertAsync(candles)` uses INSERT OR IGNORE in 500-row batches; `GetLatestTimestampAsync(symbol, interval)` returns `long?`

## Design References

- The ingestion service orchestrates: determine start → fetch batch → map → upsert → advance cursor → delay → repeat
- Mapping: `CandleSnapshotDto` → `Candle` entity. The `Symbol` and `Interval` fields are injected from the request context (not from the DTO)
- Rate limiting: configurable delay (`BatchDelayMs`) between consecutive API calls using `Task.Delay`
- Ingestion timeout: `CancellationTokenSource` with `MaxIngestionTimeoutMs` linked to the caller's token
- Per-interval error isolation: try/catch per interval loop; failed intervals are recorded in `IntervalResult.Error` and remaining intervals continue
- The service is registered as singleton because the concurrency guard (`SemaphoreSlim`) must be shared across requests

### Task 2.1: Create `ICandleIngestionService` interface with DTOs {#task-21-create-icandleingestionservice-interface-with-dtos}

Create the ingestion service interface and all associated DTOs: `IngestionRequest`, `IngestionResult`, and `IntervalResult`.

- **Complexity**: Medium
- **Risk Factors**: DTO design must match the PBI response shape exactly (totalFetched, totalInserted, totalSkipped, elapsedMs, per-interval breakdown with error)
- **Files**:
  - `src/TradingApp.Application/Abstractions/Services/ICandleIngestionService.cs` — New interface
  - `src/TradingApp.Application/Candles/Models/IngestionRequest.cs` — New request DTO
  - `src/TradingApp.Application/Candles/Models/IngestionResult.cs` — New result DTO
  - `src/TradingApp.Application/Candles/Models/IntervalResult.cs` — New per-interval result DTO
- **Success**:
  - `ICandleIngestionService` has `IngestAsync(IngestionRequest, CancellationToken)` returning `IngestionResult`
  - DTOs match the PBI response shape
  - All types compile
- **Dependencies**: None (these are pure interface/DTO definitions)

#### Implementation Details

```csharp
// src/TradingApp.Application/Abstractions/Services/ICandleIngestionService.cs — new file
using TradingApp.Application.Candles.Models;

namespace TradingApp.Application.Abstractions.Services;

public interface ICandleIngestionService
{
    Task<IngestionResult> IngestAsync(IngestionRequest request, CancellationToken cancellationToken = default);
}
```

```csharp
// src/TradingApp.Application/Candles/Models/IngestionRequest.cs — new file
namespace TradingApp.Application.Candles.Models;

public sealed class IngestionRequest
{
    public required string Symbol { get; init; }
    public required string[] Intervals { get; init; }
    public long? StartTime { get; init; }
    public long? EndTime { get; init; }
}
```

```csharp
// src/TradingApp.Application/Candles/Models/IngestionResult.cs — new file
namespace TradingApp.Application.Candles.Models;

public sealed class IngestionResult
{
    public int TotalFetched { get; init; }
    public int TotalInserted { get; init; }
    public int TotalSkipped { get; init; }
    public long ElapsedMs { get; init; }
    public required IReadOnlyList<IntervalResult> Intervals { get; init; }
}
```

```csharp
// src/TradingApp.Application/Candles/Models/IntervalResult.cs — new file
namespace TradingApp.Application.Candles.Models;

public sealed class IntervalResult
{
    public required string Interval { get; init; }
    public int Fetched { get; init; }
    public int Inserted { get; init; }
    public int Skipped { get; init; }
    public string? Error { get; init; }
}
```

##### Pattern References

- `src/TradingApp.Application/MarketData/Models/CandleDto.cs` — DTO pattern with `sealed class` and `{ get; init; }`
- `src/TradingApp.Application/Abstractions/Services/IHyperliquidRestClient.cs` — interface placement pattern

---

### Task 2.2: Implement `CandleIngestionService` {#task-22-implement-candleingestionservice}

Implement the full ingestion service with: concurrency guard, per-interval batch pagination, candle mapping, rate limiting, timeout enforcement, and structured logging.

- **Complexity**: High
- **Risk Factors**: 
  - Pagination cursor logic must correctly advance by last candle timestamp
  - Concurrency guard must prevent multiple simultaneous runs
  - Timeout enforcement must cleanly stop in-progress ingestion
  - Per-interval error isolation must not abort remaining intervals
- **Files**:
  - `src/TradingApp.Infrastructure/Services/CandleIngestionService.cs` — New implementation
- **Success**:
  - Service fetches candles in batches, advancing cursor by last candle timestamp
  - Pagination stops when API returns empty response or fewer candles than batch size
  - `SemaphoreSlim` prevents concurrent ingestion; throws `IngestionAlreadyRunningException` on contention
  - Rate limiting delays `BatchDelayMs` between API calls
  - Timeout via linked `CancellationTokenSource` stops ingestion after `MaxIngestionTimeoutMs`
  - Failed intervals are recorded with error detail; remaining intervals continue
  - Structured logging at: ingestion start, batch fetched, interval complete, interval error, ingestion complete
- **Dependencies**: Task 2.1 (interface/DTOs), Phase 1 (GetCandleSnapshotsAsync, CandleIngestionOptions), F1 (ICandleRepository, Candle entity)

#### Implementation Details

```csharp
// src/TradingApp.Infrastructure/Services/CandleIngestionService.cs — new file
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Candles.Models;
using TradingApp.Domain.Entities;
using TradingApp.Infrastructure.Hyperliquid;

namespace TradingApp.Infrastructure.Services;

public sealed class CandleIngestionService : ICandleIngestionService
{
    private readonly IHyperliquidRestClient _restClient;
    private readonly ICandleRepository _candleRepository;
    private readonly CandleIngestionOptions _options;
    private readonly ILogger<CandleIngestionService> _logger;
    private static readonly SemaphoreSlim _guard = new(1, 1);

    public CandleIngestionService(
        IHyperliquidRestClient restClient,
        ICandleRepository candleRepository,
        IOptions<CandleIngestionOptions> options,
        ILogger<CandleIngestionService> logger)
    {
        _restClient = restClient;
        _candleRepository = candleRepository;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IngestionResult> IngestAsync(IngestionRequest request, CancellationToken cancellationToken = default)
    {
        if (!_guard.Wait(0))
        {
            throw new IngestionAlreadyRunningException();
        }

        try
        {
            return await IngestCoreAsync(request, cancellationToken);
        }
        finally
        {
            _guard.Release();
        }
    }

    private async Task<IngestionResult> IngestCoreAsync(IngestionRequest request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using var timeoutCts = new CancellationTokenSource(_options.MaxIngestionTimeoutMs);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var token = linkedCts.Token;

        var coin = HyperliquidAssetMapper.ToCoin(request.Symbol);
        var endTime = request.EndTime ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        _logger.LogInformation(
            "Candle ingestion started for {Symbol} with intervals [{Intervals}]",
            coin, string.Join(", ", request.Intervals));

        var intervalResults = new List<IntervalResult>();

        foreach (var interval in request.Intervals)
        {
            if (token.IsCancellationRequested)
            {
                _logger.LogWarning("Ingestion timeout reached. Stopping.");
                break;
            }

            var intervalResult = await IngestIntervalAsync(coin, interval, request.StartTime, endTime, token);
            intervalResults.Add(intervalResult);
        }

        stopwatch.Stop();

        var result = new IngestionResult
        {
            TotalFetched = intervalResults.Sum(r => r.Fetched),
            TotalInserted = intervalResults.Sum(r => r.Inserted),
            TotalSkipped = intervalResults.Sum(r => r.Skipped),
            ElapsedMs = stopwatch.ElapsedMilliseconds,
            Intervals = intervalResults,
        };

        _logger.LogInformation(
            "Candle ingestion completed for {Symbol}. Fetched={Fetched}, Inserted={Inserted}, Skipped={Skipped}, ElapsedMs={ElapsedMs}",
            coin, result.TotalFetched, result.TotalInserted, result.TotalSkipped, result.ElapsedMs);

        return result;
    }

    private async Task<IntervalResult> IngestIntervalAsync(
        string coin, string interval, long? requestStartTime, long endTime, CancellationToken token)
    {
        var fetched = 0;
        var inserted = 0;
        var retryCount = 0;

        try
        {
            var intervalMs = HyperliquidAssetMapper.GetIntervalMs(interval);
            var cursor = requestStartTime
                ?? await _candleRepository.GetLatestTimestampAsync(coin, interval)
                ?? new DateTimeOffset(_options.DefaultStartDate).ToUnixTimeMilliseconds();

            // If resuming from latest timestamp, advance by 1ms to avoid re-fetching the same candle
            if (requestStartTime is null && cursor > new DateTimeOffset(_options.DefaultStartDate).ToUnixTimeMilliseconds())
            {
                cursor += 1;
            }

            _logger.LogInformation(
                "Ingesting {Interval} candles for {Symbol} from {StartTime} to {EndTime}",
                interval, coin, cursor, endTime);

            while (cursor < endTime)
            {
                token.ThrowIfCancellationRequested();

                var batchEnd = Math.Min(cursor + (5000L * intervalMs), endTime);

                List<CandleSnapshotDto> batch;
                try
                {
                    batch = await _restClient.GetCandleSnapshotsAsync(coin, interval, cursor, batchEnd, token);
                }
                catch (Exception ex) when (ex is not OperationCanceledException && retryCount < _options.MaxRetries)
                {
                    retryCount++;
                    var delay = (int)Math.Pow(2, retryCount) * 1000;
                    _logger.LogWarning(ex,
                        "Batch fetch failed for {Symbol}/{Interval} (retry {Retry}/{MaxRetries}). Retrying in {DelayMs}ms",
                        coin, interval, retryCount, _options.MaxRetries, delay);
                    await Task.Delay(delay, token);
                    continue;
                }

                if (batch.Count == 0)
                {
                    break;
                }

                var candles = batch.Select(dto => new Candle
                {
                    Symbol = coin,
                    Interval = interval,
                    Timestamp = dto.Timestamp,
                    Open = dto.Open,
                    High = dto.High,
                    Low = dto.Low,
                    Close = dto.Close,
                    Volume = dto.Volume,
                    NumTrades = dto.NumTrades,
                }).ToList();

                await _candleRepository.BulkInsertAsync(candles);

                var batchSent = candles.Count; // BulkInsertAsync uses INSERT OR IGNORE; actual inserts may be lower (F1 does not return affected count)
                fetched += batch.Count;
                inserted += batchSent;

                _logger.LogDebug(
                    "Batch fetched for {Symbol}/{Interval}: {Count} candles, cursor advanced to {Cursor}",
                    coin, interval, batch.Count, batch[^1].Timestamp);

                cursor = batch[^1].Timestamp + 1;
                retryCount = 0; // reset on success

                if (batch.Count < 5000)
                {
                    break; // last batch — API returns up to 5000 candles per request
                }

                await Task.Delay(_options.BatchDelayMs, token);
            }

            _logger.LogInformation(
                "Interval {Interval} complete for {Symbol}. Fetched={Fetched}, Inserted={Inserted}",
                interval, coin, fetched, inserted);

            return new IntervalResult
            {
                Interval = interval,
                Fetched = fetched,
                Inserted = inserted,
                Skipped = fetched - inserted,
                Error = null,
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Interval {Interval} for {Symbol} was cancelled. Fetched so far: {Fetched}",
                interval, coin, fetched);

            return new IntervalResult
            {
                Interval = interval,
                Fetched = fetched,
                Inserted = inserted,
                Skipped = fetched - inserted,
                Error = "Cancelled or timed out",
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Interval {Interval} for {Symbol} failed after retries. Fetched so far: {Fetched}",
                interval, coin, fetched);

            return new IntervalResult
            {
                Interval = interval,
                Fetched = fetched,
                Inserted = inserted,
                Skipped = fetched - inserted,
                Error = ex.Message,
            };
        }
    }
}
```

##### Pattern References

- `src/TradingApp.Api/Services/HyperliquidAssetMetadataCache.cs` — `SemaphoreSlim(1, 1)` concurrency guard pattern
- `src/TradingApp.Infrastructure/Services/HyperliquidRestClient.cs` — `HyperliquidAssetMapper.ToCoin()` and `GetIntervalMs()` usage
- `src/TradingApp.Infrastructure/Hyperliquid/HyperliquidAssetMapper.cs` — validation methods

---

### Task 2.3: Register in DI {#task-23-register-in-di}

Register `ICandleIngestionService` as scoped in `Program.cs` and bind `CandleIngestionOptions`. The concurrency guard uses a `static SemaphoreSlim` field in the service class, so scoped registration is safe.

- **Complexity**: Low
- **Risk Factors**: The `SemaphoreSlim` concurrency guard is a static field, shared across all scoped instances
- **Files**:
  - `src/TradingApp.Api/Program.cs` — Add options binding and service registration
- **Success**:
  - `CandleIngestionOptions` is bound and validated on start
  - `ICandleIngestionService` is registered as scoped
- **Dependencies**: Tasks 2.1, 2.2, Task 1.3 (CandleIngestionOptions)

#### Implementation Details

```csharp
// src/TradingApp.Api/Program.cs — modification
// Add after the HyperliquidOptions binding block:

// Bind CandleIngestion configuration
builder.Services.AddOptions<CandleIngestionOptions>()
    .Bind(builder.Configuration.GetSection(CandleIngestionOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Candle ingestion service (scoped — static SemaphoreSlim handles cross-request concurrency guard)
builder.Services.AddScoped<ICandleIngestionService, CandleIngestionService>();
```

##### Pattern References

- `src/TradingApp.Api/Program.cs` — existing `AddOptions<HyperliquidOptions>()` pattern, `AddScoped<>()` pattern
- `src/TradingApp.Application/Abstractions/Configuration/HyperliquidOptions.cs` — `SectionName` constant pattern

---

### Task 2.4: Write unit tests for `CandleIngestionService` {#task-24-write-unit-tests-for-candleingestionservice}

Write comprehensive unit tests covering: happy path pagination, incremental sync, empty response handling, rate limiting delay, per-interval error isolation, timeout, and concurrency guard.

- **Complexity**: High
- **Risk Factors**: Multiple async scenarios; must carefully orchestrate mock sequences for pagination; concurrency test needs parallel task execution
- **Files**:
  - `tests/TradingApp.Api.Tests/Services/CandleIngestionServiceTests.cs` — New test class
- **Success**:
  - Tests cover: initial ingestion from default start, incremental sync from latest timestamp, empty batch stops pagination, failed interval doesn't abort others, timeout stops ingestion, concurrent calls throw `IngestionAlreadyRunningException`, batch delay is applied between calls
- **Dependencies**: Tasks 2.1, 2.2

#### Implementation Details

```csharp
// tests/TradingApp.Api.Tests/Services/CandleIngestionServiceTests.cs — new file
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Candles.Models;
using TradingApp.Application.MarketData.Models;
using TradingApp.Domain.Entities;
using TradingApp.Infrastructure.Services;

namespace TradingApp.Api.Tests.Services;

[TestClass]
public sealed class CandleIngestionServiceTests
{
    private Mock<IHyperliquidRestClient> _restClientMock = default!;
    private Mock<ICandleRepository> _repositoryMock = default!;
    private Mock<ILogger<CandleIngestionService>> _loggerMock = default!;
    private IOptions<CandleIngestionOptions> _options = default!;
    private CandleIngestionService _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _restClientMock = new Mock<IHyperliquidRestClient>();
        _repositoryMock = new Mock<ICandleRepository>();
        _loggerMock = new Mock<ILogger<CandleIngestionService>>();
        _options = Options.Create(new CandleIngestionOptions
        {
            BatchDelayMs = 0, // no delay in tests
            MaxRetries = 3,
            MaxIngestionTimeoutMs = 900000,
            DefaultStartDate = new DateTime(2022, 11, 1, 0, 0, 0, DateTimeKind.Utc),
        });
        _sut = new CandleIngestionService(
            _restClientMock.Object,
            _repositoryMock.Object,
            _options,
            _loggerMock.Object);
    }

    [TestMethod]
    public async Task GivenEmptyDatabase_WhenIngest_ThenFetchesFromDefaultStartDate()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetLatestTimestampAsync("BTC", "1h"))
            .ReturnsAsync((long?)null);

        _restClientMock
            .Setup(r => r.GetCandleSnapshotsAsync("BTC", "1h", It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CandleSnapshotDto>()); // empty = stop

        var request = new IngestionRequest { Symbol = "BTC", Intervals = ["1h"] };

        // Act
        var result = await _sut.IngestAsync(request);

        // Assert
        result.TotalFetched.Should().Be(0);
        result.Intervals.Should().HaveCount(1);
        result.Intervals[0].Interval.Should().Be("1h");
    }

    [TestMethod]
    public async Task GivenExistingCandles_WhenIngest_ThenResumesFromLatestTimestamp()
    {
        // Arrange
        var latestTimestamp = 1700000000000L;
        _repositoryMock
            .Setup(r => r.GetLatestTimestampAsync("BTC", "1h"))
            .ReturnsAsync(latestTimestamp);

        _restClientMock
            .Setup(r => r.GetCandleSnapshotsAsync("BTC", "1h", latestTimestamp + 1, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CandleSnapshotDto>());

        var request = new IngestionRequest { Symbol = "BTC", Intervals = ["1h"] };

        // Act
        var result = await _sut.IngestAsync(request);

        // Assert
        _restClientMock.Verify(
            r => r.GetCandleSnapshotsAsync("BTC", "1h", latestTimestamp + 1, It.IsAny<long>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenBatchReturnsCandles_WhenIngest_ThenCallsBulkInsertAndAdvancesCursor()
    {
        // Arrange
        var candles = Enumerable.Range(0, 500).Select(i => new CandleSnapshotDto
        {
            Timestamp = 1700000000000L + (i * 3600000L),
            Open = 50000m, High = 50100m, Low = 49900m, Close = 50050m, Volume = 100m, NumTrades = 10,
        }).ToList();

        _repositoryMock.Setup(r => r.GetLatestTimestampAsync("BTC", "1h")).ReturnsAsync((long?)null);

        var callCount = 0;
        _restClientMock
            .Setup(r => r.GetCandleSnapshotsAsync("BTC", "1h", It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? candles : new List<CandleSnapshotDto>();
            });

        var request = new IngestionRequest { Symbol = "BTC", Intervals = ["1h"] };

        // Act
        var result = await _sut.IngestAsync(request);

        // Assert
        result.TotalFetched.Should().Be(500);
        _repositoryMock.Verify(r => r.BulkInsertAsync(It.Is<IReadOnlyList<Candle>>(c => c.Count == 500)), Times.Once);
    }

    [TestMethod]
    public async Task GivenOneIntervalFails_WhenIngest_ThenOtherIntervalsComplete()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetLatestTimestampAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((long?)null);

        _restClientMock
            .Setup(r => r.GetCandleSnapshotsAsync("BTC", "15m", It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        _restClientMock
            .Setup(r => r.GetCandleSnapshotsAsync("BTC", "1h", It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CandleSnapshotDto>());

        var request = new IngestionRequest { Symbol = "BTC", Intervals = ["15m", "1h"] };
        _options = Options.Create(new CandleIngestionOptions
        {
            BatchDelayMs = 0, MaxRetries = 0, MaxIngestionTimeoutMs = 900000,
            DefaultStartDate = new DateTime(2022, 11, 1, 0, 0, 0, DateTimeKind.Utc),
        });
        _sut = new CandleIngestionService(_restClientMock.Object, _repositoryMock.Object, _options, _loggerMock.Object);

        // Act
        var result = await _sut.IngestAsync(request);

        // Assert
        result.Intervals.Should().HaveCount(2);
        result.Intervals[0].Error.Should().NotBeNullOrEmpty();
        result.Intervals[1].Error.Should().BeNull();
    }
}
```

##### Pattern References

- `tests/TradingApp.Api.Tests/Services/HyperliquidOrderServiceTests.cs` — service unit test pattern with `[TestInitialize]`, `Options.Create`, `Mock<ILogger<T>>`
- `tests/TradingApp.Api.Tests/Services/MarketDataStreamServiceTests.cs` — async lifecycle testing with `CancellationToken`

---

### Task 2.5: Build and run tests {#task-25-build-and-run-tests}

Build the solution and run all tests to verify Phase 2 changes compile and pass.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification step)
- **Success**:
  - `dotnet build` succeeds with no errors
  - All existing tests continue to pass
  - New `CandleIngestionServiceTests` pass
- **Dependencies**: Tasks 2.1–2.4

## Phase Success Criteria

- `ICandleIngestionService` interface exists with `IngestAsync` method
- `IngestionRequest`, `IngestionResult`, `IntervalResult` DTOs match PBI response shape
- `CandleIngestionService` implements batch pagination with cursor advancement
- Rate limiting delay applied between batch API calls
- Concurrency guard prevents simultaneous ingestion runs via static `SemaphoreSlim`
- Per-interval error isolation: failed intervals don't abort the rest
- Timeout enforcement via linked `CancellationTokenSource`
- Structured logging at all key points
- All unit tests pass covering happy path, incremental sync, empty response, error isolation
