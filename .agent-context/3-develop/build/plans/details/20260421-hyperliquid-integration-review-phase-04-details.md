<!-- markdownlint-disable-file -->

# Task Details: Hyperliquid Integration Code Review Remediation

## Phase 4: Asset & Timeframe Expansion

## Standards and Knowledge References

- **csharp.instructions.md**: `sealed` classes, avoid hardcoded magic values, prefer configuration/dynamic data
- **testing.instructions.md**: MSTest + Moq + FluentAssertions ≤ 6, `Given_When_Then` naming, `[DataRow]` for parameterized tests
- **02-hyperliquid-integration.md**: Asset names, coin format (base only, no quote suffix)
- **32-hyperliquid-rwa-stock-perps.md**: HIP-3 asset naming — `XYZ:TSLA`, `CASH:USA500`, etc.
- **38-exchange-abstraction-architecture.md**: `IExchangeSymbolMapper`, `IExchangeCapabilities`, `ExchangeCapabilitySet.SupportedTimeframes`

## Design References

- Hyperliquid supports 100+ perp markets including crypto perps and HIP-3 real-world-asset (RWA) stock perps
- The `meta` endpoint (already used by `HyperliquidAssetMetadataCache`) returns the full asset universe dynamically
- The static `HyperliquidAssetMapper` currently hardcodes only 8 coins and 4 timeframes
- Hyperliquid actually supports timeframes: `1m`, `3m`, `5m`, `15m`, `30m`, `1h`, `2h`, `4h`, `8h`, `12h`, `1d`, `1w`, `1M`
- The `IsValidCoin` and `GetSupportedCoins` methods gate what assets users can trade — expanding them is critical

---

### Task 4.1: Remove hardcoded coin validation from `HyperliquidAssetMapper` {#task-41-remove-hardcoded-coin-validation}

Remove `IsValidCoin()` and `GetSupportedCoins()` methods that rely on the hardcoded 8-coin list. Replace `IsValidCoin` with a basic format check (non-empty, alphanumeric + colon for HIP-3). Leave the `DisplayToCoin` dictionary as a convenience lookup for common assets but don't use it for validation. (Review finding M7)

- **Complexity**: High
- **Risk Factors**: Any code calling `IsValidCoin` as a gate will now accept all coins — verify there's no security implication. `GetSupportedCoins` may be used by the UI for asset dropdowns — the UI should switch to using the metadata cache instead.
- **Files**:
  - `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidAssetMapper.cs` — modification
- **Success**:
  - `IsValidCoin` replaced with a format-based check that accepts any non-empty coin name
  - `GetSupportedCoins` removed or deprecated
  - `ToCoin` still handles existing display-to-coin mappings for backward compatibility
  - HIP-3 symbols (`XYZ:TSLA`, `CASH:USA500`) pass the new validation
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradePilot.Infrastructure/Hyperliquid/HyperliquidAssetMapper.cs — modification

    // Keep DisplayToCoin for backward-compatible display name normalization,
    // but do NOT use it for validation.

    /// <summary>
    /// Checks if a coin name is a valid Hyperliquid asset identifier.
    /// Accepts standard crypto coins (BTC, ETH, SOL, etc.) and HIP-3 RWA assets
    /// (XYZ:TSLA, CASH:USA500, etc.). Does not validate against the actual live asset list —
    /// use IHyperliquidAssetMetadataCache for definitive validation.
    /// </summary>
    public static bool IsValidCoin(string coin)
    {
        if (string.IsNullOrWhiteSpace(coin))
        {
            return false;
        }

        // Allow alphanumeric + colon (for HIP-3 namespace:symbol format)
        return coin.All(c => char.IsLetterOrDigit(c) || c == ':');
    }

    /// <summary>
    /// Returns the commonly-traded coins for UI quick-selection.
    /// This is NOT the full asset list — use IHyperliquidAssetMetadataCache for the live universe.
    /// </summary>
    public static IReadOnlyCollection<string> GetSupportedCoins()
    {
        return CoinToDisplay.Keys.OrderBy(coin => coin).ToArray();
    }
