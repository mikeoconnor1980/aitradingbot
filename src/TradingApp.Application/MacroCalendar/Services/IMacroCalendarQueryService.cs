using TradingApp.Application.MacroCalendar.Models;
using TradingApp.Domain.Enums;

namespace TradingApp.Application.MacroCalendar.Services;

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
