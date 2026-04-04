<!-- markdownlint-disable-file -->

# Task Details: AI Strategy Review

## Phase 2: AI Service Layer

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — Sealed classes, async/await with CancellationToken, IOptions injection
- `.github/instructions/dotnet-architecture.instructions.md` — Service interface in Application.Abstractions, implementation in AI layer, CQRS command/query with handler in same file
- `.github/instructions/testing.instructions.md` — MSTest, Moq for ILlmClient, FluentAssertions, Given_When_Then naming
- `.agent-context/0-knowledge/24-strategy-interpreter-architecture.md` — LLM service consumer pattern

## Design References

- The `StrategyInterpreter` is the exact precedent: it consumes `ILlmClient`, applies a system prompt, and returns a parsed result
- The review feature is simpler — it returns raw markdown instead of parsing JSON
- A second `HttpClient` registration is needed for the review LLM instance, using a marker interface `IReviewLlmClient`

### Task 2.1: Create StrategyReviewPrompt static class {#task-21-create-strategyreviewprompt-static-class}

Store the strategy review system prompt server-side as a static class constant, following the `StrategyInterpreterPrompt` pattern.

- **Complexity**: Low
- **Risk Factors**: None — prompt text is provided in the PBI
- **Files**:
  - `src/TradingApp.AI/Prompts/StrategyReviewPrompt.cs` - New file
- **Success**:
  - System prompt stored as `internal static` class with `const string SystemPrompt`
  - Prompt text matches PBI specification exactly
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.AI/Prompts/StrategyReviewPrompt.cs — new file
namespace TradingApp.AI.Prompts;

internal static class StrategyReviewPrompt
{
    public const string SystemPrompt = """
        You are an expert trading strategy reviewer.
        You are NOT executing trades, NOT validating schema, and NOT guaranteeing profitability.
        Your role is to critically review a trading strategy defined in JSON and provide a structured, objective assessment of its design, risks, and potential weaknesses.

        IMPORTANT RULES:
        - The JSON is already structurally valid and executable.
        - Do NOT validate schema or syntax.
        - Do NOT assume missing fields exist.
        - Only analyse what is explicitly present.
        - If something is missing, call it out as missing — do not infer values.
        - Do NOT claim the strategy is profitable or safe.
        - Avoid absolute statements like "this will work".
        - Be critical, realistic, and practical.
        - Distinguish clearly between facts and inferences.

        ---

        REVIEW THE STRATEGY ACROSS THESE DIMENSIONS:

        1. Strategy Summary
           - What type of strategy is this? (trend-following, mean reversion, breakout, grid, etc.)
           - Describe how it works in plain English

        2. Entry Logic Quality
           - Are the entry signals clear and logical?
           - Any risk of noise / false signals?
           - Any obvious weaknesses?

        3. Exit Logic Completeness
           - Are take profit and stop loss defined?
           - Is exit logic realistic and balanced?
           - Any missing exit conditions?

        4. Risk Management
           - Position sizing approach
           - Use of leverage
           - Stop loss presence and quality
           - Exposure concentration risks
           - Missing safeguards (daily loss caps, max trades, etc.)

        5. Strategy Weaknesses
           - Where is this likely to fail?
           - Market conditions where performance may degrade

        6. Market Regime Fit
           - Trending / ranging / volatile / low liquidity suitability

        7. Complexity & Overfitting Risk
           - Is the strategy overly complex?
           - Too many conditions?
           - Risk of curve fitting?

        8. Execution Realism
           - Would this work in real markets?
           - Consider slippage, latency, spread, liquidity

        9. Missing Elements
           - What important components are absent?

        10. Improvement Suggestions
            - Practical, actionable suggestions only

        ---

        OUTPUT FORMAT:
        - Return your review as markdown using the numbered section headings above.
        - Use bullet points within each section.
        - Keep the total review under 1500 words.
        - End with a brief one-paragraph overall assessment.

        ADDITIONAL INSTRUCTIONS:
        - Keep explanations concise but meaningful
        - Use bullet-style phrasing inside arrays
        - Be honest and critical, not polite
        - If something is good, say why — but always look for weaknesses
        - If key risk controls are missing, highlight them strongly
        """;
}
```

##### Pattern References

- `src/TradingApp.AI/Prompts/StrategyInterpreterPrompt.cs` — Static class with const string SystemPrompt, internal visibility

---

### Task 2.2: Create IStrategyReviewer interface {#task-22-create-istrategyreviewer-interface}

Create the service interface in the Application abstractions layer. Also create a marker interface `IReviewLlmClient` to enable a second independent `HttpClient`/`ILlmClient` registration.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.Application/Abstractions/Services/IStrategyReviewer.cs` - New file
  - `src/TradingApp.Application/Abstractions/Services/IReviewLlmClient.cs` - New file
