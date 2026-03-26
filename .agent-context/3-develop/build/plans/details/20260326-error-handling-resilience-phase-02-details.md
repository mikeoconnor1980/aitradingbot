<!-- markdownlint-disable-file -->

# Task Details: F8 — Error Handling & Resilience

## Phase 2: Backend HTTP Resilience

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — Sealed classes, async/await with `CancellationToken`, `IOptions<T>` for configuration
- `.github/instructions/testing.instructions.md` — MSTest, Moq, FluentAssertions ≤ v6, Given/When/Then naming
- `.github/instructions/dotnet-architecture.instructions.md` — External service implementations in `Infrastructure/Services/`
- `.agent-context/0-knowledge/02-hyperliquid-integration.md` — Rate limiting details, error handling requirements
- `.agent-context/0-knowledge/22-prelaunch-checklist.md` — Resilience requirements including retry policies

## Design References

- `Microsoft.Extensions.Http.Resilience` — Standard .NET 8+ HTTP resilience library wrapping Polly v8. Uses `AddStandardResilienceHandler()` or `AddResilienceHandler()` for custom pipelines on typed `HttpClient` registrations.
- PBI requirement: Rate-limit backoff initial 1s, max 60s, exponential increase.

### Task 2.1: Add HTTP resilience package and configure retry pipeline {#task-21-add-http-resilience-package-and-configure-retry-pipeline}

Add the `Microsoft.Extensions.Http.Resilience` NuGet package and configure a retry pipeline on the `HyperliquidRestClient` HTTP client registration. The pipeline retries on 429 (rate limit) and 5xx transient errors with exponential backoff.

- **Complexity**: Medium
- **Risk Factors**: Polly retry wraps the `HttpClient`/`DelegatingHandler` pipeline — the retry happens transparently before `PostInfoAsync`/`PostExchangeAsync` sees the response. If all retries are exhausted, the final response is returned normally and the REST client throws as before. Must verify the 5-second timeout on `HttpClient` is per-attempt (it is by default with the resilience handler).
- **Files**:
  - `src/TradingApp.Api/TradingApp.Api.csproj` — Modify: add `Microsoft.Extensions.Http.Resilience` package reference
  - `src/TradingApp.Api/Program.cs` — Modify: add `.AddResilienceHandler()` to the `AddHttpClient<>` registration
- **Success**:
  - `Microsoft.Extensions.Http.Resilience` package is referenced
  - Retry pipeline configured: retry on 429 and 5xx, exponential backoff (1s initial, 60s max), max 5 attempts
  - Retry attempts are logged automatically by the resilience pipeline
  - `dotnet build TradingApp.sln` succeeds
- **Dependencies**: Phase 1 completed

#### Implementation Details

```xml
<!-- src/TradingApp.Api/TradingApp.Api.csproj — add package reference -->
<PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="9.*" />
```

```csharp
// src/TradingApp.Api/Program.cs — modification
using Microsoft.Extensions.Http.Resilience;
using Polly;

// ... existing code ...

// Replace existing HttpClient registration:
builder.Services.AddHttpClient<IHyperliquidRestClient, HyperliquidRestClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<HyperliquidOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30); // Outer timeout caps total retry duration — with 5s per-attempt + exponential backoff, ~3 attempts complete within 30s (acts as a secondary ceiling)
})
.AddResilienceHandler("hyperliquid-retry", builder =>
{
    builder.AddRetry(new HttpRetryStrategyOptions
    {
        MaxRetryAttempts = 5,
        BackoffType = DelayBackoffType.Exponential,
        Delay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromSeconds(60),
        UseJitter = true,
        ShouldHandle = args => ValueTask.FromResult(
            args.Outcome.Result?.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
            (args.Outcome.Result is not null && (int)args.Outcome.Result.StatusCode >= 500)),
        OnRetry = args =>
        {
            var logger = args.Context.ServiceProvider?.GetService<ILoggerFactory>()?.CreateLogger("HyperliquidRetry");
            logger?.LogWarning(
                "Retrying Hyperliquid request. Attempt={AttemptNumber}, Delay={RetryDelay}ms, StatusCode={StatusCode}",
                args.AttemptNumber + 1,
                args.RetryDelay.TotalMilliseconds,
                args.Outcome.Result?.StatusCode);
            return ValueTask.CompletedTask;
        },
    });

    builder.AddTimeout(TimeSpan.FromSeconds(5)); // Per-attempt timeout
});
```

