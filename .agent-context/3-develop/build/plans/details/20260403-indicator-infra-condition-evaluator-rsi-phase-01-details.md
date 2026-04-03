<!-- markdownlint-disable-file -->

# Task Details: F5 — Indicator Infrastructure & Condition Evaluator (RSI)

## Phase 1: Indicator Infrastructure

## Standards and Knowledge References

- **csharp.instructions.md**: sealed classes, interfaces in same file as implementation for Application services, Given_When_Then test naming
- **testing.instructions.md**: MSTest, Moq, FluentAssertions 6, private static Create* helpers (no builder classes for test data)
- **dotnet-architecture.instructions.md**: Application layer service patterns, bounded context folder structure
- **Knowledge**: `14-strategy-runtime-model.md` (pipeline interfaces), `13-strategy-config-schema.md` (entry condition structure)

---

### Task 1.1: Create `IndicatorContext` model {#task-11-create-indicatorcontext-model}

Create a new model that holds computed indicator values keyed by type and period, supporting both current and previous values (needed for cross detection).

- **Complexity**: Medium
- **Risk Factors**: Must be designed to support current + previous values for cross detection; keyed lookup by (type, period)
- **Files**:
  - `src/TradingApp.Application/Trading/Models/IndicatorContext.cs` — **New**
- **Success**:
  - `IndicatorContext` class exists with `GetRsi(int period)`, `GetPreviousRsi(int period)`, `GetEma(int period)`, `GetPreviousEma(int period)` methods
  - Returns `decimal?` for missing indicators

#### Implementation Details

```csharp
// src/TradingApp.Application/Trading/Models/IndicatorContext.cs — new file
namespace TradingApp.Application.Trading.Models;

/// <summary>
/// Holds computed indicator values keyed by type and period.
/// Supports current and previous values for cross detection.
/// </summary>
public sealed class IndicatorContext
{
    private readonly Dictionary<string, decimal> _current = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, decimal> _previous = new(StringComparer.OrdinalIgnoreCase);

    public void SetRsi(int period, decimal currentValue, decimal? previousValue = null)
    {
        _current[$"RSI:{period}"] = currentValue;
        if (previousValue.HasValue)
        {
            _previous[$"RSI:{period}"] = previousValue.Value;
        }
    }

    public void SetEma(int period, decimal currentValue, decimal? previousValue = null)
    {
        _current[$"EMA:{period}"] = currentValue;
        if (previousValue.HasValue)
        {
            _previous[$"EMA:{period}"] = previousValue.Value;
        }
    }

    public void SetMacd(int fast, int slow, int signal, decimal currentValue, decimal? previousValue = null)
    {
        _current[$"MACD:{fast}:{slow}:{signal}"] = currentValue;
        if (previousValue.HasValue)
        {
            _previous[$"MACD:{fast}:{slow}:{signal}"] = previousValue.Value;
        }
    }

    public decimal? GetRsi(int period) => _current.GetValueOrDefault($"RSI:{period}");
    public decimal? GetPreviousRsi(int period) => _previous.GetValueOrDefault($"RSI:{period}");
    public decimal? GetEma(int period) => _current.GetValueOrDefault($"EMA:{period}");
    public decimal? GetPreviousEma(int period) => _previous.GetValueOrDefault($"EMA:{period}");
    public decimal? GetMacd(int fast, int slow, int signal) => _current.GetValueOrDefault($"MACD:{fast}:{slow}:{signal}");
    public decimal? GetPreviousMacd(int fast, int slow, int signal) => _previous.GetValueOrDefault($"MACD:{fast}:{slow}:{signal}");
}
```

##### Pattern References

- `src/TradingApp.Application/Trading/Models/IndicatorSnapshot.cs` — existing flat indicator model; `IndicatorContext` is the dynamic-keyed alternative

---

### Task 1.2: Create `IndicatorRequirement` and `IndicatorExtractor` {#task-12-create-indicatorrequirement-and-indicatorextractor}

Create a model representing a required indicator (type + period) and a utility that extracts required indicators from a `StrategyConfig`.

- **Complexity**: Medium
- **Risk Factors**: Must correctly handle all condition types (RSI, PriceVsEma, Macd) and TrendFilter; must handle null/empty conditions
- **Files**:
  - `src/TradingApp.Application/StrategyAuthoring/Models/IndicatorRequirement.cs` — **New**
  - `src/TradingApp.Application/StrategyAuthoring/Services/IndicatorExtractor.cs` — **New**
