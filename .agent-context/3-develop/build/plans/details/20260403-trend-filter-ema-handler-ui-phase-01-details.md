<!-- markdownlint-disable-file -->

# Task Details: F7 — Trend Filter + EMA Condition Handler + UI

## Phase 1: Backend Models & Infrastructure

## Standards and Knowledge References

- **csharp.instructions.md**: sealed classes, PascalCase, private fields with underscore prefix
- **testing.instructions.md**: MSTest, Moq, FluentAssertions v6, Given_When_Then naming, builder pattern for test data
- **dotnet-architecture.instructions.md**: models in Application/{BoundedContext}/Models, static extractor classes
- **01-trading-strategy.md**: trend filter (price > EMA 200, EMA 20 > 50) flow
- **13-strategy-config-schema.md**: canonical schema reference for TrendFilterConfig, PriceVsEmaParams

### Task 1.1: Expand TrendFilterType and TrendOperator enums {#task-11-expand-trendfiltertype-and-trendoperator-enums}

Add new enum values to support all trend filter types and operators required by F7.

- **Complexity**: Low
- **Risk Factors**: None — additive enum changes
- **Files**:
  - `src/TradePilot.Application/StrategyAuthoring/Models/TrendFilterType.cs` — add `SmaCross`, `PriceAboveEma`
  - `src/TradePilot.Application/StrategyAuthoring/Models/TrendOperator.cs` — add `CrossAbove`, `CrossBelow`, `Above`, `Below`
- **Success**:
  - `TrendFilterType` has values: `EmaCross`, `EmaSingle`, `SmaCross`, `PriceAboveEma`
  - `TrendOperator` has values: `Gt`, `Lt`, `Gte`, `Lte`, `CrossAbove`, `CrossBelow`, `Above`, `Below`
  - Solution builds without errors

#### Implementation Details

```csharp
// src/TradePilot.Application/StrategyAuthoring/Models/TrendFilterType.cs — modification
namespace TradePilot.Application.StrategyAuthoring.Models;

public enum TrendFilterType
{
    EmaCross,
    EmaSingle,
    SmaCross,
    PriceAboveEma,
}
```

```csharp
// src/TradePilot.Application/StrategyAuthoring/Models/TrendOperator.cs — modification
namespace TradePilot.Application.StrategyAuthoring.Models;

public enum TrendOperator
{
    Gt,
    Lt,
    Gte,
    Lte,
    CrossAbove,
    CrossBelow,
    Above,
    Below,
}
```

##### Pattern References

- Existing `TrendFilterType.cs` — enum pattern
- Existing `TrendOperator.cs` — enum pattern

---

### Task 1.2: Add Period property and update TrendFilterConfig serialization {#task-12-add-period-property-and-update-trendfilterconfig-serialization}

Add nullable `Period` property to `TrendFilterConfig` for `PriceAboveEma` type (which uses a single period instead of FastPeriod/SlowPeriod). Check if a JSON serialization converter exists for `TrendFilterType`/`TrendOperator` and update if needed.

- **Complexity**: Low
- **Risk Factors**: Must ensure backward compatibility — existing configs without `Period` deserialize to `null`
- **Files**:
  - `src/TradePilot.Application/StrategyAuthoring/Models/TrendFilterConfig.cs` — add `Period` property
- **Success**:
  - `TrendFilterConfig.Period` is `int?` (nullable), defaults to `null`
  - Existing JSON configs without `Period` deserialize correctly
  - Solution builds

#### Implementation Details

```csharp
// src/TradePilot.Application/StrategyAuthoring/Models/TrendFilterConfig.cs — modification
namespace TradePilot.Application.StrategyAuthoring.Models;

public sealed record TrendFilterConfig
{
    public bool Enabled { get; init; }
    public TrendFilterType Type { get; init; }
    public int? Period { get; init; }
    public int FastPeriod { get; init; }
    public int SlowPeriod { get; init; }
    public TrendOperator Operator { get; init; }
    public Direction AppliesTo { get; init; }
}
```

