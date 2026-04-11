<!-- markdownlint-disable-file -->

# Task Details: Optimizer Risk Parameter Sweep

## Phase 2: Generator Logic, API Wiring & Comprehensive Tests

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — sealed classes, pattern matching for enums, PascalCase
- `.github/instructions/testing.instructions.md` — MSTest, FluentAssertions v6, `Given_When_Then` naming, direct instantiation (no mocks for `StrategyConfigGenerator`)
- `.github/instructions/api-controllers.instructions.md` — nullable override fields on request models, validation at API boundary
- `.agent-context/0-knowledge/33-risk-management-and-trade-sizing.md` — AutoLeverage = true skips leverage sweep; RiskBased uses `RiskPerTradePercent` instead of `PositionSizeValue`

### Task 2.1: Update `GenerateRiskConfig` {#task-21-update-generateriskconfig}

Modify `GenerateRiskConfig` to branch on `bounds.PositionSizeMode`. When `RiskBased`, set `PositionSizeType = RiskBased`, pick `RiskPerTradePercent` from bounds, and conditionally handle `AutoLeverage` and leverage sweep.

- **Complexity**: Medium
- **Risk Factors**: Core logic change — must correctly handle all 3 combinations (PercentWallet, RiskBased+AutoLev, RiskBased+ManualLev)
- **Files**:
  - `src/TradingApp.Application/Optimization/Services/StrategyConfigGenerator.cs` — modify `GenerateRiskConfig` method (L260)
- **Success**:
  - `PositionSizeMode.PercentWallet` produces identical output to current behaviour
  - `PositionSizeMode.RiskBased` produces configs with `PositionSizeType = RiskBased` and `RiskPerTradePercent` from options
  - `AutoLeverage = true` candidates have `Leverage = 1m` (placeholder, derived at runtime)
  - `AutoLeverage = false` candidates sweep leverage from `LeverageMin`/`LeverageMax`
  - When `IncludeAutoLeverage = true`, candidates randomly get `AutoLeverage = true/false`
  - When `IncludeAutoLeverage = false`, all candidates get `AutoLeverage = false`
- **Dependencies**: Phase 1 (all model types exist)

#### Implementation Details

```csharp
// src/TradingApp.Application/Optimization/Services/StrategyConfigGenerator.cs — modification
// Replace the existing GenerateRiskConfig method:

private static RiskConfig GenerateRiskConfig(ParameterBounds bounds, Random rng)
{
    var maxOpenTrades = NextFrom(bounds.MaxOpenTradesOptions, rng);
    var cooldownValue = NextFrom(bounds.CooldownCandlesOptions, rng);

    if (bounds.PositionSizeMode == PositionSizeMode.RiskBased)
    {
        var autoLeverage = bounds.IncludeAutoLeverage && rng.Next(2) == 0;
        var leverage = autoLeverage
            ? 1m  // placeholder — derived from SL distance at runtime
            : NextFromRange(bounds.LeverageMin, bounds.LeverageMax, bounds.LeverageStep, rng);

        return new RiskConfig
        {
            PositionSizeType = PositionSizeType.RiskBased,
            RiskPerTradePercent = NextFrom(bounds.RiskPerTradePercentOptions, rng),
            AutoLeverage = autoLeverage,
            Leverage = leverage,
            MaxOpenTrades = maxOpenTrades,
            CooldownValue = cooldownValue,
            CooldownUnit = CooldownUnit.Candles,
            AllowSameCandleReentry = false,
        };
    }

    return new RiskConfig
    {
        PositionSizeType = PositionSizeType.PercentWallet,
        PositionSizeValue = NextFrom(bounds.PositionSizeOptions, rng),
        Leverage = NextFromRange(bounds.LeverageMin, bounds.LeverageMax, bounds.LeverageStep, rng),
        MaxOpenTrades = maxOpenTrades,
        CooldownValue = cooldownValue,
        CooldownUnit = CooldownUnit.Candles,
        AllowSameCandleReentry = false,
    };
}
```

##### Pattern References

- `src/TradingApp.Application/Optimization/Services/StrategyConfigGenerator.cs` — existing `GenerateRiskConfig` at L260, `NextFrom` and `NextFromRange` helpers

---

### Task 2.2: Update `ValidateBounds` {#task-22-update-validatebounds}

