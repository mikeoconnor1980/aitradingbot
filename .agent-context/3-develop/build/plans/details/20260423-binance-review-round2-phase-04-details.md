<!-- markdownlint-disable-file -->

# Task Details: Binance Integration Review Round 2 Fixes

## Phase 4: Structurally Correct Order Normalization

## Standards and Knowledge References

- **C# Standards** (`.github/instructions/csharp.instructions.md`): Use `sealed` classes with `init`-only properties. Keep computed properties on records.
- **Testing Standards** (`.github/instructions/testing.instructions.md`): MSTest framework, Moq for mocking, FluentAssertions ≤ v6 for assertions. Given_When_Then naming.
- **.NET Architecture** (`.github/instructions/dotnet-architecture.instructions.md`): Domain model records with value semantics.
- **Exchange Abstraction** (`.agent-context/0-knowledge/38-exchange-abstraction-architecture.md`): `BinanceExchangeSymbolMetadata` is the source record cached by `BinanceExchangeInfoCache` and consumed by `BinanceExecutionEngine` and `BinanceSymbolMetadataProvider`.

## Design References

- **Current normalization** (`BinanceExecutionEngine.NormalizeOrderSize/NormalizeOrderPrice`): Uses `int sizeDecimals`/`int priceDecimals` parameters with `(decimal)Math.Pow(10, n)` for truncation. Only correct for power-of-ten step/tick sizes.
- **Correct approach**: Use raw `decimal stepSize`/`decimal tickSize` with modular arithmetic: `Math.Truncate(value / step) * step`. This works for any step size (e.g., 0.025, 0.1, 0.001).
- **Current metadata record**: `BinanceExchangeSymbolMetadata(string Asset, string Symbol, int SizeDecimals, int PriceDecimals, int MaxLeverage)`.
- **Target metadata record**: Store raw `StepSize`/`TickSize` as `decimal`, expose `SizeDecimals`/`PriceDecimals` as computed properties for backward compatibility.
- **Consumers** of `BinanceExchangeSymbolMetadata`:
  - `BinanceExecutionEngine` — uses `SizeDecimals`/`PriceDecimals` for normalization → will switch to `StepSize`/`TickSize`
  - `BinanceSymbolMetadataProvider` — maps to `ExchangeSymbolMetadata(Asset, Symbol, SizeDecimals, PriceDecimals, MaxLeverage)` → uses computed properties, no change needed
  - `BinanceExchangeInfoCacheTests` — constructs metadata records in assertions → constructor call changes
  - `BinanceExecutionEngineTests.SetupExchangeMetadata` — constructs metadata records → constructor call changes
  - `BinanceSymbolMetadataProviderTests` — constructs metadata records → constructor call changes

### Task 4.1: Store raw step/tick size in metadata instead of decimal counts {#task-41-store-raw-step-tick-size}

Change `BinanceExchangeSymbolMetadata` from positional record with `int SizeDecimals, int PriceDecimals` to a record storing `decimal StepSize, decimal TickSize`. Add computed properties for backward compatibility with `SizeDecimals`/`PriceDecimals`.

- **Complexity**: Medium
- **Risk Factors**: All constructors across production and test code must be updated. The computed `CountDecimals` method must match the behavior of the removed `GetDecimals` method.
- **Files**:
  - `src/TradePilot.Application/Abstractions/Services/IBinanceExchangeInfoCache.cs` — Change record definition
  - `src/TradePilot.Infrastructure/Binance/BinanceExchangeInfoCache.cs` — Update record construction, remove `GetDecimals` helper
- **Success**:
  - `BinanceExchangeSymbolMetadata` stores raw `StepSize` and `TickSize` as `decimal`
  - `SizeDecimals` and `PriceDecimals` are computed from their respective step/tick sizes
  - `BinanceExchangeInfoCache.EnsureCacheAsync` constructs metadata with raw step/tick values
  - `GetDecimals` helper is removed from the cache class
