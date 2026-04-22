<!-- markdownlint-disable-file -->

# Task Details: Hyperliquid Integration Code Review Remediation

## Phase 1: Shared Utilities & Critical Code Fixes

## Standards and Knowledge References

- **csharp.instructions.md**: `sealed` classes, `static` utility classes acceptable for pure functions, `CancellationToken` propagation, `async/await` over `ContinueWith`
- **testing.instructions.md**: MSTest + Moq + FluentAssertions ≤ 6, `Given_When_Then` naming, tests within phase
- **dotnet-architecture.instructions.md**: Infrastructure layer implementations, Application layer interfaces
- **02-hyperliquid-integration.md**: Exchange data formats, wire decimal format, side codes (`B`/`A`)
- **38-exchange-abstraction-architecture.md**: `IExchangeCapabilities`, `ExchangeCapabilitySet` contract

---

### Task 1.1: Create `HyperliquidFormatting` shared utility class {#task-11-create-hyperliquidformatting-shared-utility-class}

Create a new static utility class to centralize duplicated Hyperliquid formatting/mapping methods.

- **Complexity**: Medium
- **Risk Factors**: All consumers must be updated to use the shared utility; missing a callsite would leave duplication
- **Files**:
  - `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidFormatting.cs` — new file
- **Success**:
  - New file compiles successfully
  - Contains `ToWireDecimal`, `MapOrderSide`, `ParseDecimal(string)`, `ParseDecimal(JsonElement)` methods
  - All methods are `public static` (consumed by both Infrastructure and Api projects)
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradePilot.Infrastructure/Hyperliquid/HyperliquidFormatting.cs — new file
using System.Globalization;
using System.Text.Json;

namespace TradePilot.Infrastructure.Hyperliquid;

/// <summary>
/// Shared formatting and parsing utilities for Hyperliquid data types.
/// Centralizes wire-format conversions previously duplicated across multiple service classes.
/// </summary>
public static class HyperliquidFormatting
{
    /// <summary>
    /// Formats a decimal to Hyperliquid's wire format: no trailing zeros, no trailing dot.
    /// Used for order prices, sizes, and action hashes.
    /// </summary>
    public static string ToWireDecimal(decimal value)
    {
        var formatted = value.ToString("0.############################", CultureInfo.InvariantCulture);
        return formatted.Contains('.')
            ? formatted.TrimEnd('0').TrimEnd('.')
            : formatted;
    }

    /// <summary>
    /// Maps Hyperliquid's single-char order side codes to display strings.
    /// "B" → "Buy", "A" → "Sell", anything else passes through.
    /// </summary>
    public static string MapOrderSide(string side)
    {
        return side.ToUpperInvariant() switch
        {
            "B" => "Buy",
            "A" => "Sell",
            _ => side,
        };
    }

