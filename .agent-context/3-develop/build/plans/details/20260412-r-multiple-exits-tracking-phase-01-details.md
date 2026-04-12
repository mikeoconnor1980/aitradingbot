<!-- markdownlint-disable-file -->

# Task Details: R-Multiple Exit Types & Trade Tracking

## Phase 1: Domain Models & Validation

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — sealed classes, PascalCase, Guards
- `.github/instructions/testing.instructions.md` — MSTest, Moq, FluentAssertions ≤6, Given_When_Then naming
- `.github/instructions/dotnet-architecture.instructions.md` — layered architecture, value objects
- `.agent-context/0-knowledge/33-risk-management-and-trade-sizing.md` — R-based sizing, R-multiple targets

### Task 1.1: Add `RMultiple` to `ExitRuleType` enum {#task-11-add-rmultiple-to-exitruletype-enum}

Add the `RMultiple` value to the existing `ExitRuleType` enum.

- **Complexity**: Low
- **Risk Factors**: None — purely additive enum extension
- **Files**:
  - `src/TradingApp.Application/StrategyAuthoring/Models/ExitRuleType.cs` — add new enum value
- **Success**:
  - `ExitRuleType.RMultiple` compiles and serializes as `"r_multiple"` via the global `JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)`
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Application/StrategyAuthoring/Models/ExitRuleType.cs — modification
namespace TradingApp.Application.StrategyAuthoring.Models;

public enum ExitRuleType
{
    FixedPercent,
    SwingLow,
    AtrTrailing,
    RMultiple,
}
```

##### Pattern References

- `src/TradingApp.Application/StrategyAuthoring/Models/ExitRuleType.cs` (existing enum)
- `src/TradingApp.Application/StrategyAuthoring/Serialization/StrategyJsonOptions.cs` (confirms `JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)`)

### Task 1.2: Add R-multiple validation rules {#task-12-add-r-multiple-validation-rules}

Add validation rules in `BusinessRuleValidator.ValidateExit` for the `RMultiple` TP type:
- Error if `Value < 0` (block negative R targets)
- Warning if `Value > 0 && Value < 1` (sub-1R trade warning)

- **Complexity**: Low
- **Risk Factors**: None — follows existing validation pattern
- **Files**:
  - `src/TradingApp.Application/StrategyAuthoring/Validation/BusinessRuleValidator.cs` — add validation branches
- **Success**:
  - RMultiple TP with value < 0 → error `TP_R_MULTIPLE_NEGATIVE`
  - RMultiple TP with 0 < value < 1 → warning `TP_R_MULTIPLE_SUB_ONE`
  - RMultiple TP with value >= 1 → no error/warning
- **Dependencies**: Task 1.1

#### Implementation Details

```csharp
// src/TradingApp.Application/StrategyAuthoring/Validation/BusinessRuleValidator.cs — modification
// Add to ValidateExit method, after existing TP value checks:

// ... existing code ...
if (exit.TakeProfit.Enabled && exit.TakeProfit.Value is not null && exit.TakeProfit.Value <= 0)
{
    result.Add(new ValidationError
    {
        Severity = ValidationSeverity.Error,
        FieldPath = "exit.takeProfit.value",
        Code = "TP_VALUE_INVALID",
        Message = "Take profit value must be greater than 0 when enabled.",
    });
}

