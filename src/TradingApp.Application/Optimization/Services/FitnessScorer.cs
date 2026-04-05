using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Optimization.Models;

namespace TradingApp.Application.Optimization.Services;

public interface IFitnessScorer
{
    bool IsQualified(BacktestResult result, FitnessThresholds thresholds, decimal initialCapital);
    decimal Score(BacktestResult result);
}

public sealed class FitnessScorer : IFitnessScorer
{
    private const decimal DrawdownEpsilon = 0.01m;

    public bool IsQualified(BacktestResult result, FitnessThresholds thresholds, decimal initialCapital)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(thresholds);

        if (result.TotalTrades < thresholds.MinTotalTrades)
        {
            return false;
        }

        if (result.WinRate < thresholds.MinWinRate)
        {
            return false;
        }

        var drawdownPercent = initialCapital > 0m
            ? (result.MaxDrawdownAbsolute / initialCapital) * 100m
            : 100m;

        return drawdownPercent < thresholds.MaxDrawdownPercent;
    }

    public decimal Score(BacktestResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.TotalTrades == 0)
        {
            return decimal.MinValue;
        }

        var drawdown = result.MaxDrawdownAbsolute > 0m
            ? result.MaxDrawdownAbsolute
            : DrawdownEpsilon;

        var riskAdjustedPnl = result.TotalPnL / drawdown;
        var tradeFactor = (decimal)Math.Sqrt(result.TotalTrades);

        return riskAdjustedPnl * tradeFactor;
    }
}