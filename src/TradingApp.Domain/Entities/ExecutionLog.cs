namespace TradingApp.Domain.Entities;

public sealed class ExecutionLog
{
    public Guid Id { get; set; }
    public string AgentId { get; set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    /// <summary>Optional structured data serialized as JSON.</summary>
    public string? Data { get; set; }

    public DateTimeOffset ReceivedAtUtc { get; set; }
}
