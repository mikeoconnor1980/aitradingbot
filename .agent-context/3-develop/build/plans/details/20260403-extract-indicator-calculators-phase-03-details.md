<!-- markdownlint-disable-file -->

# Task Details: F6.5 — Extract Indicator Calculators

## Phase 3: Refactor BacktestMarketContextBuilder + Extend IndicatorContext for MACD

## Standards and Knowledge References

- **csharp.instructions.md**: Sealed classes, factory methods, no regions
- **testing.instructions.md**: MSTest, FluentAssertions 6.12.2, Given_When_Then naming; existing tests updated only where necessary
- **dotnet-architecture.instructions.md**: Application layer references Domain and Indicators; no circular dependencies
- **Knowledge: 14-strategy-runtime-model.md**: Strategy engine receives `MarketContext` with `IndicatorContext` — downstream consumers unaffected by calculator refactoring
- **Knowledge: 18-backtesting-architecture.md**: Backtesting reuses same StrategyEngine, GridController, and RiskEngine — indicator calculators must produce consistent results

## Design References

- `BacktestMarketContextBuilder` currently has 5 private calculation methods: `CalculateEma`, `CalculateRsi`, `CalculatePreviousRsi`, `CalculatePreviousEma`, `CalculateAtr`. All are replaced by calls to the new static calculators.
- `IndicatorContext` stores MACD as a single scalar via `SetMacd(fast, slow, signal, value)`. This is extended to store line, signal, and histogram as separate keyed entries.
- `BuildIndicatorContext` switch has "RSI" and "EMA" cases; "MACD" case is added.

---

### Task 3.1: Extend `IndicatorContext` for MACD {#task-31-extend-indicatorcontext-for-macd}

Replace the single-scalar `SetMacd`/`GetMacd` with separate methods for line, signal, and histogram.

- **Complexity**: Medium
- **Risk Factors**: Must maintain backward compatibility — `GetMacd()` can continue to return the MACD line for existing callers. `IndicatorContextTests` needs updating.
- **Files**:
  - `src/TradePilot.Application/Trading/Models/IndicatorContext.cs` — modification
  - `tests/TradePilot.Application.Tests/Trading/Models/IndicatorContextTests.cs` — modification
- **Success**:
  - `SetMacd` stores line, signal, and histogram
  - `GetMacd` returns the MACD line (backward compatible)
  - New methods: `GetMacdSignal`, `GetMacdHistogram`, `GetPreviousMacdSignal`, `GetPreviousMacdHistogram`
  - All existing `IndicatorContextTests` still pass
  - New tests cover MACD signal and histogram
- **Dependencies**: None (can be done independently)

#### Implementation Details

**Caller verification**: `SetMacd` is currently called only from `BacktestMarketContextBuilder.BuildIndicatorContext`. No other callers exist in the codebase.

```csharp
// Replace existing SetMacd/GetMacd methods with multi-component versions

// ... existing code ...

public void SetMacd(
    int fast, int slow, int signal,
    decimal line, decimal signalLine, decimal histogram,
    decimal? previousLine = null, decimal? previousSignalLine = null, decimal? previousHistogram = null)
{
    var lineKey = CreateMacdKey(fast, slow, signal);
    var signalKey = CreateMacdSignalKey(fast, slow, signal);
    var histogramKey = CreateMacdHistogramKey(fast, slow, signal);

    _current[lineKey] = line;
    _current[signalKey] = signalLine;
    _current[histogramKey] = histogram;

    if (previousLine.HasValue)
    {
        _previous[lineKey] = previousLine.Value;
    }

    if (previousSignalLine.HasValue)
    {
        _previous[signalKey] = previousSignalLine.Value;
    }

    if (previousHistogram.HasValue)
    {
        _previous[histogramKey] = previousHistogram.Value;
    }
}

// GetMacd returns MACD line (backward compatible)
public decimal? GetMacd(int fast, int slow, int signal) =>
    GetValue(_current, CreateMacdKey(fast, slow, signal));

public decimal? GetPreviousMacd(int fast, int slow, int signal) =>
    GetValue(_previous, CreateMacdKey(fast, slow, signal));

public decimal? GetMacdSignal(int fast, int slow, int signal) =>
    GetValue(_current, CreateMacdSignalKey(fast, slow, signal));

public decimal? GetPreviousMacdSignal(int fast, int slow, int signal) =>
    GetValue(_previous, CreateMacdSignalKey(fast, slow, signal));

public decimal? GetMacdHistogram(int fast, int slow, int signal) =>
    GetValue(_current, CreateMacdHistogramKey(fast, slow, signal));

public decimal? GetPreviousMacdHistogram(int fast, int slow, int signal) =>
    GetValue(_previous, CreateMacdHistogramKey(fast, slow, signal));

// ... existing code ...

private static string CreateMacdSignalKey(int fast, int slow, int signal) =>
    $"MACD:{fast}:{slow}:{signal}:signal";

private static string CreateMacdHistogramKey(int fast, int slow, int signal) =>
    $"MACD:{fast}:{slow}:{signal}:histogram";
```

