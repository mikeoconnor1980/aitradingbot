<!-- markdownlint-disable-file -->

# Task Details: F1 — Configuration & Connectivity

## Phase 1: Solution Scaffolding, Base Classes, and Test Infrastructure

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — sealed classes, static factory methods, `_camelCase` private fields, CancellationToken, IOptions
- `.github/instructions/dotnet-architecture.instructions.md` — CQRS Command/Query records, handler base classes, bounded context folder structure, repository interfaces
- `.github/instructions/api-controllers.instructions.md` — ApiController base, IMediator + IdentityService, Envelope, CreatedResultEnvelope, ProducesResponseType
- `.github/instructions/testing.instructions.md` — MSTest + Moq + FluentAssertions ≤v6, BaseControllerTests, builder pattern, Usings.cs global usings, MockRepositoryFactory
- `.agent-context/0-knowledge/06-project-structure.md` — TradingApp.sln, 6 projects: Domain, Application, Infrastructure, Persistence, Api, Worker

## Design References

- The CQRS base classes enable the MediatR dispatch pattern: controllers never contain business logic, they dispatch to handlers via `Mediator.Send(...)`
- The Envelope pattern wraps error responses consistently across all endpoints: `Envelope` for errors, `CreatedResultEnvelope` for POST create responses
- BaseControllerTests provides a test host that wires up MediatR, DI, and mock repositories for integration-style tests
- IdentityService is a stub in this POC — auth is out of scope

---

### Task 1.1: Create solution and all project scaffolding {#task-11-create-solution-and-all-project-scaffolding}

Create the .NET solution with all 6 projects and establish the project reference graph. Domain ← Application ← Infrastructure, Application ← Persistence, Api references Application + Infrastructure, Worker references Application + Infrastructure.

- **Complexity**: Medium
- **Risk Factors**: Many dotnet CLI commands; project references must match clean architecture layering
- **Files**:
  - `TradingApp.sln` — Solution file at repo root
  - `src/TradingApp.Domain/TradingApp.Domain.csproj` — .NET 8 class library (core domain)
  - `src/TradingApp.Application/TradingApp.Application.csproj` — .NET 8 class library (CQRS, services)
  - `src/TradingApp.Infrastructure/TradingApp.Infrastructure.csproj` — .NET 8 class library (external integrations)
  - `src/TradingApp.Persistence/TradingApp.Persistence.csproj` — .NET 8 class library (EF Core, future)
  - `src/TradingApp.Api/TradingApp.Api.csproj` — .NET 8 Web API
  - `src/TradingApp.Worker/TradingApp.Worker.csproj` — .NET 8 Worker Service
- **Success**:
  - `dotnet build TradingApp.sln` succeeds
  - Project reference graph follows clean architecture (inner layers have no outward dependencies)
- **Dependencies**: None

#### Implementation Details

```bash
# Create solution
dotnet new sln -n TradingApp

# Create all projects
dotnet new classlib -n TradingApp.Domain -o src/TradingApp.Domain --framework net8.0
dotnet new classlib -n TradingApp.Application -o src/TradingApp.Application --framework net8.0
dotnet new classlib -n TradingApp.Infrastructure -o src/TradingApp.Infrastructure --framework net8.0
dotnet new classlib -n TradingApp.Persistence -o src/TradingApp.Persistence --framework net8.0
dotnet new webapi -n TradingApp.Api -o src/TradingApp.Api --framework net8.0 --no-openapi
dotnet new worker -n TradingApp.Worker -o src/TradingApp.Worker --framework net8.0

# Add all to solution
dotnet sln add src/TradingApp.Domain/TradingApp.Domain.csproj
dotnet sln add src/TradingApp.Application/TradingApp.Application.csproj
dotnet sln add src/TradingApp.Infrastructure/TradingApp.Infrastructure.csproj
dotnet sln add src/TradingApp.Persistence/TradingApp.Persistence.csproj
dotnet sln add src/TradingApp.Api/TradingApp.Api.csproj
dotnet sln add src/TradingApp.Worker/TradingApp.Worker.csproj

# Project references (clean architecture layering)
# Application depends on Domain
dotnet add src/TradingApp.Application/TradingApp.Application.csproj reference src/TradingApp.Domain/TradingApp.Domain.csproj

# Infrastructure depends on Application (dependency inversion)
dotnet add src/TradingApp.Infrastructure/TradingApp.Infrastructure.csproj reference src/TradingApp.Application/TradingApp.Application.csproj

# Persistence depends on Application + Domain
dotnet add src/TradingApp.Persistence/TradingApp.Persistence.csproj reference src/TradingApp.Application/TradingApp.Application.csproj
dotnet add src/TradingApp.Persistence/TradingApp.Persistence.csproj reference src/TradingApp.Domain/TradingApp.Domain.csproj

# Api depends on Application + Infrastructure
dotnet add src/TradingApp.Api/TradingApp.Api.csproj reference src/TradingApp.Application/TradingApp.Application.csproj
dotnet add src/TradingApp.Api/TradingApp.Api.csproj reference src/TradingApp.Infrastructure/TradingApp.Infrastructure.csproj

# Worker depends on Application + Infrastructure
dotnet add src/TradingApp.Worker/TradingApp.Worker.csproj reference src/TradingApp.Application/TradingApp.Application.csproj
dotnet add src/TradingApp.Worker/TradingApp.Worker.csproj reference src/TradingApp.Infrastructure/TradingApp.Infrastructure.csproj

# Add MediatR to Application project
dotnet add src/TradingApp.Application/TradingApp.Application.csproj package MediatR

# Add Nethereum.Signer to Infrastructure project
dotnet add src/TradingApp.Infrastructure/TradingApp.Infrastructure.csproj package Nethereum.Signer
```

