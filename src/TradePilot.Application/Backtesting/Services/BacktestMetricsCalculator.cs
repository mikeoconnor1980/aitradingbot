using TradePilot.Application.Backtesting.Models;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Enums;

namespace TradePilot.Application.Backtesting.Services;

/// <summary>
/// Computes summary backtest metrics from the trade log and equity curve.
/// </summary>
public sealed class BacktestMetricsCalculator
{
    public BacktestResult Calculate(
        IReadOnlyList<BacktestTrade> tradeLog,
        IReadOnlyList<EquitySnapshot> equityTimeSeries,
        decimal initialCapital,
        int gridCycles,
        int candlesReplayed = 0)
    {
        ArgumentNullException.ThrowIfNull(tradeLog);
        ArgumentNullException.ThrowIfNull(equityTimeSeries);

        var completedTrades = tradeLog
            .Where(trade => trade.ExitTimeUtc.HasValue && trade.PnL.HasValue)
            .ToList();

        var totalTrades = completedTrades.Count;
        var winningTrades = completedTrades.Count(trade => trade.PnL > 0);
        var losingTrades = completedTrades.Count(trade => trade.PnL < 0);
        var totalPnL = completedTrades.Sum(trade => trade.PnL ?? 0m);
        var totalFeesPaid = tradeLog.Sum(trade => trade.Fees);
        var hedgesOpened = tradeLog.Count(trade => trade.TradeType == TradeType.HedgeOpen);
        var winRate = totalTrades > 0
            ? Math.Round((decimal)winningTrades / totalTrades * 100m, 2)
            : 0m;
        var averageTradePnL = totalTrades > 0
            ? Math.Round(totalPnL / totalTrades, 4)
            : 0m;
        var averageHoldTime = CalculateAverageHoldTime(completedTrades);
        var (maxDrawdownAbsolute, maxDrawdownPercent) = CalculateMaxDrawdown(equityTimeSeries);
        var finalEquity = equityTimeSeries.Count > 0
            ? equityTimeSeries[^1].Equity
            : initialCapital;
        var rMetrics = CalculateRMetrics(completedTrades);

        return new BacktestResult
        {
            TotalTrades = totalTrades,
            WinningTrades = winningTrades,
            LosingTrades = losingTrades,
            WinRate = winRate,
            TotalPnL = totalPnL,
            MaxDrawdownAbsolute = maxDrawdownAbsolute,
            MaxDrawdownPercent = Math.Round(maxDrawdownPercent, 2),
            AverageTradePnL = averageTradePnL,
            AverageHoldTime = averageHoldTime,
            HedgesOpened = hedgesOpened,
            TotalFeesPaid = totalFeesPaid,
            GridCycles = gridCycles,
            CandlesReplayed = candlesReplayed,
            FinalEquity = finalEquity,
            Expectancy = rMetrics.Expectancy,
            ProfitFactor = rMetrics.ProfitFactor,
            Sqn = rMetrics.Sqn,
            AvgWinR = rMetrics.AvgWinR,
            AvgLossR = rMetrics.AvgLossR,
            RWinRate = rMetrics.RWinRate,
            RDistribution = rMetrics.RDistribution,
            KellyPercent = rMetrics.KellyPercent,
            HalfKellyPercent = rMetrics.HalfKellyPercent,
            WinLossRRatio = rMetrics.WinLossRRatio,
            EquityTimeSeries = equityTimeSeries.ToList(),
            TradeLog = tradeLog.ToList()
        };
    }

    private static TimeSpan CalculateAverageHoldTime(IReadOnlyList<BacktestTrade> completedTrades)
    {
        if (completedTrades.Count == 0)
        {
            return TimeSpan.Zero;
        }

        var totalHoldTimeMs = completedTrades.Sum(trade => trade.ExitTimeUtc!.Value - trade.EntryTimeUtc);
        return TimeSpan.FromMilliseconds(totalHoldTimeMs / completedTrades.Count);
    }