- **Success**:
  - `IStrategyReviewer.ReviewAsync` returns the review markdown string
  - `IReviewLlmClient` extends `ILlmClient` as a marker interface
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Application/Abstractions/Services/IStrategyReviewer.cs — new file
namespace TradingApp.Application.Abstractions.Services;

public interface IStrategyReviewer
{
    Task<string> ReviewAsync(string strategyJson, CancellationToken cancellationToken);
}
```

```csharp
// src/TradingApp.Application/Abstractions/Services/IReviewLlmClient.cs — new file
namespace TradingApp.Application.Abstractions.Services;

/// <summary>
/// Marker interface for the review-specific LLM client registration.
/// Uses independently-configured LlmReview options.
/// </summary>
public interface IReviewLlmClient : ILlmClient;
```

##### Pattern References

- `src/TradingApp.Application/Abstractions/Services/IStrategyInterpreter.cs` — Interface pattern with CancellationToken
- `src/TradingApp.Application/Abstractions/Services/ILlmClient.cs` — Base interface being extended

---

### Task 2.3: Create StrategyReviewer service implementation {#task-23-create-strategyreviewer-service-implementation}

Create the concrete AI service that calls the review LLM client with the strategy review prompt.

- **Complexity**: Medium
- **Risk Factors**: Must use `IReviewLlmClient` (not `ILlmClient`) to get the independently-configured client; error handling must be graceful
- **Files**:
  - `src/TradingApp.AI/Services/StrategyReviewer.cs` - New file
- **Success**:
  - Uses `IReviewLlmClient` for LLM calls
  - Returns raw markdown from LLM (no JSON parsing needed)
  - Graceful error handling with logging
- **Dependencies**: Task 2.1, Task 2.2

#### Implementation Details

```csharp
// src/TradingApp.AI/Services/StrategyReviewer.cs — new file
using Microsoft.Extensions.Logging;
using TradingApp.AI.Prompts;
using TradingApp.Application.Abstractions.Services;

namespace TradingApp.AI.Services;

public sealed class StrategyReviewer : IStrategyReviewer
{
    private readonly IReviewLlmClient _llmClient;
    private readonly ILogger<StrategyReviewer> _logger;

    public StrategyReviewer(IReviewLlmClient llmClient, ILogger<StrategyReviewer> logger)
    {
        _llmClient = llmClient;
        _logger = logger;
    }

    public async Task<string> ReviewAsync(string strategyJson, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(strategyJson);

        _logger.LogInformation(
            "Requesting AI strategy review for strategy JSON with length {Length}.",
            strategyJson.Length);

        try
        {
            var review = await _llmClient.CompleteAsync(
                StrategyReviewPrompt.SystemPrompt,
                strategyJson,
                cancellationToken);

            _logger.LogInformation("AI strategy review completed successfully.");

            return review;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "LLM request failed during strategy review.");
            throw;
        }
    }
}
```

##### Pattern References

- `src/TradingApp.AI/Services/StrategyInterpreter.cs` — Service structure, ILlmClient usage, logging, error handling pattern

---

### Task 2.4: Register second LLM client and reviewer service in DI {#task-24-register-second-llm-client-and-reviewer-service-in-di}

Create a `ReviewLlmClient` concrete class that extends `OpenAiCompatibleLlmClient` with `IReviewLlmClient`, and register it with its own `AddHttpClient` using `LlmReviewOptions`.

- **Complexity**: Medium
- **Risk Factors**: Must wire the HttpClient factory to use `LlmReviewOptions` (not `LlmOptions`); the `OpenAiCompatibleLlmClient` constructor takes `HttpClient` + `IOptions<LlmOptions>` — the review client needs `IOptions<LlmReviewOptions>`
- **Files**:
  - `src/TradingApp.AI/Services/ReviewLlmClient.cs` - New file
  - `src/TradingApp.AI/AiServiceExtensions.cs` - Modify
- **Success**:
  - `ReviewLlmClient` registered with independently-configured HttpClient
  - `IStrategyReviewer` registered as scoped
  - Application starts without DI errors
- **Dependencies**: Task 2.2, Task 2.3, Phase 1 Task 1.5 (LlmReviewOptions), Phase 1 Task 1.6 (options binding)

#### Implementation Details

```csharp
// src/TradingApp.AI/Services/ReviewLlmClient.cs — new file
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingApp.AI.Models;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Services;

