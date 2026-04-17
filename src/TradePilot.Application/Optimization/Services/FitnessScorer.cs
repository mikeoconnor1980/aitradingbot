using TradePilot.Application.Backtesting.Models;
using TradePilot.Application.Optimization.Models;

namespace TradePilot.Application.Optimization.Services;

public interface IFitnessScorer
{
    bool IsQualified(BacktestResult result, FitnessThresholds thresholds, decimal initialCapital);
    decimal Score(BacktestResult result);
    FitnessMetrics ComputeMetrics(BacktestResult result);
}

public sealed class FitnessScorer : IFitnessScorer
{
    private const decimal DrawdownEpsilon = 0.01m;

    /// <summary>
    /// Trade count at which metric bonuses reach full weight.
    /// Below this, Sharpe/PF bonuses are scaled down because they are
    /// statistically unreliable on small sample sizes.
    /// </summary>
    private const int MinReliableTrades = 20;

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

        var metrics = ComputeMetrics(result);

        var drawdown = result.MaxDrawdownAbsolute > 0m
            ? result.MaxDrawdownAbsolute
            : DrawdownEpsilon;

        var riskAdjustedPnl = result.TotalPnL / drawdown;
        var tradeFactor = (decimal)Math.Sqrt(result.TotalTrades);
        var baseScore = riskAdjustedPnl * tradeFactor;

        // Blend richer metrics into score (additive bonus/penalty)
        var sharpeBonus = Math.Clamp(metrics.SharpeRatio, -2m, 5m);
        var profitFactorBonus = metrics.ProfitFactor > 1m
            ? Math.Min(metrics.ProfitFactor - 1m, 3m)
            : -(1m - Math.Max(metrics.ProfitFactor, 0m));

        // Scale metric bonuses by statistical confidence — with few trades,
        // Sharpe/PF are unreliable, so their contribution is dampened.
        var confidence = (decimal)Math.Min(1.0, Math.Sqrt(result.TotalTrades) / Math.Sqrt(MinReliableTrades));

        return baseScore + confidence * ((sharpeBonus * 2m) + profitFactorBonus);
    }

    public FitnessMetrics ComputeMetrics(BacktestResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var tradePnls = result.TradeLog
            .Where(t => t.PnL.HasValue)
            .Select(t => t.PnL!.Value)
            .ToList();

        var sharpe = ComputeSharpeRatio(tradePnls);
        var sortino = ComputeSortinoRatio(tradePnls);
        var profitFactor = ComputeProfitFactor(tradePnls);
        var calmar = ComputeCalmarRatio(result.TotalPnL, result.MaxDrawdownAbsolute);

        return new FitnessMetrics
        {
            SharpeRatio = sharpe,
            SortinoRatio = sortino,
            ProfitFactor = profitFactor,
            CalmarRatio = calmar,
        };
    }

    private static decimal ComputeSharpeRatio(List<decimal> pnls)
    {
        if (pnls.Count < 2)
        {
            return 0m;
        }

        var mean = pnls.Average();
        var variance = pnls.Sum(p => (p - mean) * (p - mean)) / (pnls.Count - 1);
        var stdDev = (decimal)Math.Sqrt((double)variance);

        return stdDev > 0m ? mean / stdDev : 0m;
    }

    private static decimal ComputeSortinoRatio(List<decimal> pnls)
    {
        if (pnls.Count < 2)
        {
            return 0m;
        }

        var mean = pnls.Average();
        var downsideVariance = pnls
            .Where(p => p < 0m)
            .Sum(p => p * p) / pnls.Count;
        var downsideDev = (decimal)Math.Sqrt((double)downsideVariance);

        return downsideDev > 0m ? mean / downsideDev : (mean > 0m ? 10m : 0m);
    }

    private static decimal ComputeProfitFactor(List<decimal> pnls)
    {
        var grossProfit = pnls.Where(p => p > 0m).Sum();
        var grossLoss = Math.Abs(pnls.Where(p => p < 0m).Sum());

        return grossLoss > 0m ? grossProfit / grossLoss : (grossProfit > 0m ? 100m : 0m);
    }

    private static decimal ComputeCalmarRatio(decimal totalPnl, decimal maxDrawdown)
    {
        return maxDrawdown > 0m ? totalPnl / maxDrawdown : (totalPnl > 0m ? 100m : 0m);
    }
}