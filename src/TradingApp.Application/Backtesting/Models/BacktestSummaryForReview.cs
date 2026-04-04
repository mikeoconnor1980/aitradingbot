using System.Text.Json;
using TradingApp.Domain.Entities;
using TradingApp.Domain.Enums;

namespace TradingApp.Application.Backtesting.Models;

public sealed class BacktestSummaryForReview
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private BacktestSummaryForReview()
    {
    }

    public int TotalTrades { get; private set; }
    public decimal WinRate { get; private set; }
    public decimal TotalPnL { get; private set; }
    public decimal MaxDrawdownAbsolute { get; private set; }
    public decimal MaxDrawdownPercent { get; private set; }
    public decimal AverageTradePnL { get; private set; }
    public double AverageHoldTimeMinutes { get; private set; }
    public decimal TotalFeesPaid { get; private set; }
    public decimal InitialCapital { get; private set; }
    public decimal FinalEquity { get; private set; }
    public decimal ReturnPercent { get; private set; }
    public int CandlesReplayed { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public int DurationDays { get; private set; }
    public string DataQuality { get; private set; } = string.Empty;
    public string EquityCurveSummary { get; private set; } = string.Empty;

    public static BacktestSummaryForReview? FromBacktestRun(BacktestRun run)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (run.Status != BacktestStatus.Completed)
        {
            return null;
        }

        var startDate = DateTimeOffset.FromUnixTimeMilliseconds(run.StartDateUtc).UtcDateTime;
        var endDate = DateTimeOffset.FromUnixTimeMilliseconds(run.EndDateUtc).UtcDateTime;
        var durationDays = (int)(endDate - startDate).TotalDays;

        var dataQuality = durationDays switch
        {
            < 14 => "insufficient",
            < 30 => "limited",
            _ => "reliable",
        };

        var equitySeries = ParseEquityTimeSeries(run.EquityTimeSeriesJson);
        var finalEquity = equitySeries.Count > 0
            ? equitySeries[^1].Equity
            : run.InitialCapital + run.TotalPnl;
        var returnPercent = run.InitialCapital > 0
            ? (finalEquity - run.InitialCapital) / run.InitialCapital * 100m
            : 0m;
        var maxDrawdownPercent = run.InitialCapital > 0
            ? run.MaxDrawdown / run.InitialCapital * 100m
            : 0m;

        return new BacktestSummaryForReview
        {
            TotalTrades = run.TotalTrades,
            WinRate = run.WinRate,
            TotalPnL = run.TotalPnl,
            MaxDrawdownAbsolute = run.MaxDrawdown,
            MaxDrawdownPercent = Math.Round(maxDrawdownPercent, 2),
            AverageTradePnL = run.AverageTradePnl,
            AverageHoldTimeMinutes = run.AverageHoldTimeMinutes,
            TotalFeesPaid = run.TotalFeesPaid,
            InitialCapital = run.InitialCapital,
            FinalEquity = finalEquity,
            ReturnPercent = Math.Round(returnPercent, 2),
            CandlesReplayed = run.CandlesReplayed,
            StartDate = startDate,
            EndDate = endDate,
            DurationDays = durationDays,
            DataQuality = dataQuality,
            EquityCurveSummary = SummarizeEquityCurve(equitySeries, run.InitialCapital),
        };
    }

    private static List<EquitySnapshot> ParseEquityTimeSeries(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<EquitySnapshot>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string SummarizeEquityCurve(IReadOnlyList<EquitySnapshot> series, decimal initialCapital)
    {
        if (series.Count < 4)
        {
            return "Insufficient data points for curve analysis";
        }

        var quarterSize = Math.Max(1, series.Count / 4);
        var q1Avg = series.Take(quarterSize).Average(s => s.Equity);
        var q4Avg = series.Skip(series.Count - quarterSize).Average(s => s.Equity);

        var threshold = q1Avg * 0.01m;
        var overallDirection = q4Avg > q1Avg + threshold ? "rising"
            : q4Avg < q1Avg - threshold ? "declining"
            : "flat";

        // Compute max drawdown from equity curve (peak to trough)
        var peak = initialCapital;
        var maxDdPercent = 0m;
        foreach (var s in series)
        {
            if (s.Equity > peak)
            {
                peak = s.Equity;
            }

            if (peak > 0)
            {
                var dd = (peak - s.Equity) / peak * 100m;
                if (dd > maxDdPercent)
                {
                    maxDdPercent = dd;
                }
            }
        }

        var hasDeepDrawdowns = maxDdPercent > 10m;

        // Compute volatility (std dev of period-over-period returns)
        var isVolatile = false;
        if (series.Count > 1)
        {
            var returns = new List<decimal>();
            for (var i = 1; i < series.Count; i++)
            {
                if (series[i - 1].Equity > 0)
                {
                    returns.Add((series[i].Equity - series[i - 1].Equity) / series[i - 1].Equity);
                }
            }

            if (returns.Count > 1)
            {
                var avgReturn = returns.Average();
                var variance = returns.Average(r => (r - avgReturn) * (r - avgReturn));
                var stdDev = (decimal)Math.Sqrt((double)variance);
                isVolatile = stdDev > 0.005m;
            }
        }

        return (overallDirection, isVolatile, hasDeepDrawdowns) switch
        {
            ("rising", false, _) => "Steady upward trend with low volatility",
            ("rising", true, true) => "Generally rising but volatile with significant drawdowns",
            ("rising", true, false) => "Rising trend with moderate volatility",
            ("declining", _, true) => "Declining with deep drawdowns — equity eroding",
            ("declining", _, _) => "Gradual decline in equity",
            ("flat", true, _) => "Flat overall with high volatility — oscillating around breakeven",
            _ => "Relatively flat with minimal movement",
        };
    }
}
