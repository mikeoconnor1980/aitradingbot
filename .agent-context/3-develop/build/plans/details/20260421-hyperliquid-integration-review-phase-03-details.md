<!-- markdownlint-disable-file -->

# Task Details: Hyperliquid Integration Code Review Remediation

## Phase 3: Order Execution Improvements

## Standards and Knowledge References

- **csharp.instructions.md**: `IOptions<T>` for configuration, `sealed` classes, `Guard.Against.*` for validation
- **testing.instructions.md**: MSTest + Moq + FluentAssertions ≤ 6, `Given_When_Then` naming
- **api-controllers.instructions.md**: API response models and serialization
- **02-hyperliquid-integration.md**: Order placement, trigger orders, EIP-712 signing
- **33-risk-management-and-trade-sizing.md**: Risk engine, slippage context

---

### Task 3.1: Add `MarketOrderSlippageBps` to `HyperliquidOptions` {#task-31-add-marketorderslippagebps-to-hyperliquidoptions}

Make market order slippage configurable via `HyperliquidOptions` instead of hardcoding 5% (500 bps). (Review finding M2)

- **Complexity**: Medium
- **Risk Factors**: Must update `HyperliquidOrderService` to inject options and use the configurable value; default must maintain backward compatibility (500 bps)
- **Files**:
  - `src/TradePilot.Application/Abstractions/Configuration/HyperliquidOptions.cs` — modification
  - `src/TradePilot.Api/Services/HyperliquidOrderService.cs` — modification
- **Success**:
  - `HyperliquidOptions.MarketOrderSlippageBps` property exists with default `500`
  - `HyperliquidOrderService` reads slippage from options instead of hardcoded value
  - Slippage calculation is `midPrice * (1 + slippageBps / 10000m)` for buy, `midPrice * (1 - slippageBps / 10000m)` for sell
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradePilot.Application/Abstractions/Configuration/HyperliquidOptions.cs — modification
// Add after the Network property

    /// <summary>
    /// Maximum slippage tolerance for market orders in basis points (bps).
    /// 100 bps = 1%. Default is 500 bps (5%).
    /// </summary>
    [Range(1, 2000)]
    public int MarketOrderSlippageBps { get; set; } = 500;
```

```csharp
// src/TradePilot.Api/Services/HyperliquidOrderService.cs — modification
// 1. Inject IOptions<HyperliquidOptions> in constructor (if not already injected)
// 2. Replace hardcoded slippage calculation

        if (isMarket)
        {
            var midPrice = await GetMidPriceAsync(coin, cancellationToken);
            var slippageFactor = _options.MarketOrderSlippageBps / 10_000m;
            var slippagePrice = isBuy
                ? midPrice * (1m + slippageFactor)
                : midPrice * (1m - slippageFactor);
            price = RoundToSignificantFigures(slippagePrice, 5);
            tif = "Ioc";
            _logger.LogInformation(
                "Market order: Coin={Coin}, MidPrice={MidPrice}, SlippagePrice={SlippagePrice}, SlippageBps={SlippageBps}, IsBuy={IsBuy}",
                coin, midPrice, price, _options.MarketOrderSlippageBps, isBuy);
        }
