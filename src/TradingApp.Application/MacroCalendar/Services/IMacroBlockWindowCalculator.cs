using TradingApp.Domain.Enums;

namespace TradingApp.Application.MacroCalendar.Services;

public interface IMacroBlockWindowCalculator
{
    (int PreBlockMinutes, int PostBlockMinutes) GetWindow(MacroEventImportance importance, string category);
}
