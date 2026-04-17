<!-- markdownlint-disable-file -->

# Task Details: F9 — Natural Language Strategy Interpreter

## Phase 1: LLM Client Infrastructure

## Standards and Knowledge References

- **csharp.instructions.md**: `sealed` classes by default, `_camelCase` private fields, `Async` suffix on async methods, `IOptions<T>` for configuration, constructor injection
- **dotnet-architecture.instructions.md**: Infrastructure services have interface in `Application/Abstractions/Services/`, implementation in infrastructure project; Options in `Application/Abstractions/Configuration/`; DI extension methods per project
- **testing.instructions.md**: MSTest, Moq, FluentAssertions v6, `Given_When_Then` naming, tests within the phase

### Task 1.1: Create TradePilot.AI project and add to solution {#task-11-create-TradePilot-ai-project}

Create a new class library project `TradePilot.AI` under `src/` and add it to the solution.

- **Complexity**: Low
- **Risk Factors**: None — straightforward project scaffolding
- **Files**:
  - `src/TradePilot.AI/TradePilot.AI.csproj` — new project file
  - `TradePilot.sln` — add project reference
  - `tests/TradePilot.AI.Tests/TradePilot.AI.Tests.csproj` — new test project
- **Success**:
  - `dotnet build` succeeds with new project
  - Project appears in solution explorer under `src/` folder
- **Dependencies**: None

#### Implementation Details

```bash
# Commands to execute:
dotnet new classlib -n TradePilot.AI -o src/TradePilot.AI --framework net8.0
dotnet sln add src/TradePilot.AI/TradePilot.AI.csproj --solution-folder src
dotnet new mstest -n TradePilot.AI.Tests -o tests/TradePilot.AI.Tests --framework net8.0
dotnet sln add tests/TradePilot.AI.Tests/TradePilot.AI.Tests.csproj --solution-folder tests
```

```xml
<!-- src/TradePilot.AI/TradePilot.AI.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\TradePilot.Application\TradePilot.Application.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Http" Version="8.0.1" />
    <PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" Version="8.0.0" />
  </ItemGroup>
</Project>
```

```xml
<!-- tests/TradePilot.AI.Tests/TradePilot.AI.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\TradePilot.AI\TradePilot.AI.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="MSTest.TestAdapter" Version="3.7.3" />
    <PackageReference Include="MSTest.TestFramework" Version="3.7.3" />
    <PackageReference Include="Moq" Version="4.20.72" />
    <PackageReference Include="FluentAssertions" Version="6.12.2" />
  </ItemGroup>
</Project>
```

Also add project reference from `TradePilot.Api` to `TradePilot.AI`:

```xml
<!-- src/TradePilot.Api/TradePilot.Api.csproj — add to existing ItemGroup -->
<ProjectReference Include="..\TradePilot.AI\TradePilot.AI.csproj" />
```

##### Pattern References

- `src/TradePilot.Application/TradePilot.Application.csproj` — representative library project structure
- `tests/TradePilot.Application.Tests/TradePilot.Application.Tests.csproj` — test project structure with MSTest + Moq + FluentAssertions

### Task 1.2: Create LlmOptions configuration class {#task-12-create-llmoptions-configuration}

Create the options class for LLM provider configuration following `HyperliquidOptions` pattern.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradePilot.Application/Abstractions/Configuration/LlmOptions.cs` — new options class
- **Success**:
  - Options class validated on startup with `ValidateDataAnnotations`
  - Supports Gemini and Ollama provider configuration
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradePilot.Application/Abstractions/Configuration/LlmOptions.cs — new file
using System.ComponentModel.DataAnnotations;

namespace TradePilot.Application.Abstractions.Configuration;

public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    [Required]
    public string Provider { get; set; } = "Gemini"; // Gemini | Ollama

    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/openai/";

    [Required]
    public string ModelName { get; set; } = "gemini-2.0-flash";

    public string? ApiKey { get; set; } // Required for Gemini, optional for Ollama

    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 30;
}
```

##### Pattern References

- `src/TradePilot.Application/Abstractions/Configuration/HyperliquidOptions.cs` — IOptions with DataAnnotations and `SectionName` constant

### Task 1.3: Create ILlmClient interface and OpenAI-compatible implementation {#task-13-create-illmclient-and-implementation}

Create the LLM client abstraction in Application and the OpenAI-compatible implementation in the AI project.

- **Complexity**: Medium
- **Risk Factors**: OpenAI-compatible API response shape must be correct; timeout handling
- **Files**:
  - `src/TradePilot.Application/Abstractions/Services/ILlmClient.cs` — interface
  - `src/TradePilot.AI/Services/OpenAiCompatibleLlmClient.cs` — implementation
  - `src/TradePilot.AI/Models/ChatCompletionRequest.cs` — request model
  - `src/TradePilot.AI/Models/ChatCompletionResponse.cs` — response model
- **Success**:
  - `CompleteAsync` sends system + user messages and returns content string
  - HTTP errors are caught and wrapped in a meaningful exception
  - Timeout is respected
- **Dependencies**: Task 1.2 (LlmOptions)

