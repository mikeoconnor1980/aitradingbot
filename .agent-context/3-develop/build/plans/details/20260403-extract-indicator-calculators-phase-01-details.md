<!-- markdownlint-disable-file -->

# Task Details: F6.5 — Extract Indicator Calculators

## Phase 1: Project Scaffolding + EMA and RSI Calculators with Tests

## Standards and Knowledge References

- **csharp.instructions.md**: Static classes, sealed classes, PascalCase naming, no regions
- **testing.instructions.md**: MSTest, FluentAssertions 6.12.2, Given_When_Then naming, `[TestClass] public sealed class`, `Usings.cs` global usings
- **dotnet-architecture.instructions.md**: Pure domain/library projects have no unnecessary dependencies
- **Knowledge: 01-trading-strategy.md**: Indicators (EMA, RSI, ATR) used in grid strategy for entry/exit logic
- **Knowledge: 18-backtesting-architecture.md**: Backtesting reuses same strategy engine — indicator calculators must be shared

## Design References

- **EMA (SMA-seeded)**: Standard TradingView EMA implementation — SMA of first `period` closes as seed, then exponential smoothing. Reference: TradingView Pine Script `ta.ema()` documentation.
- **RSI (Wilder smoothing)**: Wilder's RSI from "New Concepts in Technical Trading Systems" (1978). Seed with SMA of first `period` gains/losses, then apply `((prevAvg × (period−1)) + current) / period`. Reference: TradingView Pine Script `ta.rsi()`.

---

### Task 1.1: Create Indicator Projects {#task-11-create-indicator-projects}

Create the `TradePilot.Indicators` class library project and `TradePilot.Indicators.Tests` test project.

- **Complexity**: Low
- **Risk Factors**: None — follows established project patterns
- **Files**:
  - `src/TradePilot.Indicators/TradePilot.Indicators.csproj` — new file
  - `tests/TradePilot.Indicators.Tests/TradePilot.Indicators.Tests.csproj` — new file
  - `tests/TradePilot.Indicators.Tests/Usings.cs` — new file
- **Success**:
  - Both projects build successfully
  - `TradePilot.Indicators.csproj` has ZERO project references and ZERO package references
  - `TradePilot.Indicators.Tests.csproj` references only `TradePilot.Indicators`
- **Dependencies**: None

#### Implementation Details

```xml
<!-- src/TradePilot.Indicators/TradePilot.Indicators.csproj — new file -->
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

```xml
<!-- tests/TradePilot.Indicators.Tests/TradePilot.Indicators.Tests.csproj — new file -->
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>

    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="FluentAssertions" Version="6.12.2" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.6.0" />
    <PackageReference Include="MSTest.TestAdapter" Version="3.0.4" />
    <PackageReference Include="MSTest.TestFramework" Version="3.0.4" />
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\TradePilot.Indicators\TradePilot.Indicators.csproj" />
  </ItemGroup>

</Project>
```

```csharp
// tests/TradePilot.Indicators.Tests/Usings.cs — new file
global using FluentAssertions;
global using Microsoft.VisualStudio.TestTools.UnitTesting;
```

##### Pattern References

- `src/TradePilot.Domain/TradePilot.Domain.csproj` — minimal csproj template with no dependencies
- `tests/TradePilot.Domain.Tests/TradePilot.Domain.Tests.csproj` — test project template with standard packages
- `tests/TradePilot.Application.Tests/Usings.cs` — global usings pattern

---

### Task 1.2: Add Projects to Solution {#task-12-add-projects-to-solution}

Add both new projects to `TradePilot.sln` using `dotnet sln add` commands.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `TradePilot.sln` — modification
- **Success**:
  - Both projects appear in Solution Explorer under correct folders (src / tests)
  - `dotnet build TradePilot.sln` succeeds
- **Dependencies**: Task 1.1

#### Implementation Details

Run these commands:

```powershell
dotnet sln TradePilot.sln add src/TradePilot.Indicators/TradePilot.Indicators.csproj --solution-folder src
dotnet sln TradePilot.sln add tests/TradePilot.Indicators.Tests/TradePilot.Indicators.Tests.csproj --solution-folder tests
```

##### Pattern References

- `TradePilot.sln` — existing solution structure with `src` folder GUID `{8EFC91D1-C9F7-4A74-84FE-088136B3CBA1}` and `tests` folder GUID `{267CC078-C992-4791-BACE-16A1B052B962}`

---

### Task 1.3: Implement `EmaCalculator` {#task-13-implement-emacalculator}

Create `EmaCalculator` as a sealed static class with SMA-seeded EMA calculation. This is a pure function: `IReadOnlyList<decimal>` in, `decimal?` out.

- **Complexity**: Medium
- **Risk Factors**: Algorithm correctness — must match TradingView EMA precisely. SMA seed must use first `period` values, not just first close.
- **Files**:
  - `src/TradePilot.Indicators/EmaCalculator.cs` — new file
- **Success**:
  - `EmaCalculator.Calculate(closes, period)` returns SMA-seeded EMA
  - Returns `null` when insufficient data (fewer than `period` values)
  - `CalculateSeries(closes, period)` returns full EMA series for MACD usage
- **Dependencies**: Task 1.1

#### Implementation Details

```csharp
// src/TradePilot.Indicators/EmaCalculator.cs — new file
namespace TradePilot.Indicators;

