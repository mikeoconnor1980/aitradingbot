<!-- markdownlint-disable-file -->

# Task Details: Portfolio Heat Enforcement

## Phase 1: Configuration + Heat Calculation Core

## Standards and Knowledge References

- **csharp.instructions.md**: Sealed classes, static factory methods, Guard validation, `IOptions<T>` for config
- **testing.instructions.md**: MSTest, Moq, FluentAssertions ≤ v6, Given_When_Then naming, builder pattern
- **33-risk-management-and-trade-sizing.md**: R formula, portfolio heat = sum of R, default 6%, 0 = disabled

## Design References

- R for `RiskBased` positions: `equity × riskPerTradePercent / 100` (recorded at entry)
- R for `PercentWallet`/`FixedNotional` with SL: `positionNotional × (stopLossPercent / 100)`
- R fallback (no SL): `marginUsed` (conservative proxy — margin posted to exchange)
- Portfolio heat %: `(Σ R_i / equity) × 100`

---

### Task 1.1: Add `MaxPortfolioHeatPercent` to `RiskLimitsConfig` {#task-11-add-maxportfolioheatpercent-to-risklimitsconfig}

Add the new configuration property to the existing `RiskLimitsConfig` record.

- **Complexity**: Low
- **Risk Factors**: None — additive change to an existing record
- **Files**:
  - `src/TradingApp.Application/StrategyAuthoring/Models/RiskLimitsConfig.cs` — Add new property
- **Success**:
  - `MaxPortfolioHeatPercent` property exists with default value of `6m`
  - Property has XML doc comment explaining its purpose
  - `0` means disabled
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Application/StrategyAuthoring/Models/RiskLimitsConfig.cs — modification
// Add after the CircuitBreakerCooldownMinutes property:

    /// <summary>
    /// Maximum portfolio heat (aggregate risk) as a percentage of equity.
    /// Heat = sum of R (risk in USD) across all open positions / equity × 100.
    /// 0 = disabled (no heat limit enforced).
    /// </summary>
    public decimal MaxPortfolioHeatPercent { get; init; } = 6m;
```

##### Pattern References

- `src/TradingApp.Application/StrategyAuthoring/Models/RiskLimitsConfig.cs` — existing property pattern with XML comments and defaults

---

### Task 1.2: Create `PortfolioHeatEntry` and `PortfolioHeatResult` models {#task-12-create-portfolioheatentry-and-portfolioheatresult-models}

Create models to represent per-position risk entries and heat calculation results.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.Application/Trading/Models/PortfolioHeatEntry.cs` — New file
  - `src/TradingApp.Application/Trading/Models/PortfolioHeatResult.cs` — New file
- **Success**:
  - `PortfolioHeatEntry` record with `Symbol`, `RiskUsd`, `RiskPercent` properties
  - `PortfolioHeatResult` record with `HeatPercent`, `HeatUsd`, `MaxHeatPercent`, `Entries`, `Equity`, `IsLimitExceeded`
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Application/Trading/Models/PortfolioHeatEntry.cs — new file
namespace TradingApp.Application.Trading.Models;

/// <summary>
/// Represents the risk contribution of a single open position to portfolio heat.
/// </summary>
public sealed record PortfolioHeatEntry
{
    public required string Symbol { get; init; }
    public required decimal RiskUsd { get; init; }
    public required decimal RiskPercent { get; init; }
}
```

```csharp
// src/TradingApp.Application/Trading/Models/PortfolioHeatResult.cs — new file
namespace TradingApp.Application.Trading.Models;

/// <summary>
/// Result of a portfolio heat calculation.
/// </summary>
public sealed record PortfolioHeatResult
{
    public required decimal HeatPercent { get; init; }
    public required decimal HeatUsd { get; init; }
    public required decimal MaxHeatPercent { get; init; }
    public required decimal Equity { get; init; }
    public required IReadOnlyList<PortfolioHeatEntry> Entries { get; init; }
    public bool IsLimitExceeded => MaxHeatPercent > 0 && HeatPercent > MaxHeatPercent;
    public bool IsLimitEnabled => MaxHeatPercent > 0;