The existing `CreateMacdKey` stays unchanged (`"MACD:{fast}:{slow}:{signal}"`) — it now stores the MACD line specifically, keeping backward compatibility with `GetMacd()`.

Update existing `IndicatorContextTests` to use the new `SetMacd` signature and add tests for the new getters.

##### Pattern References

- `src/TradePilot.Application/Trading/Models/IndicatorContext.cs` — existing Set/Get pattern for RSI and EMA
- `tests/TradePilot.Application.Tests/Trading/Models/IndicatorContextTests.cs` — existing test structure

---

### Task 3.2: Add Indicators Project Reference {#task-32-add-indicators-project-reference}

Add `TradePilot.Indicators` as a project reference to `TradePilot.Application.csproj`.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradePilot.Application/TradePilot.Application.csproj` — modification
- **Success**:
  - `TradePilot.Application` can reference `TradePilot.Indicators` types
  - No circular dependency
  - Solution builds
- **Dependencies**: Phase 1 completed

#### Implementation Details

```xml
<!-- src/TradePilot.Application/TradePilot.Application.csproj — modification -->
<!-- Add to existing ItemGroup with ProjectReference -->
<ItemGroup>
  <ProjectReference Include="..\TradePilot.Domain\TradePilot.Domain.csproj" />
  <ProjectReference Include="..\TradePilot.Indicators\TradePilot.Indicators.csproj" />
</ItemGroup>
```

##### Pattern References

- `src/TradePilot.Application/TradePilot.Application.csproj` — existing project reference pattern

---

### Task 3.3: Refactor `BacktestMarketContextBuilder` {#task-33-refactor-backtestmarketcontextbuilder}

Replace all private indicator calculation methods with calls to the new static calculators from `TradePilot.Indicators`. Remove the private methods.

- **Complexity**: Medium
- **Risk Factors**: The adapter layer must correctly project `List<Candle>` to `IReadOnlyList<decimal>` (closes) and `IReadOnlyList<(decimal, decimal, decimal)>` (OHLC tuples for ATR). Algorithm changes (SMA-seeded EMA, Wilder-smoothed RSI/ATR) will produce different numeric values — this is intentional per the PBI.
- **Files**:
  - `src/TradePilot.Application/Trading/Services/BacktestMarketContextBuilder.cs` — modification
- **Success**:
  - All 5 private methods (`CalculateEma`, `CalculateRsi`, `CalculatePreviousRsi`, `CalculatePreviousEma`, `CalculateAtr`) are removed
  - All calls delegate to `EmaCalculator`, `RsiCalculator`, `AtrCalculator` from `TradePilot.Indicators`
  - `IndicatorSnapshot` values use the new calculators
  - Solution builds
- **Dependencies**: Task 3.2

#### Implementation Details

**Important**: Verify the current file content matches the expected pre-refactor state before applying the full replacement. The current file has ~200 lines with 5 private calculation methods.

```csharp
// src/TradePilot.Application/Trading/Services/BacktestMarketContextBuilder.cs — full replacement
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Entities;
using TradePilot.Indicators;

