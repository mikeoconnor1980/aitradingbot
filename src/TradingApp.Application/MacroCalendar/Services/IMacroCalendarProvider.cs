using TradingApp.Application.MacroCalendar.Models;

namespace TradingApp.Application.MacroCalendar.Services;

public interface IMacroCalendarProvider
{
    string ProviderName { get; }

    Task<IReadOnlyCollection<ExternalMacroEventDto>> GetEventsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);
}
