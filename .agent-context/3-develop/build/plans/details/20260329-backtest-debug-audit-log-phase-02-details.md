<!-- markdownlint-disable-file -->

# Task Details: Backtest Debug/Audit Log

## Phase 2: Entity, Persistence & Migration

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — sealed classes, private setters, factory method pattern
- `.github/instructions/testing.instructions.md` — MSTest, FluentAssertions, SQLite in-memory for persistence tests
- `.github/instructions/dotnet-architecture.instructions.md` — EF Core inline configuration, migration conventions
- `.agent-context/0-knowledge/04-domain-model.md` — BacktestRun entity definition

## Design References

- EF Core migration pattern established in `20260328204609_AddEquityTimeSeriesToBacktestRun.cs`
- JSON blob column pattern from `TradesJson` and `EquityTimeSeriesJson` on `BacktestRun`
- No `IEntityTypeConfiguration` — all config inline in `TradingAppDbContext.OnModelCreating`

### Task 2.1: Add audit log properties to BacktestRun entity {#task-21-add-audit-log-properties-to-backtestrun-entity}

Add 4 new properties to `BacktestRun`: `AuditLogEnabled` (bool), `CandleLogJson` (string?), `OrderEventLogJson` (string?), `GridCycleLogJson` (string?). Update `CreateQueued`, `MarkCompleted`, and `Create` factory methods.

- **Complexity**: Medium
- **Risk Factors**: `MarkCompleted` signature grows from 14 to 18 parameters; ensure all call sites are updated
- **Files**:
  - `src/TradingApp.Domain/Entities/BacktestRun.cs` — modification
- **Success**:
  - 4 new properties exist on `BacktestRun`
  - `CreateQueued` accepts `auditLogEnabled` parameter and initializes JSON columns to null
  - `MarkCompleted` accepts 3 additional nullable string parameters for debug JSON
  - `Create` accepts the same additional parameters plus `auditLogEnabled`
  - Project compiles (call sites updated)
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Domain/Entities/BacktestRun.cs — modification
// Add after existing properties (after EquityTimeSeriesJson):

    public bool AuditLogEnabled { get; private set; }
    public string? CandleLogJson { get; private set; }
    public string? OrderEventLogJson { get; private set; }
    public string? GridCycleLogJson { get; private set; }
