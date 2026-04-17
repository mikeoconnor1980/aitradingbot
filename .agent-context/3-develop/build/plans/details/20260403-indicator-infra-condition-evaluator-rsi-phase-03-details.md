<!-- markdownlint-disable-file -->

# Task Details: F5 — Indicator Infrastructure & Condition Evaluator (RSI)

## Phase 3: Strategy Engine Routing & Integration

## Standards and Knowledge References

- **csharp.instructions.md**: sealed classes, DI via constructor injection
- **testing.instructions.md**: MSTest, Moq, FluentAssertions 6, Given_When_Then
- **dotnet-architecture.instructions.md**: Application service patterns
- **Knowledge**: `14-strategy-runtime-model.md` (pipeline: contextBuilder → strategyEngine → gridController → risk)
- DI registration pattern: flat explicit registration in `Program.cs`

---

### Task 3.1: Create `CompositeStrategyEngine` {#task-31-create-compositestrategyengine}

Create a composite strategy engine that routes by `StrategyMode`: grid mode delegates to `GridStrategyEngine`, signal mode delegates to `IConditionEvaluator` and maps the result to `StrategyEvaluation`.

- **Complexity**: Medium
- **Risk Factors**: Must preserve grid path exactly; must correctly map `ConditionEvaluationResult` to `StrategyEvaluation`; `IStrategyConfig` may not be `StrategyConfig` — must handle cast gracefully
- **Files**:
  - `src/TradePilot.Application/Trading/Services/CompositeStrategyEngine.cs` — **New**
- **Success**:
  - Grid mode → delegates to `GridStrategyEngine`, returns its result unchanged
  - Signal mode → calls `IConditionEvaluator.Evaluate()`, maps to `StrategyEvaluation`
  - Non-StrategyConfig → throws `ArgumentException` (existing pattern from `GridStrategyEngine`)
- **Dependencies**:
  - Phase 2 (IConditionEvaluator, ConditionEvaluationResult)

#### Implementation Details

```csharp
// src/TradePilot.Application/Trading/Services/CompositeStrategyEngine.cs — new file
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Services;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Trading;

namespace TradePilot.Application.Trading.Services;

/// <summary>
/// Routes strategy evaluation by StrategyMode:
/// - Grid → GridStrategyEngine (existing logic)
/// - Signal → IConditionEvaluator (entry condition evaluation)
/// </summary>
public sealed class CompositeStrategyEngine : IStrategyEngine
{
    private readonly GridStrategyEngine _gridEngine;
    private readonly IConditionEvaluator _conditionEvaluator;

    public CompositeStrategyEngine(
        GridStrategyEngine gridEngine,
        IConditionEvaluator conditionEvaluator)
    {
        _gridEngine = gridEngine ?? throw new ArgumentNullException(nameof(gridEngine));
        _conditionEvaluator = conditionEvaluator ?? throw new ArgumentNullException(nameof(conditionEvaluator));
    }

    public Task<StrategyEvaluation> EvaluateAsync(
        MarketContext context,
        IStrategyConfig strategyConfig,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(strategyConfig);

        if (strategyConfig is not StrategyConfig config)
        {
            throw new ArgumentException(
                $"Expected {nameof(StrategyConfig)} but received {strategyConfig.GetType().Name}.",
                nameof(strategyConfig));
        }

        return config.StrategyMode switch
        {
            StrategyMode.Signal => Task.FromResult(EvaluateSignalMode(config, context)),
            _ => _gridEngine.EvaluateAsync(context, strategyConfig, cancellationToken)
        };
    }

    private StrategyEvaluation EvaluateSignalMode(StrategyConfig config, MarketContext context)
    {
        var result = _conditionEvaluator.Evaluate(config, context);

        return new StrategyEvaluation
        {
            SetupDetected = result.SetupDetected,
            Reason = result.OverallReason
        };
    }
}
```

##### Pattern References

- `src/TradePilot.Application/Trading/Services/GridStrategyEngine.cs` — existing engine that handles grid mode; reused directly
- `src/TradePilot.Application/Abstractions/Services/IStrategyEngine.cs` — interface implemented by the composite

---

### Task 3.2: Update DI registrations {#task-32-update-di-registrations}

Update `Program.cs` to register `CompositeStrategyEngine` as `IStrategyEngine`, register `GridStrategyEngine` as a concrete type, register `IConditionEvaluator` and `IConditionHandler` implementations.

- **Complexity**: Low
- **Risk Factors**: Must register `IConditionHandler` implementations as `IEnumerable<IConditionHandler>` for the evaluator to resolve; `GridStrategyEngine` must be registered as concrete (not via interface) for `CompositeStrategyEngine` to inject
- **Files**:
  - `src/TradePilot.Api/Program.cs` — **Modified**
- **Success**:
  - `IStrategyEngine` → `CompositeStrategyEngine`
  - `GridStrategyEngine` registered as concrete scoped service
  - `IConditionEvaluator` → `ConditionEvaluator`
  - `IConditionHandler` → `RsiConditionHandler` (multi-registration via `IEnumerable<IConditionHandler>`)
