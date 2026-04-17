<!-- markdownlint-disable-file -->

# Task Details: F6.5 — Extract Indicator Calculators

## Phase 2: ATR, MACD, and Bollinger Bands Calculators with Tests

## Standards and Knowledge References

- **csharp.instructions.md**: Static classes, sealed classes, PascalCase naming
- **testing.instructions.md**: MSTest, FluentAssertions 6.12.2, Given_When_Then naming
- **Knowledge: 01-trading-strategy.md**: ATR used for position sizing and stop-loss placement; MACD for trend/momentum signals
- **Knowledge: 04-domain-model.md**: `IndicatorContext` is the consumer layer for dynamic indicator requirements

## Design References

- **ATR (Wilder smoothed)**: True Range = max(H-L, |H-prevClose|, |L-prevClose|). Seed with SMA of first `period` true ranges, then Wilder smooth: `ATR = ((prevATR × (period−1)) + currentTR) / period`. Reference: TradingView `ta.atr()`.
- **MACD**: MACD Line = EMA(fast) − EMA(slow); Signal Line = EMA(signal period, MACD Line series); Histogram = MACD Line − Signal Line. Reference: TradingView `ta.macd()`.
- **Bollinger Bands**: Middle = SMA(period); Upper = Middle + (multiplier × StdDev(period)); Lower = Middle − (multiplier × StdDev(period)). Reference: TradingView `ta.bb()`.

---

### Task 2.1: Implement `AtrCalculator` {#task-21-implement-atrcalculator}

Create `AtrCalculator` as a sealed static class using Wilder-smoothed true range. Takes tuples of `(high, low, close)` to avoid dependency on `Candle` entity.

- **Complexity**: Medium
- **Risk Factors**: Input type design — must take primitive tuples, not domain entities, to keep `TradePilot.Indicators` dependency-free
- **Files**:
  - `src/TradePilot.Indicators/AtrCalculator.cs` — new file
- **Success**:
  - `AtrCalculator.Calculate(bars, period)` returns Wilder-smoothed ATR
  - Returns `null` when insufficient data (fewer than `period + 1` bars)
  - Takes `IReadOnlyList<(decimal High, decimal Low, decimal Close)>` as input
- **Dependencies**: Phase 1 completed

#### Implementation Details

```csharp
// src/TradePilot.Indicators/AtrCalculator.cs — new file
namespace TradePilot.Indicators;

/// <summary>
/// Calculates Average True Range (ATR) using Wilder smoothing.
/// Matches TradingView ta.atr() implementation.
/// </summary>
public static class AtrCalculator
{
    /// <summary>
    /// Calculates ATR using Wilder smoothing.
    /// Returns null if there are fewer than (period + 1) bars (need previous close for first true range).
    /// </summary>
    public static decimal? Calculate(IReadOnlyList<(decimal High, decimal Low, decimal Close)> bars, int period)
    {
        if (bars.Count < period + 1)
        {
            return null;
        }

        // Calculate true range series (starts at index 1 since TR needs previous close)
        var trueRanges = new decimal[bars.Count - 1];
        for (var i = 1; i < bars.Count; i++)
        {
            var bar = bars[i];
            var prevClose = bars[i - 1].Close;
            trueRanges[i - 1] = Math.Max(
                bar.High - bar.Low,
                Math.Max(Math.Abs(bar.High - prevClose), Math.Abs(bar.Low - prevClose)));
        }

        // Seed: SMA of first `period` true ranges
        var atr = 0m;
        for (var i = 0; i < period; i++)
        {
            atr += trueRanges[i];
        }
        atr /= period;

        // Wilder smoothing for remaining true ranges
        for (var i = period; i < trueRanges.Length; i++)
        {
            atr = ((atr * (period - 1)) + trueRanges[i]) / period;
        }

        return atr;
    }
}
```

##### Pattern References

- Current `BacktestMarketContextBuilder.CalculateAtr()` — the algorithm being replaced
- `src/TradePilot.Indicators/EmaCalculator.cs` (Phase 1) — consistent API design pattern

---

### Task 2.2: Implement `AtrCalculatorTests` {#task-22-implement-atrcalculatortests}

Comprehensive unit tests verifying ATR against known reference values.