    /// <summary>
    /// Parses a string decimal value using invariant culture.
    /// Returns the parsed value or 0m if the input is null, empty, or unparseable.
    /// Prefer this over throwing on bad input — Hyperliquid may return empty strings
    /// for optional fields (e.g., entryPx when no entry exists).
    /// </summary>
    public static decimal ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0m;
        }

        return decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0m;
    }

    /// <summary>
    /// Parses a JsonElement decimal — handles both Number and String value kinds.
    /// Returns 0m for null, undefined, or unparseable elements.
    /// </summary>
    public static decimal ParseDecimal(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.GetDecimal();
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            return ParseDecimal(element.GetString());
        }

        return 0m;
    }
}
```

##### Pattern References

- `ToWireDecimal` based on `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidEip712.cs` lines 178–183
- `MapOrderSide` based on `src/TradePilot.Infrastructure/Services/HyperliquidRestClient.cs` lines 333–339
- `ParseDecimal(string)` based on `src/TradePilot.Infrastructure/Services/HyperliquidRestClient.cs` lines 343–352 (changed from throw to return 0m for safety)
- `ParseDecimal(JsonElement)` based on `src/TradePilot.Infrastructure/Services/HyperliquidAccountService.cs` lines 492–510

> **Design Decision**: `ParseDecimal(string)` returns `0m` instead of throwing `FormatException`. The throwing version in `HyperliquidRestClient` is dangerous because Hyperliquid can return empty strings for optional fields. The `0m` default is acceptable for display/mapping purposes. Callers that need to distinguish "zero" from "missing" should check the source field for null/empty before calling.

---

### Task 1.2: Fix ContinueWith anti-pattern in `HyperliquidHistoricalDataClient` {#task-12-fix-continuewith-anti-pattern-in-hyperliquidhistoricaldataclient}

Replace `ContinueWith` with `async/await` to fix exception propagation and threading issues. (Review finding C2)

- **Complexity**: Low
- **Risk Factors**: Minimal — direct replacement with equivalent async behavior
- **Files**:
  - `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidHistoricalDataClient.cs` — modification
- **Success**:
  - Method is `async Task<IReadOnlyList<CandleSnapshotDto>>`
  - Uses `await` instead of `ContinueWith`
  - Original exception types propagate correctly (no `AggregateException` wrapping)
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradePilot.Infrastructure/Hyperliquid/HyperliquidHistoricalDataClient.cs — modification
// Replace the GetCandleSnapshotsAsync method body

    // ... existing code ...

    public async Task<IReadOnlyList<CandleSnapshotDto>> GetCandleSnapshotsAsync(
        TradingPair pair,
        string timeframe,
        long startTime,
        long endTime,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pair);

        var result = await _restClient.GetCandleSnapshotsAsync(pair.Base, timeframe, startTime, endTime, cancellationToken);
        return result;
    }

    // ... existing code ...
```

##### Pattern References

- Current code: `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidHistoricalDataClient.cs` lines 21–32

---

### Task 1.3: Fix `GetFundingRatesAsync` LSP violation {#task-13-fix-getfundingratesasync-lsp-violation}

Replace `NotSupportedException` with a capability check approach. Add `SupportsFundingRateHistory` to `ExchangeCapabilitySet` and return an empty list instead of throwing. (Review finding C3)

- **Complexity**: Medium
- **Risk Factors**: Record change requires updating all construction sites; Binance adapter also creates `ExchangeCapabilitySet`
- **Files**:
  - `src/TradePilot.Application/Abstractions/Services/ExchangeCapabilitySet.cs` — modification
  - `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidHistoricalDataClient.cs` — modification
  - `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidCapabilities.cs` — modification
  - Any Binance `*Capabilities.cs` file — modification (add `SupportsFundingRateHistory: false`)
- **Success**:
  - `ExchangeCapabilitySet` has `SupportsFundingRateHistory` property
  - `HyperliquidCapabilities` sets `SupportsFundingRateHistory: false`
  - `GetFundingRatesAsync` returns empty list instead of throwing
  - Callers can check capability before calling
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradePilot.Application/Abstractions/Services/ExchangeCapabilitySet.cs — modification
// Add SupportsFundingRateHistory parameter to end of record

public sealed record ExchangeCapabilitySet(
    Exchange Exchange,
    IReadOnlySet<AssetType> SupportedProductTypes,
    bool SupportsLeverage,
    bool SupportsTriggerOrders,
    bool SupportsReduceOnly,
    bool SupportsPublicTradesStream,
    bool SupportsUserEventStream,
    bool SupportsPerUserNetworkRouting,
    IReadOnlySet<string> SupportedOrderTypes,
    IReadOnlySet<string> SupportedTimeframes,
    bool SupportsFundingRateHistory = false);
```

```csharp
// src/TradePilot.Infrastructure/Hyperliquid/HyperliquidHistoricalDataClient.cs — modification
// Replace GetFundingRatesAsync to return empty list

    public Task<IReadOnlyList<FundingRateDto>> GetFundingRatesAsync(
        TradingPair pair,
        long startTime,
        long endTime,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<FundingRateDto>>(Array.Empty<FundingRateDto>());
    }
