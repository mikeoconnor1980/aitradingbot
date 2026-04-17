using TradePilot.Domain.Enums;

namespace TradePilot.Application.MacroCalendar.Services;

public static class MacroImportanceMapper
{
    public static MacroEventImportance Map(string? raw)
    {
        var value = raw?.Trim().ToLowerInvariant() ?? string.Empty;

        return value switch
        {
            "high" => MacroEventImportance.High,
            "medium" => MacroEventImportance.Medium,
            "low" => MacroEventImportance.Low,
            "critical" => MacroEventImportance.Critical,
            _ => MacroEventImportance.Unknown,
        };
    }
}