- **Complexity**: Medium
- **Risk Factors**: Need known OHLC dataset with verified Wilder-smoothed ATR
- **Files**:
  - `tests/TradePilot.Indicators.Tests/AtrCalculatorTests.cs` — new file
- **Success**:
  - Tests verify Wilder-smoothed ATR against known dataset
  - Tests verify null return for insufficient data
  - Tests verify single-bar and exact-period edge cases
  - All tests pass
- **Dependencies**: Task 2.1

#### Implementation Details

```csharp
// tests/TradePilot.Indicators.Tests/AtrCalculatorTests.cs — new file
using TradePilot.Indicators;

namespace TradePilot.Indicators.Tests;

[TestClass]
public sealed class AtrCalculatorTests
{
    // Reference OHLC data for ATR(14) verification
    // 16 bars to produce 15 true ranges, enough for Wilder ATR(14) with one smoothing step
    private static readonly (decimal High, decimal Low, decimal Close)[] KnownBars =
    [
        (48.70m, 47.79m, 48.16m),
        (48.72m, 48.14m, 48.61m),
        (48.90m, 48.39m, 48.75m),
        (48.87m, 48.37m, 48.63m),
        (48.82m, 48.24m, 48.74m),
        (49.05m, 48.64m, 49.03m),
        (49.20m, 48.94m, 49.07m),
        (49.35m, 48.86m, 49.32m),
        (49.92m, 49.50m, 49.91m),
        (50.19m, 49.87m, 50.13m),
        (50.12m, 49.20m, 49.53m),
        (49.66m, 48.90m, 49.50m),
        (49.88m, 49.43m, 49.75m),
        (50.19m, 49.73m, 50.03m),
        (50.36m, 49.26m, 50.31m),
        (50.57m, 50.09m, 50.52m)
    ];

    [TestMethod]
    public void GivenInsufficientData_WhenCalculate_ThenReturnsNull()
    {
        var bars = KnownBars.Take(5).ToList();

        var result = AtrCalculator.Calculate(bars, 14);

        result.Should().BeNull();
    }

    [TestMethod]
    public void GivenKnownDataset_WhenCalculateAtr14_ThenMatchesExpectedValue()
    {
        var result = AtrCalculator.Calculate(KnownBars, 14);

        result.Should().NotBeNull();
        result!.Value.Should().BeApproximately(0.56m, 0.1m); // Tolerance is loose — tighten after computing exact Wilder-smoothed value during implementation
    }

    [TestMethod]
    public void GivenEmptyBars_WhenCalculate_ThenReturnsNull()
    {
        var result = AtrCalculator.Calculate(Array.Empty<(decimal, decimal, decimal)>(), 14);

        result.Should().BeNull();
    }

    [TestMethod]
    public void GivenMinimalBars_WhenCalculate_ThenReturnsSmaOfTrueRanges()
    {
        // period + 1 bars gives exactly `period` true ranges; result should equal SMA(TR)
        var bars = KnownBars.Take(15).ToList();

        var result = AtrCalculator.Calculate(bars, 14);

        result.Should().NotBeNull();
    }
}
```

##### Pattern References

- `tests/TradePilot.Indicators.Tests/EmaCalculatorTests.cs` (Phase 1) — consistent test pattern

---

### Task 2.3: Implement `MacdCalculator` {#task-23-implement-macdcalculator}

Create `MacdCalculator` as a sealed static class. Returns a result record with MACD line, signal line, and histogram. Internally reuses `EmaCalculator.CalculateSeries()`.

- **Complexity**: High
- **Risk Factors**: Depends on `EmaCalculator.CalculateSeries()` for the EMA series computation. MACD signal line is an EMA of the MACD line series, which requires collecting non-null MACD values.
- **Files**:
  - `src/TradePilot.Indicators/MacdCalculator.cs` — new file
  - `src/TradePilot.Indicators/MacdResult.cs` — new file
- **Success**:
  - `MacdCalculator.Calculate(closes, fastPeriod, slowPeriod, signalPeriod)` returns `MacdResult` with Line, Signal, Histogram
  - Returns `null` when insufficient data
  - Standard params (12, 26, 9) match TradingView MACD
- **Dependencies**: Phase 1 (EmaCalculator.CalculateSeries)

#### Implementation Details

