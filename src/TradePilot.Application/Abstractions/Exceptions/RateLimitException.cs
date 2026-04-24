namespace TradePilot.Application.Abstractions.Exceptions;

/// <summary>
/// Thrown when an exchange returns a 429 Too Many Requests response.
/// After all retry attempts are exhausted, this exception propagates
/// to indicate permanent rate-limit failure.
/// </summary>
public sealed class RateLimitException : ExchangeApiException
{
    public int? RetryAfterSeconds { get; }

    public RateLimitException(string message, int exchangeStatusCode, int? retryAfterSeconds = null, Exception? innerException = null)
        : base(message, exchangeStatusCode, "rate_limit", innerException)
    {
        RetryAfterSeconds = retryAfterSeconds;
    }
}
