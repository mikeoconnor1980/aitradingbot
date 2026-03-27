<!-- markdownlint-disable-file -->

# Task Details: F2 — Candle Ingestion Service

## Phase 1: REST Client Overload & Configuration

## Standards and Knowledge References

- **csharp.instructions.md** — `sealed` classes, `_field` naming, async/await with `CancellationToken`, `IOptions<T>` configuration
- **testing.instructions.md** — MSTest + Moq + FluentAssertions 6.x, `Given_When_Then` naming
- **dotnet-architecture.instructions.md** — Interface in `Application/Abstractions/Services/`, implementation in `Infrastructure/Services/`
- **02-hyperliquid-integration.md** — `candleSnapshot` request shape, `PostInfoAsync<T>` pattern
- **F1 PBI spec** — `Candle` entity shape (Symbol, Interval, Timestamp, OHLCV, NumTrades)

## Design References

- The Hyperliquid `candleSnapshot` API accepts `startTime` and `endTime` as Unix milliseconds and returns up to 5000 candles per request
- The existing `GetCandlesAsync` auto-computes `startTime = endTime - 500 * intervalMs` and applies `.Take(500).OrderByDescending()` — the new overload must NOT apply these limits
- `CandleSnapshotPayload` already carries both `StartTime` and `EndTime` as `long` — no wire model changes needed
- Since the `Candle` domain entity (from F1) includes `NumTrades` but the existing `CandleDto` does not, the new overload returns a richer DTO (`CandleSnapshotDto`) that includes `NumTrades`

### Task 1.1: Add `GetCandlesAsync` overload to interface {#task-11-add-getcandlesasync-overload-to-interface}

Add a new overload to `IHyperliquidRestClient` that accepts explicit `startTime` and `endTime` parameters for forward pagination. Also add `CandleSnapshotDto` to carry `NumTrades`.

- **Complexity**: Low
- **Risk Factors**: None — additive interface change
- **Files**:
  - `src/TradingApp.Application/Abstractions/Services/IHyperliquidRestClient.cs` — Add new overload signature
  - `src/TradingApp.Application/MarketData/Models/CandleSnapshotDto.cs` — New DTO with NumTrades
- **Success**:
  - Interface compiles with both the existing and new `GetCandlesAsync` signatures
  - `CandleSnapshotDto` includes all fields from `CandleDto` plus `NumTrades`
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Application/MarketData/Models/CandleSnapshotDto.cs — new file
namespace TradingApp.Application.MarketData.Models;

public sealed class CandleSnapshotDto
{
    public long Timestamp { get; init; }
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal Close { get; init; }
    public decimal Volume { get; init; }
    public int NumTrades { get; init; }
}
```

```csharp
// src/TradingApp.Application/Abstractions/Services/IHyperliquidRestClient.cs — modification
// ... existing code ...

    Task<List<CandleDto>> GetCandlesAsync(
        string asset,
        string timeframe,
        long? endTime = null,
        CancellationToken cancellationToken = default);

    Task<List<CandleSnapshotDto>> GetCandleSnapshotsAsync(
        string asset,
        string timeframe,
        long startTime,
        long endTime,
        CancellationToken cancellationToken = default);