After scaffolding, clean up template-generated placeholder files:
- Remove `src/TradingApp.Domain/Class1.cs`
- Remove `src/TradingApp.Application/Class1.cs`
- Remove `src/TradingApp.Infrastructure/Class1.cs`
- Remove `src/TradingApp.Persistence/Class1.cs`
- Remove template WeatherForecast controller/model from Api if present
- Remove template Worker.cs from Worker if you want a clean shell (or keep it as placeholder)

##### Pattern References

- `.agent-context/0-knowledge/06-project-structure.md` — solution name TradingApp.sln, 6 project names
- `.github/instructions/dotnet-architecture.instructions.md` — clean architecture layering

---

### Task 1.2: Create CQRS base records and handler base classes {#task-12-create-cqrs-base-records-and-handler-base-classes}

Create the MediatR CQRS base records (Command, CreateCommand, Query) and handler base classes in the Application project.

- **Complexity**: Medium
- **Risk Factors**: Handler base classes must be abstract and work with MediatR's `IRequestHandler<TRequest, TResponse>` pattern
- **Files**:
  - `src/TradingApp.Application/Abstractions/Commands/Command.cs` — Base command records
  - `src/TradingApp.Application/Abstractions/Commands/CommandHandler.cs` — Command handler bases
  - `src/TradingApp.Application/Abstractions/Queries/Query.cs` — Base query record
  - `src/TradingApp.Application/Abstractions/Queries/QueryHandler.cs` — Query handler base
- **Success**:
  - `Command`, `Command<T>`, `CreateCommand` records compile and are usable as MediatR requests
  - `CommandHandler<TCmd>`, `CommandHandler<TCmd, TResult>`, `CreateCommandHandler<TCmd>`, `QueryHandler<TQuery, TResult>` compile
- **Dependencies**: Task 1.1 (MediatR package installed)

#### Implementation Details

```csharp
// src/TradingApp.Application/Abstractions/Commands/Command.cs — new file
using MediatR;

namespace TradingApp.Application.Abstractions.Commands;

/// <summary>
/// Base command that returns Unit (no value).
/// </summary>
public abstract record Command : IRequest<Unit>;

/// <summary>
/// Base command that returns a typed response.
/// </summary>
public abstract record Command<T> : IRequest<T>;

/// <summary>
/// Base command for create operations that return the new entity's Guid.
/// </summary>
public abstract record CreateCommand : IRequest<Guid>;
```

