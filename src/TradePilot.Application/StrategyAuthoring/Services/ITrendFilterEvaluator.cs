using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;

namespace TradePilot.Application.StrategyAuthoring.Services;

public interface ITrendFilterEvaluator
{
    TrendFilterResult Evaluate(
        TrendFilterConfig? filter,
        Direction strategyDirection,
        IndicatorContext indicatorContext,
        MarketContext marketContext);
}