namespace TradingApp.AI.Services;

/// <summary>
/// LLM client for strategy reviews, independently configured via LlmReviewOptions.
/// Reuses the same OpenAI-compatible HTTP protocol as <see cref="OpenAiCompatibleLlmClient"/>.
/// </summary>
public sealed class ReviewLlmClient : IReviewLlmClient
{
    private readonly HttpClient _httpClient;
    private readonly LlmReviewOptions _options;
    private readonly ILogger<ReviewLlmClient> _logger;

    public ReviewLlmClient(
        HttpClient httpClient,
        IOptions<LlmReviewOptions> options,
        ILogger<ReviewLlmClient> logger)
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
        ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);

        var request = new ChatCompletionRequest
        {
            Model = _options.ModelName,
            Messages =
            [
                new ChatMessage { Role = "system", Content = systemPrompt },
                new ChatMessage { Role = "user", Content = userMessage },
            ],
        };

        var response = await _httpClient.PostAsJsonAsync("chat/completions", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Review LLM request failed with status {Status}: {Error}",
                response.StatusCode,
                errorBody);
            throw new HttpRequestException(
                $"Review LLM request failed with status {response.StatusCode}.");
        }

        var result = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken)
            ?? throw new InvalidOperationException("LLM returned null response.");

        return result.Choices.FirstOrDefault()?.Message?.Content ?? string.Empty;
    }
}
```

```csharp
// src/TradingApp.AI/AiServiceExtensions.cs — add after existing IStrategyInterpreter registration:

services.AddHttpClient<IReviewLlmClient, ReviewLlmClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<LlmReviewOptions>>().Value;

    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

    if (!string.IsNullOrWhiteSpace(options.ApiKey))
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", options.ApiKey);
    }
});

services.AddScoped<IStrategyReviewer, StrategyReviewer>();
```

Note: Add required `using TradingApp.Application.Abstractions.Configuration;` if not already present (it is — `LlmOptions` is already imported).

##### Pattern References

- `src/TradingApp.AI/AiServiceExtensions.cs` — AddHttpClient pattern with IOptions-based configuration
- `src/TradingApp.AI/Services/OpenAiCompatibleLlmClient.cs` — HTTP request/response pattern for OpenAI-compatible API

---

### Task 2.5: Create RequestStrategyReviewCommand and handler {#task-25-create-requeststrategyreviewcommand-and-handler}

Create the CQRS command that orchestrates the review: validates ownership, fetches the revision config, calls the reviewer, persists the result (overwriting any existing review for the same revision).

- **Complexity**: High
- **Risk Factors**: Must verify strategy ownership, handle review overwrite (delete + create), and call AI service
- **Files**:
  - `src/TradingApp.Application/StrategyAuthoring/Commands/RequestStrategyReviewCommand.cs` - New file
- **Success**:
  - Command validates strategy ownership
  - Fetches revision config JSON
  - Deletes existing review for the revision if present
  - Calls IStrategyReviewer and persists result
  - Returns StrategyReviewDto
- **Dependencies**: Task 2.3, Task 2.7, Phase 1 (entity + repo)

#### Implementation Details

```csharp
// src/TradingApp.Application/StrategyAuthoring/Commands/RequestStrategyReviewCommand.cs — new file
using Microsoft.Extensions.Options;
using TradingApp.Application.Abstractions.Commands;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Identity;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.StrategyAuthoring.Commands;

public sealed record RequestStrategyReviewCommand(
    Guid StrategyId,
    int RevisionNumber,
    AppIdentity Identity) : Command<StrategyReviewDto>;