- **Dependencies**:
  - Task 3.1 (CompositeStrategyEngine)

#### Implementation Details

```csharp
// src/TradePilot.Api/Program.cs — modification
// Replace:
//   builder.Services.AddScoped<IStrategyEngine, GridStrategyEngine>();
// With:
builder.Services.AddScoped<GridStrategyEngine>();
builder.Services.AddScoped<IConditionHandler, RsiConditionHandler>();
builder.Services.AddScoped<IConditionEvaluator, ConditionEvaluator>();
builder.Services.AddScoped<IStrategyEngine, CompositeStrategyEngine>();
```

Add the necessary `using` statements at the top:
```csharp
using TradePilot.Application.StrategyAuthoring.Services;
```

##### Pattern References

- `src/TradePilot.Api/Program.cs` — existing flat DI registration pattern (lines 88–103)

---

### Task 3.3: Remove `SIGNAL_MODE_NOT_SUPPORTED` info message {#task-33-remove-signal-mode-not-supported-info-message}

Remove the `SIGNAL_MODE_NOT_SUPPORTED` info message from `CrossFieldValidator.EmitV1InfoMessages()` since signal mode is now supported.

- **Complexity**: Low
- **Risk Factors**: Must update `CrossFieldValidatorTests` that assert this message
- **Files**:
  - `src/TradePilot.Application/StrategyAuthoring/Validation/CrossFieldValidator.cs` — **Modified**
  - `tests/TradePilot.Application.Tests/StrategyAuthoring/Validation/CrossFieldValidatorTests.cs` — **Modified**
- **Success**:
  - `SIGNAL_MODE_NOT_SUPPORTED` info message no longer emitted for `StrategyMode.Signal`
  - Test asserting this message is removed or updated
- **Dependencies**:
  - None (independent of engine changes, but logically part of F5 shipping)

#### Implementation Details

```csharp
// src/TradePilot.Application/StrategyAuthoring/Validation/CrossFieldValidator.cs — modification
// Remove the signal mode info block from EmitV1InfoMessages:

// DELETE this block:
//     if (config.StrategyMode == StrategyMode.Signal)
//     {
//         result.Add(new ValidationError
//         {
//             Severity = ValidationSeverity.Info,
//             FieldPath = "strategyMode",
//             Code = "SIGNAL_MODE_NOT_SUPPORTED",
//             Message = "Signal mode not yet supported for execution.",
//         });
//     }
```

```csharp
// tests/TradePilot.Application.Tests/StrategyAuthoring/Validation/CrossFieldValidatorTests.cs — modification
// Remove or update the test that asserts SIGNAL_MODE_NOT_SUPPORTED is emitted.
// Search for test methods containing "SIGNAL_MODE_NOT_SUPPORTED" and update them.
```

##### Pattern References

- `src/TradePilot.Application/StrategyAuthoring/Validation/CrossFieldValidator.cs` — lines 68–77 contain the block to remove

---

### Task 3.4: Write unit and integration tests {#task-34-write-unit-and-integration-tests}

Write tests for `CompositeStrategyEngine` routing behavior and verify end-to-end flow.

- **Complexity**: Medium
- **Risk Factors**: Must verify grid path is completely preserved; must mock `IConditionEvaluator` for unit tests
- **Files**:
  - `tests/TradePilot.Application.Tests/Trading/Services/CompositeStrategyEngineTests.cs` — **New**
- **Success**:
  - Grid mode → delegates to `GridStrategyEngine` (unchanged behavior)
  - Signal mode → delegates to `IConditionEvaluator`, maps result
  - Non-StrategyConfig → throws `ArgumentException`
- **Dependencies**:
  - Tasks 3.1–3.3

#### Implementation Details