if (exit.TakeProfit.Enabled && exit.TakeProfit.Type == ExitRuleType.RMultiple)
{
    if (exit.TakeProfit.Value is not null && exit.TakeProfit.Value < 0m)
    {
        result.Add(new ValidationError
        {
            Severity = ValidationSeverity.Error,
            FieldPath = "exit.takeProfit.value",
            Code = "TP_R_MULTIPLE_NEGATIVE",
            Message = "R-multiple take profit target must not be negative.",
        });
    }
    else if (exit.TakeProfit.Value is not null && exit.TakeProfit.Value > 0m && exit.TakeProfit.Value < 1m)
    {
        result.Add(new ValidationError
        {
            Severity = ValidationSeverity.Warning,
            FieldPath = "exit.takeProfit.value",
            Code = "TP_R_MULTIPLE_SUB_ONE",
            Message = "Sub-1R take profit — this trade relies on a high win rate to be profitable.",
        });
    }
}
// ... existing code ...
```

##### Pattern References

- `src/TradingApp.Application/StrategyAuthoring/Validation/BusinessRuleValidator.cs` (existing validation pattern with `ValidationSeverity.Error` and `ValidationSeverity.Warning`)

### Task 1.3: Add cross-field validation for RMultiple TP {#task-13-add-cross-field-validation-for-rmultiple-tp}

Add cross-field validation: RMultiple TP requires `RiskBased` position sizing and an enabled stop-loss (to establish the R unit).

- **Complexity**: Low
- **Risk Factors**: None — follows existing cross-field validation pattern
- **Files**:
  - `src/TradingApp.Application/StrategyAuthoring/Validation/CrossFieldValidator.cs` — add new cross-field rule
- **Success**:
  - RMultiple TP + non-RiskBased sizing → error
  - RMultiple TP + RiskBased + no SL → error (unless grid breakdown threshold exists)
  - RMultiple TP + RiskBased + SL enabled → no error
- **Dependencies**: Task 1.1

#### Implementation Details

```csharp
// src/TradingApp.Application/StrategyAuthoring/Validation/CrossFieldValidator.cs — modification
// Add new validation method call in Validate():

public void Validate(StrategyConfig config, ValidationResult result)
{
    ArgumentNullException.ThrowIfNull(config);
    ArgumentNullException.ThrowIfNull(result);

    ValidateStrategyModeConsistency(config, result);
    ValidateRiskBasedRequiresStopLoss(config, result);
    ValidateRMultipleTpRequiresRiskBased(config, result);
}

// Add new method:
private static void ValidateRMultipleTpRequiresRiskBased(StrategyConfig config, ValidationResult result)
{
    if (!config.Exit.TakeProfit.Enabled || config.Exit.TakeProfit.Type != ExitRuleType.RMultiple)
    {
        return;
    }

    if (config.Risk.PositionSizeType != PositionSizeType.RiskBased)
    {
        result.Add(new ValidationError
        {
            Severity = ValidationSeverity.Error,
            FieldPath = "exit.takeProfit.type",
            Code = "R_MULTIPLE_TP_REQUIRES_RISK_BASED",
            Message = "R-multiple take profit requires Risk-Based position sizing to establish the risk unit (R).",
        });
    }
}
```

##### Pattern References

- `src/TradingApp.Application/StrategyAuthoring/Validation/CrossFieldValidator.cs` (existing `ValidateRiskBasedRequiresStopLoss` pattern)

### Task 1.4: Unit tests for validation rules {#task-14-unit-tests-for-validation-rules}

Write unit tests for the new RMultiple validation rules in `BusinessRuleValidator` and `CrossFieldValidator`.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `tests/TradingApp.Application.Tests/StrategyAuthoring/Validation/BusinessRuleValidatorTests.cs` — add RMultiple test cases (create if not exists)
  - `tests/TradingApp.Application.Tests/StrategyAuthoring/Validation/CrossFieldValidatorTests.cs` — add RMultiple cross-field test cases (create if not exists)
- **Success**:
  - Tests verify: RMultiple negative → error, sub-1R → warning, ≥1R → no issue
  - Tests verify: RMultiple TP + non-RiskBased → error, RMultiple TP + RiskBased → pass
  - All tests pass
- **Dependencies**: Tasks 1.2, 1.3

### Task 1.5: Build and verify {#task-15-build-and-verify}

Build the solution and run all tests to verify no regressions.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None
- **Success**:
  - `dotnet build TradingApp.sln` succeeds
  - `dotnet test TradingApp.sln` — all tests pass
- **Dependencies**: Task 1.4

## Phase Success Criteria

- `ExitRuleType.RMultiple` exists and serializes as `"r_multiple"`
- Validation rejects negative R-multiple, warns on sub-1R
- Cross-field validation requires RiskBased sizing for RMultiple TP
- All existing tests continue to pass