/// <summary>
/// Calculates Exponential Moving Average (EMA) using SMA-seeded initialisation.
/// Matches TradingView ta.ema() implementation.
/// </summary>
public static class EmaCalculator
{
    /// <summary>
    /// Calculates the EMA value for the last element in the series.
    /// Returns null if there are fewer values than the period.
    /// </summary>
    public static decimal? Calculate(IReadOnlyList<decimal> values, int period)
    {
        if (values.Count < period)
        {
            return null;
        }

        var smoothing = 2m / (period + 1m);

        // Seed with SMA of first `period` values
        var ema = 0m;
        for (var i = 0; i < period; i++)
        {
            ema += values[i];
        }
        ema /= period;

        // Apply EMA formula from period index onward
        for (var i = period; i < values.Count; i++)
        {
            ema = ((values[i] - ema) * smoothing) + ema;
        }

        return ema;
    }

    /// <summary>
    /// Calculates the full EMA series for all values.
    /// Returns null values for indices before the warmup period.
    /// Used by MACD calculator which needs the EMA series, not just the final value.
    /// </summary>
    public static IReadOnlyList<decimal?> CalculateSeries(IReadOnlyList<decimal> values, int period)
    {
        var result = new decimal?[values.Count];

        if (values.Count < period)
        {
            return result;
        }

        var smoothing = 2m / (period + 1m);

        // Seed with SMA of first `period` values
        var ema = 0m;
        for (var i = 0; i < period; i++)
        {
            ema += values[i];
        }
        ema /= period;

        result[period - 1] = ema;

        // Apply EMA formula from period index onward
        for (var i = period; i < values.Count; i++)
        {
            ema = ((values[i] - ema) * smoothing) + ema;
            result[i] = ema;
        }

        return result;
    }
}
```

##### Pattern References

- `src/TradePilot.Application/StrategyAuthoring/Services/IndicatorExtractor.cs` — static class pattern
- Current `BacktestMarketContextBuilder.CalculateEma()` — the algorithm being replaced

---

### Task 1.4: Implement `EmaCalculatorTests` {#task-14-implement-emacalculatortests}

Comprehensive unit tests verifying EMA against known reference values.

- **Complexity**: Medium
- **Risk Factors**: Reference values must be verified against TradingView or a known correct implementation
- **Files**:
  - `tests/TradePilot.Indicators.Tests/EmaCalculatorTests.cs` — new file
- **Success**:
  - Tests verify SMA-seeded behaviour
  - Tests verify against known reference data
  - Tests verify null return for insufficient data
  - Tests verify single-element and exact-period edge cases
  - All tests pass
- **Dependencies**: Task 1.3

#### Implementation Details

```csharp
// tests/TradePilot.Indicators.Tests/EmaCalculatorTests.cs — new file
using TradePilot.Indicators;

namespace TradePilot.Indicators.Tests;

[TestClass]
public sealed class EmaCalculatorTests
{
    // Reference: Known closing prices with pre-calculated EMA values
    // Uses a small dataset where EMA(5) can be hand-verified:
    // Closes: 22, 22.27, 22.19, 22.08, 22.17, 22.18, 22.13, 22.23, 22.43, 22.24
    // SMA(5) seed = (22 + 22.27 + 22.19 + 22.08 + 22.17) / 5 = 22.142
    // EMA[5] = ((22.18 - 22.142) * 0.3333) + 22.142 = 22.1547
    // ... continuing through the series

    private static readonly decimal[] KnownCloses =
    [
        22m, 22.27m, 22.19m, 22.08m, 22.17m,
        22.18m, 22.13m, 22.23m, 22.43m, 22.24m
    ];

    [TestMethod]
    public void GivenInsufficientData_WhenCalculate_ThenReturnsNull()
    {
        var result = EmaCalculator.Calculate([10m, 20m], 5);

        result.Should().BeNull();
    }