Check serialization by searching for any custom converter for `TrendFilterType` or `TrendOperator` — if `System.Text.Json` default `JsonStringEnumConverter` is used globally, the new PascalCase enum values will serialize as `"SmaCross"`, `"PriceAboveEma"`, `"CrossAbove"`, `"CrossBelow"`, `"Above"`, `"Below"`. If the frontend sends snake_case (e.g. `"sma_cross"`, `"price_above_ema"`), a custom converter may be needed. Verify the JSON configuration in `Program.cs`.

##### Pattern References

- Existing `TrendFilterConfig.cs` — record pattern

---

### Task 1.3: Expand PriceVsEmaParams with DistanceType and DistanceValue {#task-13-expand-pricevsemaparams-with-distancetype-and-distancevalue}

Add `DistanceType` and `DistanceValue` properties to `PriceVsEmaParams` for the `near` operator.

- **Complexity**: Low
- **Risk Factors**: None — additive, nullable fields for backward compatibility
- **Files**:
  - `src/TradePilot.Application/StrategyAuthoring/Models/PriceVsEmaParams.cs` — add fields
- **Success**:
  - `PriceVsEmaParams` has `DistanceType` (string, default empty) and `DistanceValue` (decimal?, nullable)
  - Existing JSON with only `Period` and `Operator` still deserializes correctly

#### Implementation Details

```csharp
// src/TradePilot.Application/StrategyAuthoring/Models/PriceVsEmaParams.cs — modification
namespace TradePilot.Application.StrategyAuthoring.Models;

public sealed record PriceVsEmaParams : IEntryConditionParams
{
    public int Period { get; init; }
    public string Operator { get; init; } = string.Empty;
    public string DistanceType { get; init; } = string.Empty;
    public decimal? DistanceValue { get; init; }
}
```

##### Pattern References

- Existing `PriceVsEmaParams.cs`
- `RsiParams` — similar flat record pattern

---

### Task 1.4: Add SMA support to IndicatorContext {#task-14-add-sma-support-to-indicatorcontext}

Add `SetSma`, `GetSma`, and `GetPreviousSma` methods to `IndicatorContext`, following the same pattern as EMA.

- **Complexity**: Low
- **Risk Factors**: None — additive methods
- **Files**:
  - `src/TradePilot.Application/Trading/Models/IndicatorContext.cs` — add SMA methods
- **Success**:
  - `SetSma(int period, decimal currentValue, decimal? previousValue)` stores values
  - `GetSma(int period)` returns current SMA value
  - `GetPreviousSma(int period)` returns previous SMA value
  - Solution builds

#### Implementation Details

```csharp
// src/TradePilot.Application/Trading/Models/IndicatorContext.cs — add methods
// Add after existing SetMacd/GetMacd methods:

public void SetSma(int period, decimal currentValue, decimal? previousValue = null)
{
    _current[CreateSmaKey(period)] = currentValue;

    if (previousValue.HasValue)
    {
        _previous[CreateSmaKey(period)] = previousValue.Value;
    }
}

public decimal? GetSma(int period) => GetValue(_current, CreateSmaKey(period));

public decimal? GetPreviousSma(int period) => GetValue(_previous, CreateSmaKey(period));

// Add private key method:
private static string CreateSmaKey(int period) => $"SMA:{period}";
```

##### Pattern References

- `IndicatorContext.cs` — existing `SetEma`/`GetEma`/`GetPreviousEma` and `CreateEmaKey` pattern

---

### Task 1.5: Add SMA calculation to BacktestMarketContextBuilder {#task-15-add-sma-calculation-to-backtestmarketcontextbuilder}

Add `"SMA"` case to `BuildIndicatorContext` and implement `CalculateSma` / `CalculatePreviousSma` methods.

- **Complexity**: Medium
- **Risk Factors**: SMA calculation correctness — must average last N closes
- **Files**:
  - `src/TradePilot.Application/Trading/Services/BacktestMarketContextBuilder.cs` — add SMA case and calculation methods
- **Success**:
  - `BuildIndicatorContext` handles `"SMA"` requirement type
  - `CalculateSma(period)` correctly averages last `period` closes
  - `CalculatePreviousSma(period)` averages last `period` closes excluding the most recent candle
  - Solution builds

#### Implementation Details