```csharp
// src/TradingApp.Application/Abstractions/Commands/CommandHandler.cs — new file
using MediatR;

namespace TradingApp.Application.Abstractions.Commands;

/// <summary>
/// Base handler for commands that return Unit.
/// </summary>
public abstract class CommandHandler<TCommand> : IRequestHandler<TCommand, Unit>
    where TCommand : Command
{
    public abstract Task<Unit> Handle(TCommand request, CancellationToken cancellationToken);
}

/// <summary>
/// Base handler for commands that return a typed response.
/// </summary>
public abstract class CommandHandler<TCommand, TResult> : IRequestHandler<TCommand, TResult>
    where TCommand : Command<TResult>
{
    public abstract Task<TResult> Handle(TCommand request, CancellationToken cancellationToken);
}

/// <summary>
/// Base handler for create commands that return a Guid.
/// </summary>
public abstract class CreateCommandHandler<TCommand> : IRequestHandler<TCommand, Guid>
    where TCommand : CreateCommand
{
    public abstract Task<Guid> Handle(TCommand request, CancellationToken cancellationToken);
}
```

```csharp
// src/TradingApp.Application/Abstractions/Queries/Query.cs — new file
using MediatR;

namespace TradingApp.Application.Abstractions.Queries;

/// <summary>
/// Base query that returns a typed response.
/// </summary>
public abstract record Query<T> : IRequest<T>;
```

```csharp
// src/TradingApp.Application/Abstractions/Queries/QueryHandler.cs — new file
using MediatR;

namespace TradingApp.Application.Abstractions.Queries;

/// <summary>
/// Base handler for queries that return a typed response.
/// </summary>
public abstract class QueryHandler<TQuery, TResult> : IRequestHandler<TQuery, TResult>
    where TQuery : Query<TResult>
{
    public abstract Task<TResult> Handle(TQuery request, CancellationToken cancellationToken);
}
```

##### Pattern References

- `.github/instructions/dotnet-architecture.instructions.md` — CQRS Command, CreateCommand, Command<T>, Query<T> patterns; handler base classes: CreateCommandHandler, CommandHandler, QueryHandler

---

### Task 1.3: Create Envelope and CreatedResultEnvelope response wrappers {#task-13-create-envelope-and-createdresultenvelope-response-wrappers}

Create the standard API response wrapper classes used by all controllers for consistent error handling and create responses.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.Api/Infrastructure/Envelope.cs` — Error response wrapper
  - `src/TradingApp.Api/Infrastructure/CreatedResultEnvelope.cs` — Create response wrapper
- **Success**:
  - Envelope wraps error messages consistently
  - CreatedResultEnvelope returns the new entity ID on POST create
- **Dependencies**: Task 1.1

#### Implementation Details

```csharp
// src/TradingApp.Api/Infrastructure/Envelope.cs — new file
namespace TradingApp.Api.Infrastructure;

/// <summary>
/// Standard error response envelope for consistent API error formatting.
/// </summary>
public sealed class Envelope
{
    public string ErrorMessage { get; }
    public DateTime Timestamp { get; }

    public Envelope(string errorMessage)
    {
        ErrorMessage = errorMessage;
        Timestamp = DateTime.UtcNow;
    }
}
```

```csharp
// src/TradingApp.Api/Infrastructure/CreatedResultEnvelope.cs — new file
namespace TradingApp.Api.Infrastructure;

/// <summary>
/// Response envelope for POST create operations, returning the new entity ID.
/// </summary>
public sealed class CreatedResultEnvelope
{
    public Guid Id { get; }

    public CreatedResultEnvelope(Guid id)
    {
        Id = id;
    }
}
```

##### Pattern References

- `.github/instructions/api-controllers.instructions.md` — `Envelope` for error responses, `CreatedResultEnvelope` for 201 Created responses

---

### Task 1.4: Create ApiController base class {#task-14-create-apicontroller-base-class}

Create the abstract ApiController base that all controllers inherit from. It holds `IMediator` and a placeholder `IdentityService` (auth stub for POC).

- **Complexity**: Medium
- **Risk Factors**: IdentityService is a stub — no real auth in POC. Must still follow the pattern for future features.
- **Files**:
  - `src/TradingApp.Api/Infrastructure/ApiController.cs` — Abstract base controller
  - `src/TradingApp.Api/Infrastructure/IdentityService.cs` — Identity stub
  - `src/TradingApp.Application/Abstractions/Identity/AppIdentity.cs` — Identity model
- **Success**:
  - Controllers can inherit from ApiController and access `Mediator` and `IdentityService`
  - Pattern matches the api-controllers instruction file
- **Dependencies**: Task 1.1

#### Implementation Details

```csharp
// src/TradingApp.Application/Abstractions/Identity/AppIdentity.cs — new file
namespace TradingApp.Application.Abstractions.Identity;

