using TradePilot.Application.MacroCalendar.Models;
using TradePilot.Application.Trading.Models;

namespace TradePilot.Application.Abstractions.Services;

/// <summary>
/// Provides qualitative market context by calling an LLM with recent price action data.
/// Results are cached for the configured duration.
/// </summary>
public interface ILlmContextProvider
{
    Task<LlmContext?> GetContextAsync(
        string symbol,
        IndicatorSnapshot indicators,
        IReadOnlyCollection<MacroEventListItemDto>? upcomingEvents = null,
        CancellationToken cancellationToken = default);
}