```

> **Note**: Search for all `ExchangeCapabilitySet` construction sites (Binance and any other exchanges) and add the new parameter. Since the parameter has a default value (`false`), existing construction sites that don't specify it will compile — but verify Binance's capabilities and set explicitly.

##### Pattern References

- `src/TradePilot.Application/Abstractions/Services/ExchangeCapabilitySet.cs` — current record definition
- `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidCapabilities.cs` — Hyperliquid construction site

---

### Task 1.4: Fix nullable signer field inconsistency {#task-14-fix-nullable-signer-field-inconsistency}

Remove the nullable annotation from `_signer` field since the constructor parameter is non-nullable. Remove the null-conditional in `ResolveAddress`. (Review finding m1)

- **Complexity**: Low
- **Risk Factors**: Minimal — field can never be null given non-nullable constructor parameter
- **Files**:
  - `src/TradePilot.Infrastructure/Services/HyperliquidAccountService.cs` — modification
- **Success**:
  - `_signer` field is `IHyperliquidSigner` (not nullable)
  - `ResolveAddress` uses `_signer.WalletAddress` (not `_signer?.WalletAddress`)
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradePilot.Infrastructure/Services/HyperliquidAccountService.cs — modification
// Change field declaration and ResolveAddress

    private readonly IHyperliquidSigner _signer;
    // ... existing code ...

    private string ResolveAddress(string? walletAddress)
    {
        if (!string.IsNullOrWhiteSpace(walletAddress))
            return walletAddress;

        return _signer.WalletAddress
            ?? throw new InvalidOperationException("No wallet address provided and no signer configured.");
    }
```

##### Pattern References

- `src/TradePilot.Infrastructure/Services/HyperliquidAccountService.cs` lines 13–33

---

### Task 1.5: Replace duplicated `MapOrderSide` with shared utility {#task-15-replace-duplicated-maporderside-with-shared-utility}

Remove `MapOrderSide` from `HyperliquidRestClient`, `HyperliquidAccountService`, and `HyperliquidUserEventClient`. Replace all callsites with `HyperliquidFormatting.MapOrderSide`. (Review finding m3)

- **Complexity**: Low
- **Risk Factors**: Must find and update all callsites; missing one will cause compile error (good — immediate detection)
- **Files**:
  - `src/TradePilot.Infrastructure/Services/HyperliquidRestClient.cs` — modification (remove method, update calls)
  - `src/TradePilot.Infrastructure/Services/HyperliquidAccountService.cs` — modification (remove method, update calls)
  - `src/TradePilot.Infrastructure/Services/HyperliquidUserEventClient.cs` — modification (remove method, update calls)
- **Success**:
  - Zero `MapOrderSide` private methods in the 3 service files
  - All callsites use `HyperliquidFormatting.MapOrderSide`
  - Compiles and existing tests pass
- **Dependencies**: Task 1.1 (HyperliquidFormatting class exists)

---

### Task 1.6: Replace duplicated `ParseDecimal` with standardized implementation {#task-16-replace-duplicated-parsedecimal-with-standardized-implementation}

Remove `ParseDecimal` from `HyperliquidRestClient` (throws) and `HyperliquidAccountService` (returns 0m). Replace with `HyperliquidFormatting.ParseDecimal`. (Review finding m2)

- **Complexity**: Medium
- **Risk Factors**: The `HyperliquidRestClient.ParseDecimal` currently throws on bad input; changing to return 0m changes error behavior. Verify no caller relies on the thrown exception.
- **Files**:
  - `src/TradePilot.Infrastructure/Services/HyperliquidRestClient.cs` — modification (remove method, update calls)
  - `src/TradePilot.Infrastructure/Services/HyperliquidAccountService.cs` — modification (remove method, update calls)