namespace TradePilot.Application.Trading.Services;

public sealed class BacktestMarketContextBuilder : IMarketContextBuilder
{
    private readonly List<Candle> _candles = [];

    public void UpdateIndicators(Candle candle)
    {
        ArgumentNullException.ThrowIfNull(candle);
        _candles.Add(candle);
    }

    public MarketContext Build(Candle triggerCandle, Candle? latestOneHourCandle, Candle? latestFourHourCandle)
    {
        return Build(triggerCandle, latestOneHourCandle, latestFourHourCandle, null);
    }

    public MarketContext Build(
        Candle triggerCandle,
        Candle? latestOneHourCandle,
        Candle? latestFourHourCandle,
        IReadOnlyList<IndicatorRequirement>? requiredIndicators)
    {
        ArgumentNullException.ThrowIfNull(triggerCandle);

        var closes = _candles.Select(c => c.Close).ToList();
        var indicatorContext = BuildIndicatorContext(requiredIndicators, closes);

        return new MarketContext
        {
            Symbol = triggerCandle.Symbol,
            TimestampUtc = triggerCandle.Timestamp,
            CurrentCandle = triggerCandle,
            LatestOneHourCandle = latestOneHourCandle,
            LatestFourHourCandle = latestFourHourCandle,
            Indicators = new IndicatorSnapshot
            {
                EmaFast = EmaCalculator.Calculate(closes, 9) ?? 0m,
                EmaSlow = EmaCalculator.Calculate(closes, 21) ?? 0m,
                EmaTrend = latestFourHourCandle?.Close ?? EmaCalculator.Calculate(closes, 55) ?? 0m,
                Rsi = RsiCalculator.Calculate(closes, 14) ?? 50m,
                Atr = AtrCalculator.Calculate(GetBars(), 14) ?? 0m
            },
            IndicatorContext = indicatorContext
        };
    }

    private IndicatorContext? BuildIndicatorContext(
        IReadOnlyList<IndicatorRequirement>? requirements,
        IReadOnlyList<decimal> closes)
    {
        if (requirements is null || requirements.Count == 0)
        {
            return null;
        }

        var context = new IndicatorContext();
        var previousCloses = closes.Count > 1 ? (IReadOnlyList<decimal>)closes.Take(closes.Count - 1).ToList() : [];

        foreach (var requirement in requirements)
        {
            switch (requirement.Type.ToUpperInvariant())
            {
                case "RSI":
                    context.SetRsi(
                        requirement.Period,
                        RsiCalculator.Calculate(closes, requirement.Period) ?? 50m,
                        RsiCalculator.Calculate(previousCloses, requirement.Period));
                    break;

                case "EMA":
                    context.SetEma(
                        requirement.Period,
                        EmaCalculator.Calculate(closes, requirement.Period) ?? 0m,
                        EmaCalculator.Calculate(previousCloses, requirement.Period));
                    break;

                case "MACD":
                    var fast = requirement.FastPeriod ?? 12;
                    var slow = requirement.SlowPeriod ?? 26;
                    var signal = requirement.SignalPeriod ?? 9;
                    var current = MacdCalculator.Calculate(closes, fast, slow, signal);
                    var previous = MacdCalculator.Calculate(previousCloses, fast, slow, signal);

                    if (current is not null)
                    {
                        context.SetMacd(
                            fast, slow, signal,
                            current.Line, current.Signal, current.Histogram,
                            previous?.Line, previous?.Signal, previous?.Histogram);
                    }
                    break;
            }
        }

        return context;
    }

