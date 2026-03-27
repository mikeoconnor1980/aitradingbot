<!-- markdownlint-disable-file -->

# Task Details: Candle Data Persistence

## Phase 2: Host Registration, Startup Migration & Configuration

## Standards and Knowledge References

- **csharp.instructions.md** — `async/await` with `CancellationToken`, connection string via configuration
- **dotnet-architecture.instructions.md** — DI registration via extension methods, startup hooks
- **03-infrastructure-architecture.md** — Phased deployment, SQLite file path at `Data/tradingapp.db`
- **ADR 3** — SQLite for POC, connection string in appsettings.json

## Design References

- **EF Core auto-migration** — `context.Database.MigrateAsync()` called during application startup, BEFORE `app.Run()`
- **Data directory creation** — `Directory.CreateDirectory()` ensures the `Data/` folder exists before SQLite tries to create the file

### Task 2.1: Add Persistence project references to Api and Worker {#task-21-add-persistence-project-references-to-api-and-worker}

Add project references from both host projects to `TradingApp.Persistence` so they can call `AddPersistence()` and resolve the DbContext.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.Api/TradingApp.Api.csproj` — Modify: add Persistence project reference
  - `src/TradingApp.Worker/TradingApp.Worker.csproj` — Modify: add Persistence project reference
- **Success**:
  - Both projects reference `TradingApp.Persistence`
  - Solution builds successfully
- **Dependencies**: Phase 1 complete

### Task 2.2: Add connection string configuration to appsettings files {#task-22-add-connection-string-configuration-to-appsettings-files}

Add the `ConnectionStrings:DefaultConnection` section to all appsettings files for both Api and Worker.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.Api/appsettings.json` — Modify: add ConnectionStrings section
  - `src/TradingApp.Api/appsettings.Development.json` — Modify: add ConnectionStrings section (same path for dev)
  - `src/TradingApp.Worker/appsettings.json` — Modify: add ConnectionStrings section
  - `src/TradingApp.Worker/appsettings.Development.json` — Modify: add ConnectionStrings section (same path for dev)
- **Success**:
  - All 4 appsettings files contain `"ConnectionStrings": { "DefaultConnection": "Data Source=Data/tradingapp.db" }`
  - Configuration resolves correctly at runtime
- **Dependencies**: None

#### Implementation Details

```json
// src/TradingApp.Api/appsettings.json — add ConnectionStrings section
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=Data/tradingapp.db"
  },
  "Hyperliquid": {
    "BaseUrl": "https://api.hyperliquid-testnet.xyz",
    "WsBaseUrl": "wss://api.hyperliquid-testnet.xyz/ws",
    "Network": "testnet"
  }
}
```

```json
// src/TradingApp.Worker/appsettings.json — add ConnectionStrings section
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=Data/tradingapp.db"
  }
}
```

The Development appsettings files should also have the same ConnectionStrings section. If they already override logging, add the section alongside existing overrides.

##### Pattern References

- `src/TradingApp.Api/appsettings.json` — Existing configuration structure
- `src/TradingApp.Worker/appsettings.json` — Existing configuration structure

### Task 2.3: Register persistence services and add startup migration to Api {#task-23-register-persistence-services-and-add-startup-migration-to-api}

Call `AddPersistence()` in `Program.cs` and add the auto-migration startup hook that ensures the `Data/` directory exists and applies pending migrations before the app starts.

- **Complexity**: Medium
- **Risk Factors**: Must ensure `Data/` directory creation happens before `MigrateAsync()`; must not interfere with existing startup flow
- **Files**:
  - `src/TradingApp.Api/Program.cs` — Modify: add `AddPersistence()` call and migration startup hook
- **Success**:
  - `builder.Services.AddPersistence(builder.Configuration)` is called during service registration
  - After `builder.Build()` and before `app.Run()`, migrations are applied
  - `Data/` directory is created if it doesn't exist
  - API starts successfully and `Data/tradingapp.db` is created with the `Candles` table
- **Dependencies**: Task 2.1 (project reference), Task 2.2 (connection string)

#### Implementation Details

```csharp
// src/TradingApp.Api/Program.cs — modifications

// Add these usings at the top:
using Microsoft.EntityFrameworkCore;
using TradingApp.Persistence;

// Add this line after existing service registrations, before var app = builder.Build():
// ... existing registrations ...
builder.Services.AddPersistence(builder.Configuration);

var app = builder.Build();

// Add migration startup hook AFTER builder.Build() and BEFORE existing middleware:
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TradingAppDbContext>();
    var connectionString = db.Database.GetConnectionString();
    if (connectionString is not null)
    {
        var dbPath = connectionString.Replace("Data Source=", "").Trim();
        var directory = Path.GetDirectoryName(Path.GetFullPath(dbPath));
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }
    }
    await db.Database.MigrateAsync();
}

// ... existing middleware pipeline continues ...
app.Logger.LogInformation(
    "Hyperliquid wallet configured: {WalletAddress}",
    signer.WalletAddress);
// ... etc.
```

