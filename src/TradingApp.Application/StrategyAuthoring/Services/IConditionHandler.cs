using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.StrategyAuthoring.Services;

/// <summary>
/// Evaluates a single entry condition against market context and indicator data.
/// </summary>
public interface IConditionHandler
{
    EntryConditionType ConditionType { get; }

    ConditionResult Evaluate(EntryConditionConfig condition, IndicatorContext indicatorContext, MarketContext marketContext);
}