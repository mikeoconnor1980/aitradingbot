namespace TradePilot.Application.Abstractions.Exceptions;

/// <summary>
/// Base exception for errors returned by the Hyperliquid exchange API.
/// Carries the HTTP status code and a machine-readable error category
/// so the global exception filter can map it to a meaningful response.
/// </summary>
public sealed class HyperliquidApiException : ExchangeApiException
{
    public HyperliquidApiException(string message, int exchangeStatusCode, string errorCategory, Exception? innerException = null)
        : base(message, exchangeStatusCode, errorCategory, innerException)
    {
    }
}
