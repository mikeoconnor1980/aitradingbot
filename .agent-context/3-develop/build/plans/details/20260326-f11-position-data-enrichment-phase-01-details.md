<!-- markdownlint-disable-file -->

# Task Details: F11 — Position Data Enrichment

## Phase 1: Backend — Enrich PositionDto with MarginUsed and FundingRate

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — sealed classes, _camelCase fields, async suffix, CancellationToken on all async I/O
- `.github/instructions/testing.instructions.md` — MSTest + Moq + FluentAssertions ≤v6, `Given_When_Then` naming
- `.github/instructions/api-controllers.instructions.md` — ProducesResponseType annotations, controller conventions
- `.agent-context/0-knowledge/02-hyperliquid-integration.md` — Extending rule 1: simple raw reads use `PostInfoAsync` directly in Api-layer service
- `.agent-context/0-knowledge/10-architecture-decisions.md` — ADR-14: POC account-state reads bypass MediatR

## Design References

- Hyperliquid `clearinghouseState` response includes `marginUsed` and `positionValue` per position (confirmed from docs and debug endpoint)
- Hyperliquid `metaAndAssetCtxs` response returns per-asset `HyperliquidAssetCtx` with `Funding` field (hourly rate)
- `HyperliquidRestClient.GetMarketInfoAsync()` already calls `metaAndAssetCtxs` and parses the response — the same pattern is reused

### Task 1.1: Add MarginUsed and FundingRate to PositionDto {#task-11-add-marginused-and-fundingrate-to-positiondto}

Add two new properties to `PositionDto` for margin used (from clearinghouseState) and funding rate (from metaAndAssetCtxs).

- **Complexity**: Low
- **Risk Factors**: None — simple property addition
- **Files**:
  - `src/TradePilot.Api/Models/PositionDto.cs` — add `MarginUsed` and `FundingRate` decimal properties
- **Success**:
  - `PositionDto` compiles with `MarginUsed` and `FundingRate` properties
  - Existing JSON serialization continues to work (new fields default to 0m)
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradePilot.Api/Models/PositionDto.cs — modification
namespace TradePilot.Api.Models;

public sealed class PositionDto
{
    public string Asset { get; set; } = string.Empty;
    public decimal Size { get; set; }
    public string Side { get; set; } = string.Empty;
    public decimal EntryPrice { get; set; }
    public decimal MarkPrice { get; set; }
    public decimal UnrealisedPnl { get; set; }
    public decimal UnrealisedPnlPercent { get; set; }
    public decimal LiquidationPrice { get; set; }
    public int Leverage { get; set; }
    public string MarginMode { get; set; } = string.Empty;
    public decimal MarginUsed { get; set; }
    public decimal FundingRate { get; set; }
}
```

##### Pattern References

- `src/TradePilot.Api/Models/PositionDto.cs` — existing DTO pattern with `{ get; set; }` and default values

### Task 1.2: Extract marginUsed from clearinghouseState {#task-12-extract-marginused-from-clearinghousestate}

Parse the `marginUsed` field from the Hyperliquid `clearinghouseState` position object in `MapToPositions()`. This field is already present in the API response but currently ignored.

- **Complexity**: Low
- **Risk Factors**: `marginUsed` may be "0" for cross-margin positions (per Hyperliquid docs, cross margin is pooled). Use fallback: `notional / leverage` for cross positions if `marginUsed` is 0.
- **Files**:
  - `src/TradePilot.Api/Services/HyperliquidAccountService.cs` — modify `MapToPositions()` to parse and set `MarginUsed`
- **Success**:
  - `PositionDto.MarginUsed` is populated from the `marginUsed` JSON field
  - For cross-margin positions where `marginUsed` is 0, the fallback calculation `abs(size) * markPrice / leverage` is used
- **Dependencies**: Task 1.1

#### Implementation Details

```csharp
// src/TradePilot.Api/Services/HyperliquidAccountService.cs — modification to MapToPositions()
// Within the foreach loop, after extracting leverage and marginMode:

var marginUsed = ParseDecimal(GetPropertyOrDefault(position, "marginUsed"));

// Fallback for cross-margin positions where marginUsed may be 0
if (marginUsed == 0m && markPrice > 0m && leverage > 0)
{
    marginUsed = Math.Abs(size) * markPrice / leverage;
}

