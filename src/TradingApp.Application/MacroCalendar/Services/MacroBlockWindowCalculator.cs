using Microsoft.Extensions.Options;
using TradingApp.Application.MacroCalendar.Configuration;
using TradingApp.Domain.Enums;

namespace TradingApp.Application.MacroCalendar.Services;

public sealed class MacroBlockWindowCalculator : IMacroBlockWindowCalculator
{
    private readonly MacroCalendarOptions _options;

    public MacroBlockWindowCalculator(IOptions<MacroCalendarOptions> options)
    {
        _options = options.Value;
    }

    public (int PreBlockMinutes, int PostBlockMinutes) GetWindow(MacroEventImportance importance, string category)
    {
        return importance switch
        {
            MacroEventImportance.Critical or MacroEventImportance.High => (
                _options.DefaultPolicies.High.PreBlockMinutes,
                _options.DefaultPolicies.High.PostBlockMinutes),

            MacroEventImportance.Medium => (
                _options.DefaultPolicies.Medium.PreBlockMinutes,
                _options.DefaultPolicies.Medium.PostBlockMinutes),

            MacroEventImportance.Low => (
                _options.DefaultPolicies.Low.PreBlockMinutes,
                _options.DefaultPolicies.Low.PostBlockMinutes),

            _ => (0, 0),
        };
    }
}
