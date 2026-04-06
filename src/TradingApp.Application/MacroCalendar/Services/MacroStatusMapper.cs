using TradingApp.Domain.Enums;

namespace TradingApp.Application.MacroCalendar.Services;

public static class MacroStatusMapper
{
    public static MacroEventStatus Map(string? raw)
    {
        var value = raw?.Trim().ToLowerInvariant() ?? string.Empty;

        return value switch
        {
            "live" => MacroEventStatus.Live,
            "released" => MacroEventStatus.Released,
            "revised" => MacroEventStatus.Revised,
            "cancelled" => MacroEventStatus.Cancelled,
            _ => MacroEventStatus.Scheduled,
        };
    }
}