```csharp
// src/TradePilot.Application/Trading/Services/BacktestMarketContextBuilder.cs
// Add to BuildIndicatorContext switch:
case "SMA":
    context.SetSma(
        requirement.Period,
        CalculateSma(requirement.Period),
        CalculatePreviousSma(requirement.Period));
    break;

// Add new private methods:
private decimal CalculateSma(int period)
{
    if (_candles.Count == 0)
    {
        return 0m;
    }

    var startIndex = Math.Max(0, _candles.Count - period);
    var sum = 0m;
    var count = 0;

    for (var index = startIndex; index < _candles.Count; index++)
    {
        sum += _candles[index].Close;
        count++;
    }

    return sum / count;
}

private decimal CalculatePreviousSma(int period)
{
    if (_candles.Count < 2)
    {
        return 0m;
    }

    var endIndex = _candles.Count - 1;
    var startIndex = Math.Max(0, endIndex - period);
    var sum = 0m;
    var count = 0;

    for (var index = startIndex; index < endIndex; index++)
    {
        sum += _candles[index].Close;
        count++;
    }

    return count > 0 ? sum / count : 0m;
}
```

##### Pattern References

- `BacktestMarketContextBuilder.cs` — existing `CalculateEma` / `CalculatePreviousEma` pattern

---

### Task 1.6: Extend IndicatorExtractor for TrendFilter requirements {#task-16-extend-indicatorextractor-for-trendfilter-requirements}

Extend `IndicatorExtractor.Extract()` to also extract EMA/SMA requirements from `config.TrendFilter`.

- **Complexity**: Medium
- **Risk Factors**: Must handle all TrendFilterType variants correctly
- **Files**:
  - `src/TradePilot.Application/StrategyAuthoring/Services/IndicatorExtractor.cs` — add TrendFilter extraction
- **Success**:
  - `EmaCross` extracts two EMA requirements (FastPeriod, SlowPeriod)
  - `SmaCross` extracts two SMA requirements (FastPeriod, SlowPeriod)
  - `PriceAboveEma` extracts one EMA requirement (Period)
  - Disabled trend filter is skipped
  - Deduplication works across TrendFilter and EntryConditions

#### Implementation Details

```csharp
// src/TradePilot.Application/StrategyAuthoring/Services/IndicatorExtractor.cs — modification
public static IReadOnlyList<IndicatorRequirement> Extract(StrategyConfig config)
{
    ArgumentNullException.ThrowIfNull(config);

    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var requirements = new List<IndicatorRequirement>();

    ExtractFromTrendFilter(config.TrendFilter, seen, requirements);

    if (config.EntryConditions is not null)
    {
        foreach (var condition in config.EntryConditions.Where(entry => entry.Enabled))
        {
            ExtractFromCondition(condition, seen, requirements);
        }
    }

    return requirements;
}

private static void ExtractFromTrendFilter(
    TrendFilterConfig? filter,
    HashSet<string> seen,
    List<IndicatorRequirement> requirements)
{
    if (filter is null || !filter.Enabled)
    {
        return;
    }

    switch (filter.Type)
    {
        case TrendFilterType.EmaCross:
        case TrendFilterType.EmaSingle:
            if (filter.FastPeriod > 0)
                AddIfNew(seen, requirements, new IndicatorRequirement { Type = "EMA", Period = filter.FastPeriod });
            if (filter.SlowPeriod > 0)
                AddIfNew(seen, requirements, new IndicatorRequirement { Type = "EMA", Period = filter.SlowPeriod });
            break;

        case TrendFilterType.SmaCross:
            if (filter.FastPeriod > 0)
                AddIfNew(seen, requirements, new IndicatorRequirement { Type = "SMA", Period = filter.FastPeriod });
            if (filter.SlowPeriod > 0)
                AddIfNew(seen, requirements, new IndicatorRequirement { Type = "SMA", Period = filter.SlowPeriod });
            break;

        case TrendFilterType.PriceAboveEma:
            if (filter.Period.HasValue && filter.Period.Value > 0)
                AddIfNew(seen, requirements, new IndicatorRequirement { Type = "EMA", Period = filter.Period.Value });
            break;
    }
}
```

##### Pattern References

- `IndicatorExtractor.cs` — existing `Extract` and `ExtractFromCondition` methods

---

### Task 1.7: Update BusinessRuleValidator for new trend filter types {#task-17-update-businessrulevalidator-for-new-trend-filter-types}

