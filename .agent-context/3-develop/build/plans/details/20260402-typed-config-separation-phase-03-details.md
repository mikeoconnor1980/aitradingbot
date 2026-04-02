<!-- markdownlint-disable-file -->

# Task Details: F0 — Typed Config & Execution Separation

## Phase 3: Entity, Command, Mapper & API Contract

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — sealed classes, factory methods, naming
- `.github/instructions/api-controllers.instructions.md` — controller patterns, Data Annotations, exception handling
- `.github/instructions/testing.instructions.md` — MSTest, Moq, FluentAssertions, Given_When_Then naming
- `.github/instructions/dotnet-architecture.instructions.md` — entity configuration, migrations
- `.agent-context/0-knowledge/04-domain-model.md` — BacktestRun entity
- `.agent-context/0-knowledge/18-backtesting-architecture.md` — BacktestConfig, BacktestRunner, replay engine

## Design References

- Old `Application.Backtesting.Models.GridStrategyConfig` is deleted — all references switch to `Domain.Trading.GridStrategyConfig`
- `BacktestRun` entity gets `ExecutionConfigJson` property alongside existing `StrategyConfigJson`
- `StrategyConfigJson` column narrowed to strategy params only; `ExecutionConfigJson` stores fee + leverage
- `RunBacktestRequest` splits `GridStrategyConfigRequest` into `StrategyConfigRequest` + `ExecutionConfigRequest`
- `BacktestRunResponse` exposes separate `StrategyConfig` (Domain record) + `ExecutionConfig` (Domain record)
- EF migration drops old data (clean out per PBI decision) — migration is additive (new column) but old records should be cleaned first
- `BacktestProcessorService.BuildConfig` gets its final form reading from two JSON columns

---

### Task 3.1: Delete old Application.Backtesting.Models.GridStrategyConfig {#task-31-delete-old-gridstrategyconfig}

Remove the old combined `GridStrategyConfig` class from the Application layer. Fix all remaining references to use `Domain.Trading.GridStrategyConfig`.

- **Complexity**: Low
- **Risk Factors**: Must ensure no file still imports the old class
- **Files**:
  - `src/TradingApp.Application/Backtesting/Models/GridStrategyConfig.cs` — delete
  - Any remaining files with `using TradingApp.Application.Backtesting.Models;` that referenced the old class — update imports
- **Success**:
  - Old `GridStrategyConfig` class no longer exists
  - All references point to `TradingApp.Domain.Trading.GridStrategyConfig`
  - Solution compiles (temporarily — some files will break until subsequent tasks complete)
- **Dependencies**: Phase 2 complete

**Note**: After deleting this file, `BacktestProcessorService.BuildConfig` (which used it as a bridge), `RunBacktestCommand`, `BacktestRunResponseMapper`, and `BacktestsController` will have compilation errors. These are fixed in Tasks 3.2–3.8.

---

### Task 3.2: Update RunBacktestRequest with separate sections {#task-32-update-runbacktestrequest}

Split `GridStrategyConfigRequest` into `StrategyConfigRequest` + `ExecutionConfigRequest` in the API request model.

- **Complexity**: Medium
- **Risk Factors**: API contract breaking change; validation annotations must cover both sub-objects
- **Files**:
  - `src/TradingApp.Api/Models/RunBacktestRequest.cs` — rewrite with split DTOs
- **Success**:
  - `RunBacktestRequest` has `StrategyConfig` and `ExecutionConfig` nested objects
  - Data Annotations on both sub-objects
  - Old `GridStrategyConfigRequest` removed
- **Dependencies**: Task 3.1

#### Implementation Details