```

Update `CreateQueued` — add `bool auditLogEnabled = true` parameter:

```csharp
// src/TradingApp.Domain/Entities/BacktestRun.cs — modification to CreateQueued
    public static BacktestRun CreateQueued(
        string symbol,
        string intervalsJson,
        long startDateUtc,
        long endDateUtc,
        string strategyConfigJson,
        decimal initialCapital,
        bool auditLogEnabled = true)
    {
        // ... existing validation ...

        return new BacktestRun
        {
            // ... existing properties ...
            EquityTimeSeriesJson = "[]",
            AuditLogEnabled = auditLogEnabled,
            CandleLogJson = null,
            OrderEventLogJson = null,
            GridCycleLogJson = null,
            CreatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }
```

Update `MarkCompleted` — add 3 nullable string parameters:

```csharp
// src/TradingApp.Domain/Entities/BacktestRun.cs — modification to MarkCompleted
    public void MarkCompleted(
        int candlesReplayed,
        long elapsedMs,
        int totalTrades,
        int winningTrades,
        int losingTrades,
        decimal winRate,
        decimal totalPnl,
        decimal maxDrawdown,
        decimal averageTradePnl,
        double averageHoldTimeMinutes,
        int hedgesOpened,
        decimal totalFeesPaid,
        string tradesJson,
        string equityTimeSeriesJson,
        string? candleLogJson = null,
        string? orderEventLogJson = null,
        string? gridCycleLogJson = null)
    {
        // ... existing assignments ...
        TradesJson = tradesJson ?? "[]";
        EquityTimeSeriesJson = equityTimeSeriesJson ?? "[]";
        CandleLogJson = candleLogJson;
        OrderEventLogJson = orderEventLogJson;
        GridCycleLogJson = gridCycleLogJson;
    }
```

Update `Create` — add the same new parameters (with defaults for backward compatibility):

```csharp
// src/TradingApp.Domain/Entities/BacktestRun.cs — modification to Create
    public static BacktestRun Create(
        // ... existing 20 parameters ...
        string equityTimeSeriesJson = "[]",
        bool auditLogEnabled = true,
        string? candleLogJson = null,
        string? orderEventLogJson = null,
        string? gridCycleLogJson = null)
    {
        // ... existing validation and construction ...
        return new BacktestRun
        {
            // ... existing properties ...
            EquityTimeSeriesJson = equityTimeSeriesJson ?? "[]",
            AuditLogEnabled = auditLogEnabled,
            CandleLogJson = candleLogJson,
            OrderEventLogJson = orderEventLogJson,
            GridCycleLogJson = gridCycleLogJson,
            CreatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }
```

##### Pattern References

- `src/TradingApp.Domain/Entities/BacktestRun.cs` — existing `MarkCompleted` parameter pattern and nullable `ErrorMessage` property

---

### Task 2.2: Create EF Core migration for new columns {#task-22-create-ef-core-migration-for-new-columns}

Run `dotnet ef migrations add AddAuditLogToBacktestRun` to generate the migration, then verify or manually adjust. The migration adds 4 columns: `AuditLogEnabled` (INTEGER, non-null, default 0), `CandleLogJson` (TEXT, nullable), `OrderEventLogJson` (TEXT, nullable), `GridCycleLogJson` (TEXT, nullable).

- **Complexity**: Low
- **Risk Factors**: Must include `defaultValue: false` on `AuditLogEnabled` for existing rows
- **Files**:
  - `src/TradingApp.Persistence/Migrations/{timestamp}_AddAuditLogToBacktestRun.cs` — new file (auto-generated)
  - `src/TradingApp.Persistence/Migrations/{timestamp}_AddAuditLogToBacktestRun.Designer.cs` — new file (auto-generated)
  - `src/TradingApp.Persistence/Migrations/TradingAppDbContextModelSnapshot.cs` — updated
- **Success**:
  - Migration adds 4 columns to `BacktestRuns` table
  - `AuditLogEnabled` has `defaultValue: false` for backward compatibility
  - `dotnet ef database update` applies cleanly to existing SQLite database
- **Dependencies**: Task 2.1, Task 2.3

#### Implementation Details

Run from the repository root:

```bash
dotnet ef migrations add AddAuditLogToBacktestRun \
  --project src/TradingApp.Persistence \
  --startup-project src/TradingApp.Api
```

Verify the generated migration resembles:

```csharp
// Expected migration content (verify/adjust after generation)
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<bool>(
        name: "AuditLogEnabled",
        table: "BacktestRuns",
        type: "INTEGER",
        nullable: false,
        defaultValue: false);

    migrationBuilder.AddColumn<string>(
        name: "CandleLogJson",
        table: "BacktestRuns",
        type: "TEXT",
        nullable: true);

    migrationBuilder.AddColumn<string>(
        name: "OrderEventLogJson",
        table: "BacktestRuns",
        type: "TEXT",
        nullable: true);

    migrationBuilder.AddColumn<string>(
        name: "GridCycleLogJson",
        table: "BacktestRuns",
        type: "TEXT",
        nullable: true);
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropColumn(name: "AuditLogEnabled", table: "BacktestRuns");
    migrationBuilder.DropColumn(name: "CandleLogJson", table: "BacktestRuns");
    migrationBuilder.DropColumn(name: "OrderEventLogJson", table: "BacktestRuns");
    migrationBuilder.DropColumn(name: "GridCycleLogJson", table: "BacktestRuns");
}
```

##### Pattern References

- `src/TradingApp.Persistence/Migrations/20260328190000_AddBacktestRunStatus.cs` — uses `defaultValue:` for non-nullable columns added to existing tables
- `src/TradingApp.Persistence/Migrations/20260328204609_AddEquityTimeSeriesToBacktestRun.cs` — latest migration, TEXT column pattern

---

### Task 2.3: Update DbContext configuration {#task-23-update-dbcontext-configuration}

Add property configurations for the 4 new columns in `TradingAppDbContext.OnModelCreating` within the `BacktestRun` entity builder block.

- **Complexity**: Low
- **Risk Factors**: None — additive configuration
- **Files**:
  - `src/TradingApp.Persistence/TradingAppDbContext.cs` — modification
- **Success**:
  - New properties are configured (nullable JSON columns, non-nullable bool)
  - Existing configuration is unchanged
- **Dependencies**: Task 2.1

#### Implementation Details

```csharp
// src/TradingApp.Persistence/TradingAppDbContext.cs — modification
// Add inside the modelBuilder.Entity<BacktestRun>(entity => { ... }) block,
// after the existing EquityTimeSeriesJson line:

    // ... existing code ...
    entity.Property(br => br.EquityTimeSeriesJson).IsRequired();
    entity.Property(br => br.AuditLogEnabled);
    // CandleLogJson, OrderEventLogJson, GridCycleLogJson are nullable — no IsRequired()
```

##### Pattern References

- `src/TradingApp.Persistence/TradingAppDbContext.cs` — existing inline entity configuration (all config in `OnModelCreating`, no separate configuration classes)

---

### Task 2.4: Add debug data serialization to BacktestRunResponseMapper {#task-24-add-debug-data-serialization-to-backtestrunresponsemapper}

Add static serialization methods for the three debug log types, following the existing `SerializeTrades` pattern.

- **Complexity**: Low
- **Risk Factors**: None — same JSON serialization pattern
- **Files**:
  - `src/TradingApp.Application/Backtesting/BacktestRunResponseMapper.cs` — modification
- **Success**:
  - `SerializeCandleLog`, `SerializeOrderEventLog`, `SerializeGridCycleLog` methods exist
  - Methods use the same `JsonOptions` as existing serializers
- **Dependencies**: Task 1.1

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/BacktestRunResponseMapper.cs — modification
// Add after existing SerializeEquityTimeSeries method:

    public static string SerializeCandleLog(IReadOnlyList<CandleEvaluationEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return JsonSerializer.Serialize(entries, JsonOptions);
    }

    public static string SerializeOrderEventLog(IReadOnlyList<OrderEventEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return JsonSerializer.Serialize(entries, JsonOptions);
    }

    public static string SerializeGridCycleLog(IReadOnlyList<GridCycleEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return JsonSerializer.Serialize(entries, JsonOptions);
    }
```

Add necessary using at the top:

```csharp
using TradingApp.Application.Backtesting.Models;
// (may already exist — ensure CandleEvaluationEntry, OrderEventEntry, GridCycleEntry are accessible)
```

##### Pattern References

- `src/TradingApp.Application/Backtesting/BacktestRunResponseMapper.cs` — `SerializeTrades` and `SerializeEquityTimeSeries` methods using same `JsonOptions`

---

### Task 2.5: Persistence tests for new columns {#task-25-persistence-tests-for-new-columns}

Add tests to `BacktestRunRepositoryTests` verifying the new columns can be persisted and retrieved.

- **Complexity**: Medium
- **Risk Factors**: None — extends existing test class with same patterns
- **Files**:
  - `tests/TradingApp.Persistence.Tests/Repositories/BacktestRunRepositoryTests.cs` — modification
- **Success**:
  - Test verifies: BacktestRun with audit log enabled and debug JSON blobs persists and retrieves correctly
  - Test verifies: BacktestRun with null debug JSON (audit disabled) persists and retrieves correctly
  - Tests pass: `dotnet test tests/TradingApp.Persistence.Tests --filter "FullyQualifiedName~BacktestRunRepositoryTests"`
- **Dependencies**: Task 2.1, Task 2.2, Task 2.3

#### Implementation Details

```csharp
// tests/TradingApp.Persistence.Tests/Repositories/BacktestRunRepositoryTests.cs — modification
// Add two new test methods following the existing test patterns:

    [TestMethod]
    public async Task GivenBacktestRunWithAuditLog_WhenPersisted_ThenDebugDataIsRetrievable()
    {
        var entity = BacktestRun.CreateQueued(
            "BTC", "[\"15m\",\"1h\",\"4h\"]", 1000, 2000,
            "{\"gridLevels\":5}", 10000m, auditLogEnabled: true);
        entity.MarkRunning(100);
        entity.MarkCompleted(
            candlesReplayed: 100, elapsedMs: 5000,
            totalTrades: 5, winningTrades: 3, losingTrades: 2,
            winRate: 0.6m, totalPnl: 50m, maxDrawdown: 10m,
            averageTradePnl: 10m, averageHoldTimeMinutes: 60,
            hedgesOpened: 0, totalFeesPaid: 2m,
            tradesJson: "[]", equityTimeSeriesJson: "[]",
            candleLogJson: "[{\"timestampUtc\":1000}]",
            orderEventLogJson: "[{\"timestampUtc\":2000}]",
            gridCycleLogJson: "[{\"gridCycleId\":\"abc\"}]");

        await using (var writeCtx = CreateContext())
        {
            await new BacktestRunRepository(writeCtx).AddAsync(entity);
        }

        await using var readCtx = CreateContext();
        var result = await new BacktestRunRepository(readCtx).GetByIdAsync(entity.Id);

        result.Should().NotBeNull();
        result!.AuditLogEnabled.Should().BeTrue();
        result.CandleLogJson.Should().Contain("timestampUtc");
        result.OrderEventLogJson.Should().Contain("timestampUtc");
        result.GridCycleLogJson.Should().Contain("gridCycleId");
    }

    [TestMethod]
    public async Task GivenBacktestRunWithoutAuditLog_WhenPersisted_ThenDebugColumnsAreNull()
    {
        var entity = BacktestRun.CreateQueued(
            "BTC", "[\"15m\",\"1h\",\"4h\"]", 1000, 2000,
            "{\"gridLevels\":5}", 10000m, auditLogEnabled: false);
        entity.MarkRunning(100);
        entity.MarkCompleted(
            candlesReplayed: 100, elapsedMs: 5000,
            totalTrades: 0, winningTrades: 0, losingTrades: 0,
            winRate: 0m, totalPnl: 0m, maxDrawdown: 0m,
            averageTradePnl: 0m, averageHoldTimeMinutes: 0,
            hedgesOpened: 0, totalFeesPaid: 0m,
            tradesJson: "[]", equityTimeSeriesJson: "[]");

        await using (var writeCtx = CreateContext())
        {
            await new BacktestRunRepository(writeCtx).AddAsync(entity);
        }

        await using var readCtx = CreateContext();
        var result = await new BacktestRunRepository(readCtx).GetByIdAsync(entity.Id);

        result.Should().NotBeNull();
        result!.AuditLogEnabled.Should().BeFalse();
        result.CandleLogJson.Should().BeNull();
        result.OrderEventLogJson.Should().BeNull();
        result.GridCycleLogJson.Should().BeNull();
    }
```

Note: The `CreateCompletedBacktestRun` helper may need updating to use the new `CreateQueued` signature. If such a helper doesn't exist, create the entity inline using `BacktestRun.CreateQueued(...)`.

##### Pattern References

- `tests/TradingApp.Persistence.Tests/Repositories/BacktestRunRepositoryTests.cs` — existing write/read with separate DbContext pattern

## Phase Success Criteria

- `BacktestRun` entity has 4 new properties, `MarkCompleted` accepts debug data
- EF Core migration applies cleanly (including on existing databases with pre-existing rows)
- DbContext configures new columns correctly
- Serialization methods exist and follow established pattern
- All persistence tests pass: `dotnet test tests/TradingApp.Persistence.Tests`
- All existing tests still pass: `dotnet test tests/TradingApp.Application.Tests` (call sites compile with defaults)
