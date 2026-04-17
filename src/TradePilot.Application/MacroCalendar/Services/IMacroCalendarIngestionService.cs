using TradePilot.Application.MacroCalendar.Models;

namespace TradePilot.Application.MacroCalendar.Services;

public interface IMacroCalendarIngestionService
{
    Task<MacroSyncResult> SyncAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);
}