Update `ValidateTrendFilter` to conditionally validate based on filter type. `PriceAboveEma` requires `Period > 0` instead of `FastPeriod`/`SlowPeriod`. Also validate `PriceVsEmaParams.DistanceValue` when operator is `near`.

- **Complexity**: Medium
- **Risk Factors**: Must not break existing EmaCross validation
- **Files**:
  - `src/TradePilot.Application/StrategyAuthoring/Validation/BusinessRuleValidator.cs` — update `ValidateTrendFilter` and `ValidateEntryConditions`
- **Success**:
  - `PriceAboveEma` filter validates `Period > 0` (not FastPeriod/SlowPeriod)
  - `EmaCross`/`SmaCross` still validate `FastPeriod > 0` and `SlowPeriod > 0`
  - `PriceVsEmaParams` with `operator = "near"` validates `DistanceValue > 0`
  - Existing RSI validation unchanged

#### Implementation Details

```csharp
// src/TradePilot.Application/StrategyAuthoring/Validation/BusinessRuleValidator.cs
// Replace ValidateTrendFilter method:
private static void ValidateTrendFilter(TrendFilterConfig? filter, ValidationResult result)
{
    if (filter is null || !filter.Enabled)
    {
        return;
    }

    switch (filter.Type)
    {
        case TrendFilterType.EmaCross:
        case TrendFilterType.EmaSingle:
        case TrendFilterType.SmaCross:
            if (filter.FastPeriod <= 0)
            {
                result.Add(new ValidationError
                {
                    Severity = ValidationSeverity.Error,
                    FieldPath = "trendFilter.fastPeriod",
                    Code = "TREND_FAST_PERIOD_INVALID",
                    Message = "Trend filter fast period must be greater than 0.",
                });
            }

            if (filter.SlowPeriod <= 0)
            {
                result.Add(new ValidationError
                {
                    Severity = ValidationSeverity.Error,
                    FieldPath = "trendFilter.slowPeriod",
                    Code = "TREND_SLOW_PERIOD_INVALID",
                    Message = "Trend filter slow period must be greater than 0.",
                });
            }
            break;

        case TrendFilterType.PriceAboveEma:
            if (!filter.Period.HasValue || filter.Period.Value <= 0)
            {
                result.Add(new ValidationError
                {
                    Severity = ValidationSeverity.Error,
                    FieldPath = "trendFilter.period",
                    Code = "TREND_PERIOD_INVALID",
                    Message = "Trend filter period must be greater than 0.",
                });
            }
            break;
    }
}
```

```csharp
// Update PriceVsEma validation in ValidateEntryConditions:
if (condition.Params is PriceVsEmaParams priceVsEma)
{
    if (priceVsEma.Period <= 0)
    {
        result.Add(new ValidationError
        {
            Severity = ValidationSeverity.Error,
            FieldPath = $"entryConditions[{index}].params.period",
            Code = "EMA_PERIOD_INVALID",
            Message = "EMA period must be greater than 0.",
        });
    }

    var normalizedOp = priceVsEma.Operator.Trim().ToLowerInvariant();
    if (normalizedOp == "near" && (!priceVsEma.DistanceValue.HasValue || priceVsEma.DistanceValue.Value <= 0))
    {
        result.Add(new ValidationError
        {
            Severity = ValidationSeverity.Error,
            FieldPath = $"entryConditions[{index}].params.distanceValue",
            Code = "DISTANCE_VALUE_INVALID",
            Message = "Distance value must be greater than 0 when operator is 'near'.",
        });
    }
}
```

##### Pattern References

- `BusinessRuleValidator.cs` — existing `ValidateTrendFilter` and `ValidateEntryConditions` methods

---

### Task 1.8: Tests for Phase 1 changes {#task-18-tests-for-phase-1-changes}

Write unit tests for all Phase 1 model and infrastructure changes.

- **Complexity**: Medium
- **Risk Factors**: Must cover SMA correctness, IndicatorExtractor TrendFilter extraction, and validator conditional logic
- **Files**:
  - `tests/TradePilot.Application.Tests/Trading/Models/IndicatorContextTests.cs` — add SMA tests
  - `tests/TradePilot.Application.Tests/StrategyAuthoring/Services/IndicatorExtractorTests.cs` — add TrendFilter extraction tests
  - `tests/TradePilot.Application.Tests/StrategyAuthoring/Validation/BusinessRuleValidatorTests.cs` — add PriceAboveEma validation tests
  - `tests/TradePilot.Application.Tests/Trading/Services/BacktestMarketContextBuilderIndicatorTests.cs` — add SMA test