    [TestMethod]
    public void GivenExactPeriodCount_WhenCalculate_ThenReturnsSma()
    {
        // EMA with exactly `period` values should equal the SMA
        var closes = KnownCloses.Take(5).ToList();
        var expectedSma = closes.Average();

        var result = EmaCalculator.Calculate(closes, 5);

        result.Should().Be(expectedSma);
    }

    [TestMethod]
    public void GivenKnownDataset_WhenCalculateEma5_ThenMatchesExpectedValue()
    {
        var result = EmaCalculator.Calculate(KnownCloses, 5);

        // Hand-verified EMA(5) for the full 10-bar dataset
        result.Should().NotBeNull();
        result!.Value.Should().BeApproximately(22.2470m, 0.01m);
    }

    [TestMethod]
    public void GivenEmptyList_WhenCalculate_ThenReturnsNull()
    {
        var result = EmaCalculator.Calculate([], 5);

        result.Should().BeNull();
    }

    [TestMethod]
    public void GivenKnownDataset_WhenCalculateSeries_ThenFirstPeriodMinusOneEntriesAreNull()
    {
        var series = EmaCalculator.CalculateSeries(KnownCloses, 5);

        series.Should().HaveCount(KnownCloses.Length);
        series.Take(4).Should().AllSatisfy(v => v.Should().BeNull());
        series[4].Should().NotBeNull();
    }

    [TestMethod]
    public void GivenKnownDataset_WhenCalculateSeries_ThenLastValueMatchesCalculate()
    {
        var series = EmaCalculator.CalculateSeries(KnownCloses, 5);
        var singleValue = EmaCalculator.Calculate(KnownCloses, 5);

        series[^1].Should().Be(singleValue);
    }
}
```

##### Pattern References

- `tests/TradePilot.Application.Tests/Backtesting/Services/BacktestMetricsCalculatorTests.cs` — pure calculation test pattern with `BeApproximately`

---

### Task 1.5: Implement `RsiCalculator` {#task-15-implement-rsicalculator}

Create `RsiCalculator` as a sealed static class using Wilder smoothing.

- **Complexity**: Medium
- **Risk Factors**: Algorithm correctness — Wilder smoothing differs from simple RSI. Must seed with SMA of first `period` gains/losses, then apply exponential smoothing.
- **Files**:
  - `src/TradePilot.Indicators/RsiCalculator.cs` — new file
- **Success**:
  - `RsiCalculator.Calculate(closes, period)` returns Wilder-smoothed RSI
  - Returns `null` when insufficient data (fewer than `period + 1` values, since RSI needs deltas)
  - Handles edge cases: all gains → 100, all losses → 0, no movement → 50
- **Dependencies**: Task 1.1

#### Implementation Details

```csharp
// src/TradePilot.Indicators/RsiCalculator.cs — new file
namespace TradePilot.Indicators;

/// <summary>
/// Calculates Relative Strength Index (RSI) using Wilder smoothing.
/// Matches TradingView ta.rsi() implementation.
/// </summary>
public static class RsiCalculator
{
    /// <summary>
    /// Calculates RSI for the full series of closing prices.
    /// Returns null if there are fewer than (period + 1) values (need at least `period` deltas).
    /// </summary>
    public static decimal? Calculate(IReadOnlyList<decimal> closes, int period)
    {
        if (closes.Count < period + 1)
        {
            return null;
        }

        // Calculate price changes
        var deltas = new decimal[closes.Count - 1];
        for (var i = 0; i < deltas.Length; i++)
        {
            deltas[i] = closes[i + 1] - closes[i];
        }

        // Seed: SMA of first `period` gains and losses
        decimal avgGain = 0m;
        decimal avgLoss = 0m;

        for (var i = 0; i < period; i++)
        {
            if (deltas[i] >= 0)
            {
                avgGain += deltas[i];
            }
            else
            {
                avgLoss += Math.Abs(deltas[i]);
            }
        }

        avgGain /= period;
        avgLoss /= period;

        // Wilder smoothing for remaining deltas
        for (var i = period; i < deltas.Length; i++)
        {
            var gain = deltas[i] >= 0 ? deltas[i] : 0m;
            var loss = deltas[i] < 0 ? Math.Abs(deltas[i]) : 0m;

            avgGain = ((avgGain * (period - 1)) + gain) / period;
            avgLoss = ((avgLoss * (period - 1)) + loss) / period;
        }

        if (avgGain == 0m && avgLoss == 0m)
        {
            return 50m;
        }

        if (avgLoss == 0m)
        {
            return 100m;
        }

        var rs = avgGain / avgLoss;
        return 100m - (100m / (1m + rs));
    }
}
```

##### Pattern References

- Current `BacktestMarketContextBuilder.CalculateRsi()` — the algorithm being replaced
- Wilder RSI algorithm from "New Concepts in Technical Trading Systems" (J. Welles Wilder Jr., 1978)

---

### Task 1.6: Implement `RsiCalculatorTests` {#task-16-implement-rsicalculatortests}

Comprehensive unit tests verifying RSI against known reference values.

- **Complexity**: Medium
- **Risk Factors**: Reference values must match Wilder smoothed RSI (TradingView)
- **Files**:
  - `tests/TradePilot.Indicators.Tests/RsiCalculatorTests.cs` — new file
- **Success**:
  - Tests verify Wilder-smoothed RSI against known datasets
  - Tests verify null return for insufficient data
  - Tests verify edge cases (all gains → 100, all losses → 0)
  - All tests pass
- **Dependencies**: Task 1.5

#### Implementation Details

```csharp
// tests/TradePilot.Indicators.Tests/RsiCalculatorTests.cs — new file
using TradePilot.Indicators;