```

> **Note**: Keep `GetSupportedCoins()` as a convenience method for quick-select UI options, but update its documentation to clarify it's not the full list. The UI should also query the metadata cache for the complete list when a user searches for an asset.

##### Pattern References

- `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidAssetMapper.cs` — lines 82–85 (current `IsValidCoin`)
- `.agent-context/0-knowledge/32-hyperliquid-rwa-stock-perps.md` — HIP-3 naming convention

---

### Task 4.2: Expand supported timeframes in `HyperliquidAssetMapper` {#task-42-expand-supported-timeframes}

Add all Hyperliquid-supported timeframes to the `TimeframeToIntervalMs` dictionary. Currently only 4 timeframes; Hyperliquid supports 13. (Review finding m7)

- **Complexity**: Low
- **Risk Factors**: Minimal — additive change; existing 4 timeframes remain valid; new timeframes are standard interval calculations
- **Files**:
  - `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidAssetMapper.cs` — modification
- **Success**:
  - All 13 Hyperliquid timeframes present in `TimeframeToIntervalMs`
  - `IsValidTimeframe` and `GetSupportedTimeframes` reflect the expanded list
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradePilot.Infrastructure/Hyperliquid/HyperliquidAssetMapper.cs — modification
// Replace TimeframeToIntervalMs dictionary

    private static readonly Dictionary<string, long> TimeframeToIntervalMs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["1m"] = 1L * 60L * 1000L,
        ["3m"] = 3L * 60L * 1000L,
        ["5m"] = 5L * 60L * 1000L,
        ["15m"] = 15L * 60L * 1000L,
        ["30m"] = 30L * 60L * 1000L,
        ["1h"] = 60L * 60L * 1000L,
        ["2h"] = 2L * 60L * 60L * 1000L,
        ["4h"] = 4L * 60L * 60L * 1000L,
        ["8h"] = 8L * 60L * 60L * 1000L,
        ["12h"] = 12L * 60L * 60L * 1000L,
        ["1d"] = 24L * 60L * 60L * 1000L,
        ["1w"] = 7L * 24L * 60L * 60L * 1000L,
        ["1M"] = 30L * 24L * 60L * 60L * 1000L,
    };
```

> **Note**: `1M` uses 30 days as approximation. This matches Hyperliquid's convention.

##### Pattern References

- `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidAssetMapper.cs` — lines 24–30 (current 4 timeframes)
- Hyperliquid API docs: supported candle intervals

---

### Task 4.3: Update `HyperliquidCapabilities` to reflect expanded support {#task-43-update-hyperliquidcapabilities}

Update `HyperliquidCapabilities` to include the expanded timeframes (already automatic since it reads from `HyperliquidAssetMapper.GetSupportedTimeframes()`). Verify the `Supports` method works for non-hardcoded assets. (Review finding m7)

- **Complexity**: Low
- **Risk Factors**: None — `SupportedTimeframes` is already sourced from `HyperliquidAssetMapper.GetSupportedTimeframes()`, so expanding that dictionary automatically propagates
- **Files**:
  - `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidCapabilities.cs` — verification (may need no changes)
- **Success**:
  - `HyperliquidCapabilities.CapabilitySet.SupportedTimeframes` includes all 13 timeframes
  - `Supports` method works for any perp pair (not limited to 8 coins)
- **Dependencies**: Task 4.2

---

### Task 4.4: Update `HyperliquidExchangeSymbolMapper.CanMap` {#task-44-update-hyperliquidexchangesymbolmapper-canmap}

Verify `HyperliquidExchangeSymbolMapper.CanMap` does not depend on the hardcoded coin list. Currently it checks `pair.ProductType == AssetType.Perp && !string.IsNullOrWhiteSpace(pair.Base)` which is already correct — it doesn't call `IsValidCoin`. Confirm and document.

- **Complexity**: Low
- **Risk Factors**: None — verification task
- **Files**:
  - `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidExchangeSymbolMapper.cs` — verification
- **Success**:
  - `CanMap` does not call `IsValidCoin` or `GetSupportedCoins`
  - Any perp pair with a non-empty base passes `CanMap`
- **Dependencies**: None

---

### Task 4.5: Update tests {#task-45-update-tests}