```

> **Note**: Check whether `HyperliquidOrderService` already has `IOptions<HyperliquidOptions>` injected. If not, add it to the constructor and store as `private readonly HyperliquidOptions _options`.

##### Pattern References

- `src/TradePilot.Application/Abstractions/Configuration/HyperliquidOptions.cs` — existing options class
- `src/TradePilot.Api/Services/HyperliquidOrderService.cs` — lines 57–66 (current slippage code)

---

### Task 3.2: Rewrite `RoundToSignificantFigures` using pure decimal math {#task-32-rewrite-roundtosignificantfigures-using-pure-decimal-math}

Replace the `double` cast in `RoundToSignificantFigures` with pure decimal arithmetic to avoid floating-point precision loss on edge-case prices. (Review finding M3)

- **Complexity**: Medium
- **Risk Factors**: Must produce byte-for-byte identical results for typical prices (BTC ~$100k, ETH ~$3k, SOL ~$15, DOGE ~$0.001); edge cases around very small prices (< 0.00001) need careful testing
- **Files**:
  - `src/TradePilot.Api/Services/HyperliquidOrderService.cs` — modification
- **Success**:
  - `RoundToSignificantFigures` uses only `decimal` arithmetic — no `double` casts
  - Existing rounding behavior preserved for all common price ranges
  - Edge cases (very small and very large decimals) produce correct results
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradePilot.Api/Services/HyperliquidOrderService.cs — modification
// Replace RoundToSignificantFigures method

    /// <summary>
    /// Rounds a decimal to the specified number of significant figures using pure decimal math.
    /// Hyperliquid requires prices with max 5 significant figures.
    /// </summary>
    private static decimal RoundToSignificantFigures(decimal value, int significantFigures)
    {
        if (value == 0m)
        {
            return 0m;
        }

        // Count digits before decimal point using pure decimal arithmetic
        // to avoid double-precision loss from Math.Log10
        var abs = Math.Abs(value);
        var digits = 0;
        var test = abs;
        if (test >= 1m)
        {
            while (test >= 1m)
            {
                test /= 10m;
                digits++;
            }
        }
        else
        {
            while (test < 0.1m)
            {
                test *= 10m;
                digits--;
            }
        }

        var decimalPlaces = significantFigures - digits;
        if (decimalPlaces >= 0)
        {
            return Math.Round(value, decimalPlaces, MidpointRounding.AwayFromZero);
        }

        // For negative decimalPlaces (large numbers), scale down, round, scale back up
        var scale = DecimalPowerOf10(-decimalPlaces);
        return Math.Round(value / scale, 0, MidpointRounding.AwayFromZero) * scale;
    }

    private static decimal DecimalPowerOf10(int exponent)
    {
        var result = 1m;
        for (var i = 0; i < exponent; i++)
        {
            result *= 10m;
        }
        return result;
    }
```

> **Testing note**: Verify with cases like `RoundToSignificantFigures(0.00001234m, 5)` → `0.000012340m`, `RoundToSignificantFigures(100123m, 5)` → `100120m`, `RoundToSignificantFigures(3456.789m, 5)` → `3456.8m`.

##### Pattern References

- `src/TradePilot.Api/Services/HyperliquidOrderService.cs` — lines 687–698 (current implementation)

---

### Task 3.3: Use shared `ToWireDecimal` in `HyperliquidOrderService` {#task-33-use-shared-towiredecimal-in-hyperliquidorderservice}

Remove the duplicated `ToWireDecimal` from `HyperliquidOrderService` and call `HyperliquidFormatting.ToWireDecimal` instead. Also update `HyperliquidEip712` to call the shared version. (Review finding M4)

- **Complexity**: Low
- **Risk Factors**: `HyperliquidEip712` is in the Infrastructure project (same as `HyperliquidFormatting`); `HyperliquidOrderService` is in the Api project. Verify Api project references Infrastructure project.
- **Files**:
  - `src/TradePilot.Api/Services/HyperliquidOrderService.cs` — modification (remove private `ToWireDecimal`, use `HyperliquidFormatting.ToWireDecimal`)
  - `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidEip712.cs` — modification (remove private `ToWireDecimal`, use `HyperliquidFormatting.ToWireDecimal`)
- **Success**:
  - Zero `ToWireDecimal` methods outside `HyperliquidFormatting`
  - Both callers use `HyperliquidFormatting.ToWireDecimal`
  - Compiles and all signing tests pass (EIP-712 tests are critical)
- **Dependencies**: Phase 1 Task 1.1 (HyperliquidFormatting exists)

---

### Task 3.4: Add `Warnings` to `PlaceOrderResponse` {#task-34-add-warnings-to-placeorderresponse}

Add a `Warnings` list to `PlaceOrderResponse` so companion trigger order failures are surfaced to the UI rather than buried in `Detail`. (Review finding M6)

- **Complexity**: Medium
- **Risk Factors**: Frontend must handle the new `Warnings` field; existing API consumers may not expect it. Since it's a new additive property, it's backward-compatible for JSON deserialization.
- **Files**:
  - `src/TradePilot.Api/Models/PlaceOrderResponse.cs` — modification
  - `src/TradePilot.Api/Services/HyperliquidOrderService.cs` — modification (in `PlaceCompanionTriggerOrdersAsync`)
- **Success**:
  - `PlaceOrderResponse.Warnings` is `List<string>` initialized to empty
  - `PlaceCompanionTriggerOrdersAsync` populates `Warnings` instead of overwriting `Detail`
  - Frontend can display warnings to the user
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradePilot.Api/Models/PlaceOrderResponse.cs — modification

namespace TradePilot.Api.Models;

public sealed class PlaceOrderResponse
{
    public bool Success { get; set; }
    public string? OrderId { get; set; }
    public string? Status { get; set; }
    public string? Detail { get; set; }
    public List<string> Warnings { get; set; } = [];
}
```

```csharp
// src/TradePilot.Api/Services/HyperliquidOrderService.cs — modification
// In PlaceCompanionTriggerOrdersAsync, change the final block:

        if (warnings.Count > 0)
        {
            response.Warnings.AddRange(warnings);
        }
