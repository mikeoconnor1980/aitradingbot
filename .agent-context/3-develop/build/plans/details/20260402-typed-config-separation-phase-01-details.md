<!-- markdownlint-disable-file -->

# Task Details: F0 — Typed Config & Execution Separation

## Phase 1: Domain Types & Model Migration

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — sealed classes, one class per file, PascalCase
- `.github/instructions/dotnet-architecture.instructions.md` — entity/value object placement, namespace conventions
- `.agent-context/0-knowledge/04-domain-model.md` — domain entities and core types
- `.agent-context/0-knowledge/06-project-structure.md` — solution layout, project names

## Design References

- `FeeModel` retains its `CalculateFee` and `ApplySlippage` methods — only the namespace changes
- `BacktestEntryModes` renamed to `EntryModes` (more generic; Domain shouldn't have "Backtest" prefix for a strategy concept)
- Old `Application.Backtesting.Models.GridStrategyConfig` kept temporarily — deleted in Phase 3
- New types use `sealed record` with `{ get; init; }` (simple data carriers per PBI)

---

### Task 1.1: Create IStrategyConfig marker interface {#task-11-create-istrategyconfig-marker-interface}

Create the `IStrategyConfig` marker interface in the Domain project. This enables polymorphic strategy config passing through the pipeline.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.Domain/Trading/IStrategyConfig.cs` — new file
- **Success**:
  - `IStrategyConfig` interface exists in `TradingApp.Domain.Trading` namespace
  - Solution builds
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Domain/Trading/IStrategyConfig.cs — new file
namespace TradingApp.Domain.Trading;

/// <summary>
/// Marker interface for strategy configuration types.
/// Each strategy type (grid, DCA, momentum, etc.) implements this interface.
/// </summary>
public interface IStrategyConfig;
```

##### Pattern References

No existing pattern — new interface. Follows the marker interface pattern.

---

### Task 1.2: Move OrderSide enum to Domain {#task-12-move-orderside-enum-to-domain}

Move `OrderSide` from `TradingApp.Application.Trading.Models` to `TradingApp.Domain.Enums`. This is required because `FeeModel.ApplySlippage` uses `OrderSide`, and `FeeModel` is moving to Domain.

- **Complexity**: Low
- **Risk Factors**: Many files reference `OrderSide` — using statement updates required
- **Files**:
  - `src/TradingApp.Domain/Enums/OrderSide.cs` — new file (moved from Application)
  - `src/TradingApp.Application/Trading/Models/OrderSide.cs` — delete
  - Multiple files across `src/` and `tests/` — update using statements
- **Success**:
  - `OrderSide` exists in `TradingApp.Domain.Enums`
  - Old file deleted
  - All references updated
  - Solution builds
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Domain/Enums/OrderSide.cs — new file
namespace TradingApp.Domain.Enums;

public enum OrderSide
{
    Buy,
    Sell
}
```

Replace `using TradingApp.Application.Trading.Models;` with `using TradingApp.Domain.Enums;` in all files that reference `OrderSide`. Key files to update:
- `src/TradingApp.Application/Trading/Models/` — any files importing from the old location
- `src/TradingApp.Application/Backtesting/Models/FeeModel.cs` (before it moves in Task 1.3)
- `src/TradingApp.Application/Trading/Services/*.cs`
- `src/TradingApp.Application/Backtesting/Services/*.cs`
- Test files referencing `OrderSide`

Use compiler errors to find all affected files.

##### Pattern References

Based on existing `src/TradingApp.Domain/Enums/BacktestStatus.cs` enumeration placement pattern.

---

### Task 1.3: Move FeeModel to Domain {#task-13-move-feemodel-to-domain}

Move `FeeModel` from `TradingApp.Application.Backtesting.Models` to `TradingApp.Domain.Trading`. Update its `OrderSide` import to reference the new Domain location.

- **Complexity**: Medium
- **Risk Factors**: FeeModel is used across Application and Api layers — many using statement updates
- **Files**:
  - `src/TradingApp.Domain/Trading/FeeModel.cs` — new file (moved from Application)
  - `src/TradingApp.Application/Backtesting/Models/FeeModel.cs` — delete
  - Multiple files across `src/` and `tests/` — update using statements
- **Success**:
  - `FeeModel` exists in `TradingApp.Domain.Trading` namespace
  - `ApplySlippage` references `TradingApp.Domain.Enums.OrderSide`
  - Old file deleted
  - All references updated
  - Solution builds
- **Dependencies**: Task 1.2 (OrderSide moved to Domain)

#### Implementation Details

```csharp
// src/TradingApp.Domain/Trading/FeeModel.cs — new file
using TradingApp.Domain.Enums;

namespace TradingApp.Domain.Trading;

public sealed class FeeModel
{
    public decimal MakerFeeRate { get; init; } = 0.0001m;
    public decimal TakerFeeRate { get; init; } = 0.00035m;
    public decimal SlippageRate { get; init; } = 0m;

    public static FeeModel Default { get; } = new();

    public decimal CalculateFee(decimal fillSize, decimal fillPrice, bool isMaker)
    {
        var rate = isMaker ? MakerFeeRate : TakerFeeRate;
        return fillSize * fillPrice * rate;
    }

    public decimal ApplySlippage(decimal price, OrderSide side)
    {
        return side == OrderSide.Buy
            ? price * (1 + SlippageRate)
            : price * (1 - SlippageRate);
    }
}
```

Replace `using TradingApp.Application.Backtesting.Models;` with `using TradingApp.Domain.Trading;` in files that use `FeeModel`. Key files:
- `src/TradingApp.Application/Backtesting/Models/BacktestConfig.cs`
- `src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs`
- `src/TradingApp.Api/Services/BacktestProcessorService.cs`
- `src/TradingApp.Application/Trading/Services/SimulatedExecutionEngine.cs` (if it exists)
- Test files

Use compiler errors to find all affected files.

##### Pattern References

Based on existing `src/TradingApp.Application/Backtesting/Models/FeeModel.cs` — identical implementation, only namespace and `OrderSide` import change.

---

### Task 1.4: Move BacktestEntryModes to Domain {#task-14-move-backtestentrymodes-to-domain}

Move `BacktestEntryModes` from `TradingApp.Application.Backtesting.Models` to `TradingApp.Domain.Trading` and rename to `EntryModes`. The new `GridStrategyConfig` record (Domain) needs this for its `EntryMode` default value.

- **Complexity**: Low
- **Risk Factors**: Name change from `BacktestEntryModes` to `EntryModes` — find-and-replace across codebase
- **Files**:
  - `src/TradingApp.Domain/Trading/EntryModes.cs` — new file
  - `src/TradingApp.Application/Backtesting/Models/BacktestEntryModes.cs` — delete
  - Multiple files across `src/` and `tests/` — update references
- **Success**:
  - `EntryModes` class exists in `TradingApp.Domain.Trading`
  - Old `BacktestEntryModes` deleted
  - All references updated to new name and namespace
  - Solution builds
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Domain/Trading/EntryModes.cs — new file
namespace TradingApp.Domain.Trading;

public static class EntryModes
{
    public const string AutoFromSignalCandle = "AutoFromSignalCandle";
    public const string InitialMarketThenGrid = "InitialMarketThenGrid";
    public const string WaitForLimitPrice = "WaitForLimitPrice";

    public static bool IsValid(string? value)
    {
        return string.Equals(value, AutoFromSignalCandle, StringComparison.Ordinal) ||
               string.Equals(value, InitialMarketThenGrid, StringComparison.Ordinal) ||
               string.Equals(value, WaitForLimitPrice, StringComparison.Ordinal);
    }
}
```

Update all references:
- `BacktestEntryModes.AutoFromSignalCandle` → `EntryModes.AutoFromSignalCandle`
- `BacktestEntryModes.WaitForLimitPrice` → `EntryModes.WaitForLimitPrice`
- `BacktestEntryModes.IsValid(...)` → `EntryModes.IsValid(...)`
- `using TradingApp.Application.Backtesting.Models;` → `using TradingApp.Domain.Trading;`

Key files:
- `src/TradingApp.Application/Trading/Services/GridController.cs` — references `BacktestEntryModes`
- `src/TradingApp.Api/Controllers/BacktestsController.cs` — uses `BacktestEntryModes.IsValid` in validation
- Test files

##### Pattern References

Based on existing `src/TradingApp.Application/Backtesting/Models/BacktestEntryModes.cs` — identical implementation, new namespace and class name.

---

### Task 1.5: Create ExecutionConfig record {#task-15-create-executionconfig-record}

Create the `ExecutionConfig` record in `TradingApp.Domain.Trading` containing `FeeModel` and `Leverage`.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.Domain/Trading/ExecutionConfig.cs` — new file
- **Success**:
  - `ExecutionConfig` record exists with `FeeModel` and `Leverage` properties
  - Solution builds
- **Dependencies**: Task 1.3 (FeeModel in Domain)

#### Implementation Details

```csharp
// src/TradingApp.Domain/Trading/ExecutionConfig.cs — new file
namespace TradingApp.Domain.Trading;

public sealed record ExecutionConfig
{
    public FeeModel FeeModel { get; init; } = FeeModel.Default;
    public decimal Leverage { get; init; } = 1m;
}
```

##### Pattern References

New type. Follows the sealed record pattern specified in the PBI for simple data carriers.

---

### Task 1.6: Create GridStrategyConfig record {#task-16-create-gridstrategyconfig-record}

Create the new `GridStrategyConfig` record in `TradingApp.Domain.Trading` implementing `IStrategyConfig`. Contains strategy parameters only plus `PositionSize`.

- **Complexity**: Medium
- **Risk Factors**: Two `GridStrategyConfig` types will coexist temporarily (Domain and Application) until Phase 3 deletes the old one
- **Files**:
  - `src/TradingApp.Domain/Trading/GridStrategyConfig.cs` — new file
- **Success**:
  - Record exists in `TradingApp.Domain.Trading`
  - Implements `IStrategyConfig`
  - Contains only strategy params + PositionSize (no MakerFee, TakerFee, Slippage, Leverage)
  - Solution builds
- **Dependencies**: Task 1.1 (IStrategyConfig), Task 1.4 (EntryModes)

#### Implementation Details

```csharp
// src/TradingApp.Domain/Trading/GridStrategyConfig.cs — new file
namespace TradingApp.Domain.Trading;

public sealed record GridStrategyConfig : IStrategyConfig
{
    public int GridLevels { get; init; }
    public decimal GridSpacing { get; init; }
    public decimal TakeProfitPercent { get; init; }
    public decimal StopLossPercent { get; init; }
    public decimal BreakdownThreshold { get; init; }
    public string EntryMode { get; init; } = EntryModes.AutoFromSignalCandle;
    public decimal? ManualAnchorPrice { get; init; }
    public decimal PositionSize { get; init; }
}
```

**Note**: The old `Application.Backtesting.Models.GridStrategyConfig` (class, mutable, with fee fields) remains temporarily. It is used by:
- `RunBacktestCommand` and handler (serialization to DB)
- `BacktestRunResponseMapper` (deserialization from DB)
- `BacktestsController` (mapping from request)
- `BacktestProcessorService.BuildConfig` (Phase 2 temporary bridge)

These are updated in Phase 3 when the old class is deleted.

##### Pattern References

New type. Based on the field list from existing `src/TradingApp.Application/Backtesting/Models/GridStrategyConfig.cs` with execution params removed.

---

### Task 1.7: Update using statements {#task-17-update-using-statements}

Fix all compilation errors from moved types. Update using statements for `OrderSide`, `FeeModel`, `BacktestEntryModes` → `EntryModes` across the entire solution.

- **Complexity**: Low
- **Risk Factors**: High file count but mechanical changes
- **Files**:
  - All `.cs` files referencing moved types across `src/` and `tests/`
- **Success**:
  - Solution builds successfully with zero errors
  - No remaining references to old namespaces for moved types
- **Dependencies**: Tasks 1.1–1.6

**Approach**: Build the solution after Tasks 1.1–1.6. Use compiler errors to systematically fix all broken references. Changes are purely using statement additions/removals and class name renames (`BacktestEntryModes` → `EntryModes`).

---

### Task 1.8: Run build and tests {#task-18-run-build-and-tests}

Verify the solution builds and all tests pass after the model migration.

- **Complexity**: Low
- **Risk Factors**: Potential missed using statement or rename
- **Files**: None (verification step)
- **Success**:
  - `dotnet build TradingApp.sln` succeeds with zero errors
  - `dotnet test TradingApp.sln` — all tests pass
- **Dependencies**: Task 1.7

## Phase Success Criteria

- `IStrategyConfig`, `GridStrategyConfig` (Domain), `ExecutionConfig` exist in `TradingApp.Domain.Trading`
- `FeeModel` lives in `TradingApp.Domain.Trading` with working `CalculateFee`/`ApplySlippage`
- `OrderSide` lives in `TradingApp.Domain.Enums`
- `EntryModes` lives in `TradingApp.Domain.Trading`
- Old `Application.Backtesting.Models.GridStrategyConfig` still exists (temporary)
- Solution builds and all tests pass — zero behavior change