Restructure `ValidateBounds` to make the position-size options check mode-conditional. Extract the existing `PositionSizeOptions.Length == 0` check from the compound `if` statement; when `PositionSizeMode = RiskBased`, validate `RiskPerTradePercentOptions` instead. The existing `PositionSizeOptions` check should only apply when mode is `PercentWallet`.

- **Complexity**: Medium
- **Risk Factors**: Must ensure the existing validation guard for `PositionSizeOptions` is made conditional — not removed
- **Files**:
  - `src/TradingApp.Application/Optimization/Services/StrategyConfigGenerator.cs` — modify `ValidateBounds` method (L399)
- **Success**:
  - `PositionSizeMode.RiskBased` with empty `RiskPerTradePercentOptions` throws `InvalidOperationException`
  - `PositionSizeMode.PercentWallet` with empty `PositionSizeOptions` still throws (existing behaviour)
  - `PositionSizeMode.RiskBased` with non-empty `RiskPerTradePercentOptions` and empty `PositionSizeOptions` does NOT throw
- **Dependencies**: Phase 1

#### Implementation Details

```csharp
// src/TradingApp.Application/Optimization/Services/StrategyConfigGenerator.cs — modification
// Replace the ValidateBounds method:

private static void ValidateBounds(ParameterBounds bounds)
{
    EnsureRange(bounds.StopLossMin, bounds.StopLossMax, bounds.StopLossStep, nameof(bounds.StopLossMin));
    EnsureRange(bounds.TakeProfitMin, bounds.TakeProfitMax, bounds.TakeProfitStep, nameof(bounds.TakeProfitMin));
    EnsureRange(bounds.LeverageMin, bounds.LeverageMax, bounds.LeverageStep, nameof(bounds.LeverageMin));

    // Position-size options are mode-dependent
    if (bounds.PositionSizeMode == PositionSizeMode.RiskBased)
    {
        if (bounds.RiskPerTradePercentOptions.Length == 0)
        {
            throw new InvalidOperationException("Optimizer bounds must include at least one RiskPerTradePercent option when using RiskBased sizing mode.");
        }
    }
    else if (bounds.PositionSizeOptions.Length == 0)
    {
        throw new InvalidOperationException("Optimizer bounds must include at least one option for each parameter family.");
    }

    if (bounds.Directions.Length == 0
        || bounds.RsiPeriods.Length == 0
        || bounds.RsiThresholds.Length == 0
        || bounds.RsiOperators.Length == 0
        || bounds.MacdFastPeriods.Length == 0
        || bounds.MacdSlowPeriods.Length == 0
        || bounds.MacdSignalPeriods.Length == 0
        || bounds.MacdOperators.Length == 0
        || bounds.EmaPeriods.Length == 0
        || bounds.EmaProximityPercents.Length == 0
        || bounds.PriceVsEmaOperators.Length == 0
        || bounds.ExitOnOppositeSignalOptions.Length == 0
        || bounds.MaxOpenTradesOptions.Length == 0
        || bounds.CooldownCandlesOptions.Length == 0)
    {
        throw new InvalidOperationException("Optimizer bounds must include at least one option for each parameter family.");
    }
}
```

##### Pattern References

- `src/TradingApp.Application/Optimization/Services/StrategyConfigGenerator.cs` — existing `ValidateBounds` at L399

---

### Task 2.3: Update `BuildDescription` {#task-23-update-builddescription}

Update `BuildDescription` to show `R:{value}%/trade` for `RiskBased` candidates instead of `Size:{value}%`. Also show `AutoLev` when `AutoLeverage = true`.

- **Complexity**: Low
- **Risk Factors**: None — string formatting only
- **Files**:
  - `src/TradingApp.Application/Optimization/Services/StrategyConfigGenerator.cs` — modify `BuildDescription` method (L316)
- **Success**:
  - `PercentWallet` descriptions unchanged
  - `RiskBased` descriptions show `R:1%/trade` instead of `Size:0%`
  - `AutoLeverage = true` appends `AutoLev` marker
- **Dependencies**: Phase 1

#### Implementation Details

```csharp
// src/TradingApp.Application/Optimization/Services/StrategyConfigGenerator.cs — modification
// In BuildDescription, replace the line that builds the risk/size portion of the description:

// Current:
// description += $" | {entryLogic} | SL:{Format(exit.StopLoss.Value)}% TP:{Format(exit.TakeProfit.Value)}% Lev:{Format(risk.Leverage)}x Size:{Format(risk.PositionSizeValue)}%";

// Replace with:
var sizeLabel = risk.PositionSizeType == PositionSizeType.RiskBased
    ? $"R:{Format(risk.RiskPerTradePercent)}%/trade"
    : $"Size:{Format(risk.PositionSizeValue)}%";

var leverageLabel = risk.AutoLeverage ? "AutoLev" : $"Lev:{Format(risk.Leverage)}x";

description += $" | {entryLogic} | SL:{Format(exit.StopLoss.Value)}% TP:{Format(exit.TakeProfit.Value)}% {leverageLabel} {sizeLabel}";
```