#### Implementation Details

```csharp
// src/TradePilot.Application/Abstractions/Services/ILlmClient.cs — new file
namespace TradePilot.Application.Abstractions.Services;

public interface ILlmClient
{
    Task<string> CompleteAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken);
}
```

```csharp
// src/TradePilot.AI/Models/ChatCompletionRequest.cs — new file
using System.Text.Json.Serialization;

namespace TradePilot.AI.Models;

internal sealed class ChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = default!;

    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; init; } = [];

    [JsonPropertyName("temperature")]
    public decimal Temperature { get; init; } = 0.1m;

    [JsonPropertyName("response_format")]
    public ResponseFormat? ResponseFormat { get; init; }
}

internal sealed class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = default!;

    [JsonPropertyName("content")]
    public string Content { get; init; } = default!;
}

internal sealed class ResponseFormat
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "json_object";
}
```

```csharp
// src/TradePilot.AI/Models/ChatCompletionResponse.cs — new file
using System.Text.Json.Serialization;

namespace TradePilot.AI.Models;

internal sealed class ChatCompletionResponse
{
    [JsonPropertyName("choices")]
    public List<ChatChoice> Choices { get; init; } = [];
}

internal sealed class ChatChoice
{
    [JsonPropertyName("message")]
    public ChatChoiceMessage Message { get; init; } = default!;
}

internal sealed class ChatChoiceMessage
{
    [JsonPropertyName("content")]
    public string Content { get; init; } = default!;
}
```

```csharp
// src/TradePilot.AI/Services/OpenAiCompatibleLlmClient.cs — new file
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradePilot.AI.Models;
using TradePilot.Application.Abstractions.Configuration;
using TradePilot.Application.Abstractions.Services;

namespace TradePilot.AI.Services;

internal sealed class OpenAiCompatibleLlmClient : ILlmClient
{
    private readonly HttpClient _httpClient;
    private readonly LlmOptions _options;
    private readonly ILogger<OpenAiCompatibleLlmClient> _logger;

    public OpenAiCompatibleLlmClient(
        HttpClient httpClient,
        IOptions<LlmOptions> options,
        ILogger<OpenAiCompatibleLlmClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> CompleteAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken)
    {
        var request = new ChatCompletionRequest
        {
            Model = _options.ModelName,
            Messages =
            [
                new ChatMessage { Role = "system", Content = systemPrompt },
                new ChatMessage { Role = "user", Content = userMessage }
            ],
            Temperature = 0.1m,
            ResponseFormat = new ResponseFormat { Type = "json_object" }
        };

        _logger.LogInformation(
            "Sending LLM request to {Provider} model {Model}",
            _options.Provider, _options.ModelName);

        var response = await _httpClient.PostAsJsonAsync(
            "chat/completions", request, cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(
            cancellationToken: cancellationToken);

        if (result?.Choices is not { Count: > 0 })
        {
            throw new InvalidOperationException("LLM returned no choices");
        }

        return result.Choices[0].Message.Content;
    }
}
```

##### Pattern References

- `src/TradePilot.Application/Abstractions/Services/IHyperliquidRestClient.cs` — typed HTTP client interface
- `src/TradePilot.Infrastructure/Services/HyperliquidRestClient.cs` — HTTP client with IOptions, ILogger, response parsing

### Task 1.4: Create AiServiceExtensions for DI registration {#task-14-create-aiserviceextensions}

Create the DI extension method for the AI project following the Persistence pattern.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradePilot.AI/AiServiceExtensions.cs` — DI registration extension
- **Success**:
  - `AddAI(configuration)` registers all AI services
  - HttpClient configured with base URL from options
  - API key injected as Authorization header for Gemini
- **Dependencies**: Tasks 1.2, 1.3

#### Implementation Details

```csharp
// src/TradePilot.AI/AiServiceExtensions.cs — new file
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TradePilot.AI.Services;
using TradePilot.Application.Abstractions.Configuration;
using TradePilot.Application.Abstractions.Services;

namespace TradePilot.AI;

public static class AiServiceExtensions
{
    public static IServiceCollection AddAI(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<LlmOptions>()
            .Bind(configuration.GetSection(LlmOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<ILlmClient, OpenAiCompatibleLlmClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<LlmOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

            if (!string.IsNullOrEmpty(options.ApiKey))
            {
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.ApiKey);
            }
        });

        return services;
    }
}
```

##### Pattern References

- `src/TradePilot.Persistence/PersistenceServiceExtensions.cs` — DI extension method pattern
- `src/TradePilot.Api/Program.cs` — HttpClient registration with `AddHttpClient<I, C>` + IOptions resolution

### Task 1.5: Add LLM configuration to appsettings.json and wire in Program.cs {#task-15-add-configuration-and-wire-programcs}

Add the Llm configuration section and wire `AddAI` in the API startup.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradePilot.Api/appsettings.json` — add Llm section
  - `src/TradePilot.Api/Program.cs` — call `AddAI()`
- **Success**:
  - Application starts with Llm configuration section
  - No startup validation errors
- **Dependencies**: Task 1.4

#### Implementation Details