- **Dependencies**:
  - None — but should be implemented after Phase 2 (since Phase 2 also modifies cache constructor and `EnsureCacheAsync`)

#### Implementation Details

```csharp
// src/TradePilot.Application/Abstractions/Services/IBinanceExchangeInfoCache.cs — modification
// Replace the existing record:

// BEFORE:
// public sealed record BinanceExchangeSymbolMetadata(
//     string Asset,
//     string Symbol,
//     int SizeDecimals,
//     int PriceDecimals,
//     int MaxLeverage);

// AFTER:
public sealed record BinanceExchangeSymbolMetadata(
    string Asset,
    string Symbol,
    decimal StepSize,
    decimal TickSize,
    int MaxLeverage)
{
    public int SizeDecimals => CountDecimals(StepSize);
    public int PriceDecimals => CountDecimals(TickSize);

    private static int CountDecimals(decimal value)
    {
        if (value <= 0m)
        {
            return 0;
        }

        var text = value.ToString("G29", System.Globalization.CultureInfo.InvariantCulture);
        var separatorIndex = text.IndexOf('.');
        return separatorIndex >= 0 ? text.Length - separatorIndex - 1 : 0;
    }
}
```

```csharp
// src/TradePilot.Infrastructure/Binance/BinanceExchangeInfoCache.cs — modification

// 1. In EnsureCacheAsync, update the metadata construction in the symbol loop:
// BEFORE:
// refreshed[asset] = new BinanceExchangeSymbolMetadata(
//     asset,
//     symbol.Symbol,
//     GetDecimals(lotSizeFilter?.StepSize, 3),
//     GetDecimals(priceFilter?.TickSize, 2),
//     MaxLeverageByAsset.TryGetValue(asset, out var maxLeverage) ? maxLeverage : 25);

// AFTER:
refreshed[asset] = new BinanceExchangeSymbolMetadata(
    asset,
    symbol.Symbol,
    ParseStepSize(lotSizeFilter?.StepSize, 0.001m),
    ParseStepSize(priceFilter?.TickSize, 0.01m),
    leverageBrackets.TryGetValue(asset, out var maxLeverage) ? maxLeverage : 25);

// 2. Remove the existing GetDecimals helper method and replace with ParseStepSize:
// REMOVE:
// private static int GetDecimals(string? value, int fallback) { ... }

// ADD:
private static decimal ParseStepSize(string? value, decimal fallback)
{
    if (string.IsNullOrWhiteSpace(value) ||
        !decimal.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ||
        parsed <= 0)
    {
        return fallback;
    }

    return parsed;
}
```

##### Pattern References

- Existing `BinanceExchangeSymbolMetadata` record in `IBinanceExchangeInfoCache.cs` — the record being changed.
- Existing `GetDecimals` helper in `BinanceExchangeInfoCache.cs` — the logic being moved into `CountDecimals` on the record.
- `BinanceExchangeInfoCache.EnsureCacheAsync` — the method constructing metadata records.

---

### Task 4.2: Update normalization methods to use modular arithmetic {#task-42-update-normalization-methods}

Change `NormalizeOrderSize` and `NormalizeOrderPrice` in `BinanceExecutionEngine` to accept raw `decimal stepSize`/`decimal tickSize` and use `Math.Truncate(value / step) * step` instead of the current `Math.Pow(10, n)` approach.

- **Complexity**: Medium
- **Risk Factors**: Division by zero if step/tick is zero — must guard. Must update all call sites within the class to pass `metadata.StepSize`/`metadata.TickSize` instead of `metadata.SizeDecimals`/`metadata.PriceDecimals`. Error messages should reference step/tick sizes, not decimal counts.
- **Files**:
  - `src/TradePilot.Infrastructure/Binance/BinanceExecutionEngine.cs` — Update normalization methods + call sites
