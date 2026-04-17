using System.Diagnostics;

namespace TradePilot.Api.Infrastructure;

public sealed class Envelope
{
    public string ErrorMessage { get; }
    public string? ErrorCode { get; }
    public string CorrelationId { get; }
    public DateTime Timestamp { get; }

    public Envelope(string errorMessage, string? errorCode = null, string? correlationId = null)
    {
        ErrorMessage = errorMessage;
        ErrorCode = errorCode;
        CorrelationId = correlationId ?? Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        Timestamp = DateTime.UtcNow;
    }
}