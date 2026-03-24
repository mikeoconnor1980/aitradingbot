---
applyTo: "**/src/**/*.cs,**/*.csproj"
---


## Bounded Contexts

Each feature area is organized as a bounded context with its own folder structure:

```
BoundedContext/
├── Entities/           # Domain entities and aggregate roots
├── ValueObjects/       # Value objects
├── DomainEvents/       # Domain events
├── Services/           # Domain services
├── Commands/           # CQRS commands with handlers
├── Queries/            # CQRS queries with handlers
├── Models/             # DTOs
├── MappingProfiles/    # AutoMapper profiles
├── IntegrationEvents/  # Integration events with handlers
└── Jobs/               # Background jobs
```

## Core Layer Patterns

### AggregateRoot

Standard base class for domain aggregate roots:

```csharp
public abstract class AggregateRoot : EntityBase
{
    public IReadOnlyCollection<DomainEvent> DomainEvents { get; }
    public void AddDomainEvent(DomainEvent eventItem);
    public void ClearDomainEvents();
}
```

### Value Objects

- Inherit from `ValueObject`
- Immutable with private setters
- Override `GetEqualityComponents()` for value equality

### Domain Events

Domain events are records dispatched via MediatR:

- **`DomainEvent`** - Base class for synchronous domain events handled within the same transaction:
```csharp
  public abstract record DomainEvent : INotification
  {
      public Guid EventId { get; private set; } = Guid.NewGuid();
  }
```


## Core Layer Patterns

<!-- DOT-CUSTOM-START:dotnet-core -->
<!-- DOT-CUSTOM-END:dotnet-core -->

## Application Layer Patterns

<!-- DOT-CUSTOM-START:dotnet-application -->
<!-- DOT-CUSTOM-END:dotnet-application -->

<!-- DOT-CUSTOM-START:dotnet-mapping -->
### DTOs and Mapping

**AutoMapper** is used for mapping between domain entities and DTOs. Mapping profiles are defined in the Application layer, and should be organized by bounded context.

Create DTOs in `DTS.UKCT.Efiling.Application/{BoundedContext}/Models/`:

```csharp
public sealed class MyEntityDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
}

public sealed class MyEntityCreateDto
{
    public string Name { get; set; }
}
```

Create mapping profile in `DTS.UKCT.Efiling.Application/{BoundedContext}/MappingProfiles/`:

```csharp
internal sealed class MyEntityProfile : Profile
{
    public MyEntityProfile()
    {
        CreateMap<MyEntityReadModel, MyEntityDto>();
    }
}
```
<!-- DOT-CUSTOM-END:dotnet-mapping -->


### CQRS Commands

Three command types are available:

```csharp
// For commands that don't return a value
public abstract record Command : IRequest<Unit>;

// For commands that return a typed response
public abstract record Command<T> : IRequest<T>;

// For create commands that return the new entity's Guid
public abstract record CreateCommand : IRequest<Guid>;
```

**Usage examples:**

```csharp
// CreateCommand - returns Guid of created entity
public sealed record CreateEntityCommand(Guid WorkAreaId, EntityCreateDto Entity, AppIdentity Identity) : CreateCommand;

public sealed class CreateEntityCommandHandler : CreateCommandHandler<CreateEntityCommand>
{
    // Handler implementation in same file
}

// Command - no return value (update/delete operations)
public sealed record UpdateEntityCommand(Guid EntityId, EntityUpdateDto Entity, AppIdentity Identity) : Command;

public sealed class UpdateEntityCommandHandler : CommandHandler<UpdateEntityCommand>
{
    // Handler implementation
}

// Command<T> - returns typed response
public sealed record ValidateEntityCommand(Guid EntityId) : Command<ValidationResult>;

public sealed class ValidateEntityCommandHandler : CommandHandler<ValidateEntityCommand, ValidationResult>
{
    // Handler implementation
}
```

### CQRS Queries

