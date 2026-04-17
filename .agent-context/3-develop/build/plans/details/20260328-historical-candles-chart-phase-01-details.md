<!-- markdownlint-disable-file -->

# Phase 1 Details: Backend — New CQRS Query & API Endpoint

## Standards & Knowledge References

- `.github/instructions/csharp.instructions.md` — C# coding standards
- `.github/instructions/dotnet-architecture.instructions.md` — Clean architecture layers
- `.github/instructions/api-controllers.instructions.md` — Controller conventions
- `.github/instructions/testing.instructions.md` — Test conventions

---

## Task 1.1: Create `GetHistoricalCandlesQuery` and handler

**Complexity**: Low | **Risk**: Low

### Files

| Action | File |
|--------|------|
| New | `src/TradePilot.Application/MarketData/Queries/GetHistoricalCandlesQuery.cs` |

### Implementation Details

Create a new CQRS query and handler that reads candles from `ICandleRepository` instead of the Hyperliquid API.

**Query record:**

```csharp
public sealed record GetHistoricalCandlesQuery(
    string Asset,
    string Timeframe,
    long? EndTime = null,
    int Limit = 500) : Query<List<CandleDto>>;
```

**Handler logic:**

1. Validate `Asset` and `Timeframe` are not null/whitespace
2. Map `Asset` to `Symbol` — strip the `-PERP` suffix (e.g., `"BTC-PERP"` → `"BTC"`)
3. Map `Timeframe` to `Interval` — these should match (e.g., `"15m"` → `"15m"`)
4. Calculate time range:
   - If `EndTime` is provided, use it as the upper bound
   - If not, use `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()`
   - Calculate `StartTime` = `EndTime` minus `Limit` candles worth of time (e.g., `Limit * timeframeMs`)
5. Call `ICandleRepository.GetCandlesAsync(symbol, interval, startTime, endTime)`
6. Map `Candle` entities to `CandleDto` list
7. Cap results to `Limit` (take last N if more returned)

**Timeframe-to-milliseconds mapping** (private static dictionary in handler):

```csharp
private static readonly Dictionary<string, long> TimeframeMs = new()
{
    ["1m"] = 60_000L,
    ["3m"] = 180_000L,
    ["5m"] = 300_000L,
    ["15m"] = 900_000L,
    ["30m"] = 1_800_000L,
    ["1h"] = 3_600_000L,
    ["4h"] = 14_400_000L,
    ["1d"] = 86_400_000L,
};
```

**Entity-to-DTO mapping:**

```csharp
private static CandleDto MapToDto(Candle candle) => new()
{
    Timestamp = candle.Timestamp,
    Open = candle.Open,
    High = candle.High,
    Low = candle.Low,
    Close = candle.Close,
    Volume = candle.Volume,
};
```

### Pattern Reference

Follow the exact pattern of `GetCandlesQuery` / `GetCandlesQueryHandler` in `src/TradePilot.Application/MarketData/Queries/GetCandlesQuery.cs` — sealed record extending `Query<T>`, sealed handler extending `QueryHandler<TQuery, TResult>`.

### Success Criteria

- Query handler compiles and resolves `ICandleRepository` via constructor injection
- Maps asset names correctly (strips `-PERP`)
- Returns candles ordered by timestamp ascending
- Respects `Limit` parameter (default 500)
- Returns empty list when no data found (not an exception)

---

## Task 1.2: Add `GetHistoricalCandlesAsync` endpoint to `MarketDataController`

**Complexity**: Low | **Risk**: Low

### Files

| Action | File |
|--------|------|
| Modified | `src/TradePilot.Api/Controllers/MarketDataController.cs` |

### Implementation Details

Add a new endpoint to the existing `MarketDataController`:

```csharp
[HttpGet("candles/history")]
[ProducesResponseType(typeof(List<CandleDto>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
public async Task<IActionResult> GetHistoricalCandlesAsync(
    [FromQuery][Required] string asset,
    [FromQuery][Required] string timeframe,
    [FromQuery] long? endTime,
    [FromQuery] int limit = 500,
    CancellationToken cancellationToken = default)
{
    var result = await Mediator.Send(
        new GetHistoricalCandlesQuery(asset, timeframe, endTime, limit),
        cancellationToken);
    return Ok(result);
}
```

**Route**: `GET /api/market/candles/history?asset=BTC-PERP&timeframe=15m&endTime=<optional>&limit=500`

### Pattern Reference

Follow the exact same pattern as the existing `GetCandlesAsync` endpoint directly above. Same response types, same parameter style.

### Success Criteria

- Endpoint registered at `GET /api/market/candles/history`
- Accepts `asset`, `timeframe`, `endTime`, `limit` query parameters
- Returns `List<CandleDto>` (same shape as existing candles endpoint)
- MediatR dispatches to `GetHistoricalCandlesQueryHandler`

---

## Task 1.3: Write unit tests for `GetHistoricalCandlesQueryHandler`

**Complexity**: Low | **Risk**: Low

### Files

| Action | File |
|--------|------|
| New | `tests/TradePilot.Application.Tests/MarketData/Queries/GetHistoricalCandlesQueryHandlerTests.cs` |

### Implementation Details

Test cases:

1. **Returns candles from repository mapped to DTOs** — mock `ICandleRepository` returning sample candles, verify DTOs match
2. **Maps asset name correctly** — verify `"BTC-PERP"` is passed to repo as `"BTC"`
3. **Uses default limit of 500** — verify at most 500 results returned
4. **Calculates time range from endTime and limit** — verify correct startTime calculation
5. **Returns empty list when no data** — mock repo returning empty list, verify empty list (not exception)
6. **Throws for null/empty asset** — verify `ArgumentException`
7. **Throws for null/empty timeframe** — verify `ArgumentException`

### Pattern Reference

Follow existing test patterns. Use `NSubstitute` for mocking `ICandleRepository`. Follow Arrange/Act/Assert structure.

### Success Criteria

- All test cases pass
- Handler logic is fully covered: mapping, validation, time range calculation

---

## Task 1.4: Write controller integration tests

**Complexity**: Low | **Risk**: Low

### Files

| Action | File |
|--------|------|
| New or Modified | `tests/TradePilot.Api.Tests/Controllers/MarketDataControllerTests.cs` (add tests for new endpoint) |

### Implementation Details

Test cases for `GET /api/market/candles/history`:

1. **Returns 200 with candle data** — seed DB with candles, call endpoint, verify response shape
2. **Returns 200 with empty array when no data** — call endpoint for symbol with no data
3. **Respects limit parameter** — seed 1000 candles, request limit=100, verify 100 returned
4. **Respects endTime parameter** — seed candles, request with endTime, verify only older candles returned
5. **Returns 400 for missing asset** — call without asset parameter
6. **Returns 400 for missing timeframe** — call without timeframe parameter

### Pattern Reference

Follow existing controller test patterns in the project. Use `WebApplicationFactory` if available, or direct controller instantiation with mocked dependencies.

### Success Criteria

- All integration tests pass
- Endpoint behaviour verified for happy paths and validation errors

---

## Task 1.5: Build solution and run all tests

**Complexity**: Low | **Risk**: Low

### Implementation Details

1. Run `dotnet build TradePilot.sln` — must compile with zero errors
2. Run `dotnet test TradePilot.sln` — all existing and new tests must pass
3. Fix any compilation or test failures before proceeding to Phase 2

### Success Criteria

- Solution builds cleanly
- All tests pass (existing + new)