```csharp
// src/TradingApp.Api/Models/RunBacktestRequest.cs — full rewrite
using System.ComponentModel.DataAnnotations;

namespace TradingApp.Api.Models;

public sealed class RunBacktestRequest
{
    [Required]
    public string Symbol { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public string[] Intervals { get; set; } = [];

    [Required]
    public DateTime? StartDate { get; set; }

    [Required]
    public DateTime? EndDate { get; set; }

    [Required]
    [Range(0.01, (double)decimal.MaxValue)]
    public decimal? InitialCapital { get; set; }

    [Required]
    public StrategyConfigRequest StrategyConfig { get; set; } = null!;

    [Required]
    public ExecutionConfigRequest ExecutionConfig { get; set; } = null!;

    public bool EnableAuditLog { get; set; } = true;
}

public sealed class StrategyConfigRequest
{
    [Range(1, int.MaxValue)]
    public int GridLevels { get; set; }

    public string EntryMode { get; set; } = "AutoFromSignalCandle";

    public decimal? ManualAnchorPrice { get; set; }

    [Range(0.01, (double)decimal.MaxValue)]
    public decimal GridSpacing { get; set; }

    [Range(0, (double)decimal.MaxValue)]
    public decimal TakeProfitPercent { get; set; }

    [Range(0, (double)decimal.MaxValue)]
    public decimal BreakdownThreshold { get; set; }

    [Range(0, (double)decimal.MaxValue)]
    public decimal StopLossPercent { get; set; }

    [Range(0.01, (double)decimal.MaxValue)]
    public decimal PositionSize { get; set; }
}

public sealed class ExecutionConfigRequest
{
    [Range(0, 1)]
    public decimal MakerFee { get; set; }

    [Range(0, 1)]
    public decimal TakerFee { get; set; }

    [Range(0, 1)]
    public decimal Slippage { get; set; }

    [Range(1, (double)decimal.MaxValue)]
    public decimal Leverage { get; set; } = 1m;
}
```

##### Pattern References

Based on existing `src/TradingApp.Api/Models/RunBacktestRequest.cs` — same validation approach (Data Annotations), restructured into nested sections.

---

### Task 3.3: Update BacktestsController mapping and validation {#task-33-update-backtestscontroller}

Update the controller to map from split request DTOs to `Domain.GridStrategyConfig` + `ExecutionConfig`. Update inline `ValidateRequest()`.

- **Complexity**: Medium
- **Risk Factors**: Must correctly map DTO fields to domain types; validation logic in `ValidateRequest()` must match new field paths
- **Files**:
  - `src/TradingApp.Api/Controllers/BacktestsController.cs` — modify `RunAsync` and `ValidateRequest`
- **Success**:
  - Controller maps `StrategyConfigRequest` → `Domain.GridStrategyConfig`
  - Controller maps `ExecutionConfigRequest` → `ExecutionConfig` with `FeeModel`
  - `RunBacktestCommand` constructed with both typed configs
  - `ValidateRequest` updated for new request shape
- **Dependencies**: Task 3.2

#### Implementation Details

```csharp
// src/TradingApp.Api/Controllers/BacktestsController.cs — modification
using TradingApp.Domain.Trading;

// In RunAsync method, replace the GridStrategyConfig mapping:
var strategyConfig = new GridStrategyConfig
{
    GridLevels = request.StrategyConfig.GridLevels,
    EntryMode = request.StrategyConfig.EntryMode,
    ManualAnchorPrice = request.StrategyConfig.ManualAnchorPrice,
    GridSpacing = request.StrategyConfig.GridSpacing,
    TakeProfitPercent = request.StrategyConfig.TakeProfitPercent,
    BreakdownThreshold = request.StrategyConfig.BreakdownThreshold,
    StopLossPercent = request.StrategyConfig.StopLossPercent,
    PositionSize = request.StrategyConfig.PositionSize,
};

var executionConfig = new ExecutionConfig
{
    FeeModel = new FeeModel
    {
        MakerFeeRate = request.ExecutionConfig.MakerFee,
        TakerFeeRate = request.ExecutionConfig.TakerFee,
        SlippageRate = request.ExecutionConfig.Slippage,
    },
    Leverage = request.ExecutionConfig.Leverage,
};

// Update RunBacktestCommand construction (see Task 3.4 for command shape):
var result = await Mediator.Send(new RunBacktestCommand(
    request.Symbol,
    request.Intervals,
    request.StartDate!.Value,
    request.EndDate!.Value,
    strategyConfig,
    executionConfig,
    request.InitialCapital!.Value,
    request.EnableAuditLog), cancellationToken);

// Update ValidateRequest to reference new request shape:
// - request.StrategyConfig.EntryMode instead of request.StrategyConfig.EntryMode
// - request.StrategyConfig.ManualAnchorPrice instead of request.StrategyConfig.ManualAnchorPrice
// (field paths may stay the same but the DTO type changes)
```

Update the `ValidateRequest` private method:
- Replace `request.StrategyConfig.EntryMode` validation using `EntryModes.IsValid()` (updated import)
- Replace `request.StrategyConfig.ManualAnchorPrice` check for `WaitForLimitPrice` mode

##### Pattern References

Based on existing `src/TradingApp.Api/Controllers/BacktestsController.cs` lines 29–54 and validation method.

---

