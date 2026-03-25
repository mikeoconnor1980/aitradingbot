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
        var (statusCode, envelope) = context.Exception switch
        {
            DomainException ex => (StatusCodes.Status400BadRequest, new Envelope(ex.Message)),
            NotFoundException ex => (StatusCodes.Status404NotFound, new Envelope(ex.Message)),
            UnauthorizedAccessException ex => (StatusCodes.Status403Forbidden, new Envelope(ex.Message)),
            HttpRequestException => (StatusCodes.Status503ServiceUnavailable, new Envelope("External service unavailable")),
            JsonException => (StatusCodes.Status502BadGateway, new Envelope("Invalid response from external service")),
            InvalidOperationException => (StatusCodes.Status502BadGateway, new Envelope("Invalid response from external service")),
            _ => (StatusCodes.Status500InternalServerError, new Envelope("An unexpected error occurred")),
        };

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(context.Exception, "Unhandled exception occurred");
        }
        else
        {
            _logger.LogWarning(context.Exception, "Handled exception mapped to {StatusCode}", statusCode);
        }

        context.Result = new ObjectResult(envelope)
        {
            StatusCode = statusCode,
        };

        context.ExceptionHandled = true;
    }
}