/// <summary>
/// Represents the current user's identity, passed to commands and queries.
/// </summary>
public sealed class AppIdentity
{
    public string UserId { get; }
    public string Email { get; }

    public AppIdentity(string userId, string email)
    {
        UserId = userId;
        Email = email;
    }

    /// <summary>
    /// Creates a system identity for operations not tied to a user (e.g. background jobs).
    /// </summary>
    public static AppIdentity System => new("system", "system@tradingapp.local");
}
```

```csharp
// src/TradingApp.Api/Infrastructure/IdentityService.cs — new file
using TradingApp.Application.Abstractions.Identity;

namespace TradingApp.Api.Infrastructure;

/// <summary>
/// Stub identity service for POC — no authentication.
/// Returns a hardcoded developer identity. Replace with real auth in production.
/// </summary>
public sealed class IdentityService
{
    public AppIdentity Identity { get; } = new("dev-user", "developer@tradingapp.local");
}
```

```csharp
// src/TradingApp.Api/Infrastructure/ApiController.cs — new file
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace TradingApp.Api.Infrastructure;

/// <summary>
/// Abstract base controller providing MediatR and identity access to all API controllers.
/// </summary>
[ApiController]
[Produces("application/json")]
public abstract class ApiController : ControllerBase
{
    protected IMediator Mediator { get; }
    protected IdentityService IdentityService { get; }

    protected ApiController(IMediator mediator, IdentityService identityService)
    {
        Mediator = mediator;
        IdentityService = identityService;
    }
}
```

##### Pattern References

- `.github/instructions/api-controllers.instructions.md` — `ApiController` base with `IMediator` and `IdentityService` injected via constructor; `[ApiController]`, `[Produces]` attributes
- `.github/instructions/csharp.instructions.md` — sealed classes where possible (IdentityService, AppIdentity), abstract for base class

---

### Task 1.5: Configure MediatR and Program.cs shell {#task-15-configure-mediatr-and-programcs-shell}

Set up the minimal Program.cs with MediatR registration, IdentityService DI, and controller mapping. This is the shell that Phase 2 will add Hyperliquid-specific config to.

- **Complexity**: Low
- **Risk Factors**: MediatR assembly scanning must find handlers in the Application project
- **Files**:
  - `src/TradingApp.Api/Program.cs` — Replace template content with minimal shell
- **Success**:
  - MediatR is registered and can resolve handlers from TradingApp.Application assembly
  - IdentityService is registered as singleton
  - Controllers are mapped
  - App starts without errors (even without Hyperliquid config — that's added in Phase 2)
- **Dependencies**: Task 1.2, Task 1.4

#### Implementation Details

```csharp
// src/TradingApp.Api/Program.cs — replace template content
using TradingApp.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// MediatR — scan Application assembly for handlers
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<TradingApp.Application.Abstractions.Commands.Command>());

// Identity stub (replace with real auth service in production)
builder.Services.AddSingleton<IdentityService>();

builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