##### Pattern References

- `src/TradingApp.Application/Optimization/Services/StrategyConfigGenerator.cs` — existing `BuildDescription` at L316

---

### Task 2.4: Update `RunOptimizationRequest` {#task-24-update-runoptimizationrequest}

Add new nullable fields to the API request model so callers can specify the sizing mode, risk percent options, and auto-leverage preference.

- **Complexity**: Low
- **Risk Factors**: None — all fields are nullable with sensible defaults in `ParameterBounds`
- **Files**:
  - `src/TradingApp.Api/Models/RunOptimizationRequest.cs` — add properties
- **Success**:
  - New fields compile and are nullable (default to null → use `ParameterBounds` defaults)
  - Existing API callers are unaffected (new fields not required)
- **Dependencies**: Phase 1

#### Implementation Details

```csharp
// src/TradingApp.Api/Models/RunOptimizationRequest.cs — modification
// Add after the existing PositionSizePercent field:

    public decimal? PositionSizePercent { get; set; }

    // --- Risk-Based Sizing ---
    public string? PositionSizeMode { get; set; }
    public decimal[]? RiskPerTradePercentOptions { get; set; }
    public bool? IncludeAutoLeverage { get; set; }
```

Note: `PositionSizeMode` is a `string?` (not the enum directly) to follow the existing pattern where the API accepts strings and the controller parses/validates them (see `ParseDirections` pattern).

##### Pattern References

- `src/TradingApp.Api/Models/RunOptimizationRequest.cs` — existing nullable override fields pattern

---

### Task 2.5: Update `BuildBounds` {#task-25-update-buildbounds}

Update `BuildBounds` in `OptimizationsController` to map the new request fields to `ParameterBounds` properties.

- **Complexity**: Medium
- **Risk Factors**: Must correctly parse `PositionSizeMode` string to enum, with fallback to defaults
- **Files**:
  - `src/TradingApp.Api/Controllers/OptimizationsController.cs` — modify `BuildBounds` method (L130)
- **Success**:
  - `PositionSizeMode = "RiskBased"` maps to `PositionSizeMode.RiskBased`
  - `PositionSizeMode = null` defaults to `PositionSizeMode.PercentWallet`
  - `RiskPerTradePercentOptions` is passed through when provided
  - `IncludeAutoLeverage` is passed through when provided
- **Dependencies**: Task 2.4

#### Implementation Details

```csharp
// src/TradingApp.Api/Controllers/OptimizationsController.cs — modification
// In BuildBounds, add to the `return defaults with { ... }` expression:

    // Add before the closing brace of the `return defaults with { ... }` block:
    PositionSizeMode = Enum.TryParse<PositionSizeMode>(request.PositionSizeMode, ignoreCase: true, out var sizeMode)
        ? sizeMode
        : defaults.PositionSizeMode,
    RiskPerTradePercentOptions = request.RiskPerTradePercentOptions is { Length: > 0 }
        ? request.RiskPerTradePercentOptions
        : defaults.RiskPerTradePercentOptions,
    IncludeAutoLeverage = request.IncludeAutoLeverage ?? defaults.IncludeAutoLeverage,
```

Note: Add `using TradingApp.Application.Optimization.Models;` if not already present (for `PositionSizeMode` enum).

##### Pattern References

- `src/TradingApp.Api/Controllers/OptimizationsController.cs` — existing `BuildBounds` at L130, `ParseDirections` pattern for enum parsing

---

### Task 2.6: Add RiskBased unit tests {#task-26-add-riskbased-unit-tests}

Add comprehensive unit tests for all acceptance criteria. Tests use direct instantiation of `StrategyConfigGenerator` with seed-based determinism, following existing patterns.

- **Complexity**: Medium
- **Risk Factors**: Must test randomness behaviour (AutoLeverage = true/false distribution) with sufficient sample sizes
- **Files**:
  - `tests/TradingApp.Application.Tests/Optimization/StrategyConfigGeneratorTests.cs` — add test methods