### Task 3.4: Update RunBacktestCommand and handler {#task-34-update-runbacktestcommand-and-handler}

Update `RunBacktestCommand` to carry both `GridStrategyConfig` and `ExecutionConfig`. Update handler to serialize both and store separately.

- **Complexity**: Medium
- **Risk Factors**: Serialization must produce valid JSON for both configs; handler calls `BacktestRun.CreateQueued` which needs updating (Task 3.5)
- **Files**:
  - `src/TradingApp.Application/Backtesting/RunBacktestCommand.cs` — modify record and handler
- **Success**:
  - Command carries `GridStrategyConfig StrategyConfig` (Domain) + `ExecutionConfig ExecutionConfig`
  - Handler serializes both configs separately
  - Handler passes both JSON strings to `BacktestRun.CreateQueued`
- **Dependencies**: Tasks 3.1, 3.3, 3.6

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/RunBacktestCommand.cs — modification
using TradingApp.Domain.Trading;

public sealed record RunBacktestCommand(
    string Symbol,
    string[] Intervals,
    DateTime StartDate,
    DateTime EndDate,
    GridStrategyConfig StrategyConfig,     // was: GridStrategyConfig (Application)
    ExecutionConfig ExecutionConfig,        // new
    decimal InitialCapital,
    bool EnableAuditLog) : Command<BacktestRunResponse>;

// In handler:
public override async Task<BacktestRunResponse> Handle(
    RunBacktestCommand request,
    CancellationToken cancellationToken)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(request.Symbol);
    ArgumentNullException.ThrowIfNull(request.Intervals);
    ArgumentNullException.ThrowIfNull(request.StrategyConfig);
    ArgumentNullException.ThrowIfNull(request.ExecutionConfig);

    var startDateUtc = request.StartDate.Kind == DateTimeKind.Unspecified
        ? DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc)
        : request.StartDate.ToUniversalTime();
    var endDateUtc = request.EndDate.Kind == DateTimeKind.Unspecified
        ? DateTime.SpecifyKind(request.EndDate, DateTimeKind.Utc)
        : request.EndDate.ToUniversalTime();

    var strategyConfigJson = BacktestRunResponseMapper.SerializeStrategyConfig(request.StrategyConfig);
    var executionConfigJson = BacktestRunResponseMapper.SerializeExecutionConfig(request.ExecutionConfig);

    var backtestRun = BacktestRun.CreateQueued(
        symbol: request.Symbol,
        intervalsJson: JsonSerializer.Serialize(request.Intervals),
        startDateUtc: new DateTimeOffset(startDateUtc).ToUnixTimeMilliseconds(),
        endDateUtc: new DateTimeOffset(endDateUtc).ToUnixTimeMilliseconds(),
        strategyConfigJson: strategyConfigJson,
        executionConfigJson: executionConfigJson,  // new parameter
        initialCapital: request.InitialCapital,
        auditLogEnabled: request.EnableAuditLog);

    await _backtestRunRepository.AddAsync(backtestRun, cancellationToken);
    await _backtestJobQueue.EnqueueAsync(new BacktestJob(backtestRun.Id), cancellationToken);

    return BacktestRunResponseMapper.ToResponse(backtestRun);
}
```

##### Pattern References

Based on existing `src/TradingApp.Application/Backtesting/RunBacktestCommand.cs`.

---

### Task 3.5: Update BacktestRun entity {#task-35-update-backtestrun-entity}

Add `ExecutionConfigJson` property to `BacktestRun`. Update `CreateQueued` and `Create` factories to accept both JSON strings. `StrategyConfigJson` now stores strategy-only params.

- **Complexity**: Medium
- **Risk Factors**: Entity change requires migration (Task 3.9); factories used by command handler and tests
- **Files**:
  - `src/TradingApp.Domain/Entities/BacktestRun.cs` — add property, update factory methods
- **Success**:
  - `BacktestRun` has both `StrategyConfigJson` and `ExecutionConfigJson` properties
  - `CreateQueued` accepts `executionConfigJson` parameter
  - `Create` (if it exists) also updated
- **Dependencies**: None (entity is independent)

#### Implementation Details

```csharp
// src/TradingApp.Domain/Entities/BacktestRun.cs — modification

// Add property after StrategyConfigJson:
public string StrategyConfigJson { get; private set; } = string.Empty;
public string ExecutionConfigJson { get; private set; } = string.Empty;  // new