```csharp
public sealed record GetEntitiesQuery(Guid WorkAreaId, AppIdentity Identity) : Query<List<EntityDto>>;

public sealed class GetEntitiesQueryHandler : QueryHandler<GetEntitiesQuery, List<EntityDto>>
{
    // Handler implementation in same file
}
```


### Application Services

Two patterns for service interfaces:

1. **Interface in same file** - For services implemented in the Application layer:
   ```csharp
   // In Application/{BoundedContext}/Services/
   public interface IMyService
   {
       Task ProcessAsync(Guid entityId);
   }

   public sealed class MyService : IMyService
   {
       // Implementation
   }
   ```

2. **Interface in Abstractions** - For services implemented in Infrastructure (Clean Architecture dependency inversion):
```csharp
// In Application/Abstractions/Services/
public interface IExternalApiService
{
    Task<ExternalData> GetDataAsync(string id);
}

// Implementation in Infrastructure/Services/
public sealed class ExternalApiService : IExternalApiService
{
    // Implementation with HttpClient, etc.
}
```

### Repository Interfaces

<!-- DOT-CUSTOM-START:dotnet-repositories -->
**Core repositories** in `DTS.UKCT.Efiling.Application/Abstractions/Repositories/`:
- `IRepository<T>` - For standard AggregateRoots (CRUD operations)
- `IUnitOfWork` - For committing changes and dispatching domain events

#### Repository Method Selection

**Prefer `GetByIdAsync` over `GetAll`** when retrieving a single entity by its identifier. This is more efficient and expresses intent clearly.

```csharp
// Preferred - direct lookup by ID
var entity = await _repository.GetByIdAsync(entityId);

// Avoid - filtering a collection when you know the ID
var entity = await _repository.GetAll().FirstOrDefaultAsync(e => e.Id == entityId);
```

### Using GetAll with Entity Framework Async Extensions

The `GetAll` method returns an `IQueryable<T>`, allowing you to compose queries with LINQ and execute them asynchronously using Entity Framework Core extension methods.

**Required namespace:**
```csharp
using Microsoft.EntityFrameworkCore;
```

**Common async extension methods:**

```csharp
// Get first matching entity or null
var metadata = await _repository.GetAll()
    .FirstOrDefaultAsync(e => e.ScenarioId == scenarioId);

// Get filtered list
var entities = await _repository.GetAll()
    .Where(x => x.WorkAreaId == workAreaId && !x.IsDeleted)
    .ToListAsync();

// Check existence
var hasData = await _repository.GetAll()
    .Where(x => x.ScenarioId == scenarioId)
    .AnyAsync();

// Count matching records
var count = await _repository.GetAll()
    .Where(x => x.Status == Status.Active)
    .CountAsync();
```

**Important:** Always use EF Core async extensions (`ToListAsync`, `FirstOrDefaultAsync`, `AnyAsync`, `CountAsync`) rather than calling `.ToList()` or `.FirstOrDefault()` synchronously, or awaiting `GetAll()` directly.

#### Change Tracking with GetAll

The `GetAll` method accepts an optional `bool trackChanges` parameter (default: `false`):

```csharp
// Read-only queries - no change tracking (better performance)
var entities = await _repository.GetAll().ToListAsync();

// When entities will be mutated and saved - enable change tracking
var entities = await _repository.GetAll(trackChanges: true).ToListAsync();
foreach (var entity in entities)
{
    entity.UpdateStatus(newStatus);
}
await _unitOfWork.CommitAsync();
```

**When to use each:**

| Scenario | trackChanges | Reason |
|----------|--------------|--------|
| Query handlers returning DTOs | `false` | Read-only, mapped to DTOs via AutoMapper |
| Command handlers updating entities | `true` | EF Core needs to track changes for `SaveChanges()` |
| Bulk read operations for reports | `false` | Performance optimization for large datasets |
| Entities loaded then modified in same request | `true` | Changes must be detected and persisted |

**Performance note:** Disabling change tracking (`trackChanges: false`) improves query performance by skipping EF Core's identity resolution and change detection. Always prefer this for read-only operations.
<!-- DOT-CUSTOM-END:dotnet-repositories -->