- **Success**:
  - All 6 acceptance criteria have at least one corresponding test
  - Tests are deterministic (seed-based)
- **Dependencies**: Tasks 2.1–2.3

#### Implementation Details

```csharp
// tests/TradingApp.Application.Tests/Optimization/StrategyConfigGeneratorTests.cs — modification
// Add to existing StrategyConfigGeneratorTests class:

[TestMethod]
public void GivenRiskBasedMode_WhenGenerate_ThenAllCandidatesUseRiskBasedSizing()
{
    var bounds = new ParameterBounds
    {
        PositionSizeMode = PositionSizeMode.RiskBased,
        RiskPerTradePercentOptions = [0.5m, 1.0m, 1.5m, 2.0m],
    };

    var results = _generator.Generate("BTC", bounds, 50, seed: 5000);

    results.Should().OnlyContain(s =>
        s.Config.Risk.PositionSizeType == PositionSizeType.RiskBased
        && s.Config.Risk.RiskPerTradePercent > 0m);
}

[TestMethod]
public void GivenRiskBasedMode_WhenGenerate_ThenRiskPerTradePercentDrawnFromOptions()
{
    var options = new[] { 0.5m, 1.0m, 1.5m, 2.0m };
    var bounds = new ParameterBounds
    {
        PositionSizeMode = PositionSizeMode.RiskBased,
        RiskPerTradePercentOptions = options,
    };

    var results = _generator.Generate("BTC", bounds, 100, seed: 5001);

    results.Should().OnlyContain(s => options.Contains(s.Config.Risk.RiskPerTradePercent));
}

[TestMethod]
public void GivenPercentWalletMode_WhenGenerate_ThenAllCandidatesUsePercentWallet()
{
    var bounds = new ParameterBounds
    {
        PositionSizeMode = PositionSizeMode.PercentWallet,
    };

    var results = _generator.Generate("BTC", bounds, 30, seed: 5002);

    results.Should().OnlyContain(s =>
        s.Config.Risk.PositionSizeType == PositionSizeType.PercentWallet
        && s.Config.Risk.PositionSizeValue > 0m);
}

[TestMethod]
public void GivenRiskBasedWithAutoLeverageTrue_WhenGenerate_ThenLeverageNotSwept()
{
    var bounds = new ParameterBounds
    {
        PositionSizeMode = PositionSizeMode.RiskBased,
        IncludeAutoLeverage = false, // force all to AutoLeverage = false first
        LeverageMin = 3m,
        LeverageMax = 10m,
    };
    var boundsAutoOnly = bounds with { IncludeAutoLeverage = true };

    // Generate with IncludeAutoLeverage — filter to AutoLeverage = true candidates
    var results = _generator.Generate("BTC", boundsAutoOnly, 200, seed: 5003);
    var autoLeverageCandidates = results.Where(s => s.Config.Risk.AutoLeverage).ToList();

    autoLeverageCandidates.Should().NotBeEmpty("with IncludeAutoLeverage=true and 200 samples, some should have AutoLeverage=true");
    autoLeverageCandidates.Should().OnlyContain(s => s.Config.Risk.Leverage == 1m,
        "AutoLeverage=true candidates should have placeholder leverage of 1");
}

[TestMethod]
public void GivenRiskBasedWithAutoLeverageFalse_WhenGenerate_ThenLeverageSwept()
{
    var bounds = new ParameterBounds
    {
        PositionSizeMode = PositionSizeMode.RiskBased,
        IncludeAutoLeverage = false,
        LeverageMin = 3m,
        LeverageMax = 10m,
    };

    var results = _generator.Generate("BTC", bounds, 50, seed: 5004);

    results.Should().OnlyContain(s =>
        s.Config.Risk.AutoLeverage == false
        && s.Config.Risk.Leverage >= bounds.LeverageMin
        && s.Config.Risk.Leverage <= bounds.LeverageMax);
}

[TestMethod]
public void GivenRiskBasedWithIncludeAutoLeverage_WhenGenerate_ThenBothVariantsPresent()
{
    var bounds = new ParameterBounds
    {
        PositionSizeMode = PositionSizeMode.RiskBased,
        IncludeAutoLeverage = true,
    };

    var results = _generator.Generate("BTC", bounds, 200, seed: 5005);

    results.Should().Contain(s => s.Config.Risk.AutoLeverage == true,
        "some candidates should have AutoLeverage=true");
    results.Should().Contain(s => s.Config.Risk.AutoLeverage == false,
        "some candidates should have AutoLeverage=false");
}

[TestMethod]
public void GivenIncludeAutoLeverageFalse_WhenGenerate_ThenAllAutoLeverageFalse()
{
    var bounds = new ParameterBounds
    {
        PositionSizeMode = PositionSizeMode.RiskBased,
        IncludeAutoLeverage = false,
    };

    var results = _generator.Generate("BTC", bounds, 50, seed: 5006);

    results.Should().OnlyContain(s => s.Config.Risk.AutoLeverage == false);
}

[TestMethod]
public void GivenRiskBasedModeWithEmptyOptions_WhenGenerate_ThenThrows()
{
    var bounds = new ParameterBounds
    {
        PositionSizeMode = PositionSizeMode.RiskBased,
        RiskPerTradePercentOptions = [],
    };

    var action = () => _generator.Generate("BTC", bounds, 10, seed: 5007);

    action.Should().Throw<InvalidOperationException>()
        .WithMessage("*RiskPerTradePercent*");
}

[TestMethod]
public void GivenPercentWalletModeWithEmptyOptions_WhenGenerate_ThenThrows()
{
    var bounds = new ParameterBounds
    {
        PositionSizeMode = PositionSizeMode.PercentWallet,
        PositionSizeOptions = [],
    };

    var action = () => _generator.Generate("BTC", bounds, 10, seed: 5010);

    action.Should().Throw<InvalidOperationException>();
}

[TestMethod]
public void GivenRiskBasedMode_WhenGenerate_ThenDescriptionsContainRiskPercent()
{
    var bounds = new ParameterBounds
    {
        PositionSizeMode = PositionSizeMode.RiskBased,
        RiskPerTradePercentOptions = [1.0m],
        IncludeAutoLeverage = false,
    };

    var results = _generator.Generate("BTC", bounds, 10, seed: 5008);

    results.Should().OnlyContain(s => s.Description.Contains("R:1%/trade"));
}

[TestMethod]
public void GivenRiskBasedModeWithAutoLeverage_WhenGenerate_ThenDescriptionsContainAutoLev()
{
    var bounds = new ParameterBounds
    {
        PositionSizeMode = PositionSizeMode.RiskBased,
        IncludeAutoLeverage = true,
    };

    var results = _generator.Generate("BTC", bounds, 200, seed: 5009);

    var autoLevResults = results.Where(s => s.Config.Risk.AutoLeverage).ToList();
    autoLevResults.Should().NotBeEmpty();
    autoLevResults.Should().OnlyContain(s => s.Description.Contains("AutoLev"));
}
```