```

##### Pattern References

- `src/TradePilot.Api/Models/PlaceOrderResponse.cs` — current model
- `src/TradePilot.Api/Services/HyperliquidOrderService.cs` — `PlaceCompanionTriggerOrdersAsync` lines 449–519

---

### Task 3.5: Downgrade wallet address logging to Debug {#task-35-downgrade-wallet-address-logging-to-debug}

Change `LogInformation` to `LogDebug` for wallet address in `MutableSignerProvider.Configure` to reduce default log verbosity. (Review finding m4)

- **Complexity**: Low
- **Risk Factors**: None — wallet address is not a secret, but reducing default verbosity is a best practice
- **Files**:
  - `src/TradePilot.Infrastructure/Services/MutableSignerProvider.cs` — modification
- **Success**:
  - `Configure` logs wallet address at `Debug` level
  - Wallet configuration confirmation still logged at `Information` level (without the address)
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradePilot.Infrastructure/Services/MutableSignerProvider.cs — modification

        _logger.LogDebug("Wallet configured: Address={WalletAddress}", signer.WalletAddress);
```

##### Pattern References

- `src/TradePilot.Infrastructure/Services/MutableSignerProvider.cs` — line 66

---

### Task 3.6: Add and update unit tests {#task-36-add-and-update-unit-tests}

Add tests for configurable slippage, RoundToSignificantFigures precision, Warnings in PlaceOrderResponse, and updated companion order behavior.

- **Complexity**: Medium
- **Risk Factors**: Existing order service tests may need updated mock setup for `IOptions<HyperliquidOptions>`
- **Files**:
  - `tests/TradePilot.Api.Tests/Services/HyperliquidOrderServiceTests.cs` — modification
- **Success**:
  - Tests verify slippage uses configurable bps value
  - Tests verify `RoundToSignificantFigures` edge cases (small prices, large prices, zero)
  - Tests verify companion order failures populate `Warnings` instead of `Detail`
  - All existing order service tests pass with updated mock setup
- **Dependencies**: Tasks 3.1–3.5

#### Implementation Details

```csharp
// tests/TradePilot.Api.Tests/Services/HyperliquidOrderServiceTests.cs — add tests

    [TestMethod]
    public void GivenTypicalPrice_WhenRoundToSignificantFigures_ThenCorrectResult()
    {
        // Use reflection to test the private static method, or make it internal + InternalsVisibleTo
        // Alternatively, test through the public PlaceOrderAsync with a market order
    }

    [TestMethod]
    [DataRow(100000.123, 5, 100000.0)]
    [DataRow(0.00001234, 5, 0.000012340)]
    [DataRow(3456.789, 5, 3456.8)]
    public void GivenValue_WhenRoundToSignificantFigures_ThenExpectedResult(
        double input, int sigFigs, double expected)
    {
        // Test via integration: PlaceOrderAsync with market order where midPrice is set
    }

    [TestMethod]
    public async Task GivenStopLossFailure_WhenPlaceOrderWithSL_ThenWarningsPopulated()
    {
        // Arrange: mock main order success, SL trigger order failure
        // Act: call PlaceOrderAsync
        // Assert: response.Success is true, response.Warnings contains SL failure message
    }
```

##### Pattern References

- `tests/TradePilot.Api.Tests/Services/HyperliquidOrderServiceTests.cs` — existing test structure

---

### Task 3.7: Build and run all tests {#task-37-build-and-run-all-tests}

Build the solution and run all order-related tests to verify Phase 3 changes.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification only)
- **Success**:
  - `dotnet build TradePilot.sln` succeeds
  - `dotnet test tests/TradePilot.Api.Tests/ --filter "FullyQualifiedName~HyperliquidOrderService"` passes
  - `dotnet test tests/TradePilot.Infrastructure.Tests/ --filter "FullyQualifiedName~MutableSigner"` passes (if tests exist)
- **Dependencies**: Tasks 3.1–3.6

## Phase Success Criteria

- Market order slippage is configurable via `HyperliquidOptions.MarketOrderSlippageBps` (default 500 bps)
- `RoundToSignificantFigures` uses pure decimal math (no `double` casts)
- `ToWireDecimal` exists only in `HyperliquidFormatting` (zero copies elsewhere)
- `PlaceOrderResponse.Warnings` surfaces companion trigger order failures
- Wallet address logged at `Debug` level (not `Information`)
- All existing and new tests pass