// Update CreateQueued factory:
public static BacktestRun CreateQueued(
    string symbol,
    string intervalsJson,
    long startDateUtc,
    long endDateUtc,
    string strategyConfigJson,
    string executionConfigJson,       // new parameter
    decimal initialCapital,
    bool auditLogEnabled = true)
{
    return new BacktestRun
    {
        Id = Guid.NewGuid(),
        Symbol = symbol,
        IntervalsJson = intervalsJson,
        StartDateUtc = startDateUtc,
        EndDateUtc = endDateUtc,
        StrategyConfigJson = strategyConfigJson,
        ExecutionConfigJson = executionConfigJson,   // new
        InitialCapital = initialCapital,
        Status = BacktestStatus.Queued,
        AuditLogEnabled = auditLogEnabled,
        CreatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
    };
}
```

Also update the `Create` factory method if it exists (check for a second static factory that creates completed runs).

##### Pattern References

Based on existing `src/TradingApp.Domain/Entities/BacktestRun.cs` — same pattern, adding one property and one factory parameter.

---

### Task 3.6: Update BacktestRunResponseMapper {#task-36-update-backtestrunresponsemapper}

Update the mapper to serialize/deserialize both strategy and execution configs. Add `SerializeExecutionConfig` method. Update `ToResponse` to deserialize from two separate columns.

- **Complexity**: Medium
- **Risk Factors**: Deserialization must use correct JSON options; response must expose both typed configs
- **Files**:
  - `src/TradingApp.Application/Backtesting/BacktestRunResponseMapper.cs` — modify `ToResponse`, add serialization methods
- **Success**:
  - `SerializeStrategyConfig` accepts `Domain.GridStrategyConfig`
  - `SerializeExecutionConfig` method added for `ExecutionConfig`
  - `ToResponse` deserializes both columns into typed objects
  - Response includes both `StrategyConfig` and `ExecutionConfig`
- **Dependencies**: Tasks 3.5, 3.7

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/BacktestRunResponseMapper.cs — modification
using TradingApp.Domain.Trading;

// Update SerializeStrategyConfig to accept Domain type:
public static string SerializeStrategyConfig(GridStrategyConfig strategyConfig)
{
    ArgumentNullException.ThrowIfNull(strategyConfig);
    return JsonSerializer.Serialize(strategyConfig, JsonOptions);
}

// Add new method:
public static string SerializeExecutionConfig(ExecutionConfig executionConfig)
{
    ArgumentNullException.ThrowIfNull(executionConfig);
    return JsonSerializer.Serialize(executionConfig, JsonOptions);
}

// Update ToResponse:
public static BacktestRunResponse ToResponse(BacktestRun entity)
{
    ArgumentNullException.ThrowIfNull(entity);

    var strategyConfig = JsonSerializer.Deserialize<GridStrategyConfig>(
        entity.StrategyConfigJson, JsonOptions)
        ?? throw new JsonException("Stored strategy config is invalid.");

    var executionConfig = JsonSerializer.Deserialize<ExecutionConfig>(
        entity.ExecutionConfigJson, JsonOptions)
        ?? throw new JsonException("Stored execution config is invalid.");

    var trades = JsonSerializer.Deserialize<List<BacktestTrade>>(
        entity.TradesJson, JsonOptions) ?? [];
    // ... existing deserialization for equity, intervals ...

    return new BacktestRunResponse
    {
        // ... existing properties ...
        StrategyConfig = strategyConfig,      // Domain.GridStrategyConfig
        ExecutionConfig = executionConfig,     // Domain.ExecutionConfig
        // ... rest unchanged ...
    };
}
```

##### Pattern References

Based on existing `src/TradingApp.Application/Backtesting/BacktestRunResponseMapper.cs`.

---

### Task 3.7: Update BacktestRunResponse {#task-37-update-backtestrunresponse}

Add `ExecutionConfig` property to `BacktestRunResponse`. Update `StrategyConfig` type to `Domain.GridStrategyConfig`.

- **Complexity**: Low
- **Risk Factors**: Frontend must match this new response shape (Phase 4)
- **Files**:
  - `src/TradingApp.Application/Backtesting/Models/BacktestRunResponse.cs` — add property, update type
- **Success**:
  - Response has `GridStrategyConfig StrategyConfig` (Domain) + `ExecutionConfig ExecutionConfig`
- **Dependencies**: Phase 1 (Domain types exist)

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/Models/BacktestRunResponse.cs — modification
using TradingApp.Domain.Trading;

