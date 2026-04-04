<!-- markdownlint-disable-file -->

# Task Details: AI Strategy Review

## Phase 1: Domain, Persistence & Configuration

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — Sealed classes, factory method pattern, private constructors, ArgumentException guards
- `.github/instructions/dotnet-architecture.instructions.md` — Entity in Domain layer, repository interface in Application.Abstractions, concrete in Persistence
- `.github/instructions/testing.instructions.md` — MSTest, FluentAssertions, Given_When_Then naming, domain tests are pure unit tests
- `.agent-context/0-knowledge/04-domain-model.md` — Core entities and relationships
- `.agent-context/0-knowledge/06-project-structure.md` — Layer layout and project conventions

### Task 1.1: Create StrategyReview domain entity {#task-11-create-strategyreview-domain-entity}

Create a new `StrategyReview` entity in the Domain layer linked to a strategy revision. The entity stores the LLM review markdown, metadata about the model used, and timestamps.

- **Complexity**: Medium
- **Risk Factors**: Must preserve StrategyRevision immutability by using a separate entity rather than adding fields to StrategyRevision
- **Files**:
  - `src/TradingApp.Domain/Entities/StrategyReview.cs` - New file
- **Success**:
  - Entity has factory method with validation
  - Entity links to strategy revision via StrategyId + RevisionNumber composite
  - Private constructors follow codebase convention
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Domain/Entities/StrategyReview.cs — new file
namespace TradingApp.Domain.Entities;

public sealed class StrategyReview
{
    public Guid Id { get; private set; }
    public Guid StrategyId { get; private set; }
    public int RevisionNumber { get; private set; }
    public string ReviewMarkdown { get; private set; } = string.Empty;
    public string ModelName { get; private set; } = string.Empty;
    public long CreatedAtUtc { get; private set; }

    private StrategyReview()
    {
    }

