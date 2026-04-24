namespace TradePilot.Application.Abstractions.Exceptions;

/// <summary>
/// Binance-specific exchange API exception with Binance error-code details.
/// </summary>
public sealed class BinanceApiException : ExchangeApiException
{
    public BinanceApiException(string message, int httpStatusCode, int? binanceErrorCode, bool isTransient, Exception? innerException = null)
        : base(message, httpStatusCode, isTransient ? "binance_api_transient" : "binance_api_error", innerException)
    {
        BinanceErrorCode = binanceErrorCode;
        IsTransient = isTransient;
    }

    public int? BinanceErrorCode { get; }

    public bool IsTransient { get; }
}