<!-- markdownlint-disable-file -->

# Task Details: Binance USDⓈ-M Futures Data Ingestion

## Phase 5: Mark Price Klines

## Standards and Knowledge References

- **csharp.instructions.md**: CancellationToken threading, async/await
- **testing.instructions.md**: MSTest, Moq, FluentAssertions ≤ v6, tests included in phase
- **dotnet-architecture.instructions.md**: Extend existing interfaces and implementations

## Design References

- Binance `GET /fapi/v1/markPriceKlines`: Same response format as regular klines (array of arrays)
- Max 1500 candles per request, weight = 5
- Mark price candles stored with interval prefix convention (e.g., `mark-15m`) in existing `Candle` table
- This avoids schema changes — the `Interval` column already accepts any string

---

### Task 5.1: Add `GetMarkPriceKlinesAsync` to `IBinanceFuturesRestClient` and implementation {#task-51-add-mark-price-klines-to-rest-client}

Extend the Binance REST client interface and implementation with mark price kline support.

- **Complexity**: Low
- **Risk Factors**: None — same response format as regular klines, different endpoint path
- **Files**:
  - `src/TradingApp.Application/Abstractions/Services/IBinanceFuturesRestClient.cs` — Add method
  - `src/TradingApp.Infrastructure/Services/BinanceFuturesRestClient.cs` — Add implementation
- **Success**:
  - `GetMarkPriceKlinesAsync` calls `GET /fapi/v1/markPriceKlines?symbol=X&interval=Y&startTime=Z&limit=1500`
  - Returns `IReadOnlyList<CandleSnapshotDto>` (same type as regular klines)
  - Error mapping reuses existing `MapErrorResponse`
- **Dependencies**: Phase 2 (existing REST client)

#### Implementation Details

```csharp
// src/TradingApp.Application/Abstractions/Services/IBinanceFuturesRestClient.cs — modification
// Add new method to interface:
Task<IReadOnlyList<CandleSnapshotDto>> GetMarkPriceKlinesAsync(
    string futuresSymbol,
    string interval,
    long startTime,
    long? endTime = null,
    int limit = 1500,
    CancellationToken cancellationToken = default);
```

```csharp
// src/TradingApp.Infrastructure/Services/BinanceFuturesRestClient.cs — modification
// Add implementation — reuses same deserialization pattern as GetKlinesAsync:
public async Task<IReadOnlyList<CandleSnapshotDto>> GetMarkPriceKlinesAsync(
    string futuresSymbol,
    string interval,
    long startTime,
    long? endTime = null,
    int limit = 1500,
    CancellationToken cancellationToken = default)
{
    var url = $"/fapi/v1/markPriceKlines?symbol={futuresSymbol}&interval={interval}&startTime={startTime}&limit={limit}";
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

    _logger.LogDebug("Binance mark price klines: {Symbol} {Interval} from {StartTime} — {Count} candles",
        futuresSymbol, interval, startTime, klines.Count);

    return klines;
}
```

Consider extracting the shared deserialization logic into a private method to avoid duplication between `GetKlinesAsync` and `GetMarkPriceKlinesAsync`.

##### Pattern References

- `src/TradingApp.Infrastructure/Services/BinanceFuturesRestClient.cs` — existing `GetKlinesAsync` implementation

---

### Task 5.2: Extend `BinanceCandleIngestionService` for mark price kline ingestion {#task-52-extend-ingestion-service-for-mark-price}

Extend the Binance ingestion service to support mark price kline ingestion. Mark price candles are stored with `mark-` prefixed intervals (e.g., `mark-15m`) in the existing `Candle` table with `Source = "Binance"`.

- **Complexity**: Medium
- **Risk Factors**: Interval prefix must be consistent and well-documented for downstream consumers (backtester)
- **Files**:
  - `src/TradingApp.Infrastructure/Services/BinanceCandleIngestionService.cs` — Add mark price ingestion support
  - `src/TradingApp.Application/Candles/Models/IngestionRequest.cs` — Add `IncludeMarkPrice` flag (if not already a property)
- **Success**:
  - When `IncludeMarkPrice = true`, ingestion also fetches mark price klines for each interval
  - Mark price candles stored with interval = `"mark-{interval}"` (e.g., `"mark-15m"`)
  - Mark price results included in `IngestionResult.Intervals` with prefixed interval names
  - `BinanceAssetMapper.GetIntervalMs` works for mark price intervals too (strip `mark-` prefix)
- **Dependencies**: Task 5.1

#### Implementation Details

```csharp
// src/TradingApp.Application/Candles/Models/IngestionRequest.cs — modification
// Add optional property:
public bool IncludeMarkPrice { get; init; }
```

```csharp
// src/TradingApp.Infrastructure/Services/BinanceCandleIngestionService.cs — modification
// In IngestAsync, after processing regular intervals, add mark price ingestion:

// After regular interval ingestion loop completes:
if (request.IncludeMarkPrice)
{
    foreach (var interval in request.Intervals)
    {
        var markInterval = $"mark-{interval}";
        // Reuse same pagination logic but:
        // - Call _restClient.GetMarkPriceKlinesAsync() instead of GetKlinesAsync()
        // - Store with interval = markInterval
        // - Add result to intervalResults with markInterval name
        var markResult = await IngestIntervalAsync(
            request.Symbol, futuresSymbol, markInterval, interval,
            effectiveStartTime, effectiveEndTime, useMarkPrice: true, timeoutCts.Token);
        intervalResults.Add(markResult);
    }
}
```

The `IngestIntervalAsync` private method should accept a `useMarkPrice` flag to switch between `GetKlinesAsync` and `GetMarkPriceKlinesAsync`. When `useMarkPrice` is true:
- Use `GetMarkPriceKlinesAsync` for fetching
- Store candles with `mark-{interval}` as the interval
- Use `BinanceAssetMapper.GetIntervalMs(baseInterval)` for pagination math (strip `mark-` prefix)