public sealed class RequestStrategyReviewCommandHandler
    : CommandHandler<RequestStrategyReviewCommand, StrategyReviewDto>
{
    private readonly IStrategyRepository _strategyRepository;
    private readonly IStrategyRevisionRepository _revisionRepository;
    private readonly IStrategyReviewRepository _reviewRepository;
    private readonly IStrategyReviewer _reviewer;
    private readonly IOptions<LlmReviewOptions> _options;

    public RequestStrategyReviewCommandHandler(
        IStrategyRepository strategyRepository,
        IStrategyRevisionRepository revisionRepository,
        IStrategyReviewRepository reviewRepository,
        IStrategyReviewer reviewer,
        IOptions<LlmReviewOptions> options)
    {
        _strategyRepository = strategyRepository;
        _revisionRepository = revisionRepository;
        _reviewRepository = reviewRepository;
        _reviewer = reviewer;
        _options = options;
    }

    public override async Task<StrategyReviewDto> Handle(
        RequestStrategyReviewCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var strategy = await _strategyRepository.GetByIdAsync(request.StrategyId, cancellationToken)
            ?? throw new NotFoundException($"Strategy {request.StrategyId} not found.");

        if (strategy.UserId != request.Identity.UserId)
        {
            throw new NotFoundException($"Strategy {request.StrategyId} not found.");
        }

        var revision = await _revisionRepository.GetByStrategyAndRevisionAsync(
            request.StrategyId,
            request.RevisionNumber,
            cancellationToken)
            ?? throw new NotFoundException(
                $"Revision {request.RevisionNumber} not found for strategy {request.StrategyId}.");

        // Delete existing review for this revision (overwrite semantics)
        await _reviewRepository.DeleteByStrategyAndRevisionAsync(
            request.StrategyId,
            request.RevisionNumber,
            cancellationToken);

        var reviewMarkdown = await _reviewer.ReviewAsync(revision.ConfigJson, cancellationToken);

        var modelName = _options.Value.ModelName;

        var review = StrategyReview.Create(
            request.StrategyId,
            request.RevisionNumber,
            reviewMarkdown,
            modelName);

        await _reviewRepository.AddAsync(review, cancellationToken);

        return new StrategyReviewDto
        {
            Id = review.Id,
            StrategyId = review.StrategyId,
            RevisionNumber = review.RevisionNumber,
            ReviewMarkdown = review.ReviewMarkdown,
            ModelName = review.ModelName,
            CreatedAtUtc = review.CreatedAtUtc,
        };
    }
}
```

##### Pattern References

- `src/TradingApp.Application/StrategyAuthoring/Commands/InterpretStrategyCommand.cs` — Command + handler in same file, delegating to AI service
- `src/TradingApp.Application/StrategyAuthoring/Commands/UpdateStrategyCommand.cs` — Ownership check pattern (strategy.UserId != request.Identity.UserId)

---

### Task 2.6: Create GetStrategyReviewQuery and handler {#task-26-create-getstrategyreviewquery-and-handler}

Create the CQRS query that retrieves a stored review for a specific strategy revision.

- **Complexity**: Medium
- **Risk Factors**: Must verify strategy ownership before returning review
- **Files**:
  - `src/TradingApp.Application/StrategyAuthoring/Queries/GetStrategyReviewQuery.cs` - New file
- **Success**:
  - Query validates strategy ownership
  - Returns null-safe StrategyReviewDto (or throws NotFoundException)
- **Dependencies**: Phase 1 (entity + repo)

#### Implementation Details

```csharp
// src/TradingApp.Application/StrategyAuthoring/Queries/GetStrategyReviewQuery.cs — new file
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Identity;
using TradingApp.Application.Abstractions.Queries;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.StrategyAuthoring.Models;

namespace TradingApp.Application.StrategyAuthoring.Queries;

public sealed record GetStrategyReviewQuery(
    Guid StrategyId,
    int RevisionNumber,
    AppIdentity Identity) : Query<StrategyReviewDto?>;

public sealed class GetStrategyReviewQueryHandler
    : QueryHandler<GetStrategyReviewQuery, StrategyReviewDto?>
{
    private readonly IStrategyRepository _strategyRepository;
    private readonly IStrategyReviewRepository _reviewRepository;

    public GetStrategyReviewQueryHandler(
        IStrategyRepository strategyRepository,
        IStrategyReviewRepository reviewRepository)
    {
        _strategyRepository = strategyRepository;
        _reviewRepository = reviewRepository;
    }

    public override async Task<StrategyReviewDto?> Handle(
        GetStrategyReviewQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var strategy = await _strategyRepository.GetByIdAsync(request.StrategyId, cancellationToken)
            ?? throw new NotFoundException($"Strategy {request.StrategyId} not found.");

        if (strategy.UserId != request.Identity.UserId)
        {
            throw new NotFoundException($"Strategy {request.StrategyId} not found.");
        }

        var review = await _reviewRepository.GetByStrategyAndRevisionAsync(
            request.StrategyId,
            request.RevisionNumber,
            cancellationToken);

        if (review is null)
        {
            return null;
        }

        return new StrategyReviewDto
        {
            Id = review.Id,
            StrategyId = review.StrategyId,
            RevisionNumber = review.RevisionNumber,
            ReviewMarkdown = review.ReviewMarkdown,
            ModelName = review.ModelName,
            CreatedAtUtc = review.CreatedAtUtc,
        };
    }
}
```

##### Pattern References

- `src/TradingApp.Application/StrategyAuthoring/Queries/GetStrategyVersionsQuery.cs` — Query pattern with ownership check

---

### Task 2.7: Create StrategyReviewDto model {#task-27-create-strategyreviewdto-model}

Create the DTO model for returning strategy review data to the API layer.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.Application/StrategyAuthoring/Models/StrategyReviewDto.cs` - New file
- **Success**:
  - DTO includes all fields needed by the frontend
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Application/StrategyAuthoring/Models/StrategyReviewDto.cs — new file
namespace TradingApp.Application.StrategyAuthoring.Models;

