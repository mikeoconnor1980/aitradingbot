---
applyTo: "**/tests/**/*.cs,**/*.Tests/**/*.cs"
---

# Unit Testing Patterns

## Test Framework
<!-- DOT-CUSTOM-START:testing-framework -->
- **MSTest** for test execution - NEVER USE XUNIT
- **Moq** for mocking dependencies
- **FluentAssertions** for assertions - **MANDATORY** must NOT use major versions higher than 6 to avoid licencing issues
- **Builder Pattern** for test entity creation
<!-- DOT-CUSTOM-END:testing-framework -->

## Test Naming Convention

Use Given_When_Then pattern:

```csharp
[TestMethod]
public void GivenEntity_WhenCreated_ThenCreatedDomainEventPublished()
{
    // Arrange, Act, Assert
}

[TestMethod]
public async Task GivenController_WhenGetItems_ThenReturnsOk()
{
    // Arrange, Act, Assert
}
```

## Project Structure

<!-- DOT-CUSTOM-START:testing-project-structure -->
Test projects mirror the src folder structure:

```
tests/
├── DTS.UKCT.Efiling.Core.Tests/
│   ├── Builders/           # Entity builders
│   ├── Factories/          # MockRepositoryFactory
│   ├── {BoundedContext}/
│   │   └── Entities/       # Entity tests
│   └── Usings.cs           # Global usings
├── DTS.UKCT.Efiling.Application.Tests/
│   ├── {BoundedContext}/
│   │   ├── Jobs/           # Job tests
│   │   └── IntegrationEvents/
│   └── Usings.cs
└── DTS.UKCT.Efiling.Api.Tests/
    ├── Controllers/        # Controller tests
    └── Usings.cs
```

## Global Usings

Check `Usings.cs` in each test project to avoid redundant imports:

```csharp
global using FluentAssertions;
global using Microsoft.VisualStudio.TestTools.UnitTesting;
global using Moq;
```
<!-- DOT-CUSTOM-END:testing-project-structure -->

## Builder Pattern

Create builders in `tests/DTS.UKCT.Efiling.Core.Tests/Builders/`:

```csharp
public class MyEntityBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _workAreaId = Guid.NewGuid();
    private string _name = "Test Entity";

    public MyEntityBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public MyEntityBuilder WithWorkArea(Guid workAreaId)
    {
        _workAreaId = workAreaId;
        return this;
    }

    public MyEntityBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public MyEntity Build()
    {
        return MyEntity.Create(_id, _workAreaId, _name, "test@deloitte.co.uk");
    }

    public MyEntityReadModel BuildReadModel()
    {
        return new MyEntityReadModel
        {
            Id = _id,
            WorkAreaId = _workAreaId,
            Name = _name
        };
    }
}
```

<!-- DOT-CUSTOM-START:testing-custom -->
## MockRepositoryFactory

Use `MockRepositoryFactory` to create mock repositories from lists:

```csharp
// Located in tests/DTS.UKCT.Efiling.Core.Tests/Factories/MockRepositoryFactory.cs

// For standard AggregateRoots:
var entities = new List<MyEntity> { new MyEntityBuilder().Build() };
var mockRepo = MockRepositoryFactory.Create(entities);
```
<!-- DOT-CUSTOM-END:testing-custom -->

## Entity Tests

```csharp
[TestClass]
public class MyEntityTests
{
    private const string USER = "test@deloitte.co.uk";

    [TestMethod]
    public void GivenMyEntity_WhenCreated_ThenPropertiesSet()
    {
        // Arrange & Act
        var entity = new MyEntityBuilder().WithName("Test").Build();

        // Assert
        entity.Name.Should().Be("Test");
        entity.IsDeleted.Should().BeFalse();
    }

    [TestMethod]
    public void GivenMyEntity_WhenDeleted_ThenIsDeletedTrue()
    {
        // Arrange
        var entity = new MyEntityBuilder().Build();

        // Act
        entity.Delete(USER);

        // Assert
        entity.IsDeleted.Should().BeTrue();
    }

    [TestMethod]
    public void GivenMyEntity_WhenInvalidName_ThenThrowsDomainException()
    {
        // Arrange & Act
        Action action = () => MyEntity.Create(Guid.NewGuid(), Guid.NewGuid(), "", USER);

        // Assert
        action.Should().Throw<DomainException>();
    }
}
```