##### Pattern References

- `src/TradingApp.Infrastructure/Services/BinanceCandleIngestionService.cs` — existing interval ingestion loop

---

### Task 5.3: Add `IncludeMarkPrice` parameter to command and API endpoint {#task-53-add-mark-price-api-parameter}

Extend the Binance ingestion command, request model, and API endpoint to support mark price ingestion opt-in.

- **Complexity**: Low
- **Risk Factors**: None — additive change, existing behavior unchanged when flag is false/absent
- **Files**:
  - `src/TradingApp.Api/Models/IngestCandlesRequest.cs` — Add `IncludeMarkPrice` property
  - `src/TradingApp.Api/Controllers/CandlesController.cs` — Pass `IncludeMarkPrice` to command
- **Success**:
  - `POST /api/candles/ingest/binance` with `{ "symbol": "BTC", "intervals": ["15m"], "includeMarkPrice": true }` ingests both trade and mark price klines
  - Without `includeMarkPrice` (or `false`), only trade klines are ingested (existing behavior)
- **Dependencies**: Task 5.2

#### Implementation Details

```csharp
// src/TradingApp.Api/Models/IngestCandlesRequest.cs — modification
// Add optional property:
public bool IncludeMarkPrice { get; set; }
```

```csharp
// src/TradingApp.Api/Controllers/CandlesController.cs — modification
// In IngestBinanceAsync, update IngestionRequest creation:
var ingestionRequest = new IngestionRequest
{
    Symbol = request.Symbol,
    Intervals = request.Intervals,
    StartTime = request.StartTime,
    EndTime = request.EndTime,
    IncludeMarkPrice = request.IncludeMarkPrice
};
```

##### Pattern References

- `src/TradingApp.Api/Controllers/CandlesController.cs` — existing request-to-command mapping in `IngestBinanceAsync`

---

### Task 5.4: Write tests for mark price functionality {#task-54-write-mark-price-tests}

Add tests for mark price kline REST client method, ingestion service mark price path, and controller integration.

- **Complexity**: Medium
- **Risk Factors**: Tests must verify interval prefixing and correct API endpoint selection
- **Files**:
  - `tests/TradingApp.Api.Tests/Services/BinanceFuturesRestClientTests.cs` — Add mark price test
  - `tests/TradingApp.Api.Tests/Services/BinanceCandleIngestionServiceTests.cs` — Add mark price ingestion test
  - `tests/TradingApp.Api.Tests/Controllers/CandlesControllerTests.cs` — Add mark price controller test
- **Success**:
  - REST client test: `GetMarkPriceKlinesAsync` calls correct endpoint `/fapi/v1/markPriceKlines`
  - Ingestion test: `IncludeMarkPrice=true` stores candles with `mark-{interval}` intervals
  - Ingestion test: `IncludeMarkPrice=false` does not call `GetMarkPriceKlinesAsync`
  - Controller test: `includeMarkPrice=true` in request body triggers mark price ingestion
- **Dependencies**: Tasks 5.1–5.3

#### Implementation Details

```csharp
// tests/TradingApp.Api.Tests/Services/BinanceCandleIngestionServiceTests.cs — modification
// Add test:

[TestMethod]
public async Task GivenIncludeMarkPriceTrue_WhenIngestAsync_ThenFetchesMarkPriceKlines()
{
    // Arrange: configure mock to return data for both GetKlinesAsync and GetMarkPriceKlinesAsync
    // Act: call IngestAsync with IncludeMarkPrice = true
    // Assert: GetMarkPriceKlinesAsync was called
    // Assert: stored candles include mark-prefixed intervals
}

[TestMethod]
public async Task GivenIncludeMarkPriceFalse_WhenIngestAsync_ThenSkipsMarkPriceKlines()
{
    // Arrange: only configure GetKlinesAsync mock
    // Act: call IngestAsync with IncludeMarkPrice = false
    // Assert: GetMarkPriceKlinesAsync was NOT called
}
```

```csharp
// tests/TradingApp.Api.Tests/Services/BinanceFuturesRestClientTests.cs — modification
// Add test:

[TestMethod]
public async Task GivenBinanceReturnsMarkPriceKlines_WhenGetMarkPriceKlinesAsync_ThenReturnsCandleSnapshotDtos()
{
    // Verify correct URL: /fapi/v1/markPriceKlines?symbol=BTCUSDT&interval=15m&startTime=...
    // Verify response deserialization
}
```

##### Pattern References

- `tests/TradingApp.Api.Tests/Services/BinanceCandleIngestionServiceTests.cs` — existing ingestion service test pattern
- `tests/TradingApp.Api.Tests/Services/BinanceFuturesRestClientTests.cs` — existing REST client test pattern

---

### Task 5.5: Build and run tests {#task-55-build-and-run-tests}

Build and run all affected test projects.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification step)
- **Success**:
  - `dotnet build tests/TradingApp.Api.Tests` — succeeds
  - `dotnet test tests/TradingApp.Api.Tests` — all tests pass
- **Dependencies**: Task 5.4

## Phase Success Criteria

- `GetMarkPriceKlinesAsync` method added to Binance REST client interface and implementation
- Mark price klines stored with `mark-{interval}` prefix (e.g., `mark-15m`) in `Candle` table
- `IncludeMarkPrice` flag on `IngestionRequest` and API request model
- Mark price ingestion only triggered when explicitly opted in
- All new and existing tests pass
- `POST /api/candles/ingest/binance` with `includeMarkPrice: true` ingests both trade and mark price data
