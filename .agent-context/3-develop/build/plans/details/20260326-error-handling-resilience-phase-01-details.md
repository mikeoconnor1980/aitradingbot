<!-- markdownlint-disable-file -->

# Task Details: F8 — Error Handling & Resilience

## Phase 1: Backend Error Infrastructure

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — Sealed classes, `_prefix` private fields, `Create()` factory methods, `IOptions<T>`, async/await with `CancellationToken`
- `.github/instructions/testing.instructions.md` — MSTest, Moq, FluentAssertions ≤ v6, Given/When/Then naming, controller tests via WebApplicationFactory
- `.github/instructions/dotnet-architecture.instructions.md` — Exception throwing in Application/Core layers, handling at API layer via middleware
- `.github/instructions/api-controllers.instructions.md` — Extend `ApiController`, `Envelope` responses, `ProducesResponseType` attributes
- `.agent-context/0-knowledge/06-project-structure.md` — Exception types in `Application/Abstractions/Exceptions/`, filter in `Api/Infrastructure/Filters/`

## Design References

- Existing `DomainException` and `NotFoundException` in `TradePilot.Application.Abstractions.Exceptions` establish the exception pattern
- `HttpGlobalExceptionFilter` establishes the exception-to-HTTP-status mapping pattern
- `Envelope` establishes the error response shape

### Task 1.1: Create typed exception hierarchy for Hyperliquid errors {#task-11-create-typed-exception-hierarchy}

Create typed exception classes for Hyperliquid-specific error categories so the global exception filter can map them to appropriate HTTP status codes with specific error messages.

- **Complexity**: Medium
- **Risk Factors**: Must align exception hierarchy with existing `DomainException`/`NotFoundException` pattern
- **Files**:
  - `src/TradePilot.Application/Abstractions/Exceptions/HyperliquidApiException.cs` — New: Base exception for all Hyperliquid API errors
  - `src/TradePilot.Application/Abstractions/Exceptions/RateLimitException.cs` — New: 429 rate-limit exception with `RetryAfterSeconds`
  - `src/TradePilot.Application/Abstractions/Exceptions/SigningException.cs` — New: EIP-712 signing failure exception
- **Success**:
  - `HyperliquidApiException` exists with `StatusCode` and `ErrorCategory` properties
  - `RateLimitException` extends `HyperliquidApiException` with `RetryAfterSeconds`
  - `SigningException` extends `HyperliquidApiException` for signing-specific failures
  - All exception classes are `sealed`
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradePilot.Application/Abstractions/Exceptions/HyperliquidApiException.cs — new file
namespace TradePilot.Application.Abstractions.Exceptions;

/// <summary>
/// Base exception for errors returned by the Hyperliquid exchange API.
/// Carries the HTTP status code and a machine-readable error category
/// so the global exception filter can map it to a meaningful response.
/// </summary>
public class HyperliquidApiException : Exception
{
    public int ExchangeStatusCode { get; }
    public string ErrorCategory { get; }

    public HyperliquidApiException(string message, int exchangeStatusCode, string errorCategory, Exception? innerException = null)
        : base(message, innerException)
    {
        ExchangeStatusCode = exchangeStatusCode;
        ErrorCategory = errorCategory;
    }
}
```

```csharp
// src/TradePilot.Application/Abstractions/Exceptions/RateLimitException.cs — new file
namespace TradePilot.Application.Abstractions.Exceptions;

/// <summary>
/// Thrown when Hyperliquid returns a 429 Too Many Requests response.
/// After all retry attempts are exhausted, this exception propagates
/// to indicate permanent rate-limit failure.
/// </summary>
public sealed class RateLimitException : HyperliquidApiException
{
    public int? RetryAfterSeconds { get; }

    public RateLimitException(string message, int? retryAfterSeconds = null, Exception? innerException = null)
        : base(message, 429, "rate_limit", innerException)
    {
        RetryAfterSeconds = retryAfterSeconds;
    }
}
```

```csharp
// src/TradePilot.Application/Abstractions/Exceptions/SigningException.cs — new file
namespace TradePilot.Application.Abstractions.Exceptions;