> **Note**: The `await` keyword requires the top-level `app.Run()` call to remain in the async context. The existing top-level statements in `Program.cs` already support `await` (since `app.Run()` is implicitly async in ASP.NET Core minimal APIs).

##### Pattern References

- `src/TradingApp.Api/Program.cs` — Existing inline DI registration and startup pipeline
- EF Core documentation — `MigrateAsync()` for startup migration in development/POC environments

### Task 2.4: Register persistence services and add startup migration to Worker {#task-24-register-persistence-services-and-add-startup-migration-to-worker}

Call `AddPersistence()` in the Worker's `Program.cs` and add the same auto-migration startup hook.

- **Complexity**: Low
- **Risk Factors**: None — Worker is currently a 3-line bare host
- **Files**:
  - `src/TradingApp.Worker/Program.cs` — Modify: add persistence registration and migration
- **Success**:
  - `builder.Services.AddPersistence(builder.Configuration)` is called
  - Migrations are applied on startup
  - Worker starts successfully
- **Dependencies**: Task 2.1 (project reference), Task 2.2 (connection string)

#### Implementation Details

```csharp
// src/TradingApp.Worker/Program.cs — complete replacement (currently only 3 lines)
using Microsoft.EntityFrameworkCore;
using TradingApp.Persistence;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddPersistence(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TradingAppDbContext>();
    var connectionString = db.Database.GetConnectionString();
    if (connectionString is not null)
    {
        var dbPath = connectionString.Replace("Data Source=", "").Trim();
        var directory = Path.GetDirectoryName(Path.GetFullPath(dbPath));
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }
    }
    await db.Database.MigrateAsync();
}

app.Run();
```

##### Pattern References

- `src/TradingApp.Worker/Program.cs` — Current bare host (3 lines)
- `src/TradingApp.Api/Program.cs` — Migration pattern from Task 2.3

### Task 2.5: Add database files to `.gitignore` {#task-25-add-database-files-to-gitignore}

Add `Data/*.db` and related SQLite files to `.gitignore` to prevent the local database from being committed.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `.gitignore` — Modify: add SQLite database file patterns
- **Success**:
  - `Data/*.db` and `Data/*.db-shm`, `Data/*.db-wal` patterns are excluded
  - Git does not track the SQLite database file
- **Dependencies**: None

#### Implementation Details

Add to the end of the existing `.gitignore`:

```gitignore
# SQLite database files
Data/*.db
Data/*.db-shm
Data/*.db-wal
```

##### Pattern References

- `.gitignore` — Existing file structure

### Task 2.6: Build solution, run all tests, and verify API startup {#task-26-build-solution-run-all-tests-and-verify-api-startup}

Full verification that all changes compile, existing tests still pass, new tests pass, and both Api and Worker can start successfully with auto-migration.

- **Complexity**: Low
- **Risk Factors**: None (verification step)
- **Files**: None (verification step)
- **Success**:
  - `dotnet build TradingApp.sln` succeeds with 0 errors
  - `dotnet test TradingApp.sln` passes all tests
  - `dotnet run --project src/TradingApp.Api` starts without errors and creates `Data/tradingapp.db`
  - SQLite database contains `Candles` table with correct schema (verify via `sqlite3 Data/tradingapp.db ".schema Candles"` or EF Core logging)
- **Dependencies**: All previous tasks in Phase 2

#### Implementation Details

```powershell
# Build entire solution
dotnet build TradingApp.sln

# Run all tests
dotnet test TradingApp.sln

# Verify API startup creates the database
dotnet run --project src/TradingApp.Api

# In a separate terminal, verify the database file was created
Test-Path src/TradingApp.Api/Data/tradingapp.db
```

## Phase Success Criteria

- Both `TradingApp.Api` and `TradingApp.Worker` reference `TradingApp.Persistence`
- All 4 appsettings files contain `ConnectionStrings:DefaultConnection`
- Both Program.cs files call `AddPersistence()` and run `MigrateAsync()` on startup
- `Data/` directory is created automatically on first startup
- `Data/tradingapp.db` is excluded from git
- `dotnet build TradingApp.sln` succeeds
- `dotnet test TradingApp.sln` — all tests pass (existing + new)
- Api starts and creates `Data/tradingapp.db` with `Candles` table