Update existing `HyperliquidAssetMapperTests` and add new tests for expanded timeframes and relaxed coin validation.

- **Complexity**: Medium
- **Risk Factors**: Existing tests that assert on the hardcoded 8-coin list from `GetSupportedCoins()` will need updating; tests that depend on `IsValidCoin("UNKNOWN") == false` need updating
- **Files**:
  - `tests/TradePilot.Infrastructure.Tests/Services/HyperliquidAssetMapperTests.cs` — modification
- **Success**:
  - Tests verify all 13 timeframes are valid via `IsValidTimeframe`
  - Tests verify HIP-3 symbols pass `IsValidCoin` (e.g., `XYZ:TSLA`, `CASH:USA500`)
  - Tests verify unknown but well-formed coins pass `IsValidCoin` (e.g., `WIF`, `PEPE`)
  - Tests verify empty/null/whitespace fails `IsValidCoin`
  - Existing tests updated to reflect relaxed validation behavior
- **Dependencies**: Tasks 4.1–4.4

#### Implementation Details

```csharp
// tests/TradePilot.Infrastructure.Tests/Services/HyperliquidAssetMapperTests.cs — add/update tests

    [TestMethod]
    [DataRow("BTC")]
    [DataRow("ETH")]
    [DataRow("WIF")]
    [DataRow("PEPE")]
    [DataRow("XYZ:TSLA")]
    [DataRow("CASH:USA500")]
    public void GivenValidCoinName_WhenIsValidCoin_ThenReturnsTrue(string coin)
    {
        HyperliquidAssetMapper.IsValidCoin(coin).Should().BeTrue();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("BTC-PERP")]  // Contains hyphen — not a valid coin name (it's a display name)
    public void GivenInvalidCoinName_WhenIsValidCoin_ThenReturnsFalse(string? coin)
    {
        HyperliquidAssetMapper.IsValidCoin(coin!).Should().BeFalse();
    }

    [TestMethod]
    [DataRow("1m")]
    [DataRow("3m")]
    [DataRow("5m")]
    [DataRow("15m")]
    [DataRow("30m")]
    [DataRow("1h")]
    [DataRow("2h")]
    [DataRow("4h")]
    [DataRow("8h")]
    [DataRow("12h")]
    [DataRow("1d")]
    [DataRow("1w")]
    [DataRow("1M")]
    public void GivenSupportedTimeframe_WhenIsValidTimeframe_ThenReturnsTrue(string timeframe)
    {
        HyperliquidAssetMapper.IsValidTimeframe(timeframe).Should().BeTrue();
    }

    [TestMethod]
    public void GivenAllTimeframes_WhenGetSupportedTimeframes_ThenReturns13()
    {
        HyperliquidAssetMapper.GetSupportedTimeframes().Should().HaveCount(13);
    }
```

##### Pattern References

- `tests/TradePilot.Infrastructure.Tests/Services/HyperliquidAssetMapperTests.cs` — existing test structure with `[DataRow]`

---

### Task 4.6: Build and run all tests {#task-46-build-and-run-all-tests}

Build the solution and run all asset mapper and capabilities tests to verify Phase 4 changes.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification only)
- **Success**:
  - `dotnet build TradePilot.sln` succeeds
  - `dotnet test tests/TradePilot.Infrastructure.Tests/ --filter "FullyQualifiedName~AssetMapper"` passes
  - `dotnet test tests/TradePilot.Infrastructure.Tests/ --filter "FullyQualifiedName~Hyperliquid"` passes
  - `dotnet test tests/TradePilot.Api.Tests/ --filter "FullyQualifiedName~Hyperliquid"` passes
- **Dependencies**: Tasks 4.1–4.5

## Phase Success Criteria

- `IsValidCoin` accepts any well-formed coin name including HIP-3 (e.g., `XYZ:TSLA`)
- `TimeframeToIntervalMs` contains all 13 Hyperliquid timeframes
- `HyperliquidCapabilities.SupportedTimeframes` reflects all 13 timeframes
- `HyperliquidExchangeSymbolMapper.CanMap` works for any perp pair (not limited to 8 coins)
- All existing and new tests pass
- `GetSupportedCoins` docs clearly state it's a convenience subset, not the full list
