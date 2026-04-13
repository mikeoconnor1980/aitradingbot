<!-- markdownlint-disable-file -->

# Task Details: Volatility-Scaled Initial Stop Loss (ATR-Based)

## Phase 4: Optimizer Support

## Standards and Knowledge References

- **csharp.instructions.md**: `sealed` records for DTO/config classes, array initializer syntax for defaults.
- **testing.instructions.md**: MSTest + FluentAssertions v6. `GivenX_WhenY_ThenZ` naming.
- **dotnet-architecture.instructions.md**: Application-layer service with static methods for config generation.

### Task 4.1: Add ATR Fields to `ParameterBounds` {#task-41-add-atr-fields-to-parameterbounds}

Add `AtrMultiplierOptions`, `StopLossTypes`, and `AtrPeriodOptions` to `ParameterBounds` for optimizer sweeping. `StopLossTypes` controls which exit rule types the optimizer generates SL configs for.

- **Complexity**: Low
- **Risk Factors**: None — additive fields with defaults that preserve existing behaviour
- **Files**:
  - `src/TradingApp.Application/Optimization/Models/ParameterBounds.cs` - Add new fields
- **Success**:
  - `AtrMultiplierOptions` defaults to array with common values
  - `StopLossTypes` defaults to `[ExitRuleType.FixedPercent]` (preserving existing behaviour)
  - `AtrPeriodOptions` defaults to `[14]`
  - Existing optimizer behaviour unchanged when new fields not specified
- **Dependencies**: Phase 1 (enum exists)

#### Implementation Details

```csharp
// src/TradingApp.Application/Optimization/Models/ParameterBounds.cs — modification
// Add in the "--- Exit ---" section or after "--- Stop Loss ---":

// --- Stop Loss Type ---
public ExitRuleType[] StopLossTypes { get; init; } = [ExitRuleType.FixedPercent];

// --- ATR Initial Stop ---
public decimal[] AtrMultiplierOptions { get; init; } = [1.5m, 2.0m, 2.5m, 3.0m];
public int[] AtrPeriodOptions { get; init; } = [14];
```

##### Pattern References

- `src/TradingApp.Application/Optimization/Models/ParameterBounds.cs` — existing pattern: `decimal[] PositionSizeOptions`, `int[] RsiPeriods`, etc.

### Task 4.2: Extend `StrategyConfigGenerator.GenerateExitConfig` for `AtrInitial` {#task-42-extend-strategyconfiggeneratorgenerateexitconfig-for-atrinitial}

Modify `GenerateExitConfig` to select a stop-loss type from `bounds.StopLossTypes` and generate the appropriate config. When `AtrInitial` is selected, use `AtrMultiplierOptions` and `AtrPeriodOptions` instead of `StopLossMin/Max/Step`.

- **Complexity**: Medium
- **Risk Factors**: Must preserve existing behaviour when `StopLossTypes` only contains `FixedPercent`
- **Files**:
  - `src/TradingApp.Application/Optimization/Services/StrategyConfigGenerator.cs` - Extend `GenerateExitConfig`
- **Success**:
  - When `StopLossTypes = [FixedPercent]`, behaviour unchanged
  - When `StopLossTypes` includes `AtrInitial`, generates `AtrInitial` configs with `AtrMultiplier` from options
  - Generated `AtrMultiplier` values are within `AtrMultiplierOptions`
  - Generated `AtrPeriod` values are within `AtrPeriodOptions`
- **Dependencies**: Task 4.1

#### Implementation Details

```csharp
// src/TradingApp.Application/Optimization/Services/StrategyConfigGenerator.cs — modification
private static ExitConfig GenerateExitConfig(ParameterBounds bounds, Random rng)
{
    var slType = NextFrom(bounds.StopLossTypes, rng);

    var stopLoss = slType switch
    {
        ExitRuleType.AtrInitial => new ExitRuleConfig
        {
            Enabled = true,
            Type = ExitRuleType.AtrInitial,
            AtrMultiplier = NextFrom(bounds.AtrMultiplierOptions, rng),
            AtrPeriod = NextFrom(bounds.AtrPeriodOptions, rng),
        },
        _ => new ExitRuleConfig
        {
            Enabled = true,
            Type = ExitRuleType.FixedPercent,
            Value = NextFromRange(bounds.StopLossMin, bounds.StopLossMax, bounds.StopLossStep, rng),
        },
    };

    return new ExitConfig
    {
        TakeProfit = new ExitRuleConfig
        {
            Enabled = true,
            Type = ExitRuleType.FixedPercent,
            Value = NextFromRange(bounds.TakeProfitMin, bounds.TakeProfitMax, bounds.TakeProfitStep, rng),
        },
        StopLoss = stopLoss,
        ExitOnOppositeSignal = NextFrom(bounds.ExitOnOppositeSignalOptions, rng),
    };
}
```

