namespace TradePilot.Application.Abstractions.Exceptions;

/// <summary>
/// Thrown when EIP-712 signing fails or the exchange rejects the signature.
/// Distinguished from other API errors for specific UI messaging and logging.
/// </summary>
public sealed class SigningException : ExchangeApiException
{
    public SigningException(string message, Exception? innerException = null)
        : base(message, 0, "signing_error", innerException)
    {
    }
}