/// <summary>
/// Thrown when EIP-712 signing fails or the exchange rejects the signature.
/// Distinguished from other API errors for specific UI messaging and logging.
/// </summary>
public sealed class SigningException : HyperliquidApiException
{
    public SigningException(string message, Exception? innerException = null)
        : base(message, 0, "signing_error", innerException)
    {
    }
}
```

##### Pattern References

- `src/TradePilot.Application/Abstractions/Exceptions/DomainException.cs` — Established sealed exception pattern
- `src/TradePilot.Application/Abstractions/Exceptions/NotFoundException.cs` — Two-constructor pattern with sealed class

---

### Task 1.2: Enhance Envelope with ErrorCode and CorrelationId {#task-12-enhance-envelope-with-errorcode-and-correlationid}

Extend the existing `Envelope` class to include a machine-readable `ErrorCode` and a `CorrelationId` that links the error response to the corresponding log entry.

- **Complexity**: Medium
- **Risk Factors**: Additive change to existing shape — frontend must handle the new optional fields. Existing tests that assert on `Envelope` shape must be updated.
- **Files**:
  - `src/TradePilot.Api/Infrastructure/Envelope.cs` — Modify: add `ErrorCode` and `CorrelationId` properties
- **Success**:
  - `Envelope` has `ErrorCode` (nullable string) and `CorrelationId` (string) properties
  - Existing constructor still works (backward-compatible)
  - New constructor accepts error code and correlation ID
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradePilot.Api/Infrastructure/Envelope.cs — modification
namespace TradePilot.Api.Infrastructure;

public sealed class Envelope
{
    public string ErrorMessage { get; }
    public string? ErrorCode { get; }
    public string CorrelationId { get; }
    public DateTime Timestamp { get; }

    public Envelope(string errorMessage, string? errorCode = null, string? correlationId = null)
    {
        ErrorMessage = errorMessage;
        ErrorCode = errorCode;
        CorrelationId = correlationId ?? Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        Timestamp = DateTime.UtcNow;
    }
}
```

Note: `System.Diagnostics.Activity` is available from `System.Diagnostics.DiagnosticSource` which is already a transitive dependency of ASP.NET Core. Add `using System.Diagnostics;` at the top.

##### Pattern References

- `src/TradePilot.Api/Infrastructure/Envelope.cs` — Current implementation with `ErrorMessage` + `Timestamp` only

---

### Task 1.3: Add correlation ID middleware {#task-13-add-correlation-id-middleware}

Create ASP.NET Core middleware that ensures every request has a correlation ID — either from an incoming `X-Correlation-ID` header or auto-generated. The correlation ID is added to the response header and to the logger scope for all log entries.

- **Complexity**: Medium
- **Risk Factors**: Must be registered early in the pipeline (before controllers) to ensure all log entries are enriched
- **Files**:
  - `src/TradePilot.Api/Infrastructure/CorrelationIdMiddleware.cs` — New: middleware class
- **Success**:
  - Incoming requests with `X-Correlation-ID` header reuse that value
  - Requests without the header get an auto-generated correlation ID
  - Response includes `X-Correlation-ID` header
  - All log entries within the request scope include `CorrelationId` structured field
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradePilot.Api/Infrastructure/CorrelationIdMiddleware.cs — new file
using System.Diagnostics;

namespace TradePilot.Api.Infrastructure;

