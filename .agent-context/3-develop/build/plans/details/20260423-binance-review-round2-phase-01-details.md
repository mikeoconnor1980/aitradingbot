<!-- markdownlint-disable-file -->

# Task Details: Binance Integration Review Round 2 Fixes

## Phase 1: Cancel-After-Restart — Rehydrate Order Map from Exchange State

## Standards and Knowledge References

- **C# Standards** (`.github/instructions/csharp.instructions.md`): Use `sealed` classes, PascalCase naming, underscore-prefixed private fields. No regions.
- **Testing Standards** (`.github/instructions/testing.instructions.md`): MSTest framework, Moq for mocking, FluentAssertions ≤ v6 for assertions. Given_When_Then naming.
- **.NET Architecture** (`.github/instructions/dotnet-architecture.instructions.md`): Domain exceptions for business rules, infrastructure services for external API calls.
- **Exchange Abstraction** (`.agent-context/0-knowledge/38-exchange-abstraction-architecture.md`): All `IExecutionEngine` implementations must follow the same cancel contract. Hyperliquid uses silent-return (log warning + return) for unknown orders — Binance must match.
- **Binance Integration** (`.agent-context/0-knowledge/23-binance-integration.md`): `BinanceExecutionEngine._orderAssetMap` is in-memory only. `GetOpenOrdersAsync(null)` returns all open orders from Binance.

## Design References

- **IExecutionEngine contract**: Two cancel overloads — `CancelOrderAsync(string orderId, CancellationToken)` and `CancelOrderAsync(string orderId, string asset, CancellationToken)`. The single-arg overload must resolve asset from the order-asset map.
- **Hyperliquid pattern** (`LiveExecutionEngine.cs` line ~207): When `_orderAssetMap.TryGetValue` fails, logs `LogWarning` and returns silently. This is the target behavior.
- **BinanceOpenOrderSnapshot**: Has `OrderId` (long) and `Symbol` (string, e.g. `"BTCUSDT"`). Rehydration extracts `orderId.ToString(CultureInfo.InvariantCulture)` and `BinanceAssetMapper.NormalizeSymbol(symbol)`.

### Task 1.1: Add order map rehydration from Binance open orders {#task-11-add-order-map-rehydration}

Add a lazy rehydration mechanism to `BinanceExecutionEngine` that rebuilds the `_orderAssetMap` from Binance's open orders on first miss. This ensures that after a process restart, the engine can discover all pre-restart orders.

- **Complexity**: High
- **Risk Factors**: Must handle concurrent first-miss triggers safely (use `SemaphoreSlim`). Must use the correct asset normalizer. Must not fail construction if credentials aren't available yet.
- **Files**:
  - `src/TradePilot.Infrastructure/Binance/BinanceExecutionEngine.cs` — Add rehydration fields and method
- **Success**:
  - `RehydrateOrderMapAsync` calls `_authClient.GetOpenOrdersAsync(null)` and populates `_orderAssetMap` for every open order
  - Rehydration runs at most once (guarded by `_rehydrated` flag + `SemaphoreSlim`)
  - After rehydration, the map contains all open orders' `orderId → asset` mappings
- **Dependencies**:
  - None — this is the first task in Phase 1

#### Implementation Details

```csharp
// src/TradePilot.Infrastructure/Binance/BinanceExecutionEngine.cs — modification

// Add new fields after existing `_orderAssetMap` field:
// ... existing code ...
private readonly ConcurrentDictionary<string, string> _orderAssetMap = new();
private readonly SemaphoreSlim _rehydrationLock = new(1, 1);
private volatile bool _rehydrated;
// ... existing code ...

// Add new private method after the existing CancelOrderAsync methods:
private async Task RehydrateOrderMapAsync(CancellationToken cancellationToken)
{
    if (_rehydrated)
        return;

    await _rehydrationLock.WaitAsync(cancellationToken);
    try
    {
        if (_rehydrated)
            return;

        var openOrders = await _authClient.GetOpenOrdersAsync(symbol: null, cancellationToken);

        foreach (var order in openOrders)
        {
            var orderId = order.OrderId.ToString(CultureInfo.InvariantCulture);
            var asset = BinanceAssetMapper.NormalizeSymbol(order.Symbol);
            _orderAssetMap.TryAdd(orderId, asset);
        }

        _rehydrated = true;
        _logger.LogInformation("Rehydrated order-asset map with {Count} open orders from Binance.", openOrders.Count);
    }
    finally
    {
        _rehydrationLock.Release();
    }
}
```

##### Pattern References