results.Add(new PositionDto
{
    // ... existing fields ...
    MarginUsed = marginUsed,
});
```

##### Pattern References

- `src/TradePilot.Api/Services/HyperliquidAccountService.cs` — existing `ParseDecimal(GetPropertyOrDefault(...))` pattern used for all other position fields

### Task 1.3: Add GetFundingRatesAsync to Account Service {#task-13-add-getfundingratesasync-to-account-service}

Add a private method to `HyperliquidAccountService` that fetches funding rates for all assets via the `metaAndAssetCtxs` endpoint. This reuses the same API call pattern as `HyperliquidRestClient.GetMarketInfoAsync()`, but returns a dictionary of coin→fundingRate instead of a single asset's info.

Per ADR-14 Rule 1 and the extending pattern in `02-hyperliquid-integration.md`, simple raw reads go directly in the Api-layer service using `PostInfoAsync<T>`.

- **Complexity**: Medium
- **Risk Factors**: Requires cross-referencing the `metaAndAssetCtxs` response (indexed by position) with the `meta.Universe[i].Name` to get coin names. The `metaAndAssetCtxs` response returns both `universe` metadata and asset contexts in a single call, so no external cache is needed.
- **Files**:
  - `src/TradePilot.Api/Services/HyperliquidAccountService.cs` — add private method
- **Success**:
  - `GetFundingRatesAsync` returns `IReadOnlyDictionary<string, decimal>` mapping coin name → hourly funding rate
  - Method gracefully returns empty dictionary if API call fails
- **Dependencies**: None (uses existing infrastructure)

#### Implementation Details

```csharp
// src/TradePilot.Api/Services/HyperliquidAccountService.cs — modification

// Add private method to fetch all funding rates (no constructor changes needed)
    private async Task<IReadOnlyDictionary<string, decimal>> GetFundingRatesAsync(
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var request = new { type = "metaAndAssetCtxs" };
            var response = await _restClient.PostInfoAsync<JsonElement>(request, cancellationToken);

            if (response.ValueKind != JsonValueKind.Array || response.GetArrayLength() < 2)
            {
                _logger.LogWarning("Unexpected metaAndAssetCtxs response shape");
                return result;
            }

            var metaElement = response[0];
            var assetCtxs = response[1];

            if (!metaElement.TryGetProperty("universe", out var universe) ||
                universe.ValueKind != JsonValueKind.Array ||
                assetCtxs.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            var universeArray = universe.EnumerateArray().ToArray();
            var ctxArray = assetCtxs.EnumerateArray().ToArray();

            for (var i = 0; i < universeArray.Length && i < ctxArray.Length; i++)
            {
                var name = universeArray[i].TryGetProperty("name", out var nameEl)
                    ? nameEl.GetString() ?? string.Empty
                    : string.Empty;

                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                var funding = ParseDecimal(GetPropertyOrDefault(ctxArray[i], "funding"));
                result[name] = funding;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch funding rates from metaAndAssetCtxs");
        }

        return result;
    }
}
```

##### Pattern References

- `src/TradePilot.Infrastructure/Services/HyperliquidRestClient.cs` lines 168–200 — `GetMarketInfoAsync()` uses the same `metaAndAssetCtxs` endpoint, same `meta.Universe[i].Name` cross-referencing
- `src/TradePilot.Api/Services/HyperliquidAssetMetadataCache.cs` — similar `universe.EnumerateArray()` iteration pattern

### Task 1.4: Enrich GetPositionsAsync with funding rates {#task-14-enrich-getpositionsasync-with-funding-rates}

Modify `GetPositionsAsync` to call both `GetClearinghouseStateAsync` and `GetFundingRatesAsync` in parallel, then join funding rates into the position DTOs by asset name.

- **Complexity**: Medium
- **Risk Factors**: The two API calls run in parallel via `Task.WhenAll`. If the funding rate call fails, positions should still be returned with `FundingRate = 0m` (graceful degradation).
- **Files**:
  - `src/TradePilot.Api/Services/HyperliquidAccountService.cs` — modify `GetPositionsAsync`
- **Success**:
  - `GetPositionsAsync` returns positions with `FundingRate` populated from `metaAndAssetCtxs`
  - If funding rate fetch fails, positions are returned with `FundingRate = 0m`
  - Total latency increase is minimal due to parallel execution
- **Dependencies**: Task 1.2, Task 1.3

#### Implementation Details

```csharp
// src/TradePilot.Api/Services/HyperliquidAccountService.cs — modification