- **Success**:
  - Zero `ParseDecimal` private methods in both files
  - All callsites use `HyperliquidFormatting.ParseDecimal`
  - Compiles and existing tests pass
- **Dependencies**: Task 1.1 (HyperliquidFormatting class exists)

> **Behavior change note**: `HyperliquidRestClient.ParseDecimal` previously threw `FormatException` on unparseable strings. The new shared version returns `0m`. This is safer for a trading system — a `FormatException` would crash the entire request/response pipeline for a single malformed field. Returning `0m` lets the rest of the data flow through, and callers can guard against zero values at the business logic layer if needed.

---

### Task 1.7: Add unit tests {#task-17-add-unit-tests}

Add tests for `HyperliquidFormatting` utility class and `HyperliquidHistoricalDataClient`.

- **Complexity**: Medium
- **Risk Factors**: No existing test file for `HyperliquidHistoricalDataClient`; must create new one
- **Files**:
  - `tests/TradePilot.Infrastructure.Tests/Services/HyperliquidFormattingTests.cs` — new file
  - `tests/TradePilot.Infrastructure.Tests/Services/HyperliquidHistoricalDataClientTests.cs` — new file
- **Success**:
  - `HyperliquidFormattingTests` covers: `ToWireDecimal` edge cases (trailing zeros, integers, small decimals), `MapOrderSide` all branches, `ParseDecimal(string)` valid/null/empty/invalid, `ParseDecimal(JsonElement)` Number/String/Null kinds
  - `HyperliquidHistoricalDataClientTests` covers: successful candle fetch (async/await works), exception propagation (no AggregateException wrapping), funding rates returns empty list
  - All tests use `Given_When_Then` naming convention
- **Dependencies**: Tasks 1.1–1.3

#### Implementation Details

```csharp
// tests/TradePilot.Infrastructure.Tests/Services/HyperliquidFormattingTests.cs — new file
using TradePilot.Infrastructure.Hyperliquid;

namespace TradePilot.Infrastructure.Tests.Services;

[TestClass]
public sealed class HyperliquidFormattingTests
{
    [TestMethod]
    [DataRow("1.23000", "1.23")]
    [DataRow("100", "100")]
    [DataRow("0.00012340", "0.0001234")]
    [DataRow("1.0", "1")]
    public void GivenDecimalValue_WhenToWireDecimal_ThenTrailingZerosRemoved(string input, string expected)
    {
        var value = decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture);
        var result = HyperliquidFormatting.ToWireDecimal(value);
        result.Should().Be(expected);
    }

    [TestMethod]
    public void GivenZero_WhenToWireDecimal_ThenReturnsZeroString()
    {
        HyperliquidFormatting.ToWireDecimal(0m).Should().Be("0");
    }

    [TestMethod]
    [DataRow("B", "Buy")]
    [DataRow("A", "Sell")]
    [DataRow("b", "Buy")]
    [DataRow("a", "Sell")]
    [DataRow("Unknown", "Unknown")]
    public void GivenSideCode_WhenMapOrderSide_ThenReturnsExpectedString(string input, string expected)
    {
        HyperliquidFormatting.MapOrderSide(input).Should().Be(expected);
    }

    [TestMethod]
    [DataRow("1.23", 1.23)]
    [DataRow("0", 0.0)]
    [DataRow("-5.5", -5.5)]
    public void GivenValidString_WhenParseDecimal_ThenReturnsParsedValue(string input, double expected)
    {
        HyperliquidFormatting.ParseDecimal(input).Should().Be((decimal)expected);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("not-a-number")]
    public void GivenInvalidString_WhenParseDecimal_ThenReturnsZero(string? input)
    {
        HyperliquidFormatting.ParseDecimal(input).Should().Be(0m);
    }
}
```