    private IReadOnlyList<(decimal High, decimal Low, decimal Close)> GetBars()
    {
        return _candles.Select(c => (c.High, c.Low, c.Close)).ToList();
    }
}
```

**Key changes from current implementation:**
1. `CalculateEma(period)` → `EmaCalculator.Calculate(closes, period) ?? 0m`
2. `CalculateRsi(period)` → `RsiCalculator.Calculate(closes, period) ?? 50m`
3. `CalculatePreviousRsi(period)` → `RsiCalculator.Calculate(previousCloses, period)`
4. `CalculatePreviousEma(period)` → `EmaCalculator.Calculate(previousCloses, period)`
5. `CalculateAtr(period)` → `AtrCalculator.Calculate(GetBars(), period) ?? 0m`
6. All 5 private methods deleted
7. `closes` extracted once in `Build()` and passed to `BuildIndicatorContext()`
8. `previousCloses` = `closes[..^1]` computed once for "previous" values

##### Pattern References

- `src/TradePilot.Application/Trading/Services/BacktestMarketContextBuilder.cs` — current file being refactored
- `src/TradePilot.Indicators/EmaCalculator.cs`, `RsiCalculator.cs`, `AtrCalculator.cs`, `MacdCalculator.cs` — new calculator APIs

---

### Task 3.4: Add MACD Case to `BuildIndicatorContext` {#task-34-add-macd-case-to-buildindicatorcontext}

This task is covered within Task 3.3 (the `case "MACD":` branch is included in the refactored `BuildIndicatorContext` method above). No separate action needed — this task exists as a checklist verification point.

- **Complexity**: Low (included in Task 3.3)
- **Risk Factors**: None
- **Files**:
  - `src/TradePilot.Application/Trading/Services/BacktestMarketContextBuilder.cs` — covered by Task 3.3
- **Success**:
  - `case "MACD":` branch exists in `BuildIndicatorContext`
  - Uses `MacdCalculator.Calculate()` and calls `context.SetMacd()` with line, signal, histogram
  - MACD parameters come from `requirement.FastPeriod`, `requirement.SlowPeriod`, `requirement.SignalPeriod`
- **Dependencies**: Task 3.3

---

### Task 3.5: Update Existing Tests {#task-35-update-existing-tests}

Update `BacktestMarketContextBuilderIndicatorTests` to work with the refactored code. The existing tests assert non-null (structural), not specific values, so they should pass with the corrected algorithms. Verify and fix if needed.

- **Complexity**: Low
- **Risk Factors**: Algorithm changes produce different numeric values. Existing tests only check non-null, so they should pass. If any assertion checks specific values, update them.
- **Files**:
  - `tests/TradePilot.Application.Tests/Trading/Services/BacktestMarketContextBuilderIndicatorTests.cs` — verification/modification
- **Success**:
  - All existing `BacktestMarketContextBuilderIndicatorTests` pass
  - No changes needed if tests only assert structural properties (non-null)
  - If any test asserts specific values, update expected values to match corrected algorithms
- **Dependencies**: Task 3.3

#### Implementation Details

The existing tests create 20 candles and assert:
- `result.IndicatorContext.Should().NotBeNull()` — still true after refactoring
- `result.IndicatorContext!.GetRsi(14).Should().NotBeNull()` — still true (20 candles > 15 required for RSI(14))
- `result.IndicatorContext.GetPreviousRsi(14).Should().NotBeNull()` — still true (19 previous candles > 15 required)

No changes expected to the test file itself. This task is primarily a verification step.

##### Pattern References

- `tests/TradePilot.Application.Tests/Trading/Services/BacktestMarketContextBuilderIndicatorTests.cs` — current test file

---

### Task 3.6: Add MACD Integration Test {#task-36-add-macd-integration-test}

Add a test that verifies `BacktestMarketContextBuilder` correctly populates MACD line, signal, and histogram in `IndicatorContext` when MACD requirements are provided.

- **Complexity**: Medium
- **Risk Factors**: Need enough candles for MACD warmup (~35+ for standard 12/26/9)
- **Files**:
  - `tests/TradePilot.Application.Tests/Trading/Services/BacktestMarketContextBuilderIndicatorTests.cs` — modification
- **Success**:
  - New test verifies MACD line, signal, and histogram are non-null
  - New test verifies histogram = line - signal
  - Test passes
- **Dependencies**: Tasks 3.3, 3.4

#### Implementation Details

```csharp
// tests/TradePilot.Application.Tests/Trading/Services/BacktestMarketContextBuilderIndicatorTests.cs — addition

