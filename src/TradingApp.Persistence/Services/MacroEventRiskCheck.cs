using TradingApp.Application.MacroCalendar.Models;
using TradingApp.Application.MacroCalendar.Services;

namespace TradingApp.Persistence.Services;

public sealed class MacroEventRiskCheck : IMacroEventRiskCheck
{
    private readonly IMacroCalendarQueryService _queryService;

    public MacroEventRiskCheck(IMacroCalendarQueryService queryService)
    {
        _queryService = queryService;
    }

    public async Task<MacroRiskCheckResult> CheckNewEntryAsync(CancellationToken cancellationToken)
    {
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var activeBlocks = await _queryService.GetActiveBlockWindowsAsync(nowMs, cancellationToken);

        if (activeBlocks.Count == 0)
            return MacroRiskCheckResult.Allowed();

        var first = activeBlocks.First();
        return MacroRiskCheckResult.Blocked(
            $"New entries blocked due to macro event: {first.Title} ({first.Country})",
            first);
    }
}
