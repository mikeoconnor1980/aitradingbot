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