public sealed class CorrelationIdMiddleware
{
    private const string CorrelationIdHeaderName = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
    {
        var correlationId = context.Request.Headers[CorrelationIdHeaderName].FirstOrDefault()
            ?? Activity.Current?.TraceId.ToString()
            ?? Guid.NewGuid().ToString("N");

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers[CorrelationIdHeaderName] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
        }))
        {
            await _next(context);
        }
    }
}
```

##### Pattern References

- `src/TradePilot.Api/Program.cs` — Middleware registration pattern (`app.UseCors()`, `app.MapControllers()`)

---

### Task 1.4: Update HttpGlobalExceptionFilter with new exception mappings {#task-14-update-httpglobalexceptionfilter-with-new-exception-mappings}

Update the global exception filter to handle the new typed exceptions with appropriate HTTP status codes, error codes, and correlation IDs. Remove the overly-broad `InvalidOperationException → 502` mapping.

- **Complexity**: Medium
- **Risk Factors**: Changing exception mappings affects all error responses — must update tests to match. Removing `InvalidOperationException → 502` means those will now fall to the default 500 handler.
- **Files**:
  - `src/TradePilot.Api/Infrastructure/Filters/HttpGlobalExceptionFilter.cs` — Modify: add new exception cases, pass correlation ID and error code to Envelope
- **Success**:
  - `RateLimitException` maps to 429 with `error_code: "rate_limit"`
  - `SigningException` maps to 500 with `error_code: "signing_error"`
  - `HyperliquidApiException` maps based on `ExchangeStatusCode` ranges (4xx → 400, 5xx → 502)
  - `InvalidOperationException` no longer maps to 502 (falls through to 500)
  - All error responses include `CorrelationId`
  - Log entries include error category and correlation ID as structured fields
- **Dependencies**: Task 1.1, Task 1.2, Task 1.3

#### Implementation Details

```csharp
// src/TradePilot.Api/Infrastructure/Filters/HttpGlobalExceptionFilter.cs — modification
using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TradePilot.Application.Abstractions.Exceptions;

namespace TradePilot.Api.Infrastructure.Filters;

public sealed class HttpGlobalExceptionFilter : IExceptionFilter
{
    private readonly ILogger<HttpGlobalExceptionFilter> _logger;

    public HttpGlobalExceptionFilter(ILogger<HttpGlobalExceptionFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        var correlationId = context.HttpContext.Items["CorrelationId"]?.ToString()
            ?? Activity.Current?.TraceId.ToString()
            ?? Guid.NewGuid().ToString("N");

        var (statusCode, envelope) = context.Exception switch
        {
            DomainException ex => (
                StatusCodes.Status400BadRequest,
                new Envelope(ex.Message, "validation_error", correlationId)),

            NotFoundException ex => (
                StatusCodes.Status404NotFound,
                new Envelope(ex.Message, "not_found", correlationId)),

            UnauthorizedAccessException ex => (
                StatusCodes.Status403Forbidden,
                new Envelope(ex.Message, "unauthorized", correlationId)),

            RateLimitException ex => (
                StatusCodes.Status429TooManyRequests,
                new Envelope(ex.Message, "rate_limit", correlationId)),

            SigningException ex => (
                StatusCodes.Status422UnprocessableEntity,
                new Envelope(ex.Message, "signing_error", correlationId)),

            HyperliquidApiException ex when ex.ExchangeStatusCode >= 400 && ex.ExchangeStatusCode < 500 => (
                StatusCodes.Status400BadRequest,
                new Envelope(ex.Message, ex.ErrorCategory, correlationId)),

            HyperliquidApiException ex => (
                StatusCodes.Status502BadGateway,
                new Envelope(ex.Message, ex.ErrorCategory, correlationId)),

            HttpRequestException => (
                StatusCodes.Status503ServiceUnavailable,
                new Envelope("External service unavailable", "network_error", correlationId)),

            JsonException => (
                StatusCodes.Status502BadGateway,
                new Envelope("Invalid response from external service", "deserialization_error", correlationId)),

            _ => (
                StatusCodes.Status500InternalServerError,
                new Envelope("An unexpected error occurred", "internal_error", correlationId)),
        };

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                context.Exception,
                "Unhandled exception occurred. CorrelationId={CorrelationId}, ErrorCode={ErrorCode}, Endpoint={Endpoint}",
                correlationId,
                envelope.ErrorCode,
                context.HttpContext.Request.Path);
        }
        else
        {
            _logger.LogWarning(
                context.Exception,
                "Handled exception mapped to {StatusCode}. CorrelationId={CorrelationId}, ErrorCode={ErrorCode}, Endpoint={Endpoint}",
                statusCode,
                correlationId,
                envelope.ErrorCode,
                context.HttpContext.Request.Path);
        }

        context.Result = new ObjectResult(envelope)
        {
            StatusCode = statusCode,
        };

        context.ExceptionHandled = true;
    }
}
```

##### Pattern References

- `src/TradePilot.Api/Infrastructure/Filters/HttpGlobalExceptionFilter.cs` — Current implementation with 6-case switch expression

---

### Task 1.5: Refactor AccountController to use global exception filter {#task-15-refactor-accountcontroller-to-use-global-exception-filter}

Remove the per-endpoint try/catch blocks from `AccountController` and let exceptions propagate to `HttpGlobalExceptionFilter`. Change the base class to `ApiController` for consistency. This eliminates the inconsistent anonymous `{ error: "..." }` response shape.

- **Complexity**: Medium
- **Risk Factors**: Existing tests assert on the anonymous error shape (`body.GetProperty("error")`). Tests must be updated to assert on `Envelope` shape (`body.GetProperty("errorMessage")`).
- **Files**:
  - `src/TradePilot.Api/Controllers/AccountController.cs` — Modify: remove try/catch, change base class to `ApiController`, add `ProducesResponseType` attributes
  - `tests/TradePilot.Api.Tests/Controllers/AccountControllerTests.cs` — Modify: update error assertions to use `Envelope` shape
- **Success**:
  - `AccountController` extends `ApiController` (not `ControllerBase`)
  - No try/catch blocks in controller methods
  - Error responses use `Envelope` shape via global filter
  - All existing `AccountControllerTests` pass with updated assertions
- **Dependencies**: Task 1.4

#### Implementation Details

The controller methods should become thin calls that let exceptions propagate:

```csharp
// src/TradePilot.Api/Controllers/AccountController.cs — modification (example endpoint)
// Before:
// try {
//     var summary = await _accountService.GetAccountSummaryAsync(cancellationToken);
//     return Ok(summary);
// } catch (HttpRequestException ex) {
//     return StatusCode(503, new { error = "..." });
// } catch (JsonException ex) {
//     return StatusCode(502, new { error = "..." });
// }

