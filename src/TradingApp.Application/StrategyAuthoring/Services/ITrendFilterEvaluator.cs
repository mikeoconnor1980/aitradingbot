using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.StrategyAuthoring.Services;

public interface ITrendFilterEvaluator
{
    TrendFilterResult Evaluate(
        TrendFilterConfig? filter,
        Direction strategyDirection,
        IndicatorContext indicatorContext,
        MarketContext marketContext);
}