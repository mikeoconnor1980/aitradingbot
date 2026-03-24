---
applyTo: "**/*.cs"
---

# C# Coding Standards

## General Principles

- Use latest LTS version of .NET (C#) for all core services and APIs
- Keep each .NET class or interface in its own file
  - **Exception**: EventHandlers, CommandHandlers, and QueryHandlers are kept in the same file as the thing they handle
  - **Exception**: Interfaces for Application layer services can be kept in the same file as the implementation. Interfaces are used for these services to facilitate mocking in unit tests and dependency injection.
- C# classes should be `sealed` if possible
- C# classes should have a static factory method for creating instances (e.g., `Create(...)`) if they have a non-trivial constructor or validation. The constructor should be private to enforce the use of the factory method.
- Don't use regions in C# classes
- Prefer Terminal over PowerShell for command execution

## Naming Conventions

- Use PascalCase for class names, method names, and public properties
- Use camelCase with underscore prefix for private fields (e.g., `_repository`)
- Use Given_When_Then style for unit test names

## Class Structure

<!-- DOT-CUSTOM-START:csharp-class-structure -->
```csharp
public sealed class MyEntity : AggregateRoot
{
    // Private fields first
    private readonly IService _service;

    // Private constructor for factory method pattern
    private MyEntity(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    // Parameterless constructor for EF/deserialization
    private MyEntity()
    {
        // Required for deserialization
    }

    // Static factory method (if needed for validation or complex construction)
    public static MyEntity Create(Guid id, string name)
    {
        Guard.Against.NullOrEmpty(name, nameof(name));
        return new MyEntity(id, name);
    }

    // Public properties
    public string Name { get; private set; }

    // Public methods
    public void UpdateName(string name)
    {
        Guard.Against.NullOrEmpty(name, nameof(name));
        Name = name;
        PublishUpdates();
    }

    // Private methods below public methods that use them
    private void PublishUpdates()
    {
        // ...
    }
}
```
<!-- DOT-CUSTOM-END:csharp-class-structure -->

## Guard Classes

- Use Guard classes for validation logic
- Common Guards are located in `DTS.UKCT.Efiling.Core/Abstractions/Guards/` folder
- Common guards include:
  - `Guard.Against.NullOrEmpty(value, paramName)`
  - `Guard.Against.NotFound(entity, message)`
  - `Guard.Against.Exists(entity, message)`
  - `Guard.Against.LengthNotEqualTo(value, length, paramName)`
- Bounded Context-specific guards can be created in the respective Bounded Context's Guards folder
- Guards do not typically need to be used for the Enum types, as the validation for those is typically handled by the model binding in the API layer. However, if there is a need for additional validation logic beyond what model binding provides, then guards can be used for Enums as well.

### Creating New Guard Extensions

Add new guard methods as extension methods in the Guards folder:

```csharp
public static class GuardExtensions
{
    public static void CustomValidation(this IGuardClause guardClause, string value, string paramName)
    {
        if (!IsValid(value))
        {
            throw new DomainException($"Invalid {paramName}: {value}");
        }
    }
}
```

## Dependency Injection

- Inject dependencies via constructor
- Use interfaces for dependencies to enable testing
- Autofac with modules is used for configuring DI in each project layer. The modules are located in the `AutofacModules/` folder of each project.
    
## Async/Await

- Use async/await for all I/O operations
- Suffix async methods with `Async`
- Always pass CancellationToken where available

## Configuration
- Use `IOptions<T>` for configuration settings
- Configuration values are stored in Key Vault for dynamic retrieval and security
- appsettings.json should only contain non-sensitive defaults
- appsettings.Development.json can contain development-specific overrides
    - The most important setting is the Key Vault name, so that the environment configuration can be dynamically loaded from Key Vault using the user's managed identity

## Cross-Cutting Concerns

The `DTS.UKCT.Efiling.Hosting` project contains shared infrastructure:
- `Job` base class for background jobs
- Common hosting configuration
- Environment setup utilities