##### Pattern References

- `src/TradingApp.Api/Program.cs` — Current `AddHttpClient<>` registration with 5s timeout, no resilience handler

---

### Task 2.2: Enhance HyperliquidRestClient with typed exception throwing {#task-22-enhance-hyperliquidrestclient-with-typed-exception-throwing}

Update `PostInfoAsync` and `PostExchangeAsync` to throw typed `HyperliquidApiException` (and its subclasses) instead of generic `HttpRequestException`. This enables the enhanced `HttpGlobalExceptionFilter` to return specific error codes and messages.

- **Complexity**: Medium
- **Risk Factors**: `HttpRequestException` is still thrown by the `HttpClient` itself (e.g., DNS failure, timeout) — those must continue propagating as-is. Only replace the exceptions we explicitly throw after reading the response status code.
- **Files**:
  - `src/TradingApp.Infrastructure/Services/HyperliquidRestClient.cs` — Modify: replace `HttpRequestException` throws with typed exceptions after checking status code
- **Success**:
  - 429 responses throw `RateLimitException` (after Polly retries are exhausted)
  - 4xx responses throw `HyperliquidApiException` with the exchange error message
  - 5xx responses throw `HyperliquidApiException` with a generic exchange error message
  - `HttpRequestException` from network failures (thrown by `HttpClient` itself) still propagates unchanged
  - Existing functionality is preserved
- **Dependencies**: Task 1.1 (typed exceptions exist)

#### Implementation Details

```csharp
// src/TradingApp.Infrastructure/Services/HyperliquidRestClient.cs — modification to PostInfoAsync
// Replace the existing non-success handling block:
if (!response.IsSuccessStatusCode)
{
    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
    var statusCode = (int)response.StatusCode;

    _logger.LogWarning(
        "Hyperliquid API error. StatusCode={StatusCode}, Endpoint=/info, Body={ErrorBody}",
        statusCode,
        errorBody);

    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
    {
        throw new RateLimitException(
            $"Hyperliquid rate limit exceeded: {errorBody}");
    }

    throw new HyperliquidApiException(
        $"Hyperliquid API error: {errorBody}",
        statusCode,
        statusCode >= 500 ? "exchange_error" : "validation_error");
}
```

```csharp
// src/TradingApp.Infrastructure/Services/HyperliquidRestClient.cs — modification to PostExchangeAsync
// Replace the existing non-success handling block:
if (!response.IsSuccessStatusCode)
{
    var statusCode = (int)response.StatusCode;

    _logger.LogWarning(
        "Hyperliquid exchange error. StatusCode={StatusCode}, Endpoint=/exchange, Body={ResponseBody}",
        statusCode,
        responseBody);

    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
    {
        throw new RateLimitException(
            $"Hyperliquid rate limit exceeded: {responseBody}");
    }

    throw new HyperliquidApiException(
        $"Hyperliquid exchange error: {responseBody}",
        statusCode,
        statusCode >= 500 ? "exchange_error" : "validation_error");
}
```

Add required `using` at top:
```csharp
using TradingApp.Application.Abstractions.Exceptions;
```

##### Pattern References

- `src/TradingApp.Infrastructure/Services/HyperliquidRestClient.cs` — Current `PostInfoAsync` and `PostExchangeAsync` error handling

---

### Task 2.3: Update HyperliquidOrderService signing error detection {#task-23-update-hyperliquidorderservice-signing-error-detection}

Replace the fragile string-matching signing error detection with a catch for `SigningException`. Update the `HyperliquidRestClient` or `HyperliquidOrderService` to throw `SigningException` when signing-specific errors are detected.

- **Complexity**: Medium
- **Risk Factors**: The string match `ex.Message.Contains("signature")` relies on Hyperliquid including "signature" in the error body. The new approach should detect signing errors at the point where we have the most context — either in the REST client (based on status code + error body analysis) or in the order service (wrapping the existing catch).
- **Files**:
  - `src/TradingApp.Api/Services/HyperliquidOrderService.cs` — Modify: catch `HyperliquidApiException` and check for signing indicators, throw `SigningException`