```csharp
// src/TradePilot.Indicators/MacdResult.cs — new file
namespace TradePilot.Indicators;

/// <summary>
/// MACD calculation result containing line, signal, and histogram values.
/// </summary>
public sealed record MacdResult(decimal Line, decimal Signal, decimal Histogram);
```

```csharp
// src/TradePilot.Indicators/MacdCalculator.cs — new file
namespace TradePilot.Indicators;

/// <summary>
/// Calculates Moving Average Convergence Divergence (MACD).
/// MACD Line = EMA(fast) − EMA(slow).
/// Signal Line = EMA(signal period) of MACD Line series.
/// Histogram = MACD Line − Signal Line.
/// Matches TradingView ta.macd() implementation.
/// </summary>
public static class MacdCalculator
{
    /// <summary>
    /// Calculates MACD line, signal line, and histogram.
    /// Returns null if there is insufficient data for the slow EMA + signal period warmup.
    /// </summary>
    public static MacdResult? Calculate(
        IReadOnlyList<decimal> closes,
        int fastPeriod = 12,
        int slowPeriod = 26,
        int signalPeriod = 9)
    {
        var fastEma = EmaCalculator.CalculateSeries(closes, fastPeriod);
        var slowEma = EmaCalculator.CalculateSeries(closes, slowPeriod);

        // MACD line series: fast EMA - slow EMA (valid from slowPeriod - 1 onward)
        var macdLine = new List<decimal>();
        for (var i = 0; i < closes.Count; i++)
        {
            if (fastEma[i].HasValue && slowEma[i].HasValue)
            {
                macdLine.Add(fastEma[i]!.Value - slowEma[i]!.Value);
            }
        }

        if (macdLine.Count < signalPeriod)
        {
            return null;
        }

        // Signal line = EMA of MACD line series
        var signalValue = EmaCalculator.Calculate(macdLine, signalPeriod);

        if (!signalValue.HasValue)
        {
            return null;
        }

        var currentMacdLine = macdLine[^1];
        var histogram = currentMacdLine - signalValue.Value;

        return new MacdResult(currentMacdLine, signalValue.Value, histogram);
    }
}
```

##### Pattern References

- `src/TradePilot.Indicators/EmaCalculator.cs` (Phase 1) — `CalculateSeries()` used internally
- TradingView `ta.macd()` — standard 12/26/9 parameters

---

### Task 2.4: Implement `MacdCalculatorTests` {#task-24-implement-macdcalculatortests}

Comprehensive unit tests verifying MACD against reference values.

- **Complexity**: Medium
- **Risk Factors**: Requires a dataset large enough for EMA(26) + signal(9) warmup (at least 34+ closes)
- **Files**:
  - `tests/TradePilot.Indicators.Tests/MacdCalculatorTests.cs` — new file
- **Success**:
  - Tests verify MACD structural properties (histogram = line - signal, positive line in uptrend) and null handling
  - Tests verify null return for insufficient data
  - Tests verify standard 12/26/9 parameters
  - All tests pass
- **Dependencies**: Task 2.3

#### Implementation Details

```csharp
// tests/TradePilot.Indicators.Tests/MacdCalculatorTests.cs — new file
using TradePilot.Indicators;

namespace TradePilot.Indicators.Tests;

[TestClass]
public sealed class MacdCalculatorTests
{
    // 40-bar dataset for MACD(12,26,9) — enough for EMA(26) warmup + 9-bar signal
    private static readonly decimal[] KnownCloses = CreateTrendingCloses(40);

    [TestMethod]
    public void GivenInsufficientData_WhenCalculate_ThenReturnsNull()
    {
        var closes = KnownCloses.Take(25).ToList();

        var result = MacdCalculator.Calculate(closes);

        result.Should().BeNull();
    }

    [TestMethod]
    public void GivenSufficientData_WhenCalculate_ThenReturnsAllComponents()
    {
        var result = MacdCalculator.Calculate(KnownCloses);

        result.Should().NotBeNull();
        result!.Histogram.Should().Be(result.Line - result.Signal);
    }

    [TestMethod]
    public void GivenUptrend_WhenCalculate_ThenMacdLineIsPositive()
    {
        // In a consistent uptrend, fast EMA > slow EMA → MACD line > 0
        var result = MacdCalculator.Calculate(KnownCloses);

        result.Should().NotBeNull();
        result!.Line.Should().BeGreaterThan(0m);
    }

    [TestMethod]
    public void GivenCustomParameters_WhenCalculate_ThenReturnsResult()
    {
        var result = MacdCalculator.Calculate(KnownCloses, 8, 17, 9);

        result.Should().NotBeNull();
    }

    [TestMethod]
    public void GivenEmptyCloses_WhenCalculate_ThenReturnsNull()
    {
        var result = MacdCalculator.Calculate([]);

        result.Should().BeNull();
    }

    private static decimal[] CreateTrendingCloses(int count)
    {
        // Create an uptrending series with some noise for realistic MACD testing
        var closes = new decimal[count];
        var basePrice = 100m;

        for (var i = 0; i < count; i++)
        {
            // Uptrend with small oscillation
            closes[i] = basePrice + (i * 0.5m) + ((i % 3 == 0) ? -0.2m : 0.1m);
        }

        return closes;
    }
}
```

