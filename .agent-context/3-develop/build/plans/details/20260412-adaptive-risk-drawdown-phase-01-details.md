<!-- markdownlint-disable-file -->

# Task Details: Adaptive Risk (Drawdown-Adjusted)

## Phase 1: Configuration & Domain Model

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — sealed classes, Guard.Against validation, IOptions pattern
- `.github/instructions/testing.instructions.md` — MSTest, Moq, FluentAssertions, Given_When_Then naming
- `.github/instructions/dotnet-architecture.instructions.md` — layered architecture, EF Core config
- `.agent-context/0-knowledge/33-risk-management-and-trade-sizing.md` — drawdown tier specification, default values

### Task 1.1: Create DrawdownTier record {#task-11-create-drawdowntier-record}

Create a new `DrawdownTier` record to represent a single drawdown threshold and its associated scaling factor.

- **Complexity**: Low
- **Risk Factors**: None — simple record type
- **Files**:
  - `src/TradingApp.Application/StrategyAuthoring/Models/DrawdownTier.cs` — new file
- **Success**:
  - `DrawdownTier` record exists with `ThresholdPercent` and `ScalingFactor` properties
  - Record is `sealed`
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Application/StrategyAuthoring/Models/DrawdownTier.cs — new file
namespace TradingApp.Application.StrategyAuthoring.Models;

/// <summary>
/// A single drawdown tier defining a threshold and its risk scaling factor.
/// </summary>
public sealed record DrawdownTier
{
    /// <summary>Drawdown percentage threshold (e.g. 5 = 5% drawdown from HWM).</summary>
    public decimal ThresholdPercent { get; init; }

    /// <summary>Scaling factor applied to base risk (0.0–1.0). 0.0 = halt all entries.</summary>
    public decimal ScalingFactor { get; init; }
}
```

##### Pattern References

- `src/TradingApp.Application/StrategyAuthoring/Models/RiskLimitsConfig.cs` — existing sealed record pattern in the same folder

---

### Task 1.2: Add DrawdownTiers to RiskLimitsConfig {#task-12-add-drawdowntiers-to-risklimitsconfig}

Add the `DrawdownTiers` collection to the existing `RiskLimitsConfig` record with sensible defaults.

- **Complexity**: Low
- **Risk Factors**: Config binding for list of objects — verify appsettings deserialization
- **Files**:
  - `src/TradingApp.Application/StrategyAuthoring/Models/RiskLimitsConfig.cs` — modification
- **Success**:
  - `DrawdownTiers` property exists on `RiskLimitsConfig`
  - Default value provides 4 tiers matching the specification
- **Dependencies**: Task 1.1

#### Implementation Details

```csharp
// src/TradingApp.Application/StrategyAuthoring/Models/RiskLimitsConfig.cs — modification
public sealed record RiskLimitsConfig
{
    public const string SectionName = "RiskLimits";

    // ... existing properties ...

    public IReadOnlyList<DrawdownTier> DrawdownTiers { get; init; } = new[]
    {
        new DrawdownTier { ThresholdPercent = 5m, ScalingFactor = 0.75m },
        new DrawdownTier { ThresholdPercent = 10m, ScalingFactor = 0.50m },
        new DrawdownTier { ThresholdPercent = 15m, ScalingFactor = 0.0m },
    };
}
```

Note: The 0–5% range has implicit scaling factor 1.0 (no tier reached). The first tier at 5% starts the scaling. The 15% tier with 0.0 is the halt tier.

##### Pattern References

- `src/TradingApp.Application/StrategyAuthoring/Models/RiskLimitsConfig.cs` — existing record structure

---

### Task 1.3: Create RiskLimitsConfigValidator {#task-13-create-risklimitsconfigvalidator}

Create an `IValidateOptions<RiskLimitsConfig>` validator to enforce drawdown tier constraints. Register it in DI.

- **Complexity**: Medium
- **Risk Factors**: Validation must run at startup — incorrect validation could prevent app start
- **Files**:
  - `src/TradingApp.Application/StrategyAuthoring/Validation/RiskLimitsConfigValidator.cs` — new file
  - `src/TradingApp.Api/Program.cs` — add validator registration
  - `src/TradingApp.Worker/Program.cs` — add validator registration
- **Success**:
  - Tiers validated for ascending threshold order
  - Tiers validated for descending scaling factor order
  - At least one tier required if list is non-empty
  - Scaling factors validated as 0.0–1.0 range
  - Threshold percentages validated as > 0
  - Validator registered in both Api and Worker DI
- **Dependencies**: Task 1.2

#### Implementation Details

```csharp
// src/TradingApp.Application/StrategyAuthoring/Validation/RiskLimitsConfigValidator.cs — new file
using Microsoft.Extensions.Options;
using TradingApp.Application.StrategyAuthoring.Models;

namespace TradingApp.Application.StrategyAuthoring.Validation;