// After:
[HttpGet]
[ProducesResponseType(typeof(AccountSummaryDto), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
public async Task<IActionResult> GetAccountSummary(CancellationToken cancellationToken)
{
    var summary = await _accountService.GetAccountSummaryAsync(cancellationToken);
    return Ok(summary);
}
```

Test update pattern:
```csharp
// tests/TradePilot.Api.Tests/Controllers/AccountControllerTests.cs — modification
// Before:
// body.GetProperty("error").GetString().Should().Be("Hyperliquid API is unavailable");

// After:
body.GetProperty("errorMessage").GetString().Should().Be("External service unavailable");
body.GetProperty("correlationId").GetString().Should().NotBeNullOrEmpty();
```

##### Pattern References

- `src/TradePilot.Api/Controllers/MarketDataController.cs` — Controller that correctly extends `ApiController` with no try/catch
- `src/TradePilot.Api/Controllers/OrdersController.cs` — Controller that throws exceptions for filter handling

---

### Task 1.6: Register middleware and update tests {#task-16-register-middleware-and-update-tests}

Register the correlation ID middleware in `Program.cs` and ensure all existing and new tests pass.

- **Complexity**: Low
- **Risk Factors**: Middleware order matters — correlation ID must be registered before CORS and controllers
- **Files**:
  - `src/TradePilot.Api/Program.cs` — Modify: register `CorrelationIdMiddleware`
  - `tests/TradePilot.Api.Tests/Controllers/AccountControllerTests.cs` — Already updated in Task 1.5
- **Success**:
  - `CorrelationIdMiddleware` is registered in the pipeline before `UseCors()`
  - `dotnet build TradePilot.sln` passes
  - `dotnet test` passes for all test projects
- **Dependencies**: Task 1.3, Task 1.5

#### Implementation Details

```csharp
// src/TradePilot.Api/Program.cs — modification
// ... existing code ...
var app = builder.Build();

// Add before UseCors:
app.UseMiddleware<CorrelationIdMiddleware>();

app.UseCors();
app.MapControllers();
app.MapHub<MarketDataHub>("/hubs/marketdata");
// ... existing code ...
```

##### Pattern References

- `src/TradePilot.Api/Program.cs` — Current middleware pipeline order

## Phase Success Criteria

- All API error responses return `Envelope` with `ErrorMessage`, `ErrorCode`, `CorrelationId`, and `Timestamp`
- No controller has shadow try/catch blocks — all errors flow through `HttpGlobalExceptionFilter`
- Log entries include `CorrelationId` structured field via middleware log scope
- `dotnet build TradePilot.sln` succeeds
- `dotnet test` passes for all projects including updated `AccountControllerTests`