- **Success**:
  - Signing rejections are caught and wrapped in `SigningException`
  - `SigningException` maps to a specific error message in the UI via the filter ("Signature rejected — check signing configuration")
  - Existing signing rejection test passes with updated assertion
- **Dependencies**: Task 1.1, Task 2.2

#### Implementation Details

```csharp
// src/TradingApp.Api/Services/HyperliquidOrderService.cs — modification
// Replace the existing catch block:
// catch (HttpRequestException ex) when (ex.Message.Contains("signature", StringComparison.OrdinalIgnoreCase))

// With:
catch (HyperliquidApiException ex) when (
    ex.Message.Contains("signature", StringComparison.OrdinalIgnoreCase) ||
    ex.Message.Contains("INVALID_SIGNATURE", StringComparison.OrdinalIgnoreCase))
{
    _logger.LogError(ex,
        "EIP-712 signature rejected by Hyperliquid. WalletAddress={WalletAddress}, Nonce={Nonce}",
        _signer.WalletAddress, nonce);

    throw new SigningException(
        "Signature rejected — check signing configuration",
        ex);
}
```

Note: We keep the string-based detection but wrap it into a typed `SigningException` for the filter to map. The `HyperliquidApiException` catch ensures we only match exchange-originated errors, not network failures.

##### Pattern References

- `src/TradingApp.Api/Services/HyperliquidOrderService.cs` — Current `HttpRequestException` string match for signing rejection

---

### Task 2.4: Add tests for retry behaviour and typed exceptions {#task-24-add-tests-for-retry-behaviour-and-typed-exceptions}

Add unit and integration tests for the new typed exceptions and verify the retry pipeline works correctly.

- **Complexity**: Medium
- **Risk Factors**: Testing Polly retry behaviour in integration tests requires careful timing. Unit tests for typed exceptions are straightforward.
- **Files**:
  - `tests/TradingApp.Api.Tests/Services/HyperliquidOrderServiceTests.cs` — Modify: update signing rejection test to use new exception type
  - `tests/TradingApp.Api.Tests/Controllers/AccountControllerTests.cs` — Verify: existing 503 tests still pass with `Envelope` shape (updated in Phase 1)
- **Success**:
  - Signing rejection test asserts on `SigningException` being thrown
  - All existing tests pass
  - `dotnet build TradingApp.sln` succeeds
  - `dotnet test` passes for all test projects
- **Dependencies**: Tasks 2.1–2.3

#### Implementation Details

```csharp
// tests/TradingApp.Api.Tests/Services/HyperliquidOrderServiceTests.cs — modification
// Update the existing GivenSignatureRejection test:
[TestMethod]
public async Task GivenSignatureRejection_WhenPlaceOrderAsync_ThenThrowsSigningException()
{
    var request = new PlaceOrderRequest
    {
        Asset = "BTC-PERP",
        Side = "buy",
        OrderType = "limit",
        Price = 65000m,
        Size = 0.001m,
    };

    _restClientMock
        .Setup(r => r.PostExchangeAsync<HyperliquidExchangeResponse>(
            It.IsAny<object>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new HyperliquidApiException(
            "signature rejected by exchange", 400, "validation_error"));

    var action = () => _sut.PlaceOrderAsync(request);

    await action.Should().ThrowAsync<SigningException>()
        .WithMessage("*Signature rejected*");
}
```

##### Pattern References

- `tests/TradingApp.Api.Tests/Services/HyperliquidOrderServiceTests.cs` — Current signing rejection test pattern
- `tests/TradingApp.Api.Tests/Infrastructure/FakeHttpMessageHandler.cs` — HTTP simulation helper for potential pipeline tests

## Phase Success Criteria

- `Microsoft.Extensions.Http.Resilience` package is installed and referenced
- HTTP retry pipeline retries on 429 and 5xx with exponential backoff (1s initial, 60s max, 5 attempts)
- REST client throws typed `HyperliquidApiException`/`RateLimitException` instead of generic `HttpRequestException`
- Signing errors are wrapped in `SigningException`
- All retry attempts are logged with attempt number, delay, and status code
- `dotnet build TradingApp.sln` succeeds
- `dotnet test` passes for all projects