app.Run();
```

> **Note**: This is the minimal shell. Phase 2 (Task 2.6) will add Hyperliquid config binding, signer registration, HttpClient for REST client, CORS, and fail-fast validation. Keeping Phase 1's Program.cs minimal ensures the scaffolding compiles and starts independently.

##### Pattern References

- `.github/instructions/api-controllers.instructions.md` — MediatR dispatch from controllers
- `.github/instructions/dotnet-architecture.instructions.md` — handler registration from Application assembly

---

### Task 1.6: Create test projects with global usings and test infrastructure {#task-16-create-test-projects-with-global-usings-and-test-infrastructure}

Create MSTest test projects mirroring the source structure. Include global Usings.cs files, a FakeHttpMessageHandler utility, and the foundation for BaseControllerTests.

- **Complexity**: High
- **Risk Factors**: BaseControllerTests must wire up WebApplicationFactory with MediatR and mock services; FluentAssertions must be ≤v6
- **Files**:
  - `tests/TradingApp.Domain.Tests/TradingApp.Domain.Tests.csproj` — Domain test project (empty shell)
  - `tests/TradingApp.Application.Tests/TradingApp.Application.Tests.csproj` — Application test project (empty shell)
  - `tests/TradingApp.Infrastructure.Tests/TradingApp.Infrastructure.Tests.csproj` — Infrastructure test project
  - `tests/TradingApp.Api.Tests/TradingApp.Api.Tests.csproj` — API test project
  - `tests/TradingApp.Domain.Tests/Usings.cs` — Global usings
  - `tests/TradingApp.Application.Tests/Usings.cs` — Global usings
  - `tests/TradingApp.Infrastructure.Tests/Usings.cs` — Global usings
  - `tests/TradingApp.Api.Tests/Usings.cs` — Global usings
  - `tests/TradingApp.Api.Tests/Infrastructure/FakeHttpMessageHandler.cs` — Test HTTP handler
  - `tests/TradingApp.Api.Tests/Infrastructure/BaseControllerTests.cs` — Controller test base class
- **Success**:
  - All test projects compile
  - Global usings include FluentAssertions, MSTest, and Moq
  - BaseControllerTests creates a WebApplicationFactory-based test client
  - FakeHttpMessageHandler allows mocking HTTP responses in tests
- **Dependencies**: Task 1.1, Task 1.5

#### Implementation Details

Create test projects via CLI:

```bash
# Create test projects
dotnet new mstest -n TradingApp.Domain.Tests -o tests/TradingApp.Domain.Tests --framework net8.0
dotnet new mstest -n TradingApp.Application.Tests -o tests/TradingApp.Application.Tests --framework net8.0
dotnet new mstest -n TradingApp.Infrastructure.Tests -o tests/TradingApp.Infrastructure.Tests --framework net8.0
dotnet new mstest -n TradingApp.Api.Tests -o tests/TradingApp.Api.Tests --framework net8.0

# Add to solution
dotnet sln add tests/TradingApp.Domain.Tests/TradingApp.Domain.Tests.csproj
dotnet sln add tests/TradingApp.Application.Tests/TradingApp.Application.Tests.csproj
dotnet sln add tests/TradingApp.Infrastructure.Tests/TradingApp.Infrastructure.Tests.csproj
dotnet sln add tests/TradingApp.Api.Tests/TradingApp.Api.Tests.csproj

# Project references for test projects
dotnet add tests/TradingApp.Domain.Tests reference src/TradingApp.Domain/TradingApp.Domain.csproj
dotnet add tests/TradingApp.Application.Tests reference src/TradingApp.Application/TradingApp.Application.csproj
dotnet add tests/TradingApp.Infrastructure.Tests reference src/TradingApp.Infrastructure/TradingApp.Infrastructure.csproj
dotnet add tests/TradingApp.Api.Tests reference src/TradingApp.Api/TradingApp.Api.csproj

# Add test packages to all test projects
# FluentAssertions must be <= v6
foreach ($proj in @(
    "tests/TradingApp.Domain.Tests/TradingApp.Domain.Tests.csproj",
    "tests/TradingApp.Application.Tests/TradingApp.Application.Tests.csproj",
    "tests/TradingApp.Infrastructure.Tests/TradingApp.Infrastructure.Tests.csproj",
    "tests/TradingApp.Api.Tests/TradingApp.Api.Tests.csproj"
)) {
    dotnet add $proj package FluentAssertions --version 6.12.2
    dotnet add $proj package Moq
}

# Add WebApplicationFactory to Api.Tests
dotnet add tests/TradingApp.Api.Tests/TradingApp.Api.Tests.csproj package Microsoft.AspNetCore.Mvc.Testing
```

Remove template-generated test files (`UnitTest1.cs`) from each test project.

```csharp
// tests/TradingApp.Domain.Tests/Usings.cs — new file (replace template)
global using FluentAssertions;
global using Microsoft.VisualStudio.TestTools.UnitTesting;
global using Moq;
```

```csharp
// tests/TradingApp.Application.Tests/Usings.cs — new file (replace template)
global using FluentAssertions;
global using Microsoft.VisualStudio.TestTools.UnitTesting;
global using Moq;
```

```csharp
// tests/TradingApp.Infrastructure.Tests/Usings.cs — new file (replace template)
global using FluentAssertions;
global using Microsoft.VisualStudio.TestTools.UnitTesting;
global using Moq;
```

```csharp
// tests/TradingApp.Api.Tests/Usings.cs — new file (replace template)
global using FluentAssertions;
global using Microsoft.VisualStudio.TestTools.UnitTesting;
global using Moq;
global using System.Net;
```

```csharp
// tests/TradingApp.Api.Tests/Infrastructure/FakeHttpMessageHandler.cs — new file
namespace TradingApp.Api.Tests.Infrastructure;

