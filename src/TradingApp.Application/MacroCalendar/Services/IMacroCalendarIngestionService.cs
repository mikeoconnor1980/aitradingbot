using TradingApp.Application.MacroCalendar.Models;

namespace TradingApp.Application.MacroCalendar.Services;

public interface IMacroCalendarIngestionService
{
    Task<MacroSyncResult> SyncAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);
}
