using TradePilot.Domain.Enums;

namespace TradePilot.Application.MacroCalendar.Services;

public interface IMacroBlockWindowCalculator
{
    (int PreBlockMinutes, int PostBlockMinutes) GetWindow(MacroEventImportance importance, string category);
}
