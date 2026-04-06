namespace TradingApp.Application.MacroCalendar.Models;

public sealed class ExternalMacroEventDto
{
    public required string ProviderEventId { get; init; }
    public required string Title { get; init; }
    public required string Country { get; init; }
    public required string Currency { get; init; }
    public required string Category { get; init; }
    public required long ScheduledAtUtcMs { get; init; }
    public long? ReleasedAtUtcMs { get; init; }
    public required string ImportanceRaw { get; init; }
    public required string StatusRaw { get; init; }
    public string? Actual { get; init; }
    public string? Forecast { get; init; }
    public string? Previous { get; init; }
    public string? Revised { get; init; }
    public string? SourceUrl { get; init; }
    public string? RawPayloadJson { get; init; }
}