    private static (decimal absolute, decimal percent) CalculateMaxDrawdown(IReadOnlyList<EquitySnapshot> equityTimeSeries)
    {
        if (equityTimeSeries.Count == 0)
        {
            return (0m, 0m);
        }

        var peakEquity = equityTimeSeries[0].Equity;
        var maxDrawdownAbsolute = 0m;
        var maxDrawdownPercent = 0m;

        foreach (var snapshot in equityTimeSeries)
        {
            if (snapshot.Equity > peakEquity)
            {
                peakEquity = snapshot.Equity;
            }

            var drawdownAbsolute = peakEquity - snapshot.Equity;
            if (drawdownAbsolute <= maxDrawdownAbsolute)
            {
                continue;
            }

            maxDrawdownAbsolute = drawdownAbsolute;
            maxDrawdownPercent = peakEquity > 0m
                ? drawdownAbsolute / peakEquity * 100m
                : 0m;
        }

        return (maxDrawdownAbsolute, maxDrawdownPercent);
    }

    private static RMetricsSummary CalculateRMetrics(IReadOnlyList<BacktestTrade> completedTrades)
    {
        var rValues = completedTrades
            .Where(trade => trade.RMultipleResult.HasValue)
            .Select(trade => trade.RMultipleResult!.Value)
            .ToList();

        if (rValues.Count == 0)
        {
            return new RMetricsSummary();
        }

        var winners = rValues.Where(value => value > 0m).ToList();
        var losers = rValues.Where(value => value < 0m).ToList();
        var expectancyRaw = rValues.Average();
        var sumPositiveR = winners.Sum();
        var sumNegativeR = Math.Abs(losers.Sum());
        var avgWinR = winners.Count > 0 ? Math.Round(winners.Average(), 4) : (decimal?)null;
        var avgLossR = losers.Count > 0 ? Math.Round(losers.Average(), 4) : (decimal?)null;
        decimal? winLossRRatio = null;
        decimal? kellyPercent = null;
        decimal? halfKellyPercent = null;
        decimal? sqn = null;

        if (avgWinR.HasValue && avgLossR.HasValue && avgLossR.Value != 0m)
        {
            winLossRRatio = Math.Round(avgWinR.Value / Math.Abs(avgLossR.Value), 4);
            var winFraction = (decimal)winners.Count / rValues.Count;
            kellyPercent = Math.Round(winFraction - ((1m - winFraction) / winLossRRatio.Value), 4);
            halfKellyPercent = Math.Round(kellyPercent.Value / 2m, 4);
        }

        if (rValues.Count > 1)
        {
            var mean = (double)expectancyRaw;
            var variance = rValues.Sum(value => Math.Pow((double)value - mean, 2d)) / (rValues.Count - 1);
            var standardDeviation = Math.Sqrt(variance);

            if (standardDeviation > 0d)
            {
                sqn = Math.Round((decimal)(mean / standardDeviation * Math.Sqrt(rValues.Count)), 4);
            }
        }

        return new RMetricsSummary
        {
            Expectancy = Math.Round(expectancyRaw, 4),
            ProfitFactor = sumNegativeR > 0m ? Math.Round(sumPositiveR / sumNegativeR, 4) : null,
            Sqn = sqn,
            AvgWinR = avgWinR,
            AvgLossR = avgLossR,
            RWinRate = Math.Round((decimal)winners.Count / rValues.Count * 100m, 2),
            RDistribution = rValues,
            KellyPercent = kellyPercent,
            HalfKellyPercent = halfKellyPercent,
            WinLossRRatio = winLossRRatio
        };
    }

    private sealed class RMetricsSummary
    {
        public decimal? Expectancy { get; init; }
        public decimal? ProfitFactor { get; init; }
        public decimal? Sqn { get; init; }
        public decimal? AvgWinR { get; init; }
        public decimal? AvgLossR { get; init; }
        public decimal? RWinRate { get; init; }
        public IReadOnlyList<decimal>? RDistribution { get; init; }
        public decimal? KellyPercent { get; init; }
        public decimal? HalfKellyPercent { get; init; }
        public decimal? WinLossRRatio { get; init; }
    }
}