public sealed class BacktestRunResponse
{
    // ... existing properties ...
    public required GridStrategyConfig StrategyConfig { get; init; }    // type unchanged (now Domain type)
    public required ExecutionConfig ExecutionConfig { get; init; }      // new
    // ... rest unchanged ...
}
```

##### Pattern References

Based on existing `src/TradingApp.Application/Backtesting/Models/BacktestRunResponse.cs`.

---

### Task 3.8: Update BacktestProcessorService.BuildConfig (final form) {#task-38-update-backtestprocessorservice-buildconfig-final}

Replace the Phase 2 temporary bridge with the final implementation that reads from two separate JSON columns.

- **Complexity**: Medium
- **Risk Factors**: Must correctly deserialize from new column layout; no more `JsonSerializer.Deserialize<GridStrategyConfig>` from old combined JSON
- **Files**:
  - `src/TradingApp.Api/Services/BacktestProcessorService.cs` — modify `BuildConfig`
- **Success**:
  - `BuildConfig` deserializes `run.StrategyConfigJson` → `Domain.GridStrategyConfig`
  - `BuildConfig` deserializes `run.ExecutionConfigJson` → `ExecutionConfig`
  - No reference to old `Application.GridStrategyConfig` (it's deleted)
  - No `JsonSerializer.Deserialize<GridStrategyConfig>` for the old combined format
- **Dependencies**: Tasks 3.1, 3.5

#### Implementation Details

```csharp
// src/TradingApp.Api/Services/BacktestProcessorService.cs — modification
using TradingApp.Domain.Trading;
// Remove: using AppGridConfig = TradingApp.Application.Backtesting.Models.GridStrategyConfig;

private static BacktestConfig BuildConfig(BacktestRun run)
{
    var strategyConfig = JsonSerializer.Deserialize<GridStrategyConfig>(
        run.StrategyConfigJson,
        JsonOptions)
        ?? throw new InvalidOperationException("Failed to deserialize strategy config.");

    var executionConfig = JsonSerializer.Deserialize<ExecutionConfig>(
        run.ExecutionConfigJson,
        JsonOptions)
        ?? throw new InvalidOperationException("Failed to deserialize execution config.");

    return new BacktestConfig
    {
        Symbol = run.Symbol,
        Intervals = JsonSerializer.Deserialize<string[]>(
            run.IntervalsJson,
            JsonOptions) ?? [],
        StartDateUtc = run.StartDateUtc,
        EndDateUtc = run.EndDateUtc,
        InitialCapital = run.InitialCapital,
        Strategy = strategyConfig,
        Execution = executionConfig,
        EnableAuditLog = run.AuditLogEnabled,
    };
}
```

##### Pattern References

Based on existing `src/TradingApp.Api/Services/BacktestProcessorService.cs` — simplified (no more FeeModel extraction from strategy config JSON).

---

### Task 3.9: Update DbContext and add EF migration {#task-39-update-dbcontext-and-migration}

Add `ExecutionConfigJson` column configuration to `OnModelCreating`. Add EF Core migration.

- **Complexity**: Medium
- **Risk Factors**: Must clean out old backtest records before running migration (old records have combined JSON that won't match new schema expectations); migration is additive (new column) so technically non-destructive
- **Files**:
  - `src/TradingApp.Persistence/TradingAppDbContext.cs` — add column configuration
  - `src/TradingApp.Persistence/Migrations/` — new migration file (auto-generated)
- **Success**:
  - `ExecutionConfigJson` mapped as required TEXT column
  - Migration adds the column
  - `dotnet ef database update` succeeds
- **Dependencies**: Task 3.5

#### Implementation Details

```csharp
// src/TradingApp.Persistence/TradingAppDbContext.cs — modification
// In OnModelCreating, BacktestRun configuration block, add after StrategyConfigJson:

entity.Property(e => e.ExecutionConfigJson)
    .IsRequired();
```

Generate migration:

```bash
cd src/TradingApp.Persistence
dotnet ef migrations add SplitStrategyExecutionConfig --startup-project ../TradingApp.Api
```

**Pre-migration**: Clean out existing backtest data:

```sql
DELETE FROM BacktestRuns;
```

Or handle via the migration itself by providing a default value for the new column:

```csharp
// In the migration Up method, if needed:
migrationBuilder.AddColumn<string>(
    name: "ExecutionConfigJson",
    table: "BacktestRuns",
    type: "TEXT",
    nullable: false,
    defaultValue: "{}");  // safe default for any existing rows