- **Success**:
  - Given a `StrategyConfig` with RSI condition (period=14), returns `IndicatorRequirement { Type = "RSI", Period = 14 }`
  - Given a config with no conditions, returns empty list
  - Handles duplicate indicators (same RSI period from multiple conditions)
- **Dependencies**:
  - Task 1.1 (IndicatorContext model)

#### Implementation Details

```csharp
// src/TradingApp.Application/StrategyAuthoring/Models/IndicatorRequirement.cs — new file
namespace TradingApp.Application.StrategyAuthoring.Models;

/// <summary>
/// Describes an indicator that needs to be computed for strategy evaluation.
/// </summary>
public sealed record IndicatorRequirement
{
    public required string Type { get; init; }
    public int Period { get; init; }
    public int? FastPeriod { get; init; }
    public int? SlowPeriod { get; init; }
    public int? SignalPeriod { get; init; }
}
```

```csharp
// src/TradingApp.Application/StrategyAuthoring/Services/IndicatorExtractor.cs — new file
using TradingApp.Application.StrategyAuthoring.Models;

namespace TradingApp.Application.StrategyAuthoring.Services;

/// <summary>
/// Extracts required indicator computations from a strategy configuration.
/// </summary>
public static class IndicatorExtractor
{
    public static IReadOnlyList<IndicatorRequirement> Extract(StrategyConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var requirements = new HashSet<string>();
        var result = new List<IndicatorRequirement>();

        if (config.EntryConditions is not null)
        {
            foreach (var condition in config.EntryConditions.Where(c => c.Enabled))
            {
                ExtractFromCondition(condition, requirements, result);
            }
        }

        // TrendFilter extraction left for F7 — add here when trend filter evaluation is implemented

        return result;
    }

    private static void ExtractFromCondition(
        EntryConditionConfig condition,
        HashSet<string> seen,
        List<IndicatorRequirement> result)
    {
        switch (condition.Type)
        {
            case EntryConditionType.Rsi when condition.Params is RsiParams rsi:
                AddIfNew(seen, result, new IndicatorRequirement { Type = "RSI", Period = rsi.Period });
                break;

            case EntryConditionType.PriceVsEma when condition.Params is PriceVsEmaParams ema:
                AddIfNew(seen, result, new IndicatorRequirement { Type = "EMA", Period = ema.Period });
                break;

            case EntryConditionType.Macd when condition.Params is MacdParams macd:
                AddIfNew(seen, result, new IndicatorRequirement
                {
                    Type = "MACD",
                    FastPeriod = macd.FastPeriod,
                    SlowPeriod = macd.SlowPeriod,
                    SignalPeriod = macd.SignalPeriod
                });
                break;
        }
    }

    private static void AddIfNew(HashSet<string> seen, List<IndicatorRequirement> result, IndicatorRequirement req)
    {
        var key = $"{req.Type}:{req.Period}:{req.FastPeriod}:{req.SlowPeriod}:{req.SignalPeriod}";
        if (seen.Add(key))
        {
            result.Add(req);
        }
    }
}
```

##### Pattern References

- `src/TradingApp.Application/StrategyAuthoring/Models/RsiParams.cs` — `Period`, `Operator`, `Value` used for extraction
- `src/TradingApp.Application/StrategyAuthoring/Models/PriceVsEmaParams.cs` — `Period`, `Operator`
- `src/TradingApp.Application/StrategyAuthoring/Models/MacdParams.cs` — `FastPeriod`, `SlowPeriod`, `SignalPeriod`

---

### Task 1.3: Modify `IMarketContextBuilder` and `BacktestMarketContextBuilder` {#task-13-modify-imarketcontextbuilder-and-backtestmarketcontextbuilder}

Add an overload to `IMarketContextBuilder.Build()` that accepts indicator requirements from `IndicatorExtractor`. Update `BacktestMarketContextBuilder` to compute dynamic indicators and populate `IndicatorContext`.