namespace TradePilot.Indicators.Tests;

[TestClass]
public sealed class RsiCalculatorTests
{
    // Reference dataset: 15 closes producing 14 deltas for RSI(14)
    // Verified against Wilder RSI algorithm
    private static readonly decimal[] KnownCloses =
    [
        44.34m, 44.09m, 44.15m, 43.61m, 44.33m,
        44.83m, 45.10m, 45.42m, 45.84m, 46.08m,
        45.89m, 46.03m, 45.61m, 46.28m, 46.28m
    ];

    [TestMethod]
    public void GivenInsufficientData_WhenCalculate_ThenReturnsNull()
    {
        // RSI(14) needs at least 15 values (14 deltas)
        var closes = KnownCloses.Take(14).ToList();

        var result = RsiCalculator.Calculate(closes, 14);

        result.Should().BeNull();
    }

    [TestMethod]
    public void GivenKnownDataset_WhenCalculateRsi14_ThenMatchesWilderSmoothedValue()
    {
        // Wilder RSI(14) for this dataset
        var result = RsiCalculator.Calculate(KnownCloses, 14);

        result.Should().NotBeNull();
        result!.Value.Should().BeApproximately(70.46m, 0.5m);
    }

    [TestMethod]
    public void GivenAllGains_WhenCalculate_ThenReturns100()
    {
        decimal[] allGains = [10m, 11m, 12m, 13m, 14m, 15m];

        var result = RsiCalculator.Calculate(allGains, 5);

        result.Should().Be(100m);
    }

    [TestMethod]
    public void GivenAllLosses_WhenCalculate_ThenReturnsNearZero()
    {
        decimal[] allLosses = [15m, 14m, 13m, 12m, 11m, 10m];

        var result = RsiCalculator.Calculate(allLosses, 5);

        result.Should().NotBeNull();
        result!.Value.Should().BeApproximately(0m, 0.01m);
    }

    [TestMethod]
    public void GivenNoMovement_WhenCalculate_ThenHandlesGracefully()
    {
        decimal[] flat = [50m, 50m, 50m, 50m, 50m, 50m];

        var result = RsiCalculator.Calculate(flat, 5);

        // Zero gains and zero losses — conventionally returns 50 (neutral)
        result.Should().Be(50m);
    }

    [TestMethod]
    public void GivenEmptyList_WhenCalculate_ThenReturnsNull()
    {
        var result = RsiCalculator.Calculate([], 14);

        result.Should().BeNull();
    }
}
```

##### Pattern References

- `tests/TradePilot.Application.Tests/Backtesting/Services/BacktestMetricsCalculatorTests.cs` — `BeApproximately` assertion pattern

---

### Task 1.7: Build and Run Tests {#task-17-build-and-run-tests}

Build the full solution and run all tests to verify Phase 1 changes.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification only)
- **Success**:
  - `dotnet build TradePilot.sln --configuration Release` succeeds with no errors
  - `dotnet test TradePilot.sln --configuration Release --no-build` — all tests pass
  - New `TradePilot.Indicators.Tests` tests all pass
  - Existing tests remain unaffected (no code was changed, only new projects added)
- **Dependencies**: Tasks 1.1–1.6

## Phase Success Criteria

- `src/TradePilot.Indicators/` project exists with `EmaCalculator.cs` and `RsiCalculator.cs`
- `tests/TradePilot.Indicators.Tests/` project exists with `EmaCalculatorTests.cs` and `RsiCalculatorTests.cs`
- Both projects registered in `TradePilot.sln` under correct solution folders
- `TradePilot.Indicators.csproj` has zero dependencies
- All EMA and RSI tests pass with values matching TradingView reference data
- Full solution builds and all tests pass