```csharp
// tests/TradePilot.Infrastructure.Tests/Services/HyperliquidHistoricalDataClientTests.cs — new file
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;
using TradePilot.Domain.ValueObjects;
using TradePilot.Infrastructure.Hyperliquid;

namespace TradePilot.Infrastructure.Tests.Services;

[TestClass]
public sealed class HyperliquidHistoricalDataClientTests
{
    private Mock<IHyperliquidRestClient> _restClientMock = null!;
    private HyperliquidHistoricalDataClient _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _restClientMock = new Mock<IHyperliquidRestClient>();
        _sut = new HyperliquidHistoricalDataClient(_restClientMock.Object);
    }

    [TestMethod]
    public async Task GivenValidPair_WhenGetCandleSnapshots_ThenReturnsCandles()
    {
        var pair = TradingPair.Create("BTC", "USD", AssetType.Perp);
        var candles = new List<CandleSnapshotDto> { new() { Open = 100m } };
        _restClientMock
            .Setup(r => r.GetCandleSnapshotsAsync("BTC", "1h", 0L, 1000L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(candles);

        var result = await _sut.GetCandleSnapshotsAsync(pair, "1h", 0L, 1000L);

        result.Should().HaveCount(1);
        result[0].Open.Should().Be(100m);
    }

    [TestMethod]
    public async Task GivenRestClientThrows_WhenGetCandleSnapshots_ThenExceptionPropagatesDirectly()
    {
        var pair = TradingPair.Create("BTC", "USD", AssetType.Perp);
        _restClientMock
            .Setup(r => r.GetCandleSnapshotsAsync("BTC", "1h", 0L, 1000L, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("API failure"));

        var act = () => _sut.GetCandleSnapshotsAsync(pair, "1h", 0L, 1000L);

        await act.Should().ThrowAsync<HttpRequestException>().WithMessage("*API failure*");
    }

    [TestMethod]
    public async Task GivenAnyPair_WhenGetFundingRates_ThenReturnsEmptyList()
    {
        var pair = TradingPair.Create("BTC", "USD", AssetType.Perp);

        var result = await _sut.GetFundingRatesAsync(pair, 0L, 1000L);

        result.Should().BeEmpty();
    }
}
```

##### Pattern References

- Test structure: `tests/TradePilot.Infrastructure.Tests/Services/HyperliquidAssetMapperTests.cs`
- `DataRow` pattern: `tests/TradePilot.Infrastructure.Tests/Services/HyperliquidAssetMapperTests.cs`
- Mock setup pattern: `tests/TradePilot.Api.Tests/Services/HyperliquidAccountServiceTests.cs`

---

### Task 1.8: Build and run all tests {#task-18-build-and-run-all-tests}

Build the solution and run all Hyperliquid-related tests to verify Phase 1 changes.

- **Complexity**: Low
- **Risk Factors**: Existing tests may fail if `ParseDecimal` behavior change (throw → return 0m) affects mocked expectations
- **Files**: None (verification only)
- **Success**:
  - `dotnet build TradePilot.sln` succeeds with no errors
  - `dotnet test tests/TradePilot.Infrastructure.Tests/ --filter "FullyQualifiedName~Hyperliquid"` passes
  - `dotnet test tests/TradePilot.Api.Tests/ --filter "FullyQualifiedName~Hyperliquid"` passes
- **Dependencies**: Tasks 1.1–1.7

## Phase Success Criteria

- `HyperliquidFormatting` utility class exists with `ToWireDecimal`, `MapOrderSide`, `ParseDecimal` methods
- Zero duplication of `MapOrderSide` (was in 3 files)
- Zero duplication of `ParseDecimal` (was in 2 files, different signatures)
- `HyperliquidHistoricalDataClient.GetCandleSnapshotsAsync` uses `async/await` (not `ContinueWith`)
- `HyperliquidHistoricalDataClient.GetFundingRatesAsync` returns empty list (not throw)
- `ExchangeCapabilitySet` has `SupportsFundingRateHistory` property
- `HyperliquidAccountService._signer` is non-nullable
- All new tests pass, all existing tests pass