- **Complexity**: Medium
- **Risk Factors**: Must preserve original 3-parameter `Build()` overload for grid path backward compatibility; cross detection requires computing RSI on `_candles[0..^1]` for previous value
- **Files**:
  - `src/TradingApp.Application/Abstractions/Services/IMarketContextBuilder.cs` — **Modified**
  - `src/TradingApp.Application/Trading/Services/BacktestMarketContextBuilder.cs` — **Modified**
- **Success**:
  - Original `Build(candle, 1h, 4h)` still works unchanged
  - New `Build(candle, 1h, 4h, requirements)` populates `IndicatorContext` with requested indicator values
  - `IndicatorContext.GetRsi(14)` returns a computed value
  - `IndicatorContext.GetPreviousRsi(14)` returns a value (for cross detection)
- **Dependencies**:
  - Task 1.1 (IndicatorContext), Task 1.2 (IndicatorRequirement)

#### Implementation Details

```csharp
// src/TradingApp.Application/Abstractions/Services/IMarketContextBuilder.cs — modification
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Abstractions.Services;

public interface IMarketContextBuilder
{
    void UpdateIndicators(Candle candle);

    MarketContext Build(Candle triggerCandle, Candle? latestOneHourCandle, Candle? latestFourHourCandle);

    MarketContext Build(
        Candle triggerCandle,
        Candle? latestOneHourCandle,
        Candle? latestFourHourCandle,
        IReadOnlyList<IndicatorRequirement>? requiredIndicators);
}
```

```csharp
// src/TradingApp.Application/Trading/Services/BacktestMarketContextBuilder.cs — modification
// Refactor the existing Build method into two overloads.

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

    var indicatorContext = BuildIndicatorContext(requiredIndicators);

    return new MarketContext
    {
        Symbol = triggerCandle.Symbol,
        TimestampUtc = triggerCandle.Timestamp,
        CurrentCandle = triggerCandle,
        LatestOneHourCandle = latestOneHourCandle,
        LatestFourHourCandle = latestFourHourCandle,
        Indicators = new IndicatorSnapshot
        {
            EmaFast = CalculateEma(9),
            EmaSlow = CalculateEma(21),
            EmaTrend = latestFourHourCandle?.Close ?? CalculateEma(55),
            Rsi = CalculateRsi(14),
            Atr = CalculateAtr(14)
        },
        IndicatorContext = indicatorContext
    };
}

// ... existing private methods unchanged ...

// NEW: Build IndicatorContext from requirements
private IndicatorContext? BuildIndicatorContext(IReadOnlyList<IndicatorRequirement>? requirements)
{
    if (requirements is null || requirements.Count == 0)
    {
        return null;
    }

    var context = new IndicatorContext();

    foreach (var req in requirements)
    {
        switch (req.Type.ToUpperInvariant())
        {
            case "RSI":
                var currentRsi = CalculateRsi(req.Period);
                var previousRsi = CalculatePreviousRsi(req.Period);
                context.SetRsi(req.Period, currentRsi, previousRsi);
                break;

            case "EMA":
                var currentEma = CalculateEma(req.Period);
                var previousEma = CalculatePreviousEma(req.Period);
                context.SetEma(req.Period, currentEma, previousEma);
                break;

            // MACD left for F8 — add case here when needed
        }
    }

    return context;
}

// NEW: Compute RSI on all candles except the last (for cross detection)
private decimal CalculatePreviousRsi(int period)
{
    if (_candles.Count < 3)
    {
        return 50m;
    }

    var endIndex = _candles.Count - 1;
    var startIndex = Math.Max(1, endIndex - period);
    decimal gains = 0m;
    decimal losses = 0m;

    for (var index = startIndex; index < endIndex; index++)
    {
        var delta = _candles[index].Close - _candles[index - 1].Close;
        if (delta >= 0)
        {
            gains += delta;
        }
        else
        {
            losses += Math.Abs(delta);
        }
    }

    if (losses == 0m)
    {
        return 100m;
    }

    var relativeStrength = gains / losses;
    return 100m - (100m / (1m + relativeStrength));
}

// NEW: Compute EMA on all candles except the last (for cross detection)
private decimal CalculatePreviousEma(int period)
{
    if (_candles.Count < 2)
    {
        return 0m;
    }

    var closes = _candles.Take(_candles.Count - 1).Select(c => c.Close).ToList();
    var smoothing = 2m / (period + 1m);
    var ema = closes[0];

    for (var index = 1; index < closes.Count; index++)
    {
        ema = ((closes[index] - ema) * smoothing) + ema;
    }

    return ema;
}
```