```json
// src/TradePilot.Api/appsettings.json — add new section after "BinanceIngestion"
"Llm": {
  "Provider": "Gemini",
  "BaseUrl": "https://generativelanguage.googleapis.com/v1beta/openai/",
  "ModelName": "gemini-2.0-flash",
  "TimeoutSeconds": 30
}
```

```csharp
// src/TradePilot.Api/Program.cs — add after existing service registrations
// ... existing code ...
builder.Services.AddAI(builder.Configuration);
// ... existing code ...
```

##### Pattern References

- `src/TradePilot.Api/appsettings.json` — existing configuration sections (Hyperliquid, CandleIngestion, BinanceIngestion)
- `src/TradePilot.Api/Program.cs` — service registration pattern with `AddPersistence(builder.Configuration)`

### Task 1.6: Add unit tests for LLM client {#task-16-add-unit-tests}

Add unit tests for `OpenAiCompatibleLlmClient` verifying request construction, response parsing, and error handling.

- **Complexity**: Medium
- **Risk Factors**: HTTP mocking setup
- **Files**:
  - `tests/TradePilot.AI.Tests/Services/OpenAiCompatibleLlmClientTests.cs` — new test class
- **Success**:
  - Tests verify correct request shape sent to API
  - Tests verify response parsing returns content
  - Tests verify exception on empty choices
  - Tests verify timeout behaviour
  - All tests pass
- **Dependencies**: Task 1.3

#### Implementation Details

```csharp
// tests/TradePilot.AI.Tests/Services/OpenAiCompatibleLlmClientTests.cs — new file
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TradePilot.AI.Services;
using TradePilot.Application.Abstractions.Configuration;

namespace TradePilot.AI.Tests.Services;

[TestClass]
public sealed class OpenAiCompatibleLlmClientTests
{
    private Mock<IOptions<LlmOptions>> _optionsMock = default!;
    private Mock<ILogger<OpenAiCompatibleLlmClient>> _loggerMock = default!;

    [TestInitialize]
    public void Setup()
    {
        _optionsMock = new Mock<IOptions<LlmOptions>>();
        _optionsMock.Setup(o => o.Value).Returns(new LlmOptions
        {
            Provider = "Gemini",
            BaseUrl = "https://example.com/v1/",
            ModelName = "test-model",
            TimeoutSeconds = 30
        });

        _loggerMock = new Mock<ILogger<OpenAiCompatibleLlmClient>>();
    }

    [TestMethod]
    public async Task GivenValidResponse_WhenCompleteAsync_ThenReturnsContent()
    {
        // Arrange
        var responseJson = JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content = "test response" } } }
        });
        var handler = new FakeHttpMessageHandler(responseJson, HttpStatusCode.OK);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/v1/") };

        var sut = new OpenAiCompatibleLlmClient(httpClient, _optionsMock.Object, _loggerMock.Object);

        // Act
        var result = await sut.CompleteAsync("system", "user", CancellationToken.None);

        // Assert
        result.Should().Be("test response");
    }

    [TestMethod]
    public async Task GivenEmptyChoices_WhenCompleteAsync_ThenThrowsInvalidOperationException()
    {
        // Arrange
        var responseJson = JsonSerializer.Serialize(new { choices = Array.Empty<object>() });
        var handler = new FakeHttpMessageHandler(responseJson, HttpStatusCode.OK);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/v1/") };

        var sut = new OpenAiCompatibleLlmClient(httpClient, _optionsMock.Object, _loggerMock.Object);

        // Act
        var act = () => sut.CompleteAsync("system", "user", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [TestMethod]
    public async Task GivenServerError_WhenCompleteAsync_ThenThrowsHttpRequestException()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler("error", HttpStatusCode.InternalServerError);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/v1/") };

        var sut = new OpenAiCompatibleLlmClient(httpClient, _optionsMock.Object, _loggerMock.Object);

        // Act
        var act = () => sut.CompleteAsync("system", "user", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    /// <summary>Helper for mocking HttpClient responses.</summary>
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _response;
        private readonly HttpStatusCode _statusCode;

        public FakeHttpMessageHandler(string response, HttpStatusCode statusCode)
        {
            _response = response;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_response, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
```

##### Pattern References

- `tests/TradePilot.Application.Tests/Trading/Services/GridControllerTests.cs` — MSTest, FluentAssertions, Given_When_Then naming convention
- `tests/TradePilot.Api.Tests/TradePilot.Api.Tests.csproj` — test project with Moq + FluentAssertions

### Task 1.7: Build verification and architecture tests {#task-17-build-verification}

Verify the solution builds and run existing architecture tests to ensure no violations.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: No files to create
- **Success**:
  - `dotnet build TradePilot.sln` succeeds
  - `dotnet test` passes for new test project
  - Existing architecture tests still pass
- **Dependencies**: All previous tasks in phase

## Phase Success Criteria

- TradePilot.AI project exists and builds as part of the solution
- ILlmClient abstraction and OpenAI-compatible implementation are registered
- LLM configuration section exists in appsettings.json
- Unit tests for LLM client pass
- Solution builds cleanly with no warnings