public async Task<IReadOnlyList<PositionDto>> GetPositionsAsync(CancellationToken cancellationToken = default)
{
    var clearinghouseTask = GetClearinghouseStateAsync(cancellationToken);
    var fundingRatesTask = GetFundingRatesAsync(cancellationToken);

    await Task.WhenAll(clearinghouseTask, fundingRatesTask);

    var positions = MapToPositions(clearinghouseTask.Result);
    var fundingRates = fundingRatesTask.Result;

    foreach (var position in positions)
    {
        if (fundingRates.TryGetValue(position.Asset, out var rate))
        {
            position.FundingRate = rate;
        }
    }

    return positions;
}
```

##### Pattern References

- `src/TradePilot.Api/Services/HyperliquidAccountService.cs` — existing `GetPositionsAsync` single-call pattern

### Task 1.5: Add unit tests for enriched mapping {#task-15-add-unit-tests-for-enriched-mapping}

Create a new test class `HyperliquidAccountServiceTests` to unit test the enriched position mapping logic. No existing unit tests exist for this service — controller integration tests cover it indirectly but don't verify field-level mapping.

- **Complexity**: Medium
- **Risk Factors**: The service uses constructor-injected `IHyperliquidRestClient`, `IHyperliquidSigner`, and now `IHyperliquidAssetMetadataCache`. All three must be mocked. The `PostInfoAsync<JsonElement>` mock must return valid JSON that matches the Hyperliquid response shape.
- **Files**:
  - `tests/TradePilot.Api.Tests/Services/HyperliquidAccountServiceTests.cs` — new file
- **Success**:
  - Test verifies `MarginUsed` is populated from `clearinghouseState` response
  - Test verifies `FundingRate` is populated from `metaAndAssetCtxs` response
  - Test verifies cross-margin fallback for `MarginUsed` when API returns 0
  - Test verifies graceful degradation when `metaAndAssetCtxs` fails
  - All tests pass
- **Dependencies**: Task 1.1 through 1.4

#### Implementation Details

```csharp
// tests/TradePilot.Api.Tests/Services/HyperliquidAccountServiceTests.cs — new file
using System.Text.Json;
using TradePilot.Api.Models;
using TradePilot.Api.Services;
using TradePilot.Application.Abstractions.Services;

namespace TradePilot.Api.Tests.Services;

[TestClass]
public sealed class HyperliquidAccountServiceTests
{
    private Mock<IHyperliquidRestClient> _restClientMock = null!;
    private Mock<IHyperliquidSigner> _signerMock = null!;
    private HyperliquidAccountService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _restClientMock = new Mock<IHyperliquidRestClient>();
        _signerMock = new Mock<IHyperliquidSigner>();

        _signerMock.Setup(s => s.WalletAddress).Returns("0xTestWallet");