    public static StrategyReview Create(
        Guid strategyId,
        int revisionNumber,
        string reviewMarkdown,
        string modelName)
    {
        if (strategyId == Guid.Empty)
        {
            throw new ArgumentException("Strategy ID must not be empty.", nameof(strategyId));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(revisionNumber, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewMarkdown);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);

        return new StrategyReview
        {
            Id = Guid.NewGuid(),
            StrategyId = strategyId,
            RevisionNumber = revisionNumber,
            ReviewMarkdown = reviewMarkdown,
            ModelName = modelName,
            CreatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
    }
}
```

##### Pattern References

- `src/TradingApp.Domain/Entities/StrategyRevision.cs` — Factory method, private constructor, Guid.NewGuid() for Id, CreatedAtUtc as Unix ms

---

### Task 1.2: Create IStrategyReviewRepository interface {#task-12-create-istrategyreviewrepository-interface}

Create the repository interface in the Application abstractions layer.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.Application/Abstractions/Repositories/IStrategyReviewRepository.cs` - New file
- **Success**:
  - Interface defines AddAsync, GetByStrategyAndRevisionAsync, DeleteByStrategyAndRevisionAsync
- **Dependencies**: Task 1.1

#### Implementation Details

```csharp
// src/TradingApp.Application/Abstractions/Repositories/IStrategyReviewRepository.cs — new file
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Abstractions.Repositories;

public interface IStrategyReviewRepository
{
    Task AddAsync(StrategyReview review, CancellationToken cancellationToken = default);

    Task<StrategyReview?> GetByStrategyAndRevisionAsync(
        Guid strategyId,
        int revisionNumber,
        CancellationToken cancellationToken = default);

    Task DeleteByStrategyAndRevisionAsync(
        Guid strategyId,
        int revisionNumber,
        CancellationToken cancellationToken = default);
}
```

##### Pattern References

- `src/TradingApp.Application/Abstractions/Repositories/IStrategyRevisionRepository.cs` — Interface pattern with CancellationToken defaults

---

### Task 1.3: Create StrategyReviewRepository implementation {#task-13-create-strategyreviewrepository-implementation}

Create the concrete EF Core repository in the Persistence layer.

- **Complexity**: Medium
- **Risk Factors**: Delete-then-add pattern for overwriting reviews on re-run
- **Files**:
  - `src/TradingApp.Persistence/Repositories/StrategyReviewRepository.cs` - New file
- **Success**:
  - Repository methods query by composite (StrategyId, RevisionNumber)
  - DeleteByStrategyAndRevisionAsync removes existing review before re-adding
  - SaveChangesAsync called after mutations
- **Dependencies**: Task 1.1, Task 1.2

#### Implementation Details

```csharp
// src/TradingApp.Persistence/Repositories/StrategyReviewRepository.cs — new file
using Microsoft.EntityFrameworkCore;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Domain.Entities;

namespace TradingApp.Persistence.Repositories;

public sealed class StrategyReviewRepository : IStrategyReviewRepository
{
    private readonly TradingAppDbContext _context;

    public StrategyReviewRepository(TradingAppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(StrategyReview review, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(review);

        await _context.StrategyReviews.AddAsync(review, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<StrategyReview?> GetByStrategyAndRevisionAsync(
        Guid strategyId,
        int revisionNumber,
        CancellationToken cancellationToken = default)
    {
        return await _context.StrategyReviews
            .AsNoTracking()
            .FirstOrDefaultAsync(
                review => review.StrategyId == strategyId && review.RevisionNumber == revisionNumber,
                cancellationToken);
    }

    public async Task DeleteByStrategyAndRevisionAsync(
        Guid strategyId,
        int revisionNumber,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.StrategyReviews
            .FirstOrDefaultAsync(
                review => review.StrategyId == strategyId && review.RevisionNumber == revisionNumber,
                cancellationToken);

        if (existing is not null)
        {
            _context.StrategyReviews.Remove(existing);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
```

##### Pattern References

- `src/TradingApp.Persistence/Repositories/StrategyRevisionRepository.cs` — Repository pattern with DbContext, AsNoTracking for reads, SaveChangesAsync after mutations

---

### Task 1.4: Add DbContext configuration and migration {#task-14-add-dbcontext-configuration-and-migration}

Add `DbSet<StrategyReview>` and inline entity configuration to `TradingAppDbContext.OnModelCreating`, then generate an EF Core migration.

- **Complexity**: Medium
- **Risk Factors**: Must follow inline configuration pattern (no IEntityTypeConfiguration); Guid PKs use ValueGeneratedNever
- **Files**:
  - `src/TradingApp.Persistence/TradingAppDbContext.cs` - Modify (add DbSet + OnModelCreating block)
  - `src/TradingApp.Persistence/Migrations/` - New migration file (auto-generated)
- **Success**:
  - DbSet property added
  - Entity configured with table name, PK, FK to Strategy, unique index on (StrategyId, RevisionNumber)
  - Migration generated successfully via `dotnet ef migrations add AddStrategyReviews`
- **Dependencies**: Task 1.1

#### Implementation Details

```csharp
// src/TradingApp.Persistence/TradingAppDbContext.cs — modification

// Add DbSet property alongside existing DbSets:
public DbSet<StrategyReview> StrategyReviews => Set<StrategyReview>();

// Add configuration block at the end of OnModelCreating, after the StrategyRevision block:
modelBuilder.Entity<StrategyReview>(entity =>
{
    entity.ToTable("StrategyReviews");

    entity.HasKey(review => review.Id);

    entity.Property(review => review.Id)
        .ValueGeneratedNever();

    entity.Property(review => review.StrategyId)
        .IsRequired();

    entity.Property(review => review.RevisionNumber)
        .IsRequired();

    entity.Property(review => review.ReviewMarkdown)
        .IsRequired();

    entity.Property(review => review.ModelName)
        .HasMaxLength(100)
        .IsRequired();

    entity.Property(review => review.CreatedAtUtc)
        .IsRequired();

    entity.HasOne<Strategy>()
        .WithMany()
        .HasForeignKey(review => review.StrategyId)
        .OnDelete(DeleteBehavior.Cascade);

    entity.HasIndex(review => new { review.StrategyId, review.RevisionNumber })
        .IsUnique()
        .HasDatabaseName("IX_StrategyReviews_StrategyId_RevisionNumber");
});
```

After adding the configuration, generate the migration:

```bash
dotnet ef migrations add AddStrategyReviews --project src/TradingApp.Persistence --startup-project src/TradingApp.Api
```

##### Pattern References

- `src/TradingApp.Persistence/TradingAppDbContext.cs` — Inline entity configuration in OnModelCreating (StrategyRevision block at lines 201–245)

---

### Task 1.5: Create LlmReviewOptions configuration class {#task-15-create-llmreviewoptions-configuration-class}

Create a separate options class for the review LLM configuration with `SectionName = "LlmReview"`.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.Application/Abstractions/Configuration/LlmReviewOptions.cs` - New file
- **Success**:
  - Class mirrors LlmOptions structure with SectionName = "LlmReview"
  - Data annotations for validation
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Application/Abstractions/Configuration/LlmReviewOptions.cs — new file
using System.ComponentModel.DataAnnotations;

namespace TradingApp.Application.Abstractions.Configuration;

public sealed class LlmReviewOptions
{
    public const string SectionName = "LlmReview";

    [Required]
    public string Provider { get; set; } = "Gemini";

    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/openai/";

    [Required]
    public string ModelName { get; set; } = "gemini-2.5-flash-lite";

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 60;
}
```

##### Pattern References

- `src/TradingApp.Application/Abstractions/Configuration/LlmOptions.cs` — Exact same structure with different SectionName

---

### Task 1.6: Register repository and configuration in DI {#task-16-register-repository-and-configuration-in-di}

Register the new repository in `PersistenceServiceExtensions` and the new options in `AiServiceExtensions`.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.Persistence/PersistenceServiceExtensions.cs` - Modify (add repository registration)
  - `src/TradingApp.AI/AiServiceExtensions.cs` - Modify (add LlmReviewOptions binding)
- **Success**:
  - `IStrategyReviewRepository` registered as scoped
  - `LlmReviewOptions` bound and validated on start
- **Dependencies**: Task 1.2, Task 1.3, Task 1.5

#### Implementation Details

```csharp
// src/TradingApp.Persistence/PersistenceServiceExtensions.cs — add after IStrategyRevisionRepository line:
services.AddScoped<IStrategyReviewRepository, StrategyReviewRepository>();
```

```csharp
// src/TradingApp.AI/AiServiceExtensions.cs — add after LlmOptions binding block:
services.AddOptions<LlmReviewOptions>()
    .Bind(configuration.GetSection(LlmReviewOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

##### Pattern References

- `src/TradingApp.Persistence/PersistenceServiceExtensions.cs` — Repository registration pattern
- `src/TradingApp.AI/AiServiceExtensions.cs` — Options binding pattern

---

### Task 1.7: Add LlmReview section to appsettings.json {#task-17-add-llmreview-section-to-appsettingsjson}

Add the `LlmReview` configuration section to `appsettings.json` alongside the existing `Llm` section.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.Api/appsettings.json` - Modify
  - `src/TradingApp.Api/appsettings.Development.json` - Modify (if exists)
- **Success**:
  - `LlmReview` section present with all required fields
  - Application starts without validation errors
- **Dependencies**: Task 1.5

#### Implementation Details

```json
// src/TradingApp.Api/appsettings.json — add after "Llm" section:
"LlmReview": {
  "Provider": "Gemini",
  "BaseUrl": "https://generativelanguage.googleapis.com/v1beta/openai/",
  "ModelName": "gemini-2.5-flash-lite",
  "ApiKey": "",
  "TimeoutSeconds": 60
}
```

Note: The ApiKey should be empty in the committed appsettings.json and overridden via user-secrets or environment variables.

---

### Task 1.8: Write domain entity unit tests {#task-18-write-domain-entity-unit-tests}

Write unit tests for the `StrategyReview` entity's factory method following the existing `StrategyRevisionTests` pattern.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `tests/TradingApp.Domain.Tests/Entities/StrategyReviewTests.cs` - New file
- **Success**:
  - Tests cover valid creation, property assignment, all validation guards
  - Tests follow Given_When_Then naming convention
  - All tests pass
- **Dependencies**: Task 1.1

#### Implementation Details

```csharp
// tests/TradingApp.Domain.Tests/Entities/StrategyReviewTests.cs — new file
using TradingApp.Domain.Entities;

namespace TradingApp.Domain.Tests.Entities;

[TestClass]
public sealed class StrategyReviewTests
{
    [TestMethod]
    public void GivenValidInputs_WhenCreate_ThenPropertiesSet()
    {
        var strategyId = Guid.NewGuid();

        var review = StrategyReview.Create(
            strategyId,
            1,
            "## Review\n- Looks good",
            "gemini-2.5-flash-lite");

        review.Id.Should().NotBeEmpty();
        review.StrategyId.Should().Be(strategyId);
        review.RevisionNumber.Should().Be(1);
        review.ReviewMarkdown.Should().Be("## Review\n- Looks good");
        review.ModelName.Should().Be("gemini-2.5-flash-lite");
        review.CreatedAtUtc.Should().BePositive();
    }

    [TestMethod]
    public void GivenEmptyStrategyId_WhenCreate_ThenThrowsArgumentException()
    {
        var act = () => StrategyReview.Create(Guid.Empty, 1, "review", "model");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void GivenInvalidRevisionNumber_WhenCreate_ThenThrowsArgumentOutOfRangeException(int revisionNumber)
    {
        var act = () => StrategyReview.Create(Guid.NewGuid(), revisionNumber, "review", "model");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void GivenInvalidReviewMarkdown_WhenCreate_ThenThrowsArgumentException(string? reviewMarkdown)
    {
        var act = () => StrategyReview.Create(Guid.NewGuid(), 1, reviewMarkdown!, "model");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void GivenInvalidModelName_WhenCreate_ThenThrowsArgumentException(string? modelName)
    {
        var act = () => StrategyReview.Create(Guid.NewGuid(), 1, "review", modelName!);

        act.Should().Throw<ArgumentException>();
    }
}
```

##### Pattern References

- `tests/TradingApp.Domain.Tests/Entities/StrategyRevisionTests.cs` — Test structure, naming convention, DataRow for invalid inputs

---

### Task 1.9: Build and run domain tests {#task-19-build-and-run-domain-tests}

Build the solution and run domain tests to verify Phase 1 changes.

- **Complexity**: Low
- **Risk Factors**: Migration generation or build issues
- **Files**: None (verification only)
- **Success**:
  - Solution builds without errors
  - All domain tests pass
  - Migration applies cleanly
- **Dependencies**: All previous tasks in Phase 1

Run:
```bash
dotnet build
dotnet test tests/TradingApp.Domain.Tests --filter "FullyQualifiedName~StrategyReview"
```

## Phase Success Criteria

- `StrategyReview` entity exists with validated factory method
- Repository interface and EF implementation are registered
- DbContext has entity configuration and migration is generated
- `LlmReviewOptions` is configured and validated on startup
- `LlmReview` section exists in appsettings.json
- All domain entity unit tests pass