##### Pattern References

- `src/TradingApp.Application/Trading/Services/BacktestMarketContextBuilder.cs` — existing `CalculateRsi(int period)`, `CalculateEma(int period)` methods
- `src/TradingApp.Application/Abstractions/Services/IMarketContextBuilder.cs` — existing interface with 3-parameter `Build`

---

### Task 1.4: Add `IndicatorContext` to `MarketContext` {#task-14-add-indicatorcontext-to-marketcontext}

Add `IndicatorContext` as an optional (nullable) property on `MarketContext`.

- **Complexity**: Low
- **Risk Factors**: Must be nullable to avoid breaking existing grid tests that construct `MarketContext` without it
- **Files**:
  - `src/TradingApp.Application/Trading/Models/MarketContext.cs` — **Modified**
- **Success**:
  - `MarketContext.IndicatorContext` property exists and is nullable
  - Existing code constructing `MarketContext` compiles without changes
- **Dependencies**:
  - Task 1.1 (IndicatorContext model)

#### Implementation Details

```csharp
// src/TradingApp.Application/Trading/Models/MarketContext.cs — modification
// Add after the Indicators property:
public IndicatorContext? IndicatorContext { get; init; }
```

##### Pattern References

- `src/TradingApp.Application/Trading/Models/MarketContext.cs` — existing model

---

### Task 1.5: Write unit tests for Phase 1 {#task-15-write-unit-tests-for-phase-1}

Write tests for `IndicatorContext`, `IndicatorExtractor`, and the modified `BacktestMarketContextBuilder`.

- **Complexity**: Medium
- **Risk Factors**: Must follow project test conventions (MSTest, FluentAssertions 6, Given_When_Then, private static Create* helpers)
- **Files**:
  - `tests/TradingApp.Application.Tests/Trading/Models/IndicatorContextTests.cs` — **New**
  - `tests/TradingApp.Application.Tests/StrategyAuthoring/Services/IndicatorExtractorTests.cs` — **New**
  - `tests/TradingApp.Application.Tests/Trading/Services/BacktestMarketContextBuilderIndicatorTests.cs` — **New**
- **Success**:
  - `IndicatorContext.GetRsi(14)` returns set value
  - `IndicatorContext.GetRsi(20)` returns null when not set
  - `IndicatorExtractor` extracts RSI requirement from config with RSI condition
  - `IndicatorExtractor` returns empty for config with no conditions
  - `IndicatorExtractor` deduplicates same RSI period from multiple conditions
  - `BacktestMarketContextBuilder.Build(candle, null, null, requirements)` populates `IndicatorContext`
  - `BacktestMarketContextBuilder.Build(candle, null, null)` returns null `IndicatorContext`
- **Dependencies**:
  - Tasks 1.1–1.4

#### Implementation Details

```csharp
// tests/TradingApp.Application.Tests/Trading/Models/IndicatorContextTests.cs — new file
using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.Tests.Trading.Models;

[TestClass]
public sealed class IndicatorContextTests
{
    [TestMethod]
    public void GivenRsiSet_WhenGetRsi_ThenReturnsValue()
    {
        var context = new IndicatorContext();
        context.SetRsi(14, 35m, 28m);

        context.GetRsi(14).Should().Be(35m);
        context.GetPreviousRsi(14).Should().Be(28m);
    }

    [TestMethod]
    public void GivenRsiNotSet_WhenGetRsi_ThenReturnsNull()
    {
        var context = new IndicatorContext();

        context.GetRsi(14).Should().BeNull();
    }

    [TestMethod]
    public void GivenDifferentPeriods_WhenGetRsi_ThenReturnsCorrectValue()
    {
        var context = new IndicatorContext();
        context.SetRsi(14, 35m);
        context.SetRsi(21, 50m);

        context.GetRsi(14).Should().Be(35m);
        context.GetRsi(21).Should().Be(50m);
    }

    [TestMethod]
    public void GivenNoPreviousValue_WhenGetPreviousRsi_ThenReturnsNull()
    {
        var context = new IndicatorContext();
        context.SetRsi(14, 35m);

        context.GetPreviousRsi(14).Should().BeNull();
    }
}
```

