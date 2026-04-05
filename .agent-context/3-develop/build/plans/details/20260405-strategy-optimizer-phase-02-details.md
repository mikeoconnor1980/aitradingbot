<!-- markdownlint-disable-file -->

# Task Details: Strategy Optimizer — Phase 2: Persistence & API

## Phase 2: Backend — Persistence & API

## Standards and Knowledge References

- **csharp.instructions.md**: `sealed` classes, factory methods, `_camelCase` private fields
- **dotnet-architecture.instructions.md**: Repository interface + EF Core implementation, MediatR CQRS
- **api-controllers.instructions.md**: MediatR-based controllers, REST conventions
- **BacktestsController.cs**: Reference pattern for REST endpoints, 202 Accepted for async operations
- **BacktestProcessorService.cs**: Reference pattern for background job processing with SignalR progress
- **TradingAppDbContext.cs**: DbSet registration, SQLite model configuration

---

### Task 2.1: Create `IOptimizationRunRepository` and EF implementation {#task-21-create-ioptimizationrunrepository}

Create the repository abstraction and EF Core implementation for `OptimizationRun` and `OptimizationResult` persistence.

- **Complexity**: Medium
- **Risk Factors**: Must handle the parent-child relationship between `OptimizationRun` and `OptimizationResult`
- **Files**:
  - `src/TradingApp.Application/Abstractions/Repositories/IOptimizationRunRepository.cs` — new file
  - `src/TradingApp.Persistence/Repositories/OptimizationRunRepository.cs` — new file
- **Success**:
  - Repository methods compile and follow existing `IBacktestRunRepository` pattern
  - Supports: `AddAsync`, `UpdateAsync`, `GetByIdAsync`, `GetPagedListAsync`, `AddResultsAsync`

#### Implementation Details

```csharp
// src/TradingApp.Application/Abstractions/Repositories/IOptimizationRunRepository.cs
namespace TradingApp.Application.Abstractions.Repositories;

public interface IOptimizationRunRepository
{
    Task AddAsync(OptimizationRun run, CancellationToken ct = default);
    Task UpdateAsync(OptimizationRun run, CancellationToken ct = default);
    Task<OptimizationRun?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<OptimizationRun>> GetPagedListAsync(int page, int pageSize, CancellationToken ct = default);
    Task AddResultsAsync(IReadOnlyList<OptimizationResult> results, CancellationToken ct = default);
    Task<IReadOnlyList<OptimizationResult>> GetResultsByRunIdAsync(Guid runId, CancellationToken ct = default);
}
```

```csharp
// src/TradingApp.Persistence/Repositories/OptimizationRunRepository.cs
// Follow BacktestRunRepository pattern — inject TradingAppDbContext, use EF Core operations
// GetByIdAsync should NOT eager-load results — results fetched separately via GetResultsByRunIdAsync
```

---

### Task 2.2: Register `OptimizationRun` and `OptimizationResult` in DbContext {#task-22-register-entities-in-dbcontext}

Add DbSet properties and entity configuration to `TradingAppDbContext`.

- **Complexity**: Low
- **Risk Factors**: SQLite decimal conversion (follow existing Candle pattern)
- **Files**:
  - `src/TradingApp.Persistence/TradingAppDbContext.cs` — modification
- **Success**:
  - DbSets for `OptimizationRun` and `OptimizationResult` registered
  - Entity configuration in `OnModelCreating` with appropriate column types and indexes
  - `OptimizationResult` has FK to `OptimizationRun` with `IX_OptimizationResults_RunId` index
  - `EnsureCreated()` creates tables in SQLite (no formal migrations needed for POC)

#### Implementation Details

Add to TradingAppDbContext:

```csharp
public DbSet<OptimizationRun> OptimizationRuns => Set<OptimizationRun>();
public DbSet<OptimizationResult> OptimizationResults => Set<OptimizationResult>();
```

Add entity configuration in `OnModelCreating`:

```csharp
modelBuilder.Entity<OptimizationRun>(entity =>
{
    entity.ToTable("OptimizationRuns");
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Symbol).HasMaxLength(20).IsRequired();
    entity.Property(e => e.SweepConfigJson).IsRequired();
    entity.Property(e => e.ThresholdsJson).IsRequired();
    entity.Property(e => e.InitialCapital).HasConversion<double>();
    entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
});

modelBuilder.Entity<OptimizationResult>(entity =>
{
    entity.ToTable("OptimizationResults");
    entity.HasKey(e => e.Id);
    entity.Property(e => e.StrategyConfigJson).IsRequired();
    entity.Property(e => e.SignalDescription).IsRequired();
    entity.Property(e => e.FitnessScore).HasConversion<double>();
    entity.Property(e => e.TotalPnl).HasConversion<double>();
    entity.Property(e => e.WinRate).HasConversion<double>();
    entity.Property(e => e.MaxDrawdown).HasConversion<double>();
    entity.Property(e => e.TotalFeesPaid).HasConversion<double>();
    entity.Property(e => e.AverageTradePnl).HasConversion<double>();

    entity.HasIndex(e => e.OptimizationRunId)
        .HasDatabaseName("IX_OptimizationResults_RunId");
});
```