- **Success**:
  - `NormalizeOrderSize(decimal size, decimal stepSize)` uses `Math.Truncate(abs / stepSize) * stepSize`
  - `NormalizeOrderPrice(decimal price, decimal tickSize)` uses `Math.Truncate(price / tickSize) * tickSize`
  - Guard: `ArgumentOutOfRangeException` if stepSize/tickSize is zero or negative
  - All call sites updated to pass `metadata.StepSize`/`metadata.TickSize`
  - Error messages reference step/tick sizes (e.g., "normalizes to zero for BTC (stepSize: 0.001)")
- **Dependencies**:
  - Task 4.1 (metadata record must have `StepSize`/`TickSize` properties)

#### Implementation Details

```csharp
// src/TradePilot.Infrastructure/Binance/BinanceExecutionEngine.cs — modification

// 1. Update NormalizeOrderSize method:
// BEFORE:
// private static decimal NormalizeOrderSize(decimal size, int sizeDecimals)
// {
//     if (sizeDecimals < 0) throw new ArgumentOutOfRangeException(nameof(sizeDecimals));
//     if (size == 0m) return 0m;
//     var sign = Math.Sign(size);
//     var absoluteSize = Math.Abs(size);
//     var factor = (decimal)Math.Pow(10, sizeDecimals);
//     var normalized = decimal.Truncate(absoluteSize * factor) / factor;
//     return normalized * sign;
// }

// AFTER:
private static decimal NormalizeOrderSize(decimal size, decimal stepSize)
{
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stepSize);

    if (size == 0m)
    {
        return 0m;
    }

    var sign = Math.Sign(size);
    var absoluteSize = Math.Abs(size);
    var normalized = Math.Truncate(absoluteSize / stepSize) * stepSize;
    return normalized * sign;
}

// 2. Update NormalizeOrderPrice method:
// BEFORE:
// private static decimal NormalizeOrderPrice(decimal price, int priceDecimals)
// {
//     if (priceDecimals < 0) throw new ArgumentOutOfRangeException(nameof(priceDecimals));
//     if (price == 0m) return 0m;
//     var factor = (decimal)Math.Pow(10, priceDecimals);
//     return decimal.Truncate(price * factor) / factor;
// }

// AFTER:
private static decimal NormalizeOrderPrice(decimal price, decimal tickSize)
{
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tickSize);

    if (price == 0m)
    {
        return 0m;
    }

    return Math.Truncate(price / tickSize) * tickSize;
}

// 3. Update all call sites in PlaceOrderAsync:
// BEFORE:
// var normalizedSize = NormalizeOrderSize(order.Size, metadata.SizeDecimals);
// AFTER:
var normalizedSize = NormalizeOrderSize(order.Size, metadata.StepSize);
// Error message update:
// BEFORE: $"Order size {order.Size} normalizes to zero for {asset} ({metadata.SizeDecimals} size decimals)."
// AFTER:  $"Order size {order.Size} normalizes to zero for {asset} (stepSize: {metadata.StepSize})."

// BEFORE:
// normalizedPrice = NormalizeOrderPrice(order.Price, metadata.PriceDecimals);
// AFTER:
normalizedPrice = NormalizeOrderPrice(order.Price, metadata.TickSize);
// Error message update:
// BEFORE: $"Order price {order.Price} normalizes to zero for {asset} ({metadata.PriceDecimals} price decimals)."
// AFTER:  $"Order price {order.Price} normalizes to zero for {asset} (tickSize: {metadata.TickSize})."

// 4. Update call sites in PlaceTriggerOrderAsync:
// BEFORE:
// var normalizedSize = NormalizeOrderSize(size, metadata.SizeDecimals);
// AFTER:
var normalizedSize = NormalizeOrderSize(size, metadata.StepSize);
// BEFORE: $"Order size {size} normalizes to zero for {normalizedAsset} ({metadata.SizeDecimals} size decimals)."
// AFTER:  $"Order size {size} normalizes to zero for {normalizedAsset} (stepSize: {metadata.StepSize})."

// BEFORE:
// var normalizedTriggerPrice = NormalizeOrderPrice(triggerPrice, metadata.PriceDecimals);
// AFTER:
var normalizedTriggerPrice = NormalizeOrderPrice(triggerPrice, metadata.TickSize);
// BEFORE: $"Trigger price {triggerPrice} normalizes to zero for {normalizedAsset} ({metadata.PriceDecimals} price decimals)."
// AFTER:  $"Trigger price {triggerPrice} normalizes to zero for {normalizedAsset} (tickSize: {metadata.TickSize})."
```