```csharp
// tests/TradingApp.Application.Tests/StrategyAuthoring/Services/IndicatorExtractorTests.cs — new file
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.StrategyAuthoring.Services;

namespace TradingApp.Application.Tests.StrategyAuthoring.Services;

[TestClass]
public sealed class IndicatorExtractorTests
{
    [TestMethod]
    public void GivenConfigWithRsiCondition_WhenExtract_ThenReturnsRsiRequirement()
    {
        var config = CreateConfigWithRsi(14);

        var result = IndicatorExtractor.Extract(config);

        result.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Type = "RSI", Period = 14 });
    }

    [TestMethod]
    public void GivenConfigWithNoConditions_WhenExtract_ThenReturnsEmpty()
    {
        var config = CreateConfig();

        var result = IndicatorExtractor.Extract(config);

        result.Should().BeEmpty();
    }

    [TestMethod]
    public void GivenConfigWithDisabledCondition_WhenExtract_ThenReturnsEmpty()
    {
        var config = CreateConfigWithRsi(14, enabled: false);

        var result = IndicatorExtractor.Extract(config);

        result.Should().BeEmpty();
    }

    [TestMethod]
    public void GivenConfigWithDuplicateRsiPeriods_WhenExtract_ThenDeduplicates()
    {
        var config = CreateConfig(
            CreateRsiCondition(14), CreateRsiCondition(14, operatorValue: "gt", value: 70));

        var result = IndicatorExtractor.Extract(config);

        result.Should().ContainSingle();
    }

    // Private static factory helpers
    private static StrategyConfig CreateConfig(params EntryConditionConfig[] conditions)
    {
        return new StrategyConfig
        {
            StrategyMode = StrategyMode.Signal,
            StrategyName = "Test",
            Market = "BTC-USD",
            EntryLogic = EntryLogic.All,
            EntryConditions = conditions.Length > 0 ? conditions.ToList() : null,
            Risk = new RiskConfig { PositionSizeValue = 100m }
        };
    }

    private static StrategyConfig CreateConfigWithRsi(int period, bool enabled = true)
    {
        return CreateConfig(CreateRsiCondition(period, enabled: enabled));
    }

    private static EntryConditionConfig CreateRsiCondition(
        int period = 14, string operatorValue = "lt", decimal value = 40m, bool enabled = true)
    {
        return new EntryConditionConfig
        {
            Id = Guid.NewGuid().ToString(),
            Enabled = enabled,
            Type = EntryConditionType.Rsi,
            Label = $"RSI({period})",
            Params = new RsiParams { Period = period, Operator = operatorValue, Value = value }
        };
    }
}
```

##### Pattern References

- `tests/TradingApp.Application.Tests/Trading/Services/GridControllerTests.cs` — private static `Create*` helper pattern
- `tests/TradingApp.Application.Tests/Usings.cs` — global usings for FluentAssertions, MSTest, Moq

---

### Task 1.6: Build and run tests {#task-16-build-and-run-tests}

Build the solution and run all tests to verify Phase 1 changes.

- **Complexity**: Low
- **Risk Factors**: Existing tests must still pass; any build errors from interface changes must be fixed
- **Files**: None (verification only)
- **Success**:
  - `dotnet build` succeeds
  - All new Phase 1 tests pass
  - All existing `TradingApp.Application.Tests` pass (including `RealBacktestRunnerTests`)
  - Architecture tests pass (if any)

```bash
dotnet build src/TradingApp.Application/TradingApp.Application.csproj
dotnet build tests/TradingApp.Application.Tests/TradingApp.Application.Tests.csproj
dotnet test tests/TradingApp.Application.Tests --no-build
```

## Phase Success Criteria

- `IndicatorContext` model created with Set/Get methods for RSI, EMA, MACD (current + previous)
- `IndicatorExtractor` extracts required indicators from `StrategyConfig`
- `IMarketContextBuilder.Build()` has new overload accepting `IReadOnlyList<IndicatorRequirement>?`
- `BacktestMarketContextBuilder` populates `IndicatorContext` with config-driven indicators
- `MarketContext.IndicatorContext` property exists (nullable)
- All existing tests pass unchanged
- All new Phase 1 tests pass