- `BinanceAccountAdapter.MapOpenOrder` — uses `order.OrderId.ToString(CultureInfo.InvariantCulture)` and `ToAsset(order.Symbol)` (which calls `BinanceAssetMapper.NormalizeSymbol`) for the same `BinanceOpenOrderSnapshot` → string key/value conversion.
- `LiveExecutionEngine` — no rehydration exists there, but it also has the same `_orderAssetMap` in-memory pattern. This is the first engine to add rehydration.

---

### Task 1.2: Align CancelOrderAsync contract — log warning + return instead of throw {#task-12-align-cancel-contract}

Change `CancelOrderAsync(string orderId)` to attempt rehydration on a map miss and fall back to a warning+return if the order is still unknown. This matches the Hyperliquid contract.

- **Complexity**: Medium
- **Risk Factors**: Must catch rehydration failures (network errors) gracefully. Must not break the 2-arg overload. Edge case: order exists on exchange but rehydration network call fails — should still silently return, not throw.
- **Files**:
  - `src/TradePilot.Infrastructure/Binance/BinanceExecutionEngine.cs` — Modify `CancelOrderAsync(string orderId, CancellationToken)`
- **Success**:
  - No `DomainException` thrown for unknown orders
  - Rehydration is attempted once on first map miss
  - If rehydration succeeds and order is found in map, cancel proceeds normally
  - If rehydration fails or order still not in map, logs `LogWarning` and returns silently
  - Callers (`TriggerOrderManager`, `FillProcessor`) see no behavior change — they already catch exceptions
- **Dependencies**:
  - Task 1.1 (RehydrateOrderMapAsync must exist)

#### Implementation Details

```csharp
// src/TradePilot.Infrastructure/Binance/BinanceExecutionEngine.cs — modification
// Replace the existing CancelOrderAsync(string orderId, CancellationToken) method:

public async Task CancelOrderAsync(string orderId, CancellationToken cancellationToken = default)
{
    if (_orderAssetMap.TryGetValue(orderId, out var asset))
    {
        await CancelOrderAsync(orderId, asset, cancellationToken);
        return;
    }

    // Map miss — attempt rehydration from exchange
    try
    {
        await RehydrateOrderMapAsync(cancellationToken);
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to rehydrate order-asset map from Binance. Cannot cancel order {OrderId}.", orderId);
        return;
    }

    if (_orderAssetMap.TryGetValue(orderId, out asset))
    {
        await CancelOrderAsync(orderId, asset, cancellationToken);
        return;
    }

    _logger.LogWarning("Cannot cancel order {OrderId}: asset mapping not found after rehydration.", orderId);
}
```

##### Pattern References

- `LiveExecutionEngine.CancelOrderAsync` (line ~207) — the target pattern:
  ```csharp
  if (!_orderAssetMap.TryGetValue(orderId, out var asset))
  {
      _logger.LogWarning("Cannot cancel order {OrderId}: asset mapping not found.", orderId);
      return;
  }
  ```
- The Binance version adds the rehydration step between the initial miss and the final warning, which the Hyperliquid version doesn't need (Hyperliquid uses websocket-based state recovery).

---

### Task 1.3: Update unit tests for cancel behavior change {#task-13-update-cancel-tests}

Update the existing cancel test that asserts `DomainException` and add new tests for the rehydration path.

- **Complexity**: Medium
- **Risk Factors**: Mock setup for `GetOpenOrdersAsync` must use `MockBehavior.Strict` (existing pattern). Need to verify rehydration only runs once even when called concurrently.
- **Files**:
  - `tests/TradePilot.Infrastructure.Tests/Binance/BinanceExecutionEngineTests.cs` — Update and add cancel tests
- **Success**:
  - Existing `GivenUnknownOrderId_WhenCancelOrderAsync_ThenThrowsDomainException` is renamed and updated to assert no exception + LogWarning
  - New test: rehydration from open orders succeeds and cancel proceeds
  - New test: rehydration failure is handled gracefully
  - All existing tests continue to pass (mock setup for `GetOpenOrdersAsync` added where needed)
- **Dependencies**:
  - Tasks 1.1 and 1.2 (the production code changes must be in place)

#### Implementation Details