> **Note**: The description builder in `GenerateDescription` formats SL as `"SL:{Format(exit.StopLoss.Value)}%"`. For `AtrInitial`, `Value` is null. The description builder must handle this:

```csharp
// In GenerateDescription or wherever exit.StopLoss.Value is formatted:
// Check if AtrInitial: use AtrMultiplier in description instead of Value
var slDesc = exit.StopLoss.Type == ExitRuleType.AtrInitial
    ? $"SL:ATR×{exit.StopLoss.AtrMultiplier}"
    : $"SL:{Format(exit.StopLoss.Value)}%";
```

##### Pattern References

- `src/TradingApp.Application/Optimization/Services/StrategyConfigGenerator.cs` lines 240–260 — existing `GenerateExitConfig` hardcoded to `FixedPercent`
- `src/TradingApp.Application/Optimization/Services/StrategyConfigGenerator.cs` `NextFrom` helper — selects random element from array

### Task 4.3: Unit Tests for Optimizer Support {#task-43-unit-tests-for-optimizer-support}

Add tests verifying the optimizer generates correct `AtrInitial` configs and respects bounds.

- **Complexity**: Medium
- **Risk Factors**: None — follows existing test patterns
- **Files**:
  - `tests/TradingApp.Application.Tests/Optimization/StrategyConfigGeneratorTests.cs` - Add test methods
- **Success**:
  - Generated `AtrInitial` configs have valid `AtrMultiplier` from options
  - Generated `AtrInitial` configs have valid `AtrPeriod` from options
  - Default `StopLossTypes = [FixedPercent]` preserves existing behaviour
  - Sweep with `StopLossTypes = [AtrInitial]` produces only `AtrInitial` configs
  - All tests pass
- **Dependencies**: Tasks 4.1–4.2

#### Implementation Details

```csharp
// tests/TradingApp.Application.Tests/Optimization/StrategyConfigGeneratorTests.cs

[TestMethod]
public void GivenAtrInitialStopLossType_WhenGenerated_ThenConfigHasAtrInitialWithValidMultiplier()
{
    var bounds = new ParameterBounds
    {
        StopLossTypes = [ExitRuleType.AtrInitial],
        AtrMultiplierOptions = [1.0m, 1.5m, 2.0m, 2.5m, 3.0m],
        AtrPeriodOptions = [14],
        // ... other required bounds
    };

    var configs = StrategyConfigGenerator.Generate(bounds, count: 10, seed: 42);

    configs.Should().AllSatisfy(c =>
    {
        c.Exit.StopLoss.Type.Should().Be(ExitRuleType.AtrInitial);
        c.Exit.StopLoss.AtrMultiplier.Should().NotBeNull();
        bounds.AtrMultiplierOptions.Should().Contain(c.Exit.StopLoss.AtrMultiplier!.Value);
        c.Exit.StopLoss.AtrPeriod.Should().Be(14);
    });
}

[TestMethod]
public void GivenDefaultStopLossTypes_WhenGenerated_ThenAllConfigsAreFixedPercent()
{
    var bounds = new ParameterBounds(); // defaults: StopLossTypes = [FixedPercent]

    var configs = StrategyConfigGenerator.Generate(bounds, count: 10, seed: 42);

    configs.Should().AllSatisfy(c =>
    {
        c.Exit.StopLoss.Type.Should().Be(ExitRuleType.FixedPercent);
    });
}

[TestMethod]
public void GivenMixedStopLossTypes_WhenGenerated_ThenConfigsContainBothTypes()
{
    var bounds = new ParameterBounds
    {
        StopLossTypes = [ExitRuleType.FixedPercent, ExitRuleType.AtrInitial],
        AtrMultiplierOptions = [2.0m],
        // ... other required bounds
    };

    var configs = StrategyConfigGenerator.Generate(bounds, count: 50, seed: 42);

    configs.Should().Contain(c => c.Exit.StopLoss.Type == ExitRuleType.FixedPercent);
    configs.Should().Contain(c => c.Exit.StopLoss.Type == ExitRuleType.AtrInitial);
}
```

##### Pattern References

- `tests/TradingApp.Application.Tests/Optimization/StrategyConfigGeneratorTests.cs` — existing tests for determinism and bounds compliance

### Task 4.4: Build and Run All Tests {#task-44-build-and-run-all-tests}

Final build and full test run to verify all changes work together.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (build/test verification)
- **Success**:
  - `dotnet build TradingApp.sln` succeeds
  - `dotnet test TradingApp.sln --no-build --verbosity minimal` — all tests pass
  - No regressions in existing tests
- **Dependencies**: Tasks 4.1–4.3

## Phase Success Criteria

- `ParameterBounds` has `StopLossTypes`, `AtrMultiplierOptions`, and `AtrPeriodOptions` fields
- `StrategyConfigGenerator` generates valid `AtrInitial` configs when configured
- Default behaviour (only `FixedPercent`) is preserved
- Description builder handles `AtrInitial` without null reference
- All optimizer tests pass
- Full solution test suite green
