using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Enums;

namespace TradingApp.Application.Backtesting.Services;

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
}