## Infrastructure Layer Patterns

<!-- DOT-CUSTOM-START:dotnet-infrastructure -->
The Infrastructure project contains EF Core configurations, repository implementations, and external service integrations.

### Entity Framework Configurations

Tables are named based on the EF Core `EfilingDbContext.cs` DbSet properties. Pluralized names are used for tables.

Place code first configurations in `DTS.UKCT.Efiling.Infrastructure/Configurations/`:

```csharp
internal sealed class MyEntityConfiguration : IEntityTypeConfiguration<MyEntity>
{
    public void Configure(EntityTypeBuilder<MyEntity> builder)
    {
        // Configure properties
        builder.Property(e => e.Name)
            .HasColumnType("varchar(256)")
            .IsRequired();

        // Configure value objects as owned types
        builder.OwnsOne(e => e.Address, address =>
        {
            address.Property(a => a.Street)
                   .HasColumnType("varchar(128)")
                   .IsRequired();
            address.Property(a => a.City)
                   .HasColumnType("varchar(128)")
                   .IsRequired();
            address.Property(a => a.PostCode)
                   .HasColumnType("varchar(20)")
                   .IsRequired();
        }).Navigation(e => e.Address).IsRequired();

        // Configure relationships
        builder.HasMany(e => e.Children)
            .WithOne()
            .HasForeignKey(c => c.ParentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Specify precision for decimal properties
        builder.Property(e => e.TaxRate)
               .HasPrecision(19, 8);

        // JSON column example
        builder.Property(e => e.Metadata)
               .HasColumnType("nvarchar(max)")
               .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions)null));

        // Indexes
        builder.HasIndex(e => e.WorkAreaId);
        builder.HasIndex(e => new { e.WorkAreaId, e.Code }).IsUnique();
    }
}
```

### DbContext Registration

Add new DbSets to `EfilingDbContext.cs`:

```csharp
public sealed class EfilingDbContext : DbContext
{
    // Pluralized named for entities
    public DbSet<MyEntity> MyEntities { get; set; }
}
```

### External Service Implementations

Place service implementations in `DTS.UKCT.Efiling.Infrastructure/Services/`:

```csharp
public sealed class ExternalApiService : IExternalApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ExternalApiService> _logger;

    public ExternalApiService(HttpClient httpClient, ILogger<ExternalApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ExternalData> GetDataAsync(string id, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync($"api/data/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ExternalData>(cancellationToken);
    }
}
```
<!-- DOT-CUSTOM-END:dotnet-infrastructure -->

<!-- DOT-CUSTOM-START:dotnet-migrations -->
## EF Core Database Migrations

Entity Framework Core manages database schema migrations. Migrations are stored in the **DTS.UKCT.Efiling.Migrations** project under a `Migrations` folder.

### Creating a New Migration

Generate a migration using the EF CLI from the `DTS.UKCT.Efiling.Migrations` project:
```bash
dotnet ef migrations add <migration-name>
```

MUST be run from the `DTS.UKCT.Efiling.Migrations` project directory. --project and --startup-project MUST NOT be specified, as a `DbContextOptionsFactory.cs` is used to ensure the correct connection string and DbContext is used for migrations.
<!-- DOT-CUSTOM-END:dotnet-migrations -->

## Exception Handling

Exception handling and logging is managed in the top-level API/Integration layer using middleware. Application and Core layers should throw exceptions as needed without handling them internally, unless specific logging or handling is required.

<!-- DOT-CUSTOM-START:dotnet-exception-handling -->
Throwing a [NotFoundException.cs](../../src/DTS.UKCT.Efiling.Core/Abstractions/Exceptions/NotFoundException.cs)  when an entity is not found is preferred over returning null, to clearly indicate the error condition.

A custom [DomainException.cs](../../src/DTS.UKCT.Efiling.Core/Abstractions/Exceptions/DomainException.cs) can be created for domain-specific errors. This will be automatically mapped to a 400 Bad Request response by the middleware.
<!-- DOT-CUSTOM-END:dotnet-exception-handling -->