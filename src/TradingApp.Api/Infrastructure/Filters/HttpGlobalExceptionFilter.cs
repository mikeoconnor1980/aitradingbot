using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TradingApp.Application.Abstractions.Exceptions;

namespace TradingApp.Api.Infrastructure.Filters;

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

            IngestionAlreadyRunningException ex => (
                StatusCodes.Status409Conflict,
                new Envelope(ex.Message, "ingestion_conflict", correlationId)),

            BacktestUnavailableException ex => (
                StatusCodes.Status503ServiceUnavailable,
                new Envelope(ex.Message, "backtest_unavailable", correlationId)),

            HyperliquidApiException ex when ex.ExchangeStatusCode >= 400 && ex.ExchangeStatusCode < 500 => (
                StatusCodes.Status400BadRequest,
                new Envelope(ex.Message, ex.ErrorCategory, correlationId)),

            HyperliquidApiException ex => (
                StatusCodes.Status502BadGateway,
                new Envelope(ex.Message, ex.ErrorCategory, correlationId)),

            HttpRequestException => (
                StatusCodes.Status503ServiceUnavailable,
                new Envelope("External service unavailable", "network_error", correlationId)),

            OperationCanceledException => (
                StatusCodes.Status408RequestTimeout,
                new Envelope("Request was cancelled or exceeded maximum timeout", "request_timeout", correlationId)),

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