- **Success**:
  - All new tests pass
  - No regressions in existing tests
- **Dependencies**:
  - Tasks 1.1–1.7 must be completed

#### Implementation Details

```csharp
// tests/TradePilot.Application.Tests/Trading/Models/IndicatorContextTests.cs — add tests
[TestMethod]
public void GivenSmaSet_WhenGetSma_ThenReturnsValue()
{
    var context = new IndicatorContext();
    context.SetSma(20, 42000m, 41900m);

    context.GetSma(20).Should().Be(42000m);
    context.GetPreviousSma(20).Should().Be(41900m);
}

[TestMethod]
public void GivenSmaNotSet_WhenGetSma_ThenReturnsNull()
{
    var context = new IndicatorContext();

    context.GetSma(20).Should().BeNull();
}
```

```csharp
// tests/TradePilot.Application.Tests/StrategyAuthoring/Services/IndicatorExtractorTests.cs — add tests
[TestMethod]
public void GivenConfigWithEmaCrossTrendFilter_WhenExtract_ThenReturnsTwoEmaRequirements()
{
    var config = new StrategyConfig
    {
        StrategyMode = StrategyMode.Signal,
        StrategyName = "Test",
        Market = "BTC-USD",
        TrendFilter = new TrendFilterConfig
        {
            Enabled = true,
            Type = TrendFilterType.EmaCross,
            FastPeriod = 50,
            SlowPeriod = 200,
        },
        Risk = new RiskConfig { PositionSizeValue = 100m }
    };

    var result = IndicatorExtractor.Extract(config);

    result.Should().HaveCount(2);
    result.Should().Contain(r => r.Type == "EMA" && r.Period == 50);
    result.Should().Contain(r => r.Type == "EMA" && r.Period == 200);
}

[TestMethod]
public void GivenConfigWithSmaCrossTrendFilter_WhenExtract_ThenReturnsTwoSmaRequirements()
{
    var config = new StrategyConfig
    {
        StrategyMode = StrategyMode.Signal,
        StrategyName = "Test",
        Market = "BTC-USD",
        TrendFilter = new TrendFilterConfig
        {
            Enabled = true,
            Type = TrendFilterType.SmaCross,
            FastPeriod = 20,
            SlowPeriod = 50,
        },
        Risk = new RiskConfig { PositionSizeValue = 100m }
    };

    var result = IndicatorExtractor.Extract(config);

    result.Should().HaveCount(2);
    result.Should().Contain(r => r.Type == "SMA" && r.Period == 20);
    result.Should().Contain(r => r.Type == "SMA" && r.Period == 50);
}

[TestMethod]
public void GivenConfigWithPriceAboveEmaTrendFilter_WhenExtract_ThenReturnsOneEmaRequirement()
{
    var config = new StrategyConfig
    {
        StrategyMode = StrategyMode.Signal,
        StrategyName = "Test",
        Market = "BTC-USD",
        TrendFilter = new TrendFilterConfig
        {
            Enabled = true,
            Type = TrendFilterType.PriceAboveEma,
            Period = 200,
        },
        Risk = new RiskConfig { PositionSizeValue = 100m }
    };

    var result = IndicatorExtractor.Extract(config);

    result.Should().ContainSingle()
        .Which.Should().BeEquivalentTo(new { Type = "EMA", Period = 200 });
}

[TestMethod]
public void GivenDisabledTrendFilter_WhenExtract_ThenReturnsEmpty()
{
    var config = new StrategyConfig
    {
        StrategyMode = StrategyMode.Signal,
        StrategyName = "Test",
        Market = "BTC-USD",
        TrendFilter = new TrendFilterConfig
        {
            Enabled = false,
            Type = TrendFilterType.EmaCross,
            FastPeriod = 50,
            SlowPeriod = 200,
        },
        Risk = new RiskConfig { PositionSizeValue = 100m }
    };

    var result = IndicatorExtractor.Extract(config);

    result.Should().BeEmpty();
}

[TestMethod]
public void GivenTrendFilterAndConditionShareEmaPeriod_WhenExtract_ThenDeduplicates()
{
    var config = new StrategyConfig
    {
        StrategyMode = StrategyMode.Signal,
        StrategyName = "Test",
        Market = "BTC-USD",
        TrendFilter = new TrendFilterConfig
        {
            Enabled = true,
            Type = TrendFilterType.PriceAboveEma,
            Period = 50,
        },
        EntryConditions = [
            new EntryConditionConfig
            {
                Id = "ema-1",
                Enabled = true,
                Type = EntryConditionType.PriceVsEma,
                Label = "Price near EMA",
                Params = new PriceVsEmaParams { Period = 50, Operator = "near" }
            }
        ],
        Risk = new RiskConfig { PositionSizeValue = 100m }
    };

    var result = IndicatorExtractor.Extract(config);

    result.Should().ContainSingle();
}
```

