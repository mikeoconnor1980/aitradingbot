<!-- markdownlint-disable-file -->

# Task Details: Optimizer Risk Parameter Sweep

## Phase 1: Domain & Optimizer Model Extensions

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — **sealed classes by default**, one class/interface per file, PascalCase properties
- `.github/instructions/testing.instructions.md` — MSTest, FluentAssertions v6, Moq, `Given_When_Then` naming
- `.github/instructions/dotnet-architecture.instructions.md` — clean architecture layers, models in `Models/` folders
- `.agent-context/0-knowledge/33-risk-management-and-trade-sizing.md` — target RiskConfig schema with `RiskPerTradePercent` and `AutoLeverage`, `PositionSizeMode` for optimizer bounds
- `.agent-context/0-knowledge/13-strategy-config-schema.md` — serialization rules, `StrategyJsonOptions.Default` with snake_case enums

### Task 1.1: Add `RiskBased` to `PositionSizeType` enum {#task-11-add-riskbased-to-positionsizetype-enum}

Add the `RiskBased` value to the existing `PositionSizeType` enum.

- **Complexity**: Low
- **Risk Factors**: None — additive enum change, backward-compatible
- **Files**:
  - `src/TradingApp.Application/StrategyAuthoring/Models/PositionSizeType.cs` — add enum value
- **Success**:
  - `PositionSizeType.RiskBased` compiles and is the 3rd enum value
  - Existing code referencing `PercentWallet` or `FixedNotional` is unaffected
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Application/StrategyAuthoring/Models/PositionSizeType.cs — modification
namespace TradingApp.Application.StrategyAuthoring.Models;

public enum PositionSizeType
{
    PercentWallet,
    FixedNotional,
    RiskBased,
}
```

##### Pattern References

- `src/TradingApp.Application/StrategyAuthoring/Models/PositionSizeType.cs` — existing enum structure

---

### Task 1.2: Add `RiskPerTradePercent` and `AutoLeverage` to `RiskConfig` {#task-12-add-riskpertradePercent-and-autoleverage-to-riskconfig}

Add two new properties to `RiskConfig` for risk-based sizing. Both default to values that preserve existing behaviour (0 and false respectively).

- **Complexity**: Low
- **Risk Factors**: None — new optional properties with safe defaults; existing JSON without these keys deserializes to defaults
- **Files**:
  - `src/TradingApp.Application/StrategyAuthoring/Models/RiskConfig.cs` — add properties
- **Success**:
  - `RiskConfig.RiskPerTradePercent` and `RiskConfig.AutoLeverage` compile and are settable via init
  - Existing `RiskConfig` constructors/usages are unaffected
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Application/StrategyAuthoring/Models/RiskConfig.cs — modification
namespace TradingApp.Application.StrategyAuthoring.Models;

public sealed record RiskConfig
{
    public PositionSizeType PositionSizeType { get; init; }
    public decimal PositionSizeValue { get; init; }
    public decimal RiskPerTradePercent { get; init; }
    public bool AutoLeverage { get; init; }
    public decimal Leverage { get; init; } = 1m;
    public int MaxOpenTrades { get; init; } = 1;
    public int CooldownValue { get; init; }
    public CooldownUnit CooldownUnit { get; init; }
    public bool AllowSameCandleReentry { get; init; }
}
```

##### Pattern References

- `src/TradingApp.Application/StrategyAuthoring/Models/RiskConfig.cs` — existing sealed record structure

---

### Task 1.3: Create `PositionSizeMode` enum {#task-13-create-positionsizemode-enum}

Create a new enum for the optimizer to select between sizing sweep modes. This is an optimizer-specific concept (distinct from `PositionSizeType` which lives on the strategy config).

- **Complexity**: Low
- **Risk Factors**: None — new file, no existing references
- **Files**:
  - `src/TradingApp.Application/Optimization/Models/PositionSizeMode.cs` — new file
- **Success**:
  - `PositionSizeMode.PercentWallet` and `PositionSizeMode.RiskBased` compile
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Application/Optimization/Models/PositionSizeMode.cs — new file
namespace TradingApp.Application.Optimization.Models;

public enum PositionSizeMode
{
    PercentWallet,
    RiskBased,
}
```

##### Pattern References

- `src/TradingApp.Application/Optimization/Models/ParameterBounds.cs` — namespace and folder convention for optimizer models

---

### Task 1.4: Extend `ParameterBounds` with risk-based fields {#task-14-extend-parameterbounds-with-risk-based-fields}

Add `PositionSizeMode`, `RiskPerTradePercentOptions`, and `IncludeAutoLeverage` properties to `ParameterBounds`. Default values preserve existing behaviour (`PercentWallet` mode).

- **Complexity**: Low
- **Risk Factors**: None — additive properties with backward-compatible defaults
- **Files**:
  - `src/TradingApp.Application/Optimization/Models/ParameterBounds.cs` — add properties
- **Success**:
  - New properties compile and default to `PositionSizeMode.PercentWallet`, `[0.25m, 0.5m, 1.0m, 1.5m, 2.0m, 3.0m]`, and `true` respectively
  - `new ParameterBounds()` (default constructor) still works for all existing code
- **Dependencies**: Task 1.3 (PositionSizeMode enum)

#### Implementation Details

```csharp
// src/TradingApp.Application/Optimization/Models/ParameterBounds.cs — modification
// Add after the existing "// --- Position Size ---" section:

    // --- Position Size ---
    public PositionSizeMode PositionSizeMode { get; init; } = PositionSizeMode.PercentWallet;
    public decimal[] PositionSizeOptions { get; init; } = [10m, 15m, 20m];
    public decimal[] RiskPerTradePercentOptions { get; init; } = [0.25m, 0.5m, 1.0m, 1.5m, 2.0m, 3.0m];
    public bool IncludeAutoLeverage { get; init; } = true;