---

### Task 2.3: Create `RunOptimizationCommand` MediatR handler {#task-23-create-runoptimizationcommand}

Create the command that validates input, creates the `OptimizationRun` entity, persists it, and enqueues the optimization job.

- **Complexity**: Medium
- **Risk Factors**: Must serialize `SweepConfig` and `FitnessThresholds` to JSON for persistence
- **Files**:
  - `src/TradingApp.Application/Optimization/RunOptimizationCommand.cs` — new file
  - `src/TradingApp.Application/Optimization/Models/OptimizationRunResponse.cs` — new file
  - `src/TradingApp.Application/Optimization/Models/OptimizationJobQueue.cs` — new file
- **Success**:
  - Command persists `OptimizationRun` with status `Queued`
  - Returns `OptimizationRunResponse` with Id, Status
  - Job queued via channel-based `OptimizationJobQueue` (same pattern as `BacktestJobQueue`)

#### Implementation Details

```csharp
// src/TradingApp.Application/Optimization/Models/OptimizationJobQueue.cs
// Follow BacktestJobQueue pattern — System.Threading.Channels.Channel<OptimizationJob>
public sealed record OptimizationJob(Guid RunId, SweepConfig Config);
```

```csharp
// src/TradingApp.Application/Optimization/RunOptimizationCommand.cs
public sealed record RunOptimizationCommand(SweepConfig Config) : IRequest<OptimizationRunResponse>;

public sealed class RunOptimizationCommandHandler : IRequestHandler<RunOptimizationCommand, OptimizationRunResponse>
{
    // 1. Serialize SweepConfig and FitnessThresholds to JSON
    // 2. Create OptimizationRun.CreateQueued(...)
    // 3. Persist via IOptimizationRunRepository.AddAsync
    // 4. Enqueue OptimizationJob
    // 5. Return response with Id, Status
}
```

---

### Task 2.4: Create `GetOptimizationResultQuery` MediatR handler {#task-24-create-getoptimizationresultquery}

Retrieve a full optimization run with its top results.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.Application/Optimization/GetOptimizationResultQuery.cs` — new file
  - `src/TradingApp.Application/Optimization/Models/OptimizationResultResponse.cs` — new file
- **Success**:
  - Returns run metadata + list of ranked results with all metrics

#### Implementation Details

```csharp
public sealed record GetOptimizationResultQuery(Guid Id) : IRequest<OptimizationRunResponse?>;

// OptimizationRunResponse includes:
// Id, Symbol, StartDate, EndDate, InitialCapital, Status, Progress (completed/total),
// TotalCombinations, CompletedCount, QualifiedCount, ElapsedMs, ErrorMessage, CreatedAt,
// Results: IReadOnlyList<OptimizationResultResponse>

// OptimizationResultResponse includes:
// Rank, FitnessScore, SignalDescription, StrategyConfigJson,
// TotalPnl, WinRate, MaxDrawdown, TotalTrades, WinningTrades, LosingTrades,
// TotalFeesPaid, AverageTradePnl, AverageHoldTimeMinutes
```

---

### Task 2.5: Create `GetOptimizationListQuery` MediatR handler {#task-25-create-getoptimizationlistquery}

Paginated list of optimization run summaries.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.Application/Optimization/GetOptimizationListQuery.cs` — new file
- **Success**:
  - Returns paged list of run summaries (no results detail — just metadata)

---

### Task 2.6: Create `OptimizationProcessorService` background service {#task-26-create-optimizationprocessorservice}

Background service that dequeues optimization jobs, invokes `SweepRunner`, persists results, and broadcasts progress via SignalR.

- **Complexity**: High
- **Risk Factors**: Must handle cancellation, error states, and progress broadcasting safely
- **Files**:
  - `src/TradingApp.Api/Services/OptimizationProcessorService.cs` — new file
- **Success**:
  - Reads from `OptimizationJobQueue` channel
  - Calls `SweepRunner.RunAsync` with progress callback
  - Broadcasts progress via SignalR: `ReceiveOptimizationProgress { id, status, completed, total }`
  - On completion: creates `OptimizationResult` entities from `SweepResult.TopResults`, persists via repository
  - On failure: marks run as failed with error message
- **Dependencies**: `ISweepRunner`, `IOptimizationRunRepository`, `IHubContext<MarketDataHub>`

#### Implementation Details

Follow `BacktestProcessorService` pattern exactly:

```csharp
// src/TradingApp.Api/Services/OptimizationProcessorService.cs
public sealed class OptimizationProcessorService : BackgroundService
{
    // Inject: IServiceScopeFactory, OptimizationJobQueue, IHubContext<MarketDataHub>, ILogger

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _jobQueue.ReadAllAsync(stoppingToken))
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IOptimizationRunRepository>();
            var sweepRunner = scope.ServiceProvider.GetRequiredService<ISweepRunner>();

            var run = await repository.GetByIdAsync(job.RunId, stoppingToken);
            if (run is null) continue;

            run.MarkRunning();
            await repository.UpdateAsync(run, stoppingToken);
            await BroadcastProgressAsync(run);

            try
            {
                var result = await sweepRunner.RunAsync(job.Config, (completed, total) =>
                {
                    run.UpdateProgress(completed, total);
                    // Fire-and-forget broadcast (throttled to avoid flooding)
                    _ = BroadcastProgressAsync(run);
                }, stoppingToken);

                // Persist top results as OptimizationResult entities
                var resultEntities = result.TopResults.Select(r =>
                    OptimizationResult.Create(
                        run.Id, r.Rank, r.FitnessScore,
                        JsonSerializer.Serialize(r.Strategy.Config),
                        r.Strategy.Description,
                        r.BacktestResult.TotalPnL, r.BacktestResult.WinRate,
                        r.BacktestResult.MaxDrawdownAbsolute,
                        r.BacktestResult.TotalTrades, r.BacktestResult.WinningTrades,
                        r.BacktestResult.LosingTrades, r.BacktestResult.TotalFeesPaid,
                        r.BacktestResult.AverageTradePnL,
                        r.BacktestResult.AverageHoldTime.TotalMinutes))
                    .ToList();

                await repository.AddResultsAsync(resultEntities, stoppingToken);
                run.MarkCompleted(result.TotalQualified, result.ElapsedMs);
                await repository.UpdateAsync(run, stoppingToken);
                await BroadcastStatusAsync(run, "Completed");
            }
            catch (Exception ex)
            {
                run.MarkFailed(ex.Message);
                await repository.UpdateAsync(run, stoppingToken);
                await BroadcastStatusAsync(run, "Failed");
            }
        }
    }

    private async Task BroadcastProgressAsync(OptimizationRun run) =>
        await _hubContext.Clients.All.SendAsync("ReceiveOptimizationProgress", new
        {
            id = run.Id,
            status = run.Status.ToString(),
            completed = run.CompletedCount,
            total = run.TotalCombinations
        });
}
```

---

### Task 2.7: Create `OptimizationsController` with REST endpoints {#task-27-create-optimizationscontroller}

REST controller for optimization operations.

- **Complexity**: Low
- **Risk Factors**: None — follows BacktestsController pattern exactly
- **Files**:
  - `src/TradingApp.Api/Controllers/OptimizationsController.cs` — new file
  - `src/TradingApp.Api/Models/RunOptimizationRequest.cs` — new file
- **Success**:
  - `POST /api/optimizations` — start a new optimization run → 202 Accepted
  - `GET /api/optimizations` — paginated list of runs
  - `GET /api/optimizations/{id}` — get run with results

#### Implementation Details

```csharp
// src/TradingApp.Api/Models/RunOptimizationRequest.cs
public sealed class RunOptimizationRequest
{
    [Required] public string Symbol { get; set; } = string.Empty;
    [Required] public long StartDateUtc { get; set; }
    [Required] public long EndDateUtc { get; set; }
    [Range(1, 1_000_000)] public decimal InitialCapital { get; set; } = 10_000m;
    [Range(10, 5000)] public int SampleSize { get; set; } = 500;

    // Parameter bounds (optional — defaults applied)
    public decimal? StopLossMin { get; set; }
    public decimal? StopLossMax { get; set; }
    public decimal? TakeProfitMin { get; set; }
    public decimal? TakeProfitMax { get; set; }
    public decimal? LeverageMin { get; set; }
    public decimal? LeverageMax { get; set; }

    // Fitness thresholds (optional — defaults applied)
    public decimal? MinWinRate { get; set; }
    public int? MinTotalTrades { get; set; }
    public decimal? MaxDrawdownPercent { get; set; }
}
```

```csharp
// src/TradingApp.Api/Controllers/OptimizationsController.cs
[ApiController]
[Route("api/optimizations")]
public sealed class OptimizationsController : ControllerBase
{
    private readonly IMediator _mediator;

    [HttpPost]
    public async Task<IActionResult> RunOptimization([FromBody] RunOptimizationRequest request, CancellationToken ct)
    {
        // Map request → SweepConfig → RunOptimizationCommand
        // Return 202 Accepted with Location header
    }

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct)
    {
        // Return paginated list
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetResult(Guid id, CancellationToken ct)
    {
        // Return full run with results or 404
    }
}
```

---

### Task 2.8: Register DI services in `Program.cs` {#task-28-register-di-services}

Register all new services in the DI container.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.Api/Program.cs` — modification
- **Success**: All services resolvable

#### Registration

```csharp
// Application services
builder.Services.AddSingleton<OptimizationJobQueue>();
builder.Services.AddScoped<IStrategyConfigGenerator, StrategyConfigGenerator>();
builder.Services.AddScoped<IFitnessScorer, FitnessScorer>();
builder.Services.AddScoped<ISweepRunner, SweepRunner>();
builder.Services.AddScoped<IOptimizationRunRepository, OptimizationRunRepository>();

// Background service
builder.Services.AddHostedService<OptimizationProcessorService>();
```

---

### Task 2.9: Build solution and run all backend tests {#task-29-build-and-test}

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None — verification only
- **Success**:
  - `dotnet build` succeeds with zero errors
  - `dotnet test --filter "FullyQualifiedName!~AcceptanceTests"` — all tests pass
