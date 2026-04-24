namespace TradePilot.Application.Abstractions.Exceptions;

/// <summary>
/// Base exception for errors returned by exchange APIs.
/// Carries the exchange status code and a machine-readable error category
/// so upstream layers can translate failures consistently.
/// </summary>
public abstract class ExchangeApiException : Exception
{
    protected ExchangeApiException(string message, int exchangeStatusCode, string errorCategory, Exception? innerException = null)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCategory);
        ExchangeStatusCode = exchangeStatusCode;
        ErrorCategory = errorCategory;
    }

    public int ExchangeStatusCode { get; }

    public string ErrorCategory { get; }
}