##### Pattern References

- Existing `NormalizeOrderSize` / `NormalizeOrderPrice` in `BinanceExecutionEngine.cs` — the methods being replaced.
- All call sites in `PlaceOrderAsync` and `PlaceTriggerOrderAsync` — same file.

---

### Task 4.3: Update all consumers of BinanceExchangeSymbolMetadata {#task-43-update-consumers}

Update all code that constructs `BinanceExchangeSymbolMetadata` to use the new `(Asset, Symbol, StepSize, TickSize, MaxLeverage)` constructor. Verify that `BinanceSymbolMetadataProvider` still works via the computed `SizeDecimals`/`PriceDecimals` properties.

- **Complexity**: Low
- **Risk Factors**: Must find and update every constructor call. The `BinanceSymbolMetadataProvider.Map` method reads `SizeDecimals`/`PriceDecimals` which are now computed — verify it still maps correctly.
- **Files**:
  - `src/TradePilot.Infrastructure/Binance/BinanceSymbolMetadataProvider.cs` — No code change needed (reads computed properties), but verify mapping still works
  - `tests/TradePilot.Infrastructure.Tests/Binance/BinanceExecutionEngineTests.cs` — Update `SetupExchangeMetadata` helper
  - `tests/TradePilot.Infrastructure.Tests/Binance/BinanceExchangeInfoCacheTests.cs` — Update any direct metadata assertions
  - `tests/TradePilot.Infrastructure.Tests/Binance/BinanceSymbolMetadataProviderTests.cs` — Update metadata construction in tests
- **Success**:
  - All test fixtures construct metadata with `decimal StepSize, decimal TickSize` instead of `int SizeDecimals, int PriceDecimals`
  - `BinanceSymbolMetadataProvider` maps correctly via computed properties
  - No compilation errors
- **Dependencies**:
  - Task 4.1 (metadata record must be changed first)

#### Implementation Details

```csharp
// tests/TradePilot.Infrastructure.Tests/Binance/BinanceExecutionEngineTests.cs — modification

// Update SetupExchangeMetadata helper:
// BEFORE:
// private void SetupExchangeMetadata(string asset, int sizeDecimals, int priceDecimals)
// {
//     _exchangeInfoCacheMock
//         .Setup(cache => cache.GetSymbolAsync(asset, It.IsAny<CancellationToken>()))
//         .ReturnsAsync(new BinanceExchangeSymbolMetadata(asset, $"{asset}USDT", sizeDecimals, priceDecimals, 125));
// }

// AFTER:
private void SetupExchangeMetadata(string asset, decimal stepSize, decimal tickSize)
{
    _exchangeInfoCacheMock
        .Setup(cache => cache.GetSymbolAsync(asset, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new BinanceExchangeSymbolMetadata(asset, $"{asset}USDT", stepSize, tickSize, 125));
}

// Update all callers of SetupExchangeMetadata:
// BEFORE: SetupExchangeMetadata("BTC", sizeDecimals: 3, priceDecimals: 2);
// AFTER:  SetupExchangeMetadata("BTC", stepSize: 0.001m, tickSize: 0.01m);

// BEFORE: SetupExchangeMetadata("BTC", sizeDecimals: 1, priceDecimals: 2);
// AFTER:  SetupExchangeMetadata("BTC", stepSize: 0.1m, tickSize: 0.01m);
```