/// <summary>
/// Fake HTTP message handler for unit testing HTTP client calls without real network I/O.
/// Supports returning a canned response or throwing an exception.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpResponseMessage? _response;
    private readonly Exception? _exception;

    public FakeHttpMessageHandler(HttpResponseMessage response)
    {
        _response = response;
    }

    public FakeHttpMessageHandler(Exception exception)
    {
        _exception = exception;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_exception is not null)
            throw _exception;

        return Task.FromResult(_response!);
    }
}
```

```csharp
// tests/TradingApp.Api.Tests/Infrastructure/BaseControllerTests.cs — new file
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace TradingApp.Api.Tests.Infrastructure;

/// <summary>
/// Base class for API controller integration tests.
/// Provides a test client via WebApplicationFactory and helpers for assertions.
/// Subclasses can override ConfigureTestServices to register mocks.
/// </summary>
public abstract class BaseControllerTests
{
    private WebApplicationFactory<Program>? _factory;

    protected HttpClient GetTestClient()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    ConfigureTestServices(services);
                });
            });

        return _factory.CreateClient();
    }

    /// <summary>
    /// Override to register mock services for a specific test class.
    /// </summary>
    protected virtual void ConfigureTestServices(IServiceCollection services)
    {
    }

    protected static StringContent GetStringContent(object obj)
    {
        return new StringContent(
            JsonSerializer.Serialize(obj),
            Encoding.UTF8,
            "application/json");
    }
}

/// <summary>
/// Extension methods for reading and asserting HTTP response content in tests.
/// </summary>
public static class HttpResponseExtensions
{
    public static async Task<T> ReadAndAssertSuccessAsync<T>(this HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<T>();
        content.Should().NotBeNull();
        return content!;
    }

    public static async Task AssertStatusCodeAsync(this HttpResponseMessage response, HttpStatusCode expected)
    {
        response.StatusCode.Should().Be(expected);
        await Task.CompletedTask;
    }
}
```

> **Note**: The `BaseControllerTests` class uses `WebApplicationFactory<Program>` which requires the Api project's `Program` class to be accessible. The Api `.csproj` may need `<InternalsVisibleTo Include="TradingApp.Api.Tests" />` or the `Program` class needs to be made public. A common pattern is adding a partial class at the bottom of Program.cs: `public partial class Program { }`.

##### Pattern References

- `.github/instructions/testing.instructions.md` — MSTest + Moq + FluentAssertions ≤v6, global Usings.cs, BaseControllerTests, test project structure mirroring src
- `.github/instructions/api-controllers.instructions.md` — controller tests dispatch via test client

---

### Task 1.7: Build solution and verify scaffolding {#task-17-build-solution-and-verify-scaffolding}

Build the entire solution to verify all projects compile correctly and the reference graph is sound.

- **Complexity**: Low
- **Risk Factors**: NuGet restore may need internet; MediatR version compatibility
- **Files**: None (verification step)
- **Success**:
  - `dotnet build TradingApp.sln` succeeds with zero errors
  - All 10 projects (6 src + 4 test) are in the solution
- **Dependencies**: All prior tasks

#### Implementation Details

```bash
dotnet restore TradingApp.sln
dotnet build TradingApp.sln
```

If build errors occur, debug and fix before committing. Common issues:
- Missing project references
- MediatR package version conflicts
- FluentAssertions version >6 accidentally pulled
- `Program` class not accessible to test project (add `public partial class Program { }`)

##### Pattern References

- `.github/instructions/testing.instructions.md` — build must succeed before committing

---

## Phase Success Criteria

- Solution builds cleanly with all 10 projects
- CQRS base records (Command, CreateCommand, Command<T>, Query<T>) compile
- Handler base classes (CommandHandler, CreateCommandHandler, QueryHandler) compile
- Envelope and CreatedResultEnvelope compile
- ApiController base class with IMediator and IdentityService compiles
- All test projects compile with FluentAssertions ≤v6, Moq, MSTest
- BaseControllerTests and FakeHttpMessageHandler compile
- Phase is ready for a single commit