## CQRS Command and Query Tests

Command and Query Handlers SHOULD NOT have their own test classes. 
Command and Query Handlers SHOULD ONLY be tested indirectly via the API Controller Tests.


## API Controller Tests

Commands and Queries are tested via API controller tests, not directly:

```csharp
[TestClass]
public class MyFeatureControllerTests : BaseControllerTests<Startup>
{
    private const string BASE_URL = "api/my-feature";

    public MyFeatureControllerTests()
    {
        AddEngagementManagerRole();
    }

    [TestMethod]
    public async Task GivenController_WhenGetItems_ThenOk()
    {
        // Arrange
        var entity = new MyEntityBuilder().WithWorkArea(Get<WorkAreaAcl>().First().WorkAreaId).Build();
        Get<Entity>().Add(entity);
        var client = GetTestClient();

        // Act
        var response = await client.GetAsync($"{BASE_URL}/{Get<WorkAreaAcl>().First().WorkAreaId}/items");

        // Assert
        var items = await response.ReadAndAssertSuccessAsync<List<ItemDto>>();
        items.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task GivenController_WhenCreateItem_ThenCreated()
    {
        // Arrange
        var client = GetTestClient();
        var createDto = new ItemCreateDto { Name = "New Item" };

        // Act
        var response = await client.PostAsync(
            $"{BASE_URL}/{Get<WorkAreaAcl>().First().WorkAreaId}/items",
            GetStringContent(createDto));

        // Assert
        await response.AssertStatusCodeAsync(HttpStatusCode.Created);
        AssertUnitOfWorkCommitted();
    }

    [TestMethod]
    public async Task GivenController_WhenInvalidRequest_ThenBadRequest()
    {
        // Arrange
        var client = GetTestClient();
        var invalidDto = new ItemCreateDto { Name = "" };

        // Act
        var response = await client.PostAsync(
            $"{BASE_URL}/{Get<WorkAreaAcl>().First().WorkAreaId}/items",
            GetStringContent(invalidDto));

        // Assert
        var error = await response.ReadAndAssertError(HttpStatusCode.BadRequest);
        error.ErrorMessage.Should().Contain("Name");
    }
}
```

All Repositories must be Mocked and registered in `BaseControllerTests.cs`. Test data is created in the implementing ControllerTests classes using Builders.

## Phases and Commits

Each phase of work or commit MUST have associated tests for new/modified code, if required. Tests should not be left until the end of the work or a later phase/commit.

## Job Tests

```csharp
[TestClass]
public class MyJobTests
{
    private readonly Mock<IRepository<MyEntity>> _repositoryMock = new();
    private readonly Mock<ILogger<MyJob>> _loggerMock = new();
    private readonly Mock<IHostApplicationLifetime> _lifetimeMock = new();

    [TestMethod]
    public async Task GivenJob_WhenRun_ThenProcessesEntities()
    {
        // Arrange
        var entities = new List<MyEntity> { new MyEntityBuilder().Build() };
        _repositoryMock.Setup(r => r.GetAll(It.IsAny<bool>()))
            .Returns(entities.AsQueryable());

        var job = new MyJob(_repositoryMock.Object, _loggerMock.Object, _lifetimeMock.Object);

        // Act
        await job.StartAsync(CancellationToken.None);

        // Assert
        _repositoryMock.Verify(r => r.Delete(It.IsAny<MyEntity>()), Times.Once);
    }
}
```

## Building Tests

When creating or testing new unit tests, build only the test project (not the whole solution):

```bash
dotnet build tests/DTS.UKCT.Efiling.Core.Tests/DTS.UKCT.Efiling.Core.Tests.csproj
dotnet test tests/DTS.UKCT.Efiling.Core.Tests/DTS.UKCT.Efiling.Core.Tests.csproj
```

## Running Tests

When new tests are created, they should be tested more efficiently by using filters to only test the new tests.

```
dotnet test tests/DTS.UKCT.Efiling.Core.Tests --filter "FullyQualifiedName~EntityName"
```

Tests in the Core and Application layers are quick and do not require filtering.

When a phase of work is complete, all affected test projects should be run and verified. `DTS.UKCT.Efiling.Arch.Tests` .NET architecture tests should be run after any phases which include any backend changes.


**Important**: `DTS.UKCT.Efiling.AcceptanceTests` should NOT be automatically run.
