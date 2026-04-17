using TradePilot.Domain.Enums;

namespace TradePilot.Application.MacroCalendar.Models;

public sealed class MacroEventListItemDto
{
    public Guid Id { get; init; }
    public required string Title { get; init; }
    public required string Country { get; init; }
    public required string Currency { get; init; }
    public required string Category { get; init; }
    public long ScheduledAtUtc { get; init; }
    public MacroEventImportance Importance { get; init; }
    public MacroEventStatus Status { get; init; }
    public string? Forecast { get; init; }
    public string? Previous { get; init; }
    public string? Actual { get; init; }
    public long BlockStartUtc { get; init; }
    public long BlockEndUtc { get; init; }
    public bool IsBlockingNow { get; init; }
}