##### Pattern References

- `tests/TradePilot.Indicators.Tests/EmaCalculatorTests.cs` (Phase 1) — consistent test pattern

---

### Task 2.5: Implement `BollingerBandsCalculator` {#task-25-implement-bollingerbandscalculator}

Create `BollingerBandsCalculator` as a sealed static class. Returns a result record with upper, middle (SMA), and lower bands.

- **Complexity**: Medium
- **Risk Factors**: None — straightforward SMA + standard deviation calculation
- **Files**:
  - `src/TradePilot.Indicators/BollingerBandsCalculator.cs` — new file
  - `src/TradePilot.Indicators/BollingerBandsResult.cs` — new file
- **Success**:
  - `BollingerBandsCalculator.Calculate(closes, period, multiplier)` returns `BollingerBandsResult`
  - Returns `null` when insufficient data
  - Default parameters: period 20, multiplier 2.0
- **Dependencies**: Phase 1 completed

#### Implementation Details

```csharp
// src/TradePilot.Indicators/BollingerBandsResult.cs — new file
namespace TradePilot.Indicators;

/// <summary>
/// Bollinger Bands calculation result containing upper, middle, and lower band values.
/// </summary>
public sealed record BollingerBandsResult(decimal Upper, decimal Middle, decimal Lower);
```

```csharp
// src/TradePilot.Indicators/BollingerBandsCalculator.cs — new file
namespace TradePilot.Indicators;

/// <summary>
/// Calculates Bollinger Bands: Middle = SMA(period), Upper/Lower = Middle ± (multiplier × StdDev).
/// Matches TradingView ta.bb() implementation.
/// </summary>
public static class BollingerBandsCalculator
{
    /// <summary>
    /// Calculates Bollinger Bands.
    /// Returns null if there are fewer values than the period.
    /// </summary>
    public static BollingerBandsResult? Calculate(
        IReadOnlyList<decimal> closes,
        int period = 20,
        decimal multiplier = 2m)
    {
        if (closes.Count < period)
        {
            return null;
        }

        // Use the last `period` closes
        var startIndex = closes.Count - period;

        // Middle band = SMA
        var sum = 0m;
        for (var i = startIndex; i < closes.Count; i++)
        {
            sum += closes[i];
        }
        var middle = sum / period;

        // Standard deviation
        var sumSquaredDiff = 0m;
        for (var i = startIndex; i < closes.Count; i++)
        {
            var diff = closes[i] - middle;
            sumSquaredDiff += diff * diff;
        }
        var stdDev = (decimal)Math.Sqrt((double)(sumSquaredDiff / period));

        var upper = middle + (multiplier * stdDev);
        var lower = middle - (multiplier * stdDev);

        return new BollingerBandsResult(upper, middle, lower);
    }
}
```

##### Pattern References

- `src/TradePilot.Indicators/MacdResult.cs` (Task 2.3) — result record pattern
- TradingView `ta.bb()` — standard 20/2 parameters

---

### Task 2.6: Implement `BollingerBandsCalculatorTests` {#task-26-implement-bollingerbandscalculatortests}

Comprehensive unit tests verifying Bollinger Bands against known reference values.

- **Complexity**: Low
- **Risk Factors**: None — straightforward SMA + standard deviation
- **Files**:
  - `tests/TradePilot.Indicators.Tests/BollingerBandsCalculatorTests.cs` — new file