```

##### Pattern References

- `src/TradingApp.Application/Optimization/Models/ParameterBounds.cs` — existing init property pattern with array defaults

---

### Task 1.5: Add serialization tests {#task-15-add-serialization-tests}

Add tests verifying that the new `RiskBased` enum value serializes to `"risk_based"` in snake_case, and that `RiskConfig` with the new fields round-trips correctly.

- **Complexity**: Low
- **Risk Factors**: None — follows existing serialization test patterns
- **Files**:
  - `tests/TradingApp.Application.Tests/StrategyAuthoring/Models/StrategyConfigSerializationTests.cs` — add test methods
- **Success**:
  - `RiskBased` serializes to `"risk_based"` and deserializes back
  - `RiskConfig` with `RiskPerTradePercent` and `AutoLeverage` round-trips correctly
- **Dependencies**: Tasks 1.1, 1.2

#### Implementation Details

```csharp
// tests/TradingApp.Application.Tests/StrategyAuthoring/Models/StrategyConfigSerializationTests.cs — modification
// Add to existing test class:

[TestMethod]
public void GivenRiskBasedConfig_WhenSerialized_ThenEnumIsSnakeCase()
{
    var config = new StrategyConfig
    {
        StrategyMode = StrategyMode.Signal,
        Risk = new RiskConfig
        {
            PositionSizeType = PositionSizeType.RiskBased,
            RiskPerTradePercent = 1.5m,
            AutoLeverage = true,
        },
    };

    var json = JsonSerializer.Serialize(config, StrategyJsonOptions.Default);

    json.Should().Contain("\"positionSizeType\":\"risk_based\"");
    json.Should().Contain("\"riskPerTradePercent\":1.5");
    json.Should().Contain("\"autoLeverage\":true");
}

[TestMethod]
public void GivenRiskBasedConfig_WhenSerializedAndDeserialized_ThenRoundTripsCorrectly()
{
    var config = new StrategyConfig
    {
        SchemaVersion = 1,
        StrategyMode = StrategyMode.Signal,
        StrategyName = "Risk Test",
        Exchange = "Hyperliquid",
        Market = "BTC-USD",
        Timeframe = "15m",
        Direction = Direction.Long,
        Enabled = true,
        Exit = new ExitConfig
        {
            TakeProfit = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.FixedPercent, Value = 3m },
            StopLoss = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.FixedPercent, Value = 2m },
        },
        Risk = new RiskConfig
        {
            PositionSizeType = PositionSizeType.RiskBased,
            RiskPerTradePercent = 1.0m,
            AutoLeverage = true,
            Leverage = 5m,
            MaxOpenTrades = 1,
        },
    };

    var json = JsonSerializer.Serialize(config, StrategyJsonOptions.Default);
    var deserialized = JsonSerializer.Deserialize<StrategyConfig>(json, StrategyJsonOptions.Default);

    deserialized.Should().NotBeNull();
    deserialized!.Risk.PositionSizeType.Should().Be(PositionSizeType.RiskBased);
    deserialized.Risk.RiskPerTradePercent.Should().Be(1.0m);
    deserialized.Risk.AutoLeverage.Should().BeTrue();
}
```

##### Pattern References

- `tests/TradingApp.Application.Tests/StrategyAuthoring/Models/StrategyConfigSerializationTests.cs` — existing `GivenGridModeConfig_WhenSerialized_ThenEnumsAreSnakeCase` test pattern

---

### Task 1.6: Run all existing tests {#task-16-run-all-existing-tests}

Run all tests across the solution to verify that the additive model changes do not break existing functionality.

- **Complexity**: Low
- **Risk Factors**: None — changes are purely additive with safe defaults
- **Files**: None (execution only)
- **Success**:
  - All existing tests pass
  - New serialization tests pass
- **Dependencies**: Tasks 1.1–1.5

## Phase Success Criteria

- `PositionSizeType.RiskBased` exists and serializes to `"risk_based"`
- `RiskConfig` has `RiskPerTradePercent` (decimal) and `AutoLeverage` (bool) properties
- `PositionSizeMode` enum exists with `PercentWallet` and `RiskBased` values
- `ParameterBounds` has `PositionSizeMode`, `RiskPerTradePercentOptions`, and `IncludeAutoLeverage` properties with correct defaults
- All existing tests pass, plus 2 new serialization tests pass