```csharp
// tests/TradePilot.Application.Tests/Trading/Services/CompositeStrategyEngineTests.cs — new file
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Services;
using TradePilot.Application.Trading.Models;
using TradePilot.Application.Trading.Services;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Tests.Trading.Services;

[TestClass]
public sealed class CompositeStrategyEngineTests
{
    private Mock<IConditionEvaluator> _conditionEvaluatorMock = default!;
    private CompositeStrategyEngine _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _conditionEvaluatorMock = new Mock<IConditionEvaluator>();
        _sut = new CompositeStrategyEngine(new GridStrategyEngine(), _conditionEvaluatorMock.Object);
    }

    [TestMethod]
    public async Task GivenGridMode_WhenEvaluated_ThenDelegatesToGridEngine()
    {
        var config = CreateGridConfig();
        var context = CreateMarketContext();

        var result = await _sut.EvaluateAsync(context, config);

        result.SetupDetected.Should().BeTrue();
        result.Reason.Should().NotBeNullOrEmpty(); // Actual reason text depends on GridStrategyEngine logic with HTF data
        _conditionEvaluatorMock.Verify(
            e => e.Evaluate(It.IsAny<StrategyConfig>(), It.IsAny<MarketContext>()), Times.Never);
    }

    [TestMethod]
    public async Task GivenSignalMode_WhenRsiPasses_ThenSetupDetected()
    {
        var config = CreateSignalConfig();
        var context = CreateMarketContext();
        _conditionEvaluatorMock
            .Setup(e => e.Evaluate(config, context))
            .Returns(new ConditionEvaluationResult
            {
                SetupDetected = true,
                ConditionResults = [],
                OverallReason = "All 1 conditions passed."
            });

        var result = await _sut.EvaluateAsync(context, config);

        result.SetupDetected.Should().BeTrue();
        result.Reason.Should().Contain("passed");
    }

    [TestMethod]
    public async Task GivenSignalMode_WhenRsiFails_ThenNoSetup()
    {
        var config = CreateSignalConfig();
        var context = CreateMarketContext();
        _conditionEvaluatorMock
            .Setup(e => e.Evaluate(config, context))
            .Returns(new ConditionEvaluationResult
            {
                SetupDetected = false,
                ConditionResults = [],
                OverallReason = "1/1 conditions failed."
            });

        var result = await _sut.EvaluateAsync(context, config);

        result.SetupDetected.Should().BeFalse();
    }

    [TestMethod]
    public async Task GivenGridMode_WhenExistingGridTestScenario_ThenBehaviorPreserved()
    {
        // Verify the grid path is completely unchanged
        var config = CreateGridConfig();
        var contextNoHtf = new MarketContext
        {
            Symbol = "BTC-USD",
            TimestampUtc = 1000,
            CurrentCandle = CreateCandle(),
            LatestOneHourCandle = null,  // no HTF
            LatestFourHourCandle = null,
            Indicators = new IndicatorSnapshot()
        };

        var result = await _sut.EvaluateAsync(contextNoHtf, config);

        result.SetupDetected.Should().BeFalse();
        result.Reason.Should().Contain("Higher timeframe");
    }

    // Private static factory helpers
    private static StrategyConfig CreateGridConfig()
    {
        return new StrategyConfig
        {
            StrategyMode = StrategyMode.Grid,
            StrategyName = "Test Grid",
            Market = "BTC-USD",
            Grid = new GridConfig { Levels = 5, Spacing = 1.0m },
            Risk = new RiskConfig { PositionSizeValue = 100m }
        };
    }

    private static StrategyConfig CreateSignalConfig()
    {
        return new StrategyConfig
        {
            StrategyMode = StrategyMode.Signal,
            StrategyName = "Test Signal",
            Market = "BTC-USD",
            EntryLogic = EntryLogic.All,
            EntryConditions = new List<EntryConditionConfig>
            {
                new()
                {
                    Id = "rsi-1",
                    Enabled = true,
                    Type = EntryConditionType.Rsi,
                    Label = "RSI(14)",
                    Params = new RsiParams { Period = 14, Operator = "lt", Value = 40 }
                }
            },
            Risk = new RiskConfig { PositionSizeValue = 100m }
        };
    }

    private static MarketContext CreateMarketContext()
    {
        return new MarketContext
        {
            Symbol = "BTC-USD",
            TimestampUtc = 1000,
            CurrentCandle = CreateCandle(),
            LatestOneHourCandle = CreateCandle("1h"),
            LatestFourHourCandle = CreateCandle("4h"),
            Indicators = new IndicatorSnapshot()
        };
    }

    private static Candle CreateCandle(string interval = "15m")
    {
        return new Candle
        {
            Symbol = "BTC-USD",
            Interval = interval,
            Timestamp = 1000,
            Open = 100m, High = 105m, Low = 95m, Close = 102m, Volume = 1000m
        };
    }
}
```

##### Pattern References

- `tests/TradePilot.Application.Tests/Trading/Services/GridControllerTests.cs` — private static factory helpers, MSTest structure
- `tests/TradePilot.Application.Tests/Scheduling/StrategySchedulerTests.cs` — Moq mocking pattern for IStrategyEngine

---

### Task 3.5: Build and run all tests {#task-35-build-and-run-all-tests}

Build the full solution and run all test projects to verify everything works end-to-end.

- **Complexity**: Low
- **Risk Factors**: `RealBacktestRunnerTests` must still pass — they use the real `GridStrategyEngine` but now DI wires `CompositeStrategyEngine` as `IStrategyEngine`. Since `RealBacktestRunnerTests` constructs `GridStrategyEngine` directly (not via DI), they should be unaffected.
- **Files**: None (verification only)
- **Success**:
  - `dotnet build TradePilot.sln` succeeds
  - All test projects pass
  - Specifically verify `RealBacktestRunnerTests`, `GridControllerTests`, `StrategySchedulerTests` still pass
  - Verify `CrossFieldValidatorTests` pass with updated assertions

```bash
dotnet build TradePilot.sln
dotnet test TradePilot.sln
```

## Phase Success Criteria

- `CompositeStrategyEngine` created and routes grid → `GridStrategyEngine`, signal → `IConditionEvaluator`
- DI registrations updated: `IStrategyEngine` → `CompositeStrategyEngine`
- `SIGNAL_MODE_NOT_SUPPORTED` info message removed from `CrossFieldValidator`
- All new Phase 3 tests pass
- All existing tests pass (grid path fully preserved)
- Full solution builds and all test projects pass
