<!-- markdownlint-disable-file -->

# Task Details: Partial Close at R-Levels

## Phase 1: Domain Models, Validation & Serialization

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — `sealed` records, static factory methods, Guard validation
- `.github/instructions/testing.instructions.md` — MSTest, FluentAssertions ≤ v6, Moq, Given_When_Then naming
- `.github/instructions/dotnet-architecture.instructions.md` — Models in Application layer, CQRS patterns
- `.agent-context/0-knowledge/33-risk-management-and-trade-sizing.md` — Partial close concept, tranche examples
- `.agent-context/0-knowledge/13-strategy-config-schema.md` — ExitConfig sub-model reference

## Design References

- `ExitConfig` is a `sealed record` stored as JSON in `Strategy.ConfigJson` via `StrategyJsonOptions.Default`
- Adding new fields to `ExitConfig` requires no DB migration — `DefaultIgnoreCondition = Never` means existing rows with missing JSON keys deserialize to C# defaults (`null` for nullable reference types)
- `ExitRuleType.RMultiple` already exists as an enum value

### Task 1.1: Create `PartialCloseLevel` record and extend `ExitConfig` {#task-11-create-partialcloselevel-record-and-extend-exitconfig}

Create a new `PartialCloseLevel` record to represent a single R-level tranche, and add an optional `PartialCloses` list to `ExitConfig`.

- **Complexity**: Medium
- **Risk Factors**: Must maintain backward compatibility with existing serialized strategies
- **Files**:
  - `src/TradePilot.Application/StrategyAuthoring/Models/PartialCloseLevel.cs` — New file
  - `src/TradePilot.Application/StrategyAuthoring/Models/ExitConfig.cs` — Add `PartialCloses` property
- **Success**:
  - `PartialCloseLevel` record exists with `AtRMultiple` and `ClosePercent` properties
  - `ExitConfig.PartialCloses` is an optional `IReadOnlyList<PartialCloseLevel>?` defaulting to `null`
  - Existing strategies without partial closes deserialize correctly (property is `null`)

#### Implementation Details

```csharp
// src/TradePilot.Application/StrategyAuthoring/Models/PartialCloseLevel.cs — new file
namespace TradePilot.Application.StrategyAuthoring.Models;

public sealed record PartialCloseLevel
{
    public decimal AtRMultiple { get; init; }
    public decimal ClosePercent { get; init; }
}
```

```csharp
// src/TradePilot.Application/StrategyAuthoring/Models/ExitConfig.cs — modification
public sealed record ExitConfig
{
    public ExitRuleConfig TakeProfit { get; init; } = new();
    public ExitRuleConfig StopLoss { get; init; } = new();
    public bool ExitOnOppositeSignal { get; init; }
    public IReadOnlyList<PartialCloseLevel>? PartialCloses { get; init; }  // NEW
}
```

##### Pattern References

- `src/TradePilot.Application/StrategyAuthoring/Models/ExitConfig.cs` — existing record structure
- `src/TradePilot.Application/StrategyAuthoring/Models/ExitRuleConfig.cs` — sealed record pattern with init properties

### Task 1.2: Add validation rules for partial close configuration {#task-12-add-validation-rules-for-partial-close-configuration}

Extend `BusinessRuleValidator` (or the relevant validation service) to validate partial close configuration rules.

- **Complexity**: Medium
- **Risk Factors**: Must correctly handle edge cases (empty list vs null, zero values, negatives)
- **Files**:
  - `src/TradePilot.Application/StrategyAuthoring/Validation/BusinessRuleValidator.cs` — Add partial close validation rules
- **Success**:
  - Validation error if `ClosePercent` values sum > 100
  - Validation error if any `AtRMultiple` ≤ 0
  - Validation error if any `ClosePercent` < 1 or > 100
  - Validation error if duplicate `AtRMultiple` values
  - Validation error if `PartialCloses` is non-empty but `PositionSizeType` ≠ `RiskBased`
  - Validation warning if partial closes configured but no stop loss on remainder
  - `null` or empty `PartialCloses` passes validation (feature is optional)

#### Implementation Details

Find the existing validation method where `ExitConfig` is validated (look for `TP_R_MULTIPLE_NEGATIVE` etc.) and add a new section:

```csharp
// Inside the exit validation method — add after existing TP/SL validation
if (config.Exit.PartialCloses is { Count: > 0 } partialCloses)
{
    if (config.Risk.PositionSizeType != PositionSizeType.RiskBased)
    {
        result.AddError("PARTIAL_CLOSE_REQUIRES_RISK_BASED",
            "Partial closes require Risk-Based position sizing (R must be known)");
    }

    foreach (var level in partialCloses)
    {
        if (level.AtRMultiple <= 0)
            result.AddError("PARTIAL_CLOSE_R_NEGATIVE",
                $"Partial close R-multiple must be > 0, got {level.AtRMultiple}");

        if (level.ClosePercent < 1 || level.ClosePercent > 100)
            result.AddError("PARTIAL_CLOSE_PERCENT_INVALID",
                $"Partial close percent must be 1-100, got {level.ClosePercent}");
    }

    var totalPercent = partialCloses.Sum(pc => pc.ClosePercent);
    if (totalPercent > 100)
        result.AddError("PARTIAL_CLOSE_PERCENT_EXCEEDS_100",
            $"Partial close percentages sum to {totalPercent}%, must not exceed 100%");

    var duplicateRLevels = partialCloses
        .GroupBy(pc => pc.AtRMultiple)
        .Where(g => g.Count() > 1)
        .Select(g => g.Key);
    foreach (var dup in duplicateRLevels)
        result.AddError("PARTIAL_CLOSE_DUPLICATE_R",
            $"Duplicate partial close R-level: {dup}");

    if (totalPercent < 100 && !config.Exit.StopLoss.Enabled)
        result.AddWarning("PARTIAL_CLOSE_NO_TRAILING_STOP",
            "Partial closes sum to less than 100% but no stop loss is configured for the remainder");
}
```