```csharp
// tests/TradePilot.Infrastructure.Tests/Binance/BinanceExecutionEngineTests.cs — modification

// 1. Rename and update existing test:
// OLD: GivenUnknownOrderId_WhenCancelOrderAsync_ThenThrowsDomainException
// NEW:
[TestMethod]
public async Task GivenUnknownOrderId_WhenCancelOrderAsync_ThenRehydratesAndLogsWarning()
{
    // Arrange — GetOpenOrdersAsync returns empty list (order not on exchange either)
    _authClientMock
        .Setup(c => c.GetOpenOrdersAsync(null, It.IsAny<CancellationToken>()))
        .ReturnsAsync(Array.Empty<BinanceOpenOrderSnapshot>());

    // Act — should NOT throw
    await _sut.CancelOrderAsync("unknown-order");

    // Assert
    _authClientMock.Verify(c => c.GetOpenOrdersAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    _loggerMock.Verify(
        x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("asset mapping not found after rehydration")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
        Times.Once);
}

// 2. New test — rehydration finds the order and cancel succeeds:
[TestMethod]
public async Task GivenRestartedProcess_WhenCancelOrderForPreRestartOrder_ThenRehydratesAndCancels()
{
    // Arrange — order exists on Binance
    var openOrders = new[]
    {
        new BinanceOpenOrderSnapshot { OrderId = 12345, Symbol = "BTCUSDT" }
    };
    _authClientMock
        .Setup(c => c.GetOpenOrdersAsync(null, It.IsAny<CancellationToken>()))
        .ReturnsAsync(openOrders);
    _authClientMock
        .Setup(c => c.CancelOrderAsync("BTCUSDT", 12345L, It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);

    // Act
    await _sut.CancelOrderAsync("12345");

    // Assert — rehydration called, then cancel called
    _authClientMock.Verify(c => c.GetOpenOrdersAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    _authClientMock.Verify(c => c.CancelOrderAsync("BTCUSDT", 12345L, It.IsAny<CancellationToken>()), Times.Once);
}

// 3. New test — rehydration network failure handled gracefully:
[TestMethod]
public async Task GivenRehydrationFailure_WhenCancelOrderAsync_ThenLogsWarningAndReturns()
{
    // Arrange — GetOpenOrdersAsync throws (network failure)
    _authClientMock
        .Setup(c => c.GetOpenOrdersAsync(null, It.IsAny<CancellationToken>()))
        .ThrowsAsync(new HttpRequestException("Connection refused"));

    // Act — should NOT throw
    await _sut.CancelOrderAsync("orphaned-order");

    // Assert
    _loggerMock.Verify(
        x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to rehydrate")),
            It.IsAny<HttpRequestException>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
        Times.Once);
}
```

**Note**: The existing test `GivenTrackedOrderId_WhenCancelOrderAsync_ThenUsesTrackedAssetMapping` should continue passing without modification because the order was placed through `PlaceOrderAsync` (which populates `_orderAssetMap`), so the first `TryGetValue` succeeds and rehydration is never triggered. However, since `MockBehavior.Strict` is used, you may need to add a default setup for `GetOpenOrdersAsync` in `[TestInitialize]` if any existing test triggers the rehydration path unexpectedly:

```csharp
// In [TestInitialize] — add default setup:
_authClientMock
    .Setup(c => c.GetOpenOrdersAsync(null, It.IsAny<CancellationToken>()))
    .ReturnsAsync(Array.Empty<BinanceOpenOrderSnapshot>());
```

##### Pattern References

- Existing test `GivenUnknownOrderId_WhenCancelOrderAsync_ThenThrowsDomainException` — the test we're renaming/replacing
- Existing test `GivenTrackedOrderId_WhenCancelOrderAsync_ThenUsesTrackedAssetMapping` — pattern for place-then-cancel flow
- Logger mock verification pattern — standard Moq `.Verify(x => x.Log(...))` for `ILogger`

---

### Task 1.4: Build and verify all tests pass {#task-14-build-and-verify}

Build the solution and run all tests to verify no regressions.

- **Complexity**: Low
- **Risk Factors**: Strict mocks may fail if `GetOpenOrdersAsync` is called unexpectedly in existing tests.
- **Files**:
  - Solution level — all projects
- **Success**:
  - `dotnet build TradePilot.sln` succeeds with no errors
  - `dotnet test TradePilot.sln` — all tests pass
  - No new warnings in modified files
- **Dependencies**:
  - Tasks 1.1, 1.2, 1.3

## Phase Success Criteria

- `CancelOrderAsync(orderId)` no longer throws `DomainException` for unknown orders
- After process restart, rehydration from `GetOpenOrdersAsync` populates the order-asset map
- If rehydration fails, cancel gracefully logs and returns (no crash, no unhandled exception)
- Behavior matches `LiveExecutionEngine` (Hyperliquid) — silent-return pattern
- All existing tests pass, new tests verify rehydration success/failure paths
- Solution builds without errors