// ... existing code ...
```

##### Pattern References

- `src/TradingApp.Application/Abstractions/Services/IHyperliquidRestClient.cs` — existing interface pattern
- `src/TradingApp.Application/MarketData/Models/CandleDto.cs` — existing DTO pattern with `{ get; init; }` properties

---

### Task 1.2: Implement `GetCandlesAsync` overload {#task-12-implement-getcandlesasync-overload}

Implement the new `GetCandleSnapshotsAsync` method in `HyperliquidRestClient`. This method accepts explicit `startTime` and `endTime`, does NOT apply `.Take(500)` or `.OrderByDescending()`, and includes `NumTrades` in the result.

- **Complexity**: Medium
- **Risk Factors**: Must not break the existing `GetCandlesAsync` method; must return all candles from the API response
- **Files**:
  - `src/TradingApp.Infrastructure/Services/HyperliquidRestClient.cs` — Add new method implementation
- **Success**:
  - Method packages `startTime`/`endTime` directly into `CandleSnapshotPayload`
  - Returns all candles from the response without filtering or reordering
  - Maps `HyperliquidCandle.NumTrades` to `CandleSnapshotDto.NumTrades`
- **Dependencies**: Task 1.1

#### Implementation Details

```csharp
// src/TradingApp.Infrastructure/Services/HyperliquidRestClient.cs — modification
// Add after the existing GetCandlesAsync method

    public async Task<List<CandleSnapshotDto>> GetCandleSnapshotsAsync(
        string asset,
        string timeframe,
        long startTime,
        long endTime,
        CancellationToken cancellationToken = default)
    {
        var normalizedTimeframe = timeframe.ToLowerInvariant();
        _ = HyperliquidAssetMapper.GetIntervalMs(normalizedTimeframe); // validate; throws DomainException on invalid
        var coin = HyperliquidAssetMapper.ToCoin(asset);

        var request = new HyperliquidCandleSnapshotRequest
        {
            Req = new CandleSnapshotPayload
            {
                Coin = coin,
                Interval = normalizedTimeframe,
                StartTime = startTime,
                EndTime = endTime,
            },
        };

        var candles = await PostInfoAsync<List<HyperliquidCandle>>(request, cancellationToken);

        return candles
            .Select(c => new CandleSnapshotDto
            {
                Timestamp = c.OpenTime,
                Open = ParseDecimal(c.Open),
                High = ParseDecimal(c.High),
                Low = ParseDecimal(c.Low),
                Close = ParseDecimal(c.Close),
                Volume = ParseDecimal(c.Volume),
                NumTrades = c.NumTrades,
            })
            .OrderBy(c => c.Timestamp)
            .ToList();
    }
```

##### Pattern References

- `src/TradingApp.Infrastructure/Services/HyperliquidRestClient.cs` — existing `GetCandlesAsync` implementation pattern (lines ~228-262)
- `src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidCandle.cs` — wire model with `NumTrades` field
- `src/TradingApp.Infrastructure/Hyperliquid/Models/CandleSnapshotPayload.cs` — already has `StartTime`/`EndTime`

---

### Task 1.3: Create `CandleIngestionOptions` {#task-13-create-candleingestionoptions}

Create the configuration options class following the established `HyperliquidOptions` pattern with `SectionName` constant and data annotations.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.Application/Abstractions/Configuration/CandleIngestionOptions.cs` — New options class
- **Success**:
  - Options class has `SectionName = "CandleIngestion"`
  - All properties have sensible defaults matching the PBI spec
  - Data annotations validate required/range constraints
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Application/Abstractions/Configuration/CandleIngestionOptions.cs — new file
using System.ComponentModel.DataAnnotations;

namespace TradingApp.Application.Abstractions.Configuration;

public sealed class CandleIngestionOptions
{
    public const string SectionName = "CandleIngestion";

    [Range(0, 10000)]
    public int BatchDelayMs { get; set; } = 200;

    [Range(0, 10)]
    public int MaxRetries { get; set; } = 3;

    [Range(60000, 3600000)]
    public int MaxIngestionTimeoutMs { get; set; } = 900000;

    [Required]
    public DateTime DefaultStartDate { get; set; } = new(2022, 11, 1, 0, 0, 0, DateTimeKind.Utc);
}
```

##### Pattern References

- `src/TradingApp.Application/Abstractions/Configuration/HyperliquidOptions.cs` — `SectionName` constant, `[Required]`/data annotation pattern

---

### Task 1.4: Add configuration to appsettings {#task-14-add-configuration-to-appsettings}

Add the `CandleIngestion` section to `appsettings.json` with default values matching the PBI spec.

- **Complexity**: Low
- **Risk Factors**: None — additive JSON change
- **Files**:
  - `src/TradingApp.Api/appsettings.json` — Add `CandleIngestion` section
- **Success**:
  - `CandleIngestion` section exists with `BatchDelayMs`, `MaxRetries`, `MaxIngestionTimeoutMs`, `DefaultStartDate`
- **Dependencies**: None

---

### Task 1.5: Write unit tests for the new `GetCandlesAsync` overload {#task-15-write-unit-tests-for-overload}

Add tests for `GetCandleSnapshotsAsync` in the Infrastructure test project. Since the existing `HyperliquidRestClient` tests live in `TradingApp.Api.Tests` (using `FakeHttpMessageHandler`), place these tests there for consistency.

- **Complexity**: Medium
- **Risk Factors**: Need to mock `PostInfoAsync` or use `FakeHttpMessageHandler`; must verify no `.Take()` or `.OrderByDescending()` is applied
- **Files**:
  - `tests/TradingApp.Api.Tests/Services/HyperliquidRestClientCandleSnapshotTests.cs` — New test class
- **Success**:
  - Tests verify: correct `startTime`/`endTime` in request payload, all candles returned without filtering, `NumTrades` mapped correctly, `DomainException` thrown for invalid timeframe
- **Dependencies**: Tasks 1.1, 1.2

#### Implementation Details

```csharp
// tests/TradingApp.Api.Tests/Services/HyperliquidRestClientCandleSnapshotTests.cs — new file
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingApp.Api.Tests.Infrastructure;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.MarketData.Models;
using TradingApp.Infrastructure.Hyperliquid.Models;
using TradingApp.Infrastructure.Services;