[TestMethod]
public void GivenMacdRequirement_WhenBuild_ThenMacdValuesArePopulated()
{
    var sut = new BacktestMarketContextBuilder();
    var candles = CreateCandles(40);

    foreach (var candle in candles)
    {
        sut.UpdateIndicators(candle);
    }

    var result = sut.Build(
        candles[^1],
        null,
        null,
        [new IndicatorRequirement
        {
            Type = "MACD",
            FastPeriod = 12,
            SlowPeriod = 26,
            SignalPeriod = 9
        }]);

    result.IndicatorContext.Should().NotBeNull();
    result.IndicatorContext!.GetMacd(12, 26, 9).Should().NotBeNull();
    result.IndicatorContext.GetMacdSignal(12, 26, 9).Should().NotBeNull();
    result.IndicatorContext.GetMacdHistogram(12, 26, 9).Should().NotBeNull();

    // Histogram should equal line minus signal
    var line = result.IndicatorContext.GetMacd(12, 26, 9)!.Value;
    var signal = result.IndicatorContext.GetMacdSignal(12, 26, 9)!.Value;
    var histogram = result.IndicatorContext.GetMacdHistogram(12, 26, 9)!.Value;
    histogram.Should().Be(line - signal);
}

[TestMethod]
public void GivenMacdRequirement_WhenBuild_ThenPreviousMacdValuesArePopulated()
{
    var sut = new BacktestMarketContextBuilder();
    var candles = CreateCandles(40);

    foreach (var candle in candles)
    {
        sut.UpdateIndicators(candle);
    }

    var result = sut.Build(
        candles[^1],
        null,
        null,
        [new IndicatorRequirement
        {
            Type = "MACD",
            FastPeriod = 12,
            SlowPeriod = 26,
            SignalPeriod = 9
        }]);

    result.IndicatorContext.Should().NotBeNull();
    result.IndicatorContext!.GetPreviousMacd(12, 26, 9).Should().NotBeNull();
    result.IndicatorContext.GetPreviousMacdSignal(12, 26, 9).Should().NotBeNull();
    result.IndicatorContext.GetPreviousMacdHistogram(12, 26, 9).Should().NotBeNull();
}
```

##### Pattern References

- `tests/TradePilot.Application.Tests/Trading/Services/BacktestMarketContextBuilderIndicatorTests.cs` — existing test structure with `CreateCandles` helper

---

### Task 3.7: Build and Run All Tests {#task-37-build-and-run-all-tests}

Build the full solution and run ALL tests to verify Phase 3 changes and ensure no regressions.

- **Complexity**: Low
- **Risk Factors**: Algorithm corrections (EMA, RSI, ATR) change numeric outputs. Existing tests that check structural properties (non-null) should still pass. Any test pinning specific indicator values will need updating.
- **Files**: None (verification only)
- **Success**:
  - `dotnet build TradePilot.sln --configuration Release` succeeds with no errors
  - `dotnet test TradePilot.sln --configuration Release --no-build` — ALL tests pass across ALL projects
  - Zero test failures
- **Dependencies**: Tasks 3.1–3.6

## Phase Success Criteria

- `IndicatorContext` exposes MACD line, signal, and histogram via `GetMacd()`, `GetMacdSignal()`, `GetMacdHistogram()` (+ previous variants)
- `BacktestMarketContextBuilder` has zero private indicator calculation methods
- `BacktestMarketContextBuilder` uses `EmaCalculator`, `RsiCalculator`, `AtrCalculator`, `MacdCalculator` from `TradePilot.Indicators`
- MACD case added to `BuildIndicatorContext` switch
- All existing tests pass (structural assertions unaffected by algorithm corrections)
- New MACD integration tests pass
- Full solution builds and all tests pass: `dotnet build TradePilot.sln && dotnet test TradePilot.sln`