    public static PortfolioHeatResult Empty(decimal maxHeatPercent = 0m) => new()
    {
        HeatPercent = 0m,
        HeatUsd = 0m,
        MaxHeatPercent = maxHeatPercent,
        Equity = 0m,
        Entries = []
    };
}
```

##### Pattern References

- `src/TradingApp.Application/Trading/Models/MarketContext.cs` — sealed record model pattern
- `src/TradingApp.Application/MarketData/Models/AccountSummaryDto.cs` — DTO property pattern

---

### Task 1.3: Create `PortfolioHeatCalculator` static class {#task-13-create-portfolioheatcalculator-static-class}

Create a stateless calculator that computes portfolio heat from position data.

- **Complexity**: Medium
- **Risk Factors**: Edge cases in R estimation for positions without SL
- **Files**:
  - `src/TradingApp.Application/Trading/Services/PortfolioHeatCalculator.cs` — New file
- **Success**:
  - `CalculateFromPositions` method computes heat from `PositionDto[]` + equity
  - `CalculateFromTrackedRisks` method computes heat from a dictionary of symbol → R USD
  - `EstimatePositionRisk` method estimates R for a single exchange position
  - Handles edge cases: zero equity, empty positions, no SL (margin fallback)
- **Dependencies**: Task 1.2

#### Implementation Details

```csharp
// src/TradingApp.Application/Trading/Services/PortfolioHeatCalculator.cs — new file
using TradingApp.Application.MarketData.Models;
using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.Trading.Services;

/// <summary>
/// Stateless calculator for portfolio heat (aggregate risk across open positions).
/// Used by both LiveRiskEngine (from tracked state) and API endpoint (from exchange positions).
/// </summary>
public static class PortfolioHeatCalculator
{
    /// <summary>
    /// Calculate portfolio heat from live exchange positions.
    /// Used by the API endpoint to compute heat on-the-fly.
    /// </summary>
    public static PortfolioHeatResult CalculateFromPositions(
        IReadOnlyList<PositionDto> positions,
        decimal equity,
        decimal maxHeatPercent)
    {
        if (positions.Count == 0 || equity <= 0)
        {
            return PortfolioHeatResult.Empty(maxHeatPercent);
        }

        var entries = new List<PortfolioHeatEntry>(positions.Count);
        var totalRiskUsd = 0m;

        foreach (var position in positions)
        {
            var riskUsd = EstimatePositionRisk(position);
            var riskPercent = equity > 0 ? (riskUsd / equity) * 100m : 0m;
            totalRiskUsd += riskUsd;

            entries.Add(new PortfolioHeatEntry
            {
                Symbol = position.Asset,
                RiskUsd = riskUsd,
                RiskPercent = riskPercent
            });
        }

        return new PortfolioHeatResult
        {
            HeatPercent = equity > 0 ? (totalRiskUsd / equity) * 100m : 0m,
            HeatUsd = totalRiskUsd,
            MaxHeatPercent = maxHeatPercent,
            Equity = equity,
            Entries = entries
        };
    }

    /// <summary>
    /// Calculate portfolio heat from the risk engine's tracked position risks.
    /// Used by LiveRiskEngine during signal validation.
    /// </summary>
    public static decimal CalculateHeatPercent(
        IEnumerable<decimal> positionRisksUsd,
        decimal equity)
    {
        if (equity <= 0)
        {
            return 0m;
        }

        var totalRisk = 0m;
        foreach (var risk in positionRisksUsd)
        {
            totalRisk += risk;
        }

        return (totalRisk / equity) * 100m;
    }