namespace TradingApp.Api.Tests.Services;

[TestClass]
public sealed class HyperliquidRestClientCandleSnapshotTests
{
    private const long StartTime = 1700000000000L;
    private const long EndTime = 1700001800000L;

    private static HyperliquidRestClient CreateClient(HttpResponseMessage response)
    {
        var handler = new FakeHttpMessageHandler(response);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.test.xyz") };
        var options = Options.Create(new HyperliquidOptions
        {
            BaseUrl = "https://api.test.xyz",
            Network = "testnet",
        });
        var logger = new Mock<ILogger<HyperliquidRestClient>>();
        return new HyperliquidRestClient(httpClient, options, logger.Object);
    }

    [TestMethod]
    public async Task GivenValidParams_WhenGetCandleSnapshots_ThenReturnsAllCandlesWithNumTrades()
    {
        // Arrange
        var candles = new List<HyperliquidCandle>
        {
            new() { OpenTime = StartTime, Open = "50000", High = "50100", Low = "49900", Close = "50050", Volume = "100", NumTrades = 143 },
            new() { OpenTime = StartTime + 900000, Open = "50050", High = "50200", Low = "50000", Close = "50150", Volume = "90", NumTrades = 98 },
        };
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(candles)),
        };
        var client = CreateClient(response);

        // Act
        var result = await client.GetCandleSnapshotsAsync("BTC", "15m", StartTime, EndTime);

        // Assert
        result.Should().HaveCount(2);
        result[0].NumTrades.Should().Be(143);
        result[1].NumTrades.Should().Be(98);
        result[0].Timestamp.Should().BeLessThan(result[1].Timestamp); // ordered ascending
    }

    [TestMethod]
    public async Task GivenInvalidTimeframe_WhenGetCandleSnapshots_ThenThrowsDomainException()
    {
        // Arrange
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        var client = CreateClient(response);

        // Act
        Func<Task> act = () => client.GetCandleSnapshotsAsync("BTC", "invalid", StartTime, EndTime);

        // Assert
        await act.Should().ThrowAsync<DomainException>();
    }
}
```

##### Pattern References

- `tests/TradingApp.Api.Tests/Infrastructure/FakeHttpMessageHandler.cs` — HTTP response faking
- `tests/TradingApp.Api.Tests/Services/HyperliquidOrderServiceTests.cs` — service test structure with `Options.Create`

---

### Task 1.6: Build and run tests {#task-16-build-and-run-tests}

Build the solution and run all tests to verify Phase 1 changes compile and pass.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification step)
- **Success**:
  - `dotnet build` succeeds with no errors
  - All existing tests continue to pass
  - New `HyperliquidRestClientCandleSnapshotTests` pass
- **Dependencies**: Tasks 1.1–1.5

## Phase Success Criteria

- `IHyperliquidRestClient` has a new `GetCandleSnapshotsAsync(asset, timeframe, startTime, endTime)` method
- `HyperliquidRestClient` implements the method without `.Take()` or `.OrderByDescending()` filtering
- `CandleSnapshotDto` includes `NumTrades` alongside all OHLCV fields
- `CandleIngestionOptions` is defined with sensible defaults and `SectionName` constant
- `appsettings.json` contains CandleIngestion configuration section
- All tests pass including new overload tests