public sealed class StrategyReviewDto
{
    public Guid Id { get; set; }
    public Guid StrategyId { get; set; }
    public int RevisionNumber { get; set; }
    public string ReviewMarkdown { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public long CreatedAtUtc { get; set; }
}
```

##### Pattern References

- `src/TradingApp.Application/StrategyAuthoring/Models/StrategyIntentDto.cs` — DTO naming and property pattern

---

### Task 2.8: Write StrategyReviewer unit tests {#task-28-write-strategyreviewer-unit-tests}

Write unit tests for the `StrategyReviewer` service, mocking `IReviewLlmClient`.

- **Complexity**: Medium
- **Risk Factors**: None
- **Files**:
  - `tests/TradingApp.AI.Tests/Services/StrategyReviewerTests.cs` - New file
- **Success**:
  - Tests cover successful review, LLM failure, null/empty input validation
  - IReviewLlmClient is mocked via Moq
  - All tests pass
- **Dependencies**: Task 2.3

#### Implementation Details

```csharp
// tests/TradingApp.AI.Tests/Services/StrategyReviewerTests.cs — new file
using Microsoft.Extensions.Logging;
using TradingApp.AI.Services;
using TradingApp.Application.Abstractions.Services;

namespace TradingApp.AI.Tests.Services;

[TestClass]
public sealed class StrategyReviewerTests
{
    private Mock<IReviewLlmClient> _llmClientMock = null!;
    private StrategyReviewer _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _llmClientMock = new Mock<IReviewLlmClient>();
        _sut = new StrategyReviewer(
            _llmClientMock.Object,
            Mock.Of<ILogger<StrategyReviewer>>());
    }

    [TestMethod]
    public async Task GivenValidStrategyJson_WhenReviewAsync_ThenReturnsReviewMarkdown()
    {
        var expectedReview = "## 1. Strategy Summary\n- Grid strategy";
        _llmClientMock
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReview);

        var result = await _sut.ReviewAsync("{\"grid\":{}}", CancellationToken.None);

        result.Should().Be(expectedReview);
        _llmClientMock.Verify(
            c => c.CompleteAsync(It.IsAny<string>(), "{\"grid\":{}}", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenLlmFailure_WhenReviewAsync_ThenThrows()
    {
        _llmClientMock
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("LLM unavailable"));

        var act = () => _sut.ReviewAsync("{\"grid\":{}}", CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task GivenInvalidInput_WhenReviewAsync_ThenThrowsArgumentException(string? strategyJson)
    {
        var act = () => _sut.ReviewAsync(strategyJson!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
```

##### Pattern References

- `tests/TradingApp.AI.Tests/Services/StrategyInterpreterTests.cs` — Mock ILlmClient, TestInitialize, assertion pattern

---

### Task 2.9: Build and run AI tests {#task-29-build-and-run-ai-tests}

Build the solution and run AI layer tests to verify Phase 2 changes.

- **Complexity**: Low
- **Risk Factors**: DI wiring issues
- **Files**: None (verification only)
- **Success**:
  - Solution builds without errors
  - All AI tests pass
- **Dependencies**: All previous tasks in Phase 2

Run:
```bash
dotnet build
dotnet test tests/TradingApp.AI.Tests --filter "FullyQualifiedName~StrategyReviewer"
```

## Phase Success Criteria

- Strategy review prompt stored server-side
- `IStrategyReviewer` and `StrategyReviewer` service implemented
- `ReviewLlmClient` provides independently-configured LLM access
- CQRS command for requesting reviews with ownership validation and overwrite semantics
- CQRS query for retrieving reviews with ownership validation
- `StrategyReviewDto` model created
- All AI service unit tests pass