Note: Add `using TradingApp.Application.Optimization.Models;` to the test file for `PositionSizeMode`.

##### Pattern References

- `tests/TradingApp.Application.Tests/Optimization/StrategyConfigGeneratorTests.cs` — existing test patterns: direct `_generator` instantiation, `new ParameterBounds { ... }` with init overrides, seed-based determinism, FluentAssertions `Should().OnlyContain()` / `Should().Contain()`

---

### Task 2.7: Verify existing tests and run all {#task-27-verify-existing-tests-and-run-all}

Run the complete test suite to verify:
1. All existing `PercentWallet`-mode tests pass unchanged
2. All new `RiskBased` tests pass
3. Serialization tests pass
4. No regressions in the rest of the solution

- **Complexity**: Low
- **Risk Factors**: None — verification step
- **Files**: None (execution only)
- **Success**:
  - `dotnet test` passes with 0 failures
  - All new tests appear in the test output
- **Dependencies**: Tasks 2.1–2.6

## Phase Success Criteria

- `GenerateRiskConfig` produces `RiskBased` configs when `PositionSizeMode = RiskBased`
- `GenerateRiskConfig` produces `PercentWallet` configs when `PositionSizeMode = PercentWallet` (unchanged)
- `AutoLeverage = true` candidates skip leverage sweep; `AutoLeverage = false` candidates sweep normally
- `IncludeAutoLeverage = true` produces both variants; `IncludeAutoLeverage = false` produces only `AutoLeverage = false`
- `ValidateBounds` rejects empty `RiskPerTradePercentOptions` in `RiskBased` mode
- `BuildDescription` shows `R:{value}%/trade` for `RiskBased` and `AutoLev` for auto-leverage candidates
- API accepts new fields and wires them through to `ParameterBounds`
- All 10+ new tests pass, all existing tests pass
