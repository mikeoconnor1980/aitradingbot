<!-- markdownlint-disable-file -->

# Task Details: Show Trades on Main Chart

## Phase 1: Backend API Extension

## Standards and Knowledge References

- `.github/instructions/api-controllers.instructions.md` — Controller structure, `[FromQuery]` binding patterns, `ProducesResponseType` attributes
- `.github/instructions/csharp.instructions.md` — Async patterns, `CancellationToken` threading, sealed classes
- `.github/instructions/testing.instructions.md` — MSTest, Moq, FluentAssertions, `Given_When_Then` naming, tests within phase
- `.agent-context/0-knowledge/02-hyperliquid-integration.md` — REST/WS client architecture, fill retrieval
- `.agent-context/0-knowledge/10-architecture-decisions.md` — ADR 14: direct service injection for exchange reads

### Task 1.1: Add optional asset parameter to fills endpoint and service interface {#task-11-add-optional-asset-parameter-to-fills-endpoint-and-service-interface}

Add an optional `[FromQuery] string? asset` parameter to the `GetRecentFillsAsync` controller action and thread it through the `IHyperliquidAccountService` interface.

- **Complexity**: Low
- **Risk Factors**: None — straightforward parameter addition following existing nullable `[FromQuery]` patterns
- **Files**:
  - `src/TradePilot.Api/Controllers/AccountController.cs` — Add `[FromQuery] string? asset` parameter to `GetRecentFillsAsync` action
  - `src/TradePilot.Api/Services/IHyperliquidAccountService.cs` — Add `string? asset = null` parameter to interface method