        _sut = new HyperliquidAccountService(
            _restClientMock.Object,
            _signerMock.Object,
            Mock.Of<ILogger<HyperliquidAccountService>>());
    }

    [TestMethod]
    public async Task GivenPositionWithMarginUsed_WhenGetPositions_ThenMarginUsedIsMapped()
    {
        // Arrange
        var clearinghouseJson = JsonSerializer.Deserialize<JsonElement>("""
        {
            "assetPositions": [{
                "position": {
                    "coin": "BTC",
                    "szi": "0.01",
                    "entryPx": "60000",
                    "markPx": "61000",
                    "unrealizedPnl": "10",
                    "returnOnEquity": "0.01",
                    "liquidationPx": "55000",
                    "marginUsed": "122",
                    "leverage": { "type": "isolated", "value": 5 }
                }
            }],
            "marginSummary": { "accountValue": "1000" },
            "withdrawable": "500",
            "crossMaintenanceMarginUsed": "50"
        }
        """);

        var metaAndAssetCtxsJson = JsonSerializer.Deserialize<JsonElement>("""
        [
            { "universe": [{ "name": "BTC", "szDecimals": 5 }] },
            [{ "funding": "0.0001", "markPx": "61000", "openInterest": "100", "midPx": "61000", "oraclePx": "61000", "prevDayPx": "60000", "dayNtlVlm": "500000" }]
        ]
        """);

        SetupClearinghouseStateResponse(clearinghouseJson);
        SetupMetaAndAssetCtxsResponse(metaAndAssetCtxsJson);

        // Act
        var positions = await _sut.GetPositionsAsync();

        // Assert
        positions.Should().HaveCount(1);
        positions[0].MarginUsed.Should().Be(122m);
    }

    [TestMethod]
    public async Task GivenPositionWithFundingRate_WhenGetPositions_ThenFundingRateIsMapped()
    {
        // Arrange
        var clearinghouseJson = JsonSerializer.Deserialize<JsonElement>("""
        {
            "assetPositions": [{
                "position": {
                    "coin": "BTC",
                    "szi": "0.01",
                    "entryPx": "60000",
                    "markPx": "61000",
                    "unrealizedPnl": "10",
                    "returnOnEquity": "0.01",
                    "liquidationPx": "55000",
                    "marginUsed": "122",
                    "leverage": { "type": "isolated", "value": 5 }
                }
            }],
            "marginSummary": { "accountValue": "1000" },
            "withdrawable": "500",
            "crossMaintenanceMarginUsed": "50"
        }
        """);

        var metaAndAssetCtxsJson = JsonSerializer.Deserialize<JsonElement>("""
        [
            { "universe": [{ "name": "BTC", "szDecimals": 5 }] },
            [{ "funding": "0.0001", "markPx": "61000", "openInterest": "100", "midPx": "61000", "oraclePx": "61000", "prevDayPx": "60000", "dayNtlVlm": "500000" }]
        ]
        """);

        SetupClearinghouseStateResponse(clearinghouseJson);
        SetupMetaAndAssetCtxsResponse(metaAndAssetCtxsJson);

        // Act
        var positions = await _sut.GetPositionsAsync();

        // Assert
        positions.Should().HaveCount(1);
        positions[0].FundingRate.Should().Be(0.0001m);
    }

    [TestMethod]
    public async Task GivenCrossMarginPositionWithZeroMarginUsed_WhenGetPositions_ThenMarginUsedFallsBackToNotionalOverLeverage()
    {
        // Arrange — cross margin position where marginUsed = 0
        var clearinghouseJson = JsonSerializer.Deserialize<JsonElement>("""
        {
            "assetPositions": [{
                "position": {
                    "coin": "ETH",
                    "szi": "1.0",
                    "entryPx": "3000",
                    "markPx": "3100",
                    "unrealizedPnl": "100",
                    "returnOnEquity": "0.033",
                    "liquidationPx": "2500",
                    "marginUsed": "0",
                    "leverage": { "type": "cross", "value": 10 }
                }
            }],
            "marginSummary": { "accountValue": "5000" },
            "withdrawable": "3000",
            "crossMaintenanceMarginUsed": "200"
        }
        """);

        var metaAndAssetCtxsJson = JsonSerializer.Deserialize<JsonElement>("""
        [
            { "universe": [{ "name": "ETH", "szDecimals": 4 }] },
            [{ "funding": "-0.0002", "markPx": "3100", "openInterest": "200", "midPx": "3100", "oraclePx": "3100", "prevDayPx": "3000", "dayNtlVlm": "100000" }]
        ]
        """);

        SetupClearinghouseStateResponse(clearinghouseJson);
        SetupMetaAndAssetCtxsResponse(metaAndAssetCtxsJson);

        // Act
        var positions = await _sut.GetPositionsAsync();

        // Assert — fallback: abs(1.0) * 3100 / 10 = 310
        positions.Should().HaveCount(1);
        positions[0].MarginUsed.Should().Be(310m);
    }

    [TestMethod]
    public async Task GivenMetaAndAssetCtxsFails_WhenGetPositions_ThenPositionsReturnedWithZeroFundingRate()
    {
        // Arrange
        var clearinghouseJson = JsonSerializer.Deserialize<JsonElement>("""
        {
            "assetPositions": [{
                "position": {
                    "coin": "BTC",
                    "szi": "0.01",
                    "entryPx": "60000",
                    "markPx": "61000",
                    "unrealizedPnl": "10",
                    "returnOnEquity": "0.01",
                    "liquidationPx": "55000",
                    "marginUsed": "122",
                    "leverage": { "type": "isolated", "value": 5 }
                }
            }],
            "marginSummary": { "accountValue": "1000" },
            "withdrawable": "500",
            "crossMaintenanceMarginUsed": "50"
        }
        """);

        SetupClearinghouseStateResponse(clearinghouseJson);

        // metaAndAssetCtxs call throws
        _restClientMock
            .Setup(c => c.PostInfoAsync<JsonElement>(
                It.Is<object>(r => r.ToString()!.Contains("metaAndAssetCtxs")),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("API unavailable"));

        // Act
        var positions = await _sut.GetPositionsAsync();

        // Assert — positions returned, funding rate is 0
        positions.Should().HaveCount(1);
        positions[0].Asset.Should().Be("BTC");
        positions[0].MarginUsed.Should().Be(122m);
        positions[0].FundingRate.Should().Be(0m);
    }

    private void SetupClearinghouseStateResponse(JsonElement response)
    {
        _restClientMock
            .Setup(c => c.PostInfoAsync<JsonElement>(
                It.Is<object>(r => r.ToString()!.Contains("clearinghouseState")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
    }

    private void SetupMetaAndAssetCtxsResponse(JsonElement response)
    {
        _restClientMock
            .Setup(c => c.PostInfoAsync<JsonElement>(
                It.Is<object>(r => r.ToString()!.Contains("metaAndAssetCtxs")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
    }
}
```

**Important note**: The `It.Is<object>(r => r.ToString()!.Contains("clearinghouseState"))` matcher may not work with anonymous objects. If the anonymous object `ToString()` doesn't produce the expected string, the implementing agent should use a `Callback` to inspect the actual request shape or use a custom matcher. The test JSON uses raw string literals (C# 11 `"""`) for readability. The implementing agent should verify mock setup works with the actual anonymous object types used in the service and adjust the matcher strategy accordingly.

##### Pattern References

- `tests/TradePilot.Api.Tests/Services/HyperliquidOrderServiceTests.cs` — unit test pattern for Api-layer services with `_restClientMock`, `_signerMock` mocks
- `tests/TradePilot.Api.Tests/Usings.cs` — global usings: FluentAssertions, MSTest, Moq

### Task 1.6: Update AccountControllerTests for enriched DTO {#task-16-update-accountcontrollertests-for-enriched-dto}

Update the existing `AccountControllerTests.GivenPositionsExist_WhenGetPositions_ThenReturnsOkWithPositions` test to include the new `MarginUsed`, `FundingRate`, and `Leverage` fields in the test fixture and assertion.

- **Complexity**: Low
- **Risk Factors**: None — extending existing test data
- **Files**:
  - `tests/TradePilot.Api.Tests/Controllers/AccountControllerTests.cs` — modify test fixture
- **Success**:
  - Position test fixture includes `MarginUsed`, `FundingRate`, `Leverage`, `MarginMode`
  - `BeEquivalentTo` assertion verifies all fields match including new ones
  - All existing tests continue to pass
- **Dependencies**: Task 1.1

#### Implementation Details

```csharp
// tests/TradePilot.Api.Tests/Controllers/AccountControllerTests.cs — modification
// In GivenPositionsExist_WhenGetPositions_ThenReturnsOkWithPositions:
var positions = new List<PositionDto>
{
    new()
    {
        Asset = "BTC",
        Size = 0.1m,
        Side = "Long",
        EntryPrice = 60000m,
        MarkPrice = 61000m,
        UnrealisedPnl = 100m,
        UnrealisedPnlPercent = 1.67m,
        LiquidationPrice = 55000m,
        Leverage = 10,
        MarginMode = "cross",
        MarginUsed = 610m,
        FundingRate = 0.0001m,
    },
};
```

##### Pattern References

- `tests/TradePilot.Api.Tests/Controllers/AccountControllerTests.cs` — existing test fixture and `BeEquivalentTo` assertion pattern

### Task 1.7: Run all backend tests {#task-17-run-all-backend-tests}

Run all backend test projects to verify nothing is broken by the changes.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification step)
- **Success**:
  - `dotnet test` passes for all test projects: `TradePilot.Api.Tests`, `TradePilot.Infrastructure.Tests`, `TradePilot.Application.Tests`, `TradePilot.Domain.Tests`
  - Zero test failures
- **Dependencies**: All previous tasks in Phase 1

## Phase Success Criteria

- `PositionDto` includes `MarginUsed` and `FundingRate` properties
- `GET /api/account/positions` returns positions with populated `MarginUsed` and `FundingRate`
- `MarginUsed` falls back to `notional / leverage` for cross-margin positions where API returns 0
- Funding rate is fetched from `metaAndAssetCtxs` and joined by asset name
- If `metaAndAssetCtxs` fails, positions are still returned with `FundingRate = 0m`
- All existing and new backend tests pass