public sealed class RiskLimitsConfigValidator : IValidateOptions<RiskLimitsConfig>
{
    public ValidateOptionsResult Validate(string? name, RiskLimitsConfig options)
    {
        var tiers = options.DrawdownTiers;
        if (tiers is null || tiers.Count == 0)
            return ValidateOptionsResult.Success;

        for (var i = 0; i < tiers.Count; i++)
        {
            if (tiers[i].ThresholdPercent <= 0)
                return ValidateOptionsResult.Fail($"DrawdownTiers[{i}].ThresholdPercent must be greater than 0.");

            if (tiers[i].ScalingFactor < 0m || tiers[i].ScalingFactor > 1m)
                return ValidateOptionsResult.Fail($"DrawdownTiers[{i}].ScalingFactor must be between 0.0 and 1.0.");

            if (i > 0 && tiers[i].ThresholdPercent <= tiers[i - 1].ThresholdPercent)
                return ValidateOptionsResult.Fail("DrawdownTiers must be in ascending ThresholdPercent order.");

            if (i > 0 && tiers[i].ScalingFactor >= tiers[i - 1].ScalingFactor)
                return ValidateOptionsResult.Fail("DrawdownTiers must be in descending ScalingFactor order.");
        }

        return ValidateOptionsResult.Success;
    }
}
```

```csharp
// src/TradingApp.Api/Program.cs — add after existing RiskLimitsConfig registration
builder.Services.AddSingleton<IValidateOptions<RiskLimitsConfig>, RiskLimitsConfigValidator>();
```

```csharp
// src/TradingApp.Worker/Program.cs — add .ValidateDataAnnotations().ValidateOnStart() to existing
// RiskLimitsConfig registration (currently only has .Bind()), then add validator:
builder.Services.AddOptions<RiskLimitsConfig>()
    .Bind(builder.Configuration.GetSection(RiskLimitsConfig.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<RiskLimitsConfig>, RiskLimitsConfigValidator>();
```

##### Pattern References

- `src/TradingApp.Api/Program.cs` — existing `ValidateDataAnnotations().ValidateOnStart()` pattern
- `src/TradingApp.Application/StrategyAuthoring/Validation/BusinessRuleValidator.cs` — existing validation class in same folder

---

### Task 1.4: Add HighWaterMarkUsd to Strategy entity and database {#task-14-add-highwatermarkusd-to-strategy-entity-and-database}

Add a nullable `HighWaterMarkUsd` column to the `Strategy` entity for persisting the equity high-water mark across restarts.

- **Complexity**: Medium
- **Risk Factors**: SQLite dev mode uses `EnsureCreated` — new column on existing table requires special handling. SQL Server uses EF migrations.
- **Files**:
  - `src/TradingApp.Domain/Entities/Strategy.cs` — add property
  - `src/TradingApp.Persistence/TradingAppDbContext.cs` — add fluent config
  - `src/TradingApp.Persistence/Migrations/` — new migration file
- **Success**:
  - `Strategy.HighWaterMarkUsd` nullable decimal property exists
  - EF Core mapping uses `HasConversion<double?>()` for SQLite compatibility
  - SQL Server migration adds the column
  - Existing strategies get `NULL` as default (no HWM tracked yet)
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Domain/Entities/Strategy.cs — add property (matches private-set pattern)
public decimal? HighWaterMarkUsd { get; private set; }

// Add public method to update HWM (follows entity encapsulation pattern)
public void UpdateHighWaterMark(decimal highWaterMark)
{
    HighWaterMarkUsd = highWaterMark;
    UpdatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
```

```csharp
// src/TradingApp.Persistence/TradingAppDbContext.cs — add in OnModelCreating Strategy block
entity.Property(s => s.HighWaterMarkUsd)
    .HasConversion<double?>()
    .HasColumnName("HighWaterMarkUsd");
```

Migration command:
```bash
dotnet ef migrations add AddHighWaterMarkToStrategy --project src/TradingApp.Persistence --startup-project src/TradingApp.Api
```

##### Pattern References

- `src/TradingApp.Persistence/TradingAppDbContext.cs` — existing `HasConversion<double>()` pattern for decimals
- `src/TradingApp.Persistence/Migrations/` — existing migration examples with `AddColumn<double>`

---

### Task 1.5: Update appsettings.json with default DrawdownTiers {#task-15-update-appsettingsjson-with-default-drawdowntiers}

Add default drawdown tier configuration to both Api and Worker appsettings files.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.Api/appsettings.json` — add DrawdownTiers to RiskLimits section
  - `src/TradingApp.Worker/appsettings.json` — add DrawdownTiers to RiskLimits section
- **Success**:
  - Both appsettings files contain DrawdownTiers array matching default values
- **Dependencies**: Task 1.2

#### Implementation Details

```json
"RiskLimits": {
  "MaxDailyLossUsd": 500,
  "MaxOpenOrders": 20,
  "MaxOrderSizeUsd": 10000,
  "CircuitBreakerCooldownMinutes": 60,
  "MaxPortfolioHeatPercent": 6,
  "DrawdownTiers": [
    { "ThresholdPercent": 5, "ScalingFactor": 0.75 },
    { "ThresholdPercent": 10, "ScalingFactor": 0.50 },
    { "ThresholdPercent": 15, "ScalingFactor": 0.0 }
  ]
}
```

##### Pattern References

- `src/TradingApp.Worker/appsettings.json` — existing RiskLimits block
- `src/TradingApp.Api/appsettings.json` — existing RiskLimits block

---

### Task 1.6: Unit tests for Phase 1 {#task-16-unit-tests-for-phase-1}

Write unit tests for `DrawdownTier`, `RiskLimitsConfig` defaults, and `RiskLimitsConfigValidator`.

- **Complexity**: Medium
- **Risk Factors**: None
- **Files**:
  - `tests/TradingApp.Application.Tests/StrategyAuthoring/Validation/RiskLimitsConfigValidatorTests.cs` — new file
- **Success**:
  - Tests verify validator accepts valid tier configurations
  - Tests verify validator rejects out-of-order thresholds
  - Tests verify validator rejects out-of-order scaling factors
  - Tests verify validator rejects out-of-range scaling factors
  - Tests verify validator accepts empty tier list
  - Tests verify default tiers on `RiskLimitsConfig` are valid
  - All tests pass
- **Dependencies**: Tasks 1.1–1.3

#### Implementation Details

```csharp
// tests/TradingApp.Application.Tests/StrategyAuthoring/Validation/RiskLimitsConfigValidatorTests.cs — new file
using FluentAssertions;
using Microsoft.Extensions.Options;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.StrategyAuthoring.Validation;

namespace TradingApp.Application.Tests.StrategyAuthoring.Validation;

[TestClass]
public sealed class RiskLimitsConfigValidatorTests
{
    private RiskLimitsConfigValidator _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _sut = new RiskLimitsConfigValidator();
    }

    [TestMethod]
    public void GivenDefaultTiers_WhenValidated_ThenSucceeds()
    {
        var config = new RiskLimitsConfig();
        var result = _sut.Validate(null, config);
        result.Succeeded.Should().BeTrue();
    }

    [TestMethod]
    public void GivenEmptyTiers_WhenValidated_ThenSucceeds()
    {
        var config = new RiskLimitsConfig { DrawdownTiers = [] };
        var result = _sut.Validate(null, config);
        result.Succeeded.Should().BeTrue();
    }

    [TestMethod]
    public void GivenThresholdsNotAscending_WhenValidated_ThenFails()
    {
        var config = new RiskLimitsConfig
        {
            DrawdownTiers =
            [
                new DrawdownTier { ThresholdPercent = 10m, ScalingFactor = 0.75m },
                new DrawdownTier { ThresholdPercent = 5m, ScalingFactor = 0.50m },
            ]
        };
        var result = _sut.Validate(null, config);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("ascending");
    }

    [TestMethod]
    public void GivenScalingFactorAboveOne_WhenValidated_ThenFails()
    {
        var config = new RiskLimitsConfig
        {
            DrawdownTiers = [new DrawdownTier { ThresholdPercent = 5m, ScalingFactor = 1.5m }]
        };
        var result = _sut.Validate(null, config);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("between 0.0 and 1.0");
    }

    [TestMethod]
    public void GivenScalingFactorsNotDescending_WhenValidated_ThenFails()
    {
        var config = new RiskLimitsConfig
        {
            DrawdownTiers =
            [
                new DrawdownTier { ThresholdPercent = 5m, ScalingFactor = 0.50m },
                new DrawdownTier { ThresholdPercent = 10m, ScalingFactor = 0.75m },
            ]
        };
        var result = _sut.Validate(null, config);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("descending");
    }

    [TestMethod]
    public void GivenNegativeThreshold_WhenValidated_ThenFails()
    {
        var config = new RiskLimitsConfig
        {
            DrawdownTiers = [new DrawdownTier { ThresholdPercent = -1m, ScalingFactor = 0.75m }]
        };
        var result = _sut.Validate(null, config);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("greater than 0");
    }
}
```

##### Pattern References

- `tests/TradingApp.Application.Tests/Trading/Services/LiveRiskEngineTests.cs` — MSTest class structure, Setup pattern

---

### Task 1.7: Run architecture tests {#task-17-run-architecture-tests}

Build the solution and run all tests to verify no regressions.

- **Complexity**: Low
- **Risk Factors**: Existing tests may fail if EF model changes aren't compatible
- **Files**: None (verification only)
- **Success**:
  - `dotnet build TradingApp.sln` succeeds
  - `dotnet test TradingApp.sln` — all existing tests pass
  - New validation tests pass
- **Dependencies**: Tasks 1.1–1.6

## Phase Success Criteria

- `DrawdownTier` record exists in `TradingApp.Application/StrategyAuthoring/Models/`
- `RiskLimitsConfig` has `DrawdownTiers` property with sensible defaults
- `RiskLimitsConfigValidator` validates tier ordering and ranges
- `Strategy.HighWaterMarkUsd` nullable column exists with EF mapping and migration
- Both appsettings files include default DrawdownTiers
- All validation tests pass
- Solution builds and all existing tests pass