```csharp
// tests/TradePilot.Infrastructure.Tests/Binance/BinanceSymbolMetadataProviderTests.cs — modification

// Update metadata construction in GivenCacheWithSymbols test:
// BEFORE: ["BTC"] = new("BTC", "BTCUSDT", 3, 1, 125),
// AFTER:  ["BTC"] = new("BTC", "BTCUSDT", 0.001m, 0.1m, 125),

// Update metadata construction in GivenKnownAsset test:
// BEFORE: new BinanceExchangeSymbolMetadata("ETH", "ETHUSDT", 3, 2, 100)
// AFTER:  new BinanceExchangeSymbolMetadata("ETH", "ETHUSDT", 0.001m, 0.01m, 100)

// The ExchangeSymbolMetadata assertion stays the same because it uses computed SizeDecimals/PriceDecimals:
// result[0].Should().BeEquivalentTo(new ExchangeSymbolMetadata("BTC", "BTCUSDT", 3, 1, 125));
// This works because BinanceExchangeSymbolMetadata(StepSize: 0.001m).SizeDecimals == 3
// and BinanceExchangeSymbolMetadata(TickSize: 0.1m).PriceDecimals == 1
```

##### Pattern References

- `BinanceExecutionEngineTests.SetupExchangeMetadata` — the test helper being updated.
- `BinanceSymbolMetadataProviderTests` — test fixture construction.
- `BinanceSymbolMetadataProvider.Map` — reads computed `SizeDecimals`/`PriceDecimals` properties, no code change needed.

---

### Task 4.4: Add unit tests for non-power-of-ten normalization {#task-44-add-normalization-tests}

Add tests verifying that the modular arithmetic normalization works for non-power-of-ten step sizes (e.g., stepSize = 0.025). Also verify the zero step-size guard.

- **Complexity**: Low
- **Risk Factors**: Must test edge cases: very small step sizes, exactly-on-step values, zero-size guard.
- **Files**:
  - `tests/TradePilot.Infrastructure.Tests/Binance/BinanceExecutionEngineTests.cs` — Add normalization tests
- **Success**:
  - Test: non-power-of-ten step size (0.025) truncates correctly
  - Test: power-of-ten step size (0.001) still works as before
  - Test: zero step-size throws `ArgumentOutOfRangeException`
  - Test: normalization to zero with small size + large step size
- **Dependencies**:
  - Tasks 4.1, 4.2, 4.3 (production code changes and test fixture updates must be in place)

#### Implementation Details