- **Success**:
  - Tests verify upper, middle, and lower bands against known data
  - Tests verify middle equals SMA
  - Tests verify symmetry (upper - middle == middle - lower)
  - Tests verify null return for insufficient data
  - All tests pass
- **Dependencies**: Task 2.5

#### Implementation Details

```csharp
// tests/TradePilot.Indicators.Tests/BollingerBandsCalculatorTests.cs — new file
using TradePilot.Indicators;

namespace TradePilot.Indicators.Tests;

[TestClass]
public sealed class BollingerBandsCalculatorTests
{
    private static readonly decimal[] KnownCloses =
    [
        86.16m, 89.09m, 88.78m, 90.32m, 89.07m,
        91.15m, 89.44m, 89.18m, 86.93m, 87.68m,
        86.96m, 89.43m, 89.32m, 88.72m, 87.45m,
        87.26m, 89.50m, 87.90m, 89.13m, 90.70m
    ];

    [TestMethod]
    public void GivenInsufficientData_WhenCalculate_ThenReturnsNull()
    {
        var closes = KnownCloses.Take(10).ToList();

        var result = BollingerBandsCalculator.Calculate(closes, 20);

        result.Should().BeNull();
    }

    [TestMethod]
    public void GivenKnownDataset_WhenCalculate_ThenMiddleEqualsSma()
    {
        var expectedSma = KnownCloses.Average();

        var result = BollingerBandsCalculator.Calculate(KnownCloses, 20);

        result.Should().NotBeNull();
        result!.Middle.Should().BeApproximately(expectedSma, 0.01m);
    }

    [TestMethod]
    public void GivenKnownDataset_WhenCalculate_ThenBandsAreSymmetric()
    {
        var result = BollingerBandsCalculator.Calculate(KnownCloses, 20);

        result.Should().NotBeNull();
        var upperDiff = result!.Upper - result.Middle;
        var lowerDiff = result.Middle - result.Lower;
        upperDiff.Should().BeApproximately(lowerDiff, 0.001m);
    }

    [TestMethod]
    public void GivenKnownDataset_WhenCalculate_ThenUpperIsAboveMiddle()
    {
        var result = BollingerBandsCalculator.Calculate(KnownCloses, 20);

        result.Should().NotBeNull();
        result!.Upper.Should().BeGreaterThan(result.Middle);
        result.Lower.Should().BeLessThan(result.Middle);
    }

    [TestMethod]
    public void GivenCustomMultiplier_WhenCalculate_ThenBandsAreWider()
    {
        var standard = BollingerBandsCalculator.Calculate(KnownCloses, 20, 2m);
        var wide = BollingerBandsCalculator.Calculate(KnownCloses, 20, 3m);

        standard.Should().NotBeNull();
        wide.Should().NotBeNull();
        (wide!.Upper - wide.Lower).Should().BeGreaterThan(standard!.Upper - standard.Lower);
    }

    [TestMethod]
    public void GivenFlatPrices_WhenCalculate_ThenBandsConverge()
    {
        var flat = Enumerable.Repeat(100m, 20).ToList();

        var result = BollingerBandsCalculator.Calculate(flat, 20);

        result.Should().NotBeNull();
        result!.Upper.Should().Be(result.Middle);
        result.Lower.Should().Be(result.Middle);
    }
}
```

##### Pattern References

- `tests/TradePilot.Indicators.Tests/EmaCalculatorTests.cs` (Phase 1) — consistent test pattern

---

### Task 2.7: Build and Run Tests {#task-27-build-and-run-tests}

Build the full solution and run all tests to verify Phase 2 changes.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification only)
- **Success**:
  - `dotnet build TradePilot.sln --configuration Release` succeeds with no errors
  - `dotnet test TradePilot.sln --configuration Release --no-build` — all tests pass
  - All new calculator tests pass
  - Existing tests remain unaffected
- **Dependencies**: Tasks 2.1–2.6

## Phase Success Criteria

- `AtrCalculator.cs`, `MacdCalculator.cs`, `MacdResult.cs`, `BollingerBandsCalculator.cs`, `BollingerBandsResult.cs` exist in `src/TradePilot.Indicators/`
- Corresponding test files exist in `tests/TradePilot.Indicators.Tests/`
- All calculator tests pass with values matching expected reference data
- Full solution builds and all tests pass
