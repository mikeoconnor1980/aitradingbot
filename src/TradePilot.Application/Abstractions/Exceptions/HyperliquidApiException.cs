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
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCategory);
        ExchangeStatusCode = exchangeStatusCode;
        ErrorCategory = errorCategory;
    }
}
