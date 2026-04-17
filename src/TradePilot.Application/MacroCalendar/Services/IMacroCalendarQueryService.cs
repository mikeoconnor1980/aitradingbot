using TradePilot.Application.MacroCalendar.Models;
using TradePilot.Domain.Enums;

namespace TradePilot.Application.MacroCalendar.Services;

public interface IMacroCalendarQueryService
{
    Task<IReadOnlyCollection<MacroEventListItemDto>> GetUpcomingEventsAsync(
        long fromUtcMs,
        long toUtcMs,
        string? currency,
        MacroEventImportance? minimumImportance,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<MacroEventListItemDto>> GetActiveBlockWindowsAsync(
        long nowUtcMs,
        CancellationToken cancellationToken);
}