```csharp
// tests/TradePilot.Application.Tests/StrategyAuthoring/Validation/BusinessRuleValidatorTests.cs — add tests
[TestMethod]
public void GivenPriceAboveEmaWithPeriodZero_WhenValidated_ThenError()
{
    var config = new StrategyConfig
    {
        TrendFilter = new TrendFilterConfig
        {
            Enabled = true,
            Type = TrendFilterType.PriceAboveEma,
            Period = 0,
        },
    };
    var result = new ValidationResult();

    _sut.Validate(config, result);

    result.Errors.Should().Contain(error => error.Code == "TREND_PERIOD_INVALID");
}

[TestMethod]
public void GivenPriceAboveEmaWithValidPeriod_WhenValidated_ThenNoTrendFilterErrors()
{
    var config = new StrategyConfig
    {
        TrendFilter = new TrendFilterConfig
        {
            Enabled = true,
            Type = TrendFilterType.PriceAboveEma,
            Period = 200,
        },
    };
    var result = new ValidationResult();

    _sut.Validate(config, result);

    result.Errors.Should().NotContain(error =>
        error.Code == "TREND_FAST_PERIOD_INVALID"
        || error.Code == "TREND_SLOW_PERIOD_INVALID"
        || error.Code == "TREND_PERIOD_INVALID");
}

[TestMethod]
public void GivenPriceVsEmaWithNearOperatorAndNoDistanceValue_WhenValidated_ThenError()
{
    var config = new StrategyConfig
    {
        EntryConditions =
        [
            new EntryConditionConfig
            {
                Type = EntryConditionType.PriceVsEma,
                Params = new PriceVsEmaParams { Period = 50, Operator = "near", DistanceValue = null },
            },
        ],
    };
    var result = new ValidationResult();

    _sut.Validate(config, result);

    result.Errors.Should().Contain(error => error.Code == "DISTANCE_VALUE_INVALID");
}
```

##### Pattern References

- `IndicatorContextTests.cs` — existing RSI test pattern
- `IndicatorExtractorTests.cs` — existing extraction test pattern
- `BusinessRuleValidatorTests.cs` — existing validation test pattern

---

### Task 1.9: Build and run architecture tests {#task-19-build-and-run-architecture-tests}

Build the solution and run all affected test projects to verify Phase 1 changes compile and pass.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: N/A (build and test commands)
- **Success**:
  - `dotnet build TradePilot.sln --configuration Release` succeeds
  - `dotnet test tests/TradePilot.Application.Tests/TradePilot.Application.Tests.csproj --configuration Release --no-build` all pass
  - `dotnet test tests/TradePilot.Domain.Tests/TradePilot.Domain.Tests.csproj --configuration Release --no-build` all pass

## Phase Success Criteria

- All new enum values compile and are usable
- `TrendFilterConfig.Period` property exists and is nullable
- `PriceVsEmaParams` has `DistanceType` and `DistanceValue` properties
- `IndicatorContext` supports SMA get/set
- `BacktestMarketContextBuilder` computes SMA values
- `IndicatorExtractor` extracts TrendFilter indicator requirements
- `BusinessRuleValidator` validates `PriceAboveEma` and `PriceVsEmaParams.DistanceValue` correctly
- All existing and new tests pass