```csharp
// tests/TradePilot.Infrastructure.Tests/Binance/BinanceExecutionEngineTests.cs — modification
// Add new tests after existing normalization tests:

[TestMethod]
public async Task GivenNonPowerOfTenStepSize_WhenPlaceOrderAsync_ThenNormalizesViaModularArithmetic()
{
    // stepSize = 0.025 (not a power of ten)
    SetupExchangeMetadata("BTC", stepSize: 0.025m, tickSize: 0.5m);

    BinancePlaceOrderRequest? capturedRequest = null;
    _authClientMock
        .Setup(client => client.PlaceOrderAsync(It.IsAny<BinancePlaceOrderRequest>(), It.IsAny<CancellationToken>()))
        .Callback<BinancePlaceOrderRequest, CancellationToken>((request, _) => capturedRequest = request)
        .ReturnsAsync(new BinancePlaceOrderResult { OrderId = 999L, Status = "NEW" });

    // 1.037 / 0.025 = 41.48 → truncate to 41 → 41 * 0.025 = 1.025
    var orderId = await _sut.PlaceOrderAsync(CreateLimitOrder(size: 1.037m, price: 67890.7m));

    orderId.Should().Be("999");
    capturedRequest.Should().NotBeNull();
    capturedRequest!.Quantity.Should().Be(1.025m);
    // 67890.7 / 0.5 = 135781.4 → truncate to 135781 → 135781 * 0.5 = 67890.5
    capturedRequest.Price.Should().Be(67890.5m);
}

[TestMethod]
public async Task GivenSizeSmallerThanStepSize_WhenPlaceOrderAsync_ThenThrowsDomainException()
{
    // stepSize = 0.1, size = 0.05 → normalizes to 0
    SetupExchangeMetadata("BTC", stepSize: 0.1m, tickSize: 0.01m);

    var act = () => _sut.PlaceOrderAsync(CreateLimitOrder(size: 0.05m));

    await act.Should().ThrowAsync<DomainException>()
        .WithMessage("*normalizes to zero*");
}

[TestMethod]
public async Task GivenExactStepSizeMultiple_WhenPlaceOrderAsync_ThenNoTruncation()
{
    // 0.075 is an exact multiple of 0.025 (3 * 0.025)
    SetupExchangeMetadata("BTC", stepSize: 0.025m, tickSize: 0.01m);

    BinancePlaceOrderRequest? capturedRequest = null;
    _authClientMock
        .Setup(client => client.PlaceOrderAsync(It.IsAny<BinancePlaceOrderRequest>(), It.IsAny<CancellationToken>()))
        .Callback<BinancePlaceOrderRequest, CancellationToken>((request, _) => capturedRequest = request)
        .ReturnsAsync(new BinancePlaceOrderResult { OrderId = 888L, Status = "NEW" });

    await _sut.PlaceOrderAsync(CreateLimitOrder(size: 0.075m, price: 50000.01m));

    capturedRequest!.Quantity.Should().Be(0.075m);
    capturedRequest.Price.Should().Be(50000.01m);
}
```

**Note**: The existing tests `GivenLimitOrderWithExcessPrecision_WhenPlaceOrderAsync_ThenNormalizesBeforeSubmission` and `GivenTriggerOrderWithExcessPrecision_WhenPlaceTriggerOrderAsync_ThenNormalizesBeforeSubmission` will continue to pass because their step/tick sizes are power-of-ten values (0.001 / 0.01 / 0.1) where the modular arithmetic produces the same result as the old decimal-count approach.

##### Pattern References

- Existing test `GivenLimitOrderWithExcessPrecision_WhenPlaceOrderAsync_ThenNormalizesBeforeSubmission` — pattern for place-order test with captured request.
- Existing test `GivenOrderThatNormalizesToZero_WhenPlaceOrderAsync_ThenThrowsDomainException` — pattern for testing zero normalization.

---

### Task 4.5: Build and verify all tests pass {#task-45-build-and-verify}

Build the solution and run all tests to verify no regressions.

- **Complexity**: Low
- **Risk Factors**: `BinanceExchangeSymbolMetadata` constructor change affects every test that creates this record. Must verify all fixtures are updated.
- **Files**:
  - Solution level — all projects
- **Success**:
  - `dotnet build TradePilot.sln` succeeds with no errors
  - `dotnet test TradePilot.sln` — all tests pass
  - No new warnings in modified files
- **Dependencies**:
  - Tasks 4.1, 4.2, 4.3, 4.4

## Phase Success Criteria

- `BinanceExchangeSymbolMetadata` stores raw `StepSize`/`TickSize` values
- Computed `SizeDecimals`/`PriceDecimals` properties maintain backward compatibility
- `NormalizeOrderSize` and `NormalizeOrderPrice` use modular arithmetic (`Math.Truncate(value / step) * step`)
- Non-power-of-ten step sizes are handled correctly (e.g., 0.025)
- Zero step-size guard throws `ArgumentOutOfRangeException`
- All existing tests pass with updated fixtures
- New tests verify non-power-of-ten normalization
- `BinanceSymbolMetadataProvider` mapping still works via computed properties
- Solution builds without errors
