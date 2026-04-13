<!-- markdownlint-disable-file -->

# Task Details: Partial Close at R-Levels

## Phase 4: Optimizer Integration

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — sealed classes, init properties
- `.github/instructions/testing.instructions.md` — MSTest, FluentAssertions ≤ v6, Given_When_Then naming
- `.agent-context/0-knowledge/33-risk-management-and-trade-sizing.md` — Partial close profiles

## Design References

- `ParameterBounds` currently has `StopLossMin/Max/Step`, `TakeProfitMin/Max/Step`, `LeverageMin/Max/Step`, `RiskPerTradePercentOptions`
- `StrategyConfigGenerator.GenerateExitConfig` always generates `ExitRuleType.FixedPercent` for TP/SL — never `RMultiple`
- Adding `IncludePartialCloses` boolean to `ParameterBounds` controls whether generated candidates include partial close tranches
- Use predefined common profiles rather than random tranche permutations — keeps search space manageable

### Task 4.1: Add `IncludePartialCloses` to `ParameterBounds` {#task-41-add-includepartialcloses-to-parameterbounds}

Add an `IncludePartialCloses` boolean to `ParameterBounds` (default: `false`).

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.Application/Optimization/Models/ParameterBounds.cs` — Add property
- **Success**:
  - `IncludePartialCloses` property exists, defaults to `false`
  - Existing optimizer runs unaffected (default = false)

#### Implementation Details

```csharp
// src/TradingApp.Application/Optimization/Models/ParameterBounds.cs — modification
public sealed record ParameterBounds
{
    // ... existing properties ...
    public bool IncludePartialCloses { get; init; }  // NEW — default false
}
```

##### Pattern References

- `src/TradingApp.Application/Optimization/Models/ParameterBounds.cs` — existing parameter bounds structure

### Task 4.2: Extend `StrategyConfigGenerator` for partial close candidates {#task-42-extend-strategyconfiggenerator-for-partial-close-candidates}

When `IncludePartialCloses = true` and the generated exit config uses `RiskBased` sizing, randomly assign a partial close profile from a predefined set of common configurations. Some candidates should have no partial closes (to let the optimizer compare).

- **Complexity**: Medium
- **Risk Factors**: Must use the RNG seed for determinism. Must only apply when `PositionSizeType = RiskBased`.
- **Files**:
  - `src/TradingApp.Application/Optimization/Services/StrategyConfigGenerator.cs` — Extend `GenerateExitConfig` or add `GeneratePartialCloses`
- **Success**:
  - When `IncludePartialCloses = true`: ~50% of candidates get a partial close profile, ~50% get null
  - Predefined profiles: "25/25/50 at 1R/2R/3R", "50/50 at 1R/2R", "33/33/34 at 1R/2R/3R", "none"
  - Deterministic with same seed
  - When `IncludePartialCloses = false`: all candidates have `PartialCloses = null` (existing behavior)

#### Implementation Details

```csharp
// StrategyConfigGenerator.cs — add helper method

private static readonly IReadOnlyList<PartialCloseLevel[]?> PartialCloseProfiles = new PartialCloseLevel[]?[]
{
    null, // no partial closes
    null, // weight toward "no partial closes" (50% chance)
    new[]
    {
        new PartialCloseLevel { AtRMultiple = 1m, ClosePercent = 25 },
        new PartialCloseLevel { AtRMultiple = 2m, ClosePercent = 25 },
        new PartialCloseLevel { AtRMultiple = 3m, ClosePercent = 50 },
    },
    new[]
    {
        new PartialCloseLevel { AtRMultiple = 1m, ClosePercent = 50 },
        new PartialCloseLevel { AtRMultiple = 2m, ClosePercent = 50 },
    },
    new[]
    {
        new PartialCloseLevel { AtRMultiple = 1m, ClosePercent = 33 },
        new PartialCloseLevel { AtRMultiple = 2m, ClosePercent = 33 },
        new PartialCloseLevel { AtRMultiple = 3m, ClosePercent = 34 },
    },
};

private IReadOnlyList<PartialCloseLevel>? GeneratePartialCloses(
    ParameterBounds bounds, RiskConfig riskConfig, Random rng)
{
    if (!bounds.IncludePartialCloses) return null;
    if (riskConfig.PositionSizeType != PositionSizeType.RiskBased) return null;

    var profile = PartialCloseProfiles[rng.Next(PartialCloseProfiles.Count)];
    return profile;
}

// Then in GenerateExitConfig or Generate, set:
// exitConfig = exitConfig with { PartialCloses = GeneratePartialCloses(bounds, rng) };
```

##### Pattern References

- `src/TradingApp.Application/Optimization/Services/StrategyConfigGenerator.cs` — existing `Generate` and random sampling pattern

### Task 4.3: Add unit tests for optimizer partial close generation {#task-43-add-unit-tests-for-optimizer-partial-close-generation}

Add tests to `StrategyConfigGeneratorTests` for partial close candidate generation.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `tests/TradingApp.Application.Tests/Optimization/StrategyConfigGeneratorTests.cs` — Add tests
- **Success**:
  - Test: `IncludePartialCloses = false` → all candidates have `PartialCloses = null`
  - Test: `IncludePartialCloses = true` → some candidates have partial closes, some don't
  - Test: generated partial close profiles are valid (sum ≤ 100, R > 0)
  - Test: deterministic with same seed
  - All tests pass

#### Implementation Details

```csharp
[TestMethod]
public void GivenIncludePartialClosesFalse_WhenGenerated_ThenNoPartialCloses()
{
    var bounds = CreateBounds() with { IncludePartialCloses = false };
    var configs = _sut.Generate("BTC", bounds, 20, seed: 42);

    configs.Should().AllSatisfy(c => c.Exit.PartialCloses.Should().BeNull());
}

[TestMethod]
public void GivenIncludePartialClosesTrue_WhenGenerated_ThenMixOfPartialAndNone()
{
    var bounds = CreateBounds() with { IncludePartialCloses = true };
    var configs = _sut.Generate("BTC", bounds, 20, seed: 42);

    configs.Should().Contain(c => c.Exit.PartialCloses != null);
    configs.Should().Contain(c => c.Exit.PartialCloses == null);
}
```

##### Pattern References

- `tests/TradingApp.Application.Tests/Optimization/StrategyConfigGeneratorTests.cs` — existing parameter generation tests

### Task 4.4: Run architecture tests {#task-44-run-architecture-tests}

Run the solution's architecture tests to ensure no layer violations were introduced.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (test execution only)
- **Success**:
  - All architecture tests pass
  - All existing tests continue to pass

## Phase Success Criteria

- `ParameterBounds.IncludePartialCloses` property exists
- Optimizer generates candidates with and without partial closes when enabled
- Deterministic generation with same seed
- All new and existing tests pass
