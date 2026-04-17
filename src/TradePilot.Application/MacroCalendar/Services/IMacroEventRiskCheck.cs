using TradePilot.Application.MacroCalendar.Models;

namespace TradePilot.Application.MacroCalendar.Services;

/// <summary>
/// Checks whether new entries should be blocked due to active macro event windows.
/// Designed to be called by the RiskEngine or before order submission.
/// </summary>
public interface IMacroEventRiskCheck
{
    Task<MacroRiskCheckResult> CheckNewEntryAsync(CancellationToken cancellationToken);
}

public sealed class MacroRiskCheckResult
{
    public bool IsBlocked { get; private init; }
    public string Reason { get; private init; } = string.Empty;
    public MacroEventListItemDto? BlockingEvent { get; private init; }

    public static MacroRiskCheckResult Allowed()
        => new() { IsBlocked = false, Reason = "No active macro block window" };

    public static MacroRiskCheckResult Blocked(string reason, MacroEventListItemDto blockingEvent)
        => new() { IsBlocked = true, Reason = reason, BlockingEvent = blockingEvent };
}
