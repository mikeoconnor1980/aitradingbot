using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;

namespace TradePilot.Application.StrategyAuthoring.Services;

/// <summary>
/// Evaluates a single entry condition against market context and indicator data.
/// </summary>
public interface IConditionHandler
{
    EntryConditionType ConditionType { get; }

    IReadOnlyCollection<EntryConditionType> SupportedConditionTypes => new[] { ConditionType };

    ConditionResult Evaluate(EntryConditionConfig condition, IndicatorContext indicatorContext, MarketContext marketContext);
}