##### Pattern References

- `tests/TradePilot.Application.Tests/StrategyAuthoring/Validation/BusinessRuleValidatorTests.cs` — existing TP_R_MULTIPLE_NEGATIVE / TP_R_MULTIPLE_SUB_ONE validation pattern

### Task 1.3: Add unit tests for partial close validation {#task-13-add-unit-tests-for-partial-close-validation}

Add tests to `BusinessRuleValidatorTests` for all partial close validation rules.

- **Complexity**: Medium
- **Risk Factors**: None — straightforward test additions
- **Files**:
  - `tests/TradePilot.Application.Tests/StrategyAuthoring/Validation/BusinessRuleValidatorTests.cs` — Add test methods
- **Success**:
  - Tests cover: sum > 100, negative R, zero R, percent < 1, percent > 100, duplicate R-levels, non-RiskBased sizing, null/empty passes, warning for no trailing stop
  - All tests pass

#### Implementation Details

```csharp
[TestMethod]
public void GivenPartialClosesExceeding100Percent_WhenValidated_ThenError()
{
    // Arrange
    var config = CreateConfig();
    config = config with
    {
        Risk = config.Risk with { PositionSizeType = PositionSizeType.RiskBased },
        Exit = config.Exit with
        {
            PartialCloses = new[]
            {
                new PartialCloseLevel { AtRMultiple = 1m, ClosePercent = 60 },
                new PartialCloseLevel { AtRMultiple = 2m, ClosePercent = 60 },
            }
        }
    };

    // Act
    var result = _sut.Validate(config);

    // Assert
    result.Errors.Should().Contain(e => e.Code == "PARTIAL_CLOSE_PERCENT_EXCEEDS_100");
}

[TestMethod]
public void GivenPartialClosesWithNegativeR_WhenValidated_ThenError()
{
    var config = CreateConfig();
    config = config with
    {
        Risk = config.Risk with { PositionSizeType = PositionSizeType.RiskBased },
        Exit = config.Exit with
        {
            PartialCloses = new[]
            {
                new PartialCloseLevel { AtRMultiple = -1m, ClosePercent = 50 },
            }
        }
    };

    var result = _sut.Validate(config);

    result.Errors.Should().Contain(e => e.Code == "PARTIAL_CLOSE_R_NEGATIVE");
}

[TestMethod]
public void GivenPartialClosesWithPercentWallet_WhenValidated_ThenError()
{
    var config = CreateConfig();
    config = config with
    {
        Risk = config.Risk with { PositionSizeType = PositionSizeType.PercentWallet },
        Exit = config.Exit with
        {
            PartialCloses = new[]
            {
                new PartialCloseLevel { AtRMultiple = 1m, ClosePercent = 50 },
            }
        }
    };

    var result = _sut.Validate(config);

    result.Errors.Should().Contain(e => e.Code == "PARTIAL_CLOSE_REQUIRES_RISK_BASED");
}

[TestMethod]
public void GivenNullPartialCloses_WhenValidated_ThenNoErrors()
{
    var config = CreateConfig();
    config = config with { Exit = config.Exit with { PartialCloses = null } };

    var result = _sut.Validate(config);

    result.Errors.Should().NotContain(e => e.Code.StartsWith("PARTIAL_CLOSE"));
}

[TestMethod]
public void GivenDuplicateRLevels_WhenValidated_ThenError()
{
    var config = CreateConfig();
    config = config with
    {
        Risk = config.Risk with { PositionSizeType = PositionSizeType.RiskBased },
        Exit = config.Exit with
        {
            PartialCloses = new[]
            {
                new PartialCloseLevel { AtRMultiple = 1m, ClosePercent = 25 },
                new PartialCloseLevel { AtRMultiple = 1m, ClosePercent = 25 },
            }
        }
    };

    var result = _sut.Validate(config);

    result.Errors.Should().Contain(e => e.Code == "PARTIAL_CLOSE_DUPLICATE_R");
}
```

##### Pattern References

- `tests/TradePilot.Application.Tests/StrategyAuthoring/Validation/BusinessRuleValidatorTests.cs` — existing `GivenNegativeRMultipleTakeProfit_WhenValidated_ThenError` pattern

### Task 1.4: Verify JSON serialization backward compatibility {#task-14-verify-json-serialization-backward-compatibility}

Add a test to verify that existing `ExitConfig` JSON without `partialCloses` deserializes correctly with the new field defaulting to `null`.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `tests/TradePilot.Application.Tests/StrategyAuthoring/Serialization/` — New or existing test file for serialization round-trip
- **Success**:
  - JSON without `partialCloses` key deserializes to `ExitConfig` with `PartialCloses = null`
  - JSON with `partialCloses` array round-trips correctly
  - `StrategyJsonOptions.Default` handles the new record type

### Task 1.5: Run architecture tests {#task-15-run-architecture-tests}

Run the solution's architecture tests to ensure no layer violations were introduced.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (test execution only)
- **Success**:
  - All architecture tests pass
  - All existing tests continue to pass

## Phase Success Criteria

- `PartialCloseLevel` record exists and is part of `ExitConfig`
- All validation rules implemented and tested
- JSON backward compatibility verified
- All tests pass including architecture tests