- **Success**:
  - `GET /api/account/fills` continues to work without the asset parameter (backward compatible)
  - `GET /api/account/fills?asset=BTC-PERP` compiles and passes the asset value to the service
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradePilot.Api/Controllers/AccountController.cs — modification
// ... existing code ...
[HttpGet("fills")]
[ProducesResponseType(typeof(IReadOnlyList<FillEventDto>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
[ProducesResponseType(typeof(Envelope), StatusCodes.Status502BadGateway)]
public async Task<IActionResult> GetRecentFillsAsync(
    [FromQuery] string? asset,
    CancellationToken cancellationToken)
{
    var fills = await _accountService.GetRecentFillsAsync(asset, cancellationToken);
    return Ok(fills);
}
// ... existing code ...
```

```csharp
// src/TradePilot.Api/Services/IHyperliquidAccountService.cs — modification
public interface IHyperliquidAccountService
{
    // ... existing methods ...
    Task<IReadOnlyList<FillEventDto>> GetRecentFillsAsync(
        string? asset = null,
        CancellationToken cancellationToken = default);
}
```

##### Pattern References

- `src/TradePilot.Api/Controllers/BacktestsController.cs` — nullable `[FromQuery] string? symbol` parameter pattern
- `src/TradePilot.Api/Controllers/MarketDataController.cs` — `[FromQuery]` asset parameter for filtering

---

### Task 1.2: Implement asset filtering in HyperliquidAccountService {#task-12-implement-asset-filtering-in-hyperliquidaccountservice}

Update `HyperliquidAccountService.GetRecentFillsAsync` to accept the optional `asset` parameter, convert it via `HyperliquidAssetMapper.ToCoin()`, and filter results. When `asset` is provided, use `userFills` (all-time, no startTime) to ensure all historical fills for the asset are returned. When `asset` is null, keep the existing 24-hour lookback behavior.

- **Complexity**: Medium
- **Risk Factors**: Asset name conversion must use `HyperliquidAssetMapper.ToCoin()` to map display name ("BTC-PERP") to coin symbol ("BTC") used in `FillEventDto.Asset`
- **Files**:
  - `src/TradePilot.Api/Services/HyperliquidAccountService.cs` — Implement asset parameter, filtering, and conditional time window
- **Success**:
  - `GetRecentFillsAsync(null)` returns same result as before (24h lookback, all assets)
  - `GetRecentFillsAsync("BTC-PERP")` returns all-time fills filtered to asset "BTC" only
  - `GetRecentFillsAsync("ETH-PERP")` returns all-time fills filtered to asset "ETH" only
- **Dependencies**: Task 1.1 (interface change)

#### Implementation Details

```csharp
// src/TradePilot.Api/Services/HyperliquidAccountService.cs — modification
using TradePilot.Infrastructure.Hyperliquid;

public async Task<IReadOnlyList<FillEventDto>> GetRecentFillsAsync(
    string? asset = null,
    CancellationToken cancellationToken = default)
{
    // When asset is provided, fetch all-time fills; otherwise keep 24h window
    long? startTime = asset is null
        ? DateTimeOffset.UtcNow.AddHours(-24).ToUnixTimeMilliseconds()
        : null;

    var fills = await _restClient.GetUserFillsAsync(
        _signer.WalletAddress, startTime, cancellationToken);

    if (asset is not null)
    {
        var coin = HyperliquidAssetMapper.ToCoin(asset);
        fills = fills
            .Where(f => f.Asset.Equals(coin, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    return fills;
}
```

##### Pattern References

- `src/TradePilot.Api/Services/HyperliquidAccountService.cs` — existing `GetRecentFillsAsync` implementation
- `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidAssetMapper.cs` — `ToCoin("BTC-PERP")` → `"BTC"`

---

### Task 1.3: Remove Take(50) cap from HyperliquidRestClient.GetUserFillsAsync {#task-13-remove-take50-cap-from-hyperliquidrestclientgetuserfillsasync}

Remove the hardcoded `.Take(50)` in `HyperliquidRestClient.GetUserFillsAsync`. The cap was previously applied before any asset filter, which could silently drop fills for a specific asset. With asset filtering now in the service layer, the cap is no longer needed — fill volume is naturally bounded by trading activity.

- **Complexity**: Low
- **Risk Factors**: Removing the cap means more fills may be returned when no asset filter is applied (e.g. activity feed). This is acceptable — the PBI's non-functional requirement states "fills are few relative to candles".
- **Files**:
  - `src/TradePilot.Infrastructure/Services/HyperliquidRestClient.cs` — Remove `.Take(50)` from the LINQ chain
- **Success**:
  - `GetUserFillsAsync` returns all fills matching the time window, ordered by timestamp descending
  - No `Take()` cap in the method
- **Dependencies**: None (can be done independently)

#### Implementation Details

```csharp
// src/TradePilot.Infrastructure/Services/HyperliquidRestClient.cs — modification
// In GetUserFillsAsync, change the return statement from:
return fills
    .Select(f => new FillEventDto { /* ... */ })
    .OrderByDescending(f => f.Timestamp)
    .Take(50)
    .ToList();

// To:
return fills
    .Select(f => new FillEventDto { /* ... */ })
    .OrderByDescending(f => f.Timestamp)
    .ToList();
```

##### Pattern References

- `src/TradePilot.Infrastructure/Services/HyperliquidRestClient.cs` — existing `GetUserFillsAsync` implementation

---

### Task 1.4: Update AccountController tests for asset filtering {#task-14-update-accountcontroller-tests-for-asset-filtering}

Add test cases to `AccountControllerTests` verifying the new asset query parameter: filtering by asset returns only matching fills, omitting the parameter returns all fills (backward compatibility), and an asset with no fills returns an empty list.

- **Complexity**: Medium
- **Risk Factors**: `AccountControllerTests` uses its own `WebApplicationFactory` pattern (not `BaseControllerTests`). New tests must follow the existing class's pattern.
- **Files**:
  - `tests/TradePilot.Api.Tests/Controllers/AccountControllerTests.cs` — Add new test methods
- **Success**:
  - Test `GivenFillsForMultipleAssets_WhenGetFillsWithAssetFilter_ThenReturnsOnlyMatchingFills` passes
  - Test `GivenFillsForMultipleAssets_WhenGetFillsWithoutAssetFilter_ThenReturnsAllFills` passes
  - Test `GivenNoFillsForAsset_WhenGetFillsWithAssetFilter_ThenReturnsEmptyList` passes
- **Dependencies**: Tasks 1.1, 1.2

#### Implementation Details

```csharp
// tests/TradePilot.Api.Tests/Controllers/AccountControllerTests.cs — new test methods

[TestMethod]
public async Task GivenFillsForMultipleAssets_WhenGetFillsWithAssetFilter_ThenReturnsOnlyMatchingFills()
{
    // Arrange
    IReadOnlyList<FillEventDto> fills = new List<FillEventDto>
    {
        new() { Timestamp = DateTime.UtcNow, Asset = "BTC", Side = "Buy", Direction = "Open Long",
                Size = 0.1m, Price = 65000m, Fee = 0.01m, ClosedPnl = 0m, OrderId = "order-1" },
        new() { Timestamp = DateTime.UtcNow, Asset = "ETH", Side = "Sell", Direction = "Close Long",
                Size = 1m, Price = 3200m, Fee = 0.02m, ClosedPnl = 50m, OrderId = "order-2" },
        new() { Timestamp = DateTime.UtcNow, Asset = "BTC", Side = "Sell", Direction = "Close Long",
                Size = 0.1m, Price = 66000m, Fee = 0.01m, ClosedPnl = 100m, OrderId = "order-3" },
    };
    _accountServiceMock
        .Setup(s => s.GetRecentFillsAsync("BTC-PERP", It.IsAny<CancellationToken>()))
        .ReturnsAsync(fills.Where(f => f.Asset == "BTC").ToList());

    // Act
    var response = await _client.GetAsync("api/account/fills?asset=BTC-PERP");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var result = await response.Content.ReadFromJsonAsync<List<FillEventDto>>();
    result.Should().HaveCount(2);
    result.Should().OnlyContain(f => f.Asset == "BTC");
}

[TestMethod]
public async Task GivenFillsForMultipleAssets_WhenGetFillsWithoutAssetFilter_ThenReturnsAllFills()
{
    // Arrange
    IReadOnlyList<FillEventDto> fills = new List<FillEventDto>
    {
        new() { Timestamp = DateTime.UtcNow, Asset = "BTC", Side = "Buy", Direction = "Open Long",
                Size = 0.1m, Price = 65000m, Fee = 0.01m, ClosedPnl = 0m, OrderId = "order-1" },
        new() { Timestamp = DateTime.UtcNow, Asset = "ETH", Side = "Sell", Direction = "Close Long",
                Size = 1m, Price = 3200m, Fee = 0.02m, ClosedPnl = 50m, OrderId = "order-2" },
    };
    _accountServiceMock
        .Setup(s => s.GetRecentFillsAsync(null, It.IsAny<CancellationToken>()))
        .ReturnsAsync(fills);

    // Act
    var response = await _client.GetAsync("api/account/fills");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var result = await response.Content.ReadFromJsonAsync<List<FillEventDto>>();
    result.Should().HaveCount(2);
}

[TestMethod]
public async Task GivenNoFillsForAsset_WhenGetFillsWithAssetFilter_ThenReturnsEmptyList()
{
    // Arrange
    _accountServiceMock
        .Setup(s => s.GetRecentFillsAsync("SOL-PERP", It.IsAny<CancellationToken>()))
        .ReturnsAsync(new List<FillEventDto>());

    // Act
    var response = await _client.GetAsync("api/account/fills?asset=SOL-PERP");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var result = await response.Content.ReadFromJsonAsync<List<FillEventDto>>();
    result.Should().BeEmpty();
}
```

##### Pattern References

- `tests/TradePilot.Api.Tests/Controllers/AccountControllerTests.cs` — existing `GivenRecentFills_WhenGetFills_ThenReturnsSemanticFillFields` test pattern

---

### Task 1.5: Build and run backend tests {#task-15-build-and-run-backend-tests}

Build the solution and run all affected test projects to verify the changes compile and all tests pass.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: No file changes
- **Success**:
  - `dotnet build TradePilot.sln` succeeds with no errors
  - `dotnet test tests/TradePilot.Api.Tests` passes all tests including the new ones
- **Dependencies**: Tasks 1.1–1.4

## Phase Success Criteria

- `GET /api/account/fills` continues to return all recent fills (backward compatible)
- `GET /api/account/fills?asset=BTC-PERP` returns only BTC fills (all-time, no 24h limit)
- `Take(50)` cap removed from `HyperliquidRestClient`
- All existing and new backend tests pass
- Solution builds cleanly
