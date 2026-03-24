namespace TradingApp.Api.Infrastructure;

public sealed class Envelope
{
    public string ErrorMessage { get; }
    public DateTime Timestamp { get; }

    public Envelope(string errorMessage)
    {
        ErrorMessage = errorMessage;
        Timestamp = DateTime.UtcNow;
    }
}