    /// <summary>
    /// Estimate the risk (R) in USD for an open position from exchange data.
    /// If stop-loss is set: R = |SL - entry| × |size|
    /// If no stop-loss: R = marginUsed (conservative proxy)
    /// </summary>
    public static decimal EstimatePositionRisk(PositionDto position)
    {
        if (position.StopLossPrice.HasValue && position.StopLossPrice.Value > 0)
        {
            return Math.Abs(position.StopLossPrice.Value - position.EntryPrice) * Math.Abs(position.Size);
        }

        // Fallback: use margin as conservative proxy for risk
        return Math.Abs(position.MarginUsed);
    }
}
```

##### Pattern References

- `src/TradingApp.Application/Trading/Services/PositionSizeResolver.cs` — static service pattern, R calculation logic

---

### Task 1.4: Update `appsettings.json` with `RiskLimits` section {#task-14-update-appssettingsjson-with-risklimits-section}

Add or update the `RiskLimits` configuration section in both API and Worker config files.

- **Complexity**: Low
- **Risk Factors**: None — additive config
- **Files**:
  - `src/TradingApp.Api/appsettings.json` — Add `RiskLimits` section (does not exist yet)
  - `src/TradingApp.Worker/appsettings.json` — Add `MaxPortfolioHeatPercent` to existing `RiskLimits` section
- **Success**:
  - Both config files contain `MaxPortfolioHeatPercent: 6`
  - Existing `RiskLimits` values in Worker config preserved
- **Dependencies**: Task 1.1

---

### Task 1.5: Unit tests for `PortfolioHeatCalculator` {#task-15-unit-tests-for-portfolioheatcalculator}

Create comprehensive tests for the heat calculation logic.

- **Complexity**: Medium
- **Risk Factors**: None — pure calculation tests
- **Files**:
  - `tests/TradingApp.Application.Tests/Trading/Services/PortfolioHeatCalculatorTests.cs` — New file
- **Success**:
  - Tests for `CalculateFromPositions`: empty positions, single position with SL, single position without SL, multiple mixed positions, zero equity
  - Tests for `CalculateHeatPercent`: basic calculation, empty risks, zero equity
  - Tests for `EstimatePositionRisk`: position with SL, position without SL, SL price = 0
  - All tests pass: `dotnet test tests/TradingApp.Application.Tests/ --filter "FullyQualifiedName~PortfolioHeatCalculator"`
- **Dependencies**: Task 1.3

#### Implementation Details

```csharp
// tests/TradingApp.Application.Tests/Trading/Services/PortfolioHeatCalculatorTests.cs — new file
using TradingApp.Application.MarketData.Models;
using TradingApp.Application.Trading.Services;

namespace TradingApp.Application.Tests.Trading.Services;

[TestClass]
public sealed class PortfolioHeatCalculatorTests
{
    [TestMethod]
    public void GivenPositionWithStopLoss_WhenEstimateRisk_ThenCalculatesFromSlDistance()
    {
        // Arrange
        var position = new PositionDto
        {
            Asset = "BTC",
            Size = 0.1m,
            EntryPrice = 50_000m,
            StopLossPrice = 48_500m,
            MarginUsed = 500m
        };

        // Act
        var risk = PortfolioHeatCalculator.EstimatePositionRisk(position);

        // Assert — |48500 - 50000| × 0.1 = 150
        risk.Should().Be(150m);
    }

    [TestMethod]
    public void GivenPositionWithoutStopLoss_WhenEstimateRisk_ThenUsesMarginAsFallback()
    {
        // Arrange
        var position = new PositionDto
        {
            Asset = "ETH",
            Size = 1m,
            EntryPrice = 3_000m,
            StopLossPrice = null,
            MarginUsed = 300m
        };

        // Act
        var risk = PortfolioHeatCalculator.EstimatePositionRisk(position);

        // Assert
        risk.Should().Be(300m);
    }

    [TestMethod]
    public void GivenMultiplePositions_WhenCalculateFromPositions_ThenSumsRisk()
    {
        // Arrange
        var positions = new List<PositionDto>
        {
            new() { Asset = "BTC", Size = 0.1m, EntryPrice = 50_000m, StopLossPrice = 49_000m, MarginUsed = 500m },
            new() { Asset = "ETH", Size = 1m, EntryPrice = 3_000m, StopLossPrice = null, MarginUsed = 300m }
        };
        var equity = 10_000m;

        // Act
        var result = PortfolioHeatCalculator.CalculateFromPositions(positions, equity, 6m);

        // Assert — BTC R = |49000-50000| × 0.1 = 100; ETH R = 300 (margin); total = 400
        result.HeatUsd.Should().Be(400m);
        result.HeatPercent.Should().Be(4m); // 400/10000 × 100
        result.Entries.Should().HaveCount(2);
        result.IsLimitExceeded.Should().BeFalse();
    }

    [TestMethod]
    public void GivenEmptyPositions_WhenCalculateFromPositions_ThenReturnsEmpty()
    {
        // Act
        var result = PortfolioHeatCalculator.CalculateFromPositions([], 10_000m, 6m);

        // Assert
        result.HeatPercent.Should().Be(0m);
        result.HeatUsd.Should().Be(0m);
        result.Entries.Should().BeEmpty();
    }

    [TestMethod]
    public void GivenZeroEquity_WhenCalculateFromPositions_ThenReturnsEmpty()
    {
        // Arrange
        var positions = new List<PositionDto>
        {
            new() { Asset = "BTC", Size = 0.1m, EntryPrice = 50_000m, StopLossPrice = 49_000m, MarginUsed = 500m }
        };

        // Act
        var result = PortfolioHeatCalculator.CalculateFromPositions(positions, 0m, 6m);

        // Assert
        result.HeatPercent.Should().Be(0m);
    }

    [TestMethod]
    public void GivenHeatExceedsLimit_WhenCalculateFromPositions_ThenIsLimitExceededTrue()
    {
        // Arrange — 6 positions each with R = $100, equity = $1000 → heat = 60%
        var positions = Enumerable.Range(0, 6).Select(i => new PositionDto
        {
            Asset = $"TOKEN{i}",
            Size = 1m,
            EntryPrice = 100m,
            StopLossPrice = null,
            MarginUsed = 100m
        }).ToList();

        // Act
        var result = PortfolioHeatCalculator.CalculateFromPositions(positions, 1_000m, 6m);

        // Assert
        result.HeatPercent.Should().Be(60m);
        result.IsLimitExceeded.Should().BeTrue();
    }

    [TestMethod]
    public void GivenDisabledLimit_WhenCalculateFromPositions_ThenIsLimitExceededFalse()
    {
        // Arrange
        var positions = new List<PositionDto>
        {
            new() { Asset = "BTC", Size = 1m, EntryPrice = 100m, StopLossPrice = null, MarginUsed = 100m }
        };

        // Act
        var result = PortfolioHeatCalculator.CalculateFromPositions(positions, 100m, 0m); // 0 = disabled

        // Assert
        result.IsLimitExceeded.Should().BeFalse();
        result.IsLimitEnabled.Should().BeFalse();
    }

    [TestMethod]
    public void GivenTrackedRisks_WhenCalculateHeatPercent_ThenReturnsCorrectPercent()
    {
        // Arrange
        var risks = new[] { 100m, 100m, 100m }; // 3 positions, R = $100 each

        // Act
        var heatPercent = PortfolioHeatCalculator.CalculateHeatPercent(risks, 10_000m);

        // Assert
        heatPercent.Should().Be(3m); // 300/10000 × 100
    }
}
```

##### Pattern References

- `tests/TradingApp.Application.Tests/Trading/Services/LiveRiskEngineTests.cs` — test setup pattern
- `tests/TradingApp.Application.Tests/Trading/Services/PositionSizeResolverTests.cs` — static service test pattern

---

### Task 1.6: Build verification {#task-16-build-verification}

Build the affected projects and run the new tests.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification only)
- **Success**:
  - `dotnet build src/TradingApp.Application/TradingApp.Application.csproj` succeeds
  - `dotnet test tests/TradingApp.Application.Tests/ --filter "FullyQualifiedName~PortfolioHeatCalculator"` — all tests pass
- **Dependencies**: Tasks 1.1–1.5

## Phase Success Criteria

- `RiskLimitsConfig` has `MaxPortfolioHeatPercent` property with default 6
- `PortfolioHeatCalculator` correctly computes heat from both exchange positions and tracked risks
- `appsettings.json` files contain the `RiskLimits` section
- All unit tests pass