```

##### Pattern References

Based on existing migrations in `src/TradingApp.Persistence/Migrations/` — follows `{yyyyMMddHHmmss}_{PascalCaseDescription}` naming convention and inline `OnModelCreating` configuration pattern.

---

### Task 3.10: Update API and controller tests {#task-310-update-api-and-controller-tests}

Update `BacktestsControllerTests` to use the new split request shape and response shape.

- **Complexity**: Medium
- **Risk Factors**: Test helper methods (`CreateValidRequest`, `CreateBacktestRun`) need restructuring
- **Files**:
  - `tests/TradingApp.Api.Tests/Controllers/BacktestsControllerTests.cs` — update request/response shapes, helper methods
- **Success**:
  - `CreateValidRequest` returns request with split `StrategyConfig` + `ExecutionConfig` sections
  - `CreateBacktestRun` constructs runs with separate JSON columns
  - All controller tests compile and pass
- **Dependencies**: Tasks 3.2–3.9

#### Implementation Details

```csharp
// tests/TradingApp.Api.Tests/Controllers/BacktestsControllerTests.cs — modification
using TradingApp.Domain.Trading;

// Update CreateValidRequest helper:
private static RunBacktestRequest CreateValidRequest() => new()
{
    Symbol = "BTC",
    Intervals = ["15m", "1h", "4h"],
    StartDate = new DateTime(2025, 1, 1),
    EndDate = new DateTime(2025, 2, 1),
    InitialCapital = 10000m,
    StrategyConfig = new StrategyConfigRequest
    {
        GridLevels = 10,
        GridSpacing = 0.5m,
        TakeProfitPercent = 2.0m,
        StopLossPercent = 5.0m,
        BreakdownThreshold = 3.0m,
        PositionSize = 100m,
    },
    ExecutionConfig = new ExecutionConfigRequest
    {
        MakerFee = 0.0001m,
        TakerFee = 0.00035m,
        Slippage = 0.0005m,
        Leverage = 1m,
    },
};

// Update CreateBacktestRun helper — now needs two JSON columns:
private static BacktestRun CreateBacktestRun(...)
{
    var strategyConfigJson = JsonSerializer.Serialize(new GridStrategyConfig
    {
        GridLevels = 10,
        GridSpacing = 0.5m,
        TakeProfitPercent = 2.0m,
        StopLossPercent = 5.0m,
        BreakdownThreshold = 3.0m,
        PositionSize = 100m,
    });
    var executionConfigJson = JsonSerializer.Serialize(new ExecutionConfig
    {
        FeeModel = new FeeModel
        {
            MakerFeeRate = 0.0001m,
            TakerFeeRate = 0.00035m,
            SlippageRate = 0.0005m,
        },
        Leverage = 1m,
    });
    return BacktestRun.CreateQueued(
        symbol: "BTC",
        intervalsJson: ...,
        startDateUtc: ...,
        endDateUtc: ...,
        strategyConfigJson: strategyConfigJson,
        executionConfigJson: executionConfigJson,
        initialCapital: 10000m);
}

// Update response assertions to check for both StrategyConfig and ExecutionConfig properties
```

##### Pattern References

Based on existing `tests/TradingApp.Api.Tests/Controllers/BacktestsControllerTests.cs`.

---

### Task 3.11: Run build and all tests {#task-311-run-build-and-tests}

Verify the solution builds and all tests pass after the entity, command, mapper, and API changes.

- **Complexity**: Low
- **Risk Factors**: Migration must apply cleanly; all JSON serialization/deserialization must round-trip correctly
- **Files**: None (verification step)
- **Success**:
  - `dotnet build TradingApp.sln` succeeds
  - `dotnet test TradingApp.sln` — all tests pass
  - No references to old `Application.Backtesting.Models.GridStrategyConfig` remain
- **Dependencies**: Tasks 3.1–3.10

## Phase Success Criteria

- Old `Application.Backtesting.Models.GridStrategyConfig` deleted
- `BacktestRun` entity has `StrategyConfigJson` + `ExecutionConfigJson` columns
- `RunBacktestRequest` uses split `StrategyConfigRequest` + `ExecutionConfigRequest`
- `BacktestRunResponse` exposes both `StrategyConfig` and `ExecutionConfig`
- `RunBacktestCommand` carries both typed configs
- `BacktestProcessorService.BuildConfig` reads from two JSON columns
- EF migration exists and applies cleanly
- No `JsonSerializer.Deserialize<GridStrategyConfig>` in engine, controller, or processor service
- All tests pass
