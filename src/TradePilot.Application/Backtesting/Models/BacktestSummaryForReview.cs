using System.Text.Json;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;

namespace TradePilot.Application.Backtesting.Models;

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

    // Enriched trade-level metrics
    public decimal ProfitFactor { get; private set; }
    public decimal SharpeRatio { get; private set; }
    public int MaxConsecutiveLosses { get; private set; }
    public decimal AverageWinSize { get; private set; }
    public decimal AverageLossSize { get; private set; }
    public decimal RewardRiskRatio { get; private set; }
    public decimal FeeToGrossProfitRatio { get; private set; }
    public int WinningTrades { get; private set; }
    public int LosingTrades { get; private set; }
    public decimal LargestWin { get; private set; }
    public decimal LargestLoss { get; private set; }
    public IReadOnlyList<DrawdownEpisode> TopDrawdownEpisodes { get; private set; } = [];
    public RegimeSegmentationSummary? RegimeSegmentation { get; private set; }

    public static BacktestSummaryForReview? FromBacktestRun(
        BacktestRun run,
        IReadOnlyList<FundingRate>? fundingRates = null)
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

        var trades = ParseTrades(run.TradesJson);
        var tradeMetrics = ComputeTradeMetrics(trades, run.TotalFeesPaid);
        var drawdownEpisodes = ComputeTopDrawdownEpisodes(equitySeries, run.InitialCapital, maxCount: 3);
        var candleEvaluations = ParseCandleEvaluations(run.CandleLogJson);
        var gridCycles = ParseGridCycles(run.GridCycleLogJson);
        var regimeSegmentation = ComputeRegimeSegmentation(
            candleEvaluations,
            gridCycles,
            fundingRates ?? []);

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
            WinningTrades = run.WinningTrades,
            LosingTrades = run.LosingTrades,
            ProfitFactor = tradeMetrics.ProfitFactor,
            SharpeRatio = tradeMetrics.SharpeRatio,
            MaxConsecutiveLosses = tradeMetrics.MaxConsecutiveLosses,
            AverageWinSize = tradeMetrics.AverageWinSize,
            AverageLossSize = tradeMetrics.AverageLossSize,
            RewardRiskRatio = tradeMetrics.RewardRiskRatio,
            FeeToGrossProfitRatio = tradeMetrics.FeeToGrossProfitRatio,
            LargestWin = tradeMetrics.LargestWin,
            LargestLoss = tradeMetrics.LargestLoss,
            TopDrawdownEpisodes = drawdownEpisodes,
            RegimeSegmentation = regimeSegmentation,
        };
    }

    private static List<BacktestTrade> ParseTrades(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<BacktestTrade>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static List<CandleEvaluationEntry> ParseCandleEvaluations(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<CandleEvaluationEntry>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static List<GridCycleEntry> ParseGridCycles(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<GridCycleEntry>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static RegimeSegmentationSummary ComputeRegimeSegmentation(
        IReadOnlyList<CandleEvaluationEntry> candleEvaluations,
        IReadOnlyList<GridCycleEntry> gridCycles,
        IReadOnlyList<FundingRate> fundingRates)
    {
        const string openInterestUnavailableNote = "Historical open-interest snapshots are not persisted for backtests yet, so open-interest trend segmentation is unavailable for this run.";

        if (candleEvaluations.Count == 0 || gridCycles.Count == 0)
        {
            var unavailableReason = candleEvaluations.Count == 0
                ? "Audit log candle evaluations are unavailable, so regime segmentation could not be computed."
                : "No completed grid cycles were recorded for this run, so regime segmentation could not be computed.";

            return new RegimeSegmentationSummary
            {
                UnavailableReason = unavailableReason,
                OpenInterestTrendNote = openInterestUnavailableNote,
            };
        }

        var deployCandidates = candleEvaluations
            .Where(entry => !entry.IsWarmup)
            .OrderBy(entry => entry.TimestampUtc)
            .ToList();

        var deployByCycleId = deployCandidates
            .Where(entry =>
                !string.IsNullOrWhiteSpace(entry.GridCycleId)
                && entry.SignalsEmitted.Any(signal => string.Equals(signal, "DeployGrid", StringComparison.OrdinalIgnoreCase)))
            .GroupBy(entry => entry.GridCycleId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var atrValues = deployCandidates
            .Where(entry => entry.Atr > 0m)
            .Select(entry => entry.Atr)
            .OrderBy(value => value)
            .ToList();

        var normalizedAtrValues = deployCandidates
            .Where(entry => entry.Atr > 0m && entry.Close > 0m)
            .Select(entry => entry.Atr / entry.Close)
            .OrderBy(value => value)
            .ToList();

        var orderedFundingRates = fundingRates
            .OrderBy(rate => rate.Timestamp)
            .ToList();

        var observations = new List<GridCycleRegimeObservation>();

        foreach (var gridCycle in gridCycles)
        {
            if (!deployByCycleId.TryGetValue(gridCycle.GridCycleId, out var deployEntry))
            {
                deployEntry = deployCandidates
                    .Where(entry => string.Equals(entry.GridCycleId, gridCycle.GridCycleId, StringComparison.Ordinal))
                    .OrderBy(entry => entry.TimestampUtc)
                    .FirstOrDefault();
            }

            if (deployEntry is null)
            {
                continue;
            }

            observations.Add(new GridCycleRegimeObservation(
                Trend: ClassifyTrend(deployEntry),
                AtrPercentile: ClassifyAtrPercentile(deployEntry.Atr, atrValues),
                Volatility: ClassifyVolatility(deployEntry, normalizedAtrValues),
                Funding: ClassifyFundingBucket(FindFundingRateAtOrBefore(orderedFundingRates, deployEntry.TimestampUtc)?.Rate),
                Session: ClassifySession(deployEntry.TimestampUtc),
                CyclePnl: gridCycle.CyclePnl,
                CycleDurationMs: gridCycle.CycleDurationMs));
        }

        if (observations.Count == 0)
        {
            return new RegimeSegmentationSummary
            {
                UnavailableReason = "Completed grid cycles were found, but none could be matched to deploy-time audit entries.",
                OpenInterestTrendNote = openInterestUnavailableNote,
            };
        }

        return new RegimeSegmentationSummary
        {
            CompletedGridCyclesAnalysed = observations.Count,
            TrendSegments = BuildSegmentStats(observations, observation => observation.Trend, TrendOrder),
            AtrPercentileSegments = BuildSegmentStats(observations, observation => observation.AtrPercentile, AtrPercentileOrder),
            VolatilitySegments = BuildSegmentStats(observations, observation => observation.Volatility, VolatilityOrder),
            FundingSegments = BuildSegmentStats(observations, observation => observation.Funding, FundingOrder),
            SessionSegments = BuildSegmentStats(observations, observation => observation.Session, SessionOrder),
            OpenInterestTrendNote = openInterestUnavailableNote,
        };
    }

    private static TradeMetrics ComputeTradeMetrics(List<BacktestTrade> trades, decimal totalFeesPaid)
    {
        var closedTrades = trades.Where(t => t.PnL.HasValue).ToList();

        if (closedTrades.Count == 0)
        {
            return TradeMetrics.Empty;
        }

        var wins = closedTrades.Where(t => t.PnL!.Value > 0).ToList();
        var losses = closedTrades.Where(t => t.PnL!.Value < 0).ToList();

        var grossProfit = wins.Sum(t => t.PnL!.Value);
        var grossLoss = Math.Abs(losses.Sum(t => t.PnL!.Value));

        var profitFactor = grossLoss > 0 ? Math.Round(grossProfit / grossLoss, 2) : grossProfit > 0 ? 999m : 0m;

        var avgWin = wins.Count > 0 ? Math.Round(wins.Average(t => t.PnL!.Value), 2) : 0m;
        var avgLoss = losses.Count > 0 ? Math.Round(Math.Abs(losses.Average(t => t.PnL!.Value)), 2) : 0m;
        var rewardRisk = avgLoss > 0 ? Math.Round(avgWin / avgLoss, 2) : avgWin > 0 ? 999m : 0m;

        var largestWin = wins.Count > 0 ? wins.Max(t => t.PnL!.Value) : 0m;
        var largestLoss = losses.Count > 0 ? Math.Abs(losses.Min(t => t.PnL!.Value)) : 0m;

        var feeRatio = grossProfit > 0 ? Math.Round(totalFeesPaid / grossProfit * 100m, 1) : 0m;

        // Max consecutive losses
        var maxConsecLosses = 0;
        var currentStreak = 0;
        foreach (var trade in closedTrades.OrderBy(t => t.EntryTimeUtc))
        {
            if (trade.PnL!.Value < 0)
            {
                currentStreak++;
                maxConsecLosses = Math.Max(maxConsecLosses, currentStreak);
            }
            else
            {
                currentStreak = 0;
            }
        }

        // Sharpe ratio (annualized from per-trade returns)
        var sharpe = ComputeSharpeRatio(closedTrades);

        return new TradeMetrics(
            profitFactor, sharpe, maxConsecLosses,
            avgWin, avgLoss, rewardRisk, feeRatio,
            largestWin, largestLoss);
    }

    private static decimal ComputeSharpeRatio(List<BacktestTrade> closedTrades)
    {
        if (closedTrades.Count < 2)
        {
            return 0m;
        }

        var pnls = closedTrades.Select(t => (double)t.PnL!.Value).ToList();
        var avgPnl = pnls.Average();
        var variance = pnls.Average(p => (p - avgPnl) * (p - avgPnl));
        var stdDev = Math.Sqrt(variance);

        if (stdDev < 0.0001)
        {
            return 0m;
        }

        // Annualize assuming ~252 trading days
        var perTradeSharpe = avgPnl / stdDev;
        var annualized = perTradeSharpe * Math.Sqrt(Math.Min(closedTrades.Count, 252));

        return Math.Round((decimal)annualized, 2);
    }

    private static List<DrawdownEpisode> ComputeTopDrawdownEpisodes(
        IReadOnlyList<EquitySnapshot> series,
        decimal initialCapital,
        int maxCount)
    {
        if (series.Count < 2)
        {
            return [];
        }

        var episodes = new List<DrawdownEpisode>();
        var peak = initialCapital;
        var peakIndex = -1;
        var inDrawdown = false;
        var troughEquity = peak;
        var troughIndex = 0;

        for (var i = 0; i < series.Count; i++)
        {
            var equity = series[i].Equity;

            if (equity >= peak)
            {
                if (inDrawdown && peak > 0)
                {
                    var depthPct = Math.Round((peak - troughEquity) / peak * 100m, 2);
                    var startDate = DateTimeOffset.FromUnixTimeMilliseconds(series[Math.Max(0, peakIndex)].TimestampUtc).UtcDateTime;
                    var troughDate = DateTimeOffset.FromUnixTimeMilliseconds(series[troughIndex].TimestampUtc).UtcDateTime;
                    var recoveryDate = DateTimeOffset.FromUnixTimeMilliseconds(series[i].TimestampUtc).UtcDateTime;
                    var recoveryCandles = i - troughIndex;

                    episodes.Add(new DrawdownEpisode(startDate, troughDate, recoveryDate, depthPct, recoveryCandles));
                }

                peak = equity;
                peakIndex = i;
                inDrawdown = false;
                troughEquity = equity;
                troughIndex = i;
            }
            else
            {
                inDrawdown = true;
                if (equity < troughEquity)
                {
                    troughEquity = equity;
                    troughIndex = i;
                }
            }
        }

        // Handle open drawdown (not yet recovered)
        if (inDrawdown && peak > 0)
        {
            var depthPct = Math.Round((peak - troughEquity) / peak * 100m, 2);
            var startDate = DateTimeOffset.FromUnixTimeMilliseconds(series[Math.Max(0, peakIndex)].TimestampUtc).UtcDateTime;
            var troughDate = DateTimeOffset.FromUnixTimeMilliseconds(series[troughIndex].TimestampUtc).UtcDateTime;

            episodes.Add(new DrawdownEpisode(startDate, troughDate, null, depthPct, null));
        }

        return episodes
            .OrderByDescending(e => e.DepthPercent)
            .Take(maxCount)
            .ToList();
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

    private static IReadOnlyList<RegimeSegmentStat> BuildSegmentStats(
        IReadOnlyList<GridCycleRegimeObservation> observations,
        Func<GridCycleRegimeObservation, string> keySelector,
        IReadOnlyList<string> orderedKeys)
    {
        var grouped = observations
            .GroupBy(keySelector, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => CreateSegmentStat(group.Key, group), StringComparer.Ordinal);

        var orderedResults = orderedKeys
            .Where(grouped.ContainsKey)
            .Select(key => grouped[key])
            .ToList();

        orderedResults.AddRange(grouped
            .Where(entry => !orderedKeys.Contains(entry.Key, StringComparer.Ordinal))
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => entry.Value));

        return orderedResults;
    }

    private static RegimeSegmentStat CreateSegmentStat(
        string segment,
        IEnumerable<GridCycleRegimeObservation> group)
    {
        var items = group.ToList();
        var cycleCount = items.Count;
        var winningCycles = items.Count(item => item.CyclePnl > 0m);
        var losingCycles = items.Count(item => item.CyclePnl < 0m);
        var totalCyclePnl = items.Sum(item => item.CyclePnl);
        var averageCyclePnl = cycleCount > 0 ? Math.Round(totalCyclePnl / cycleCount, 2) : 0m;
        var winRate = cycleCount > 0 ? Math.Round((decimal)winningCycles / cycleCount * 100m, 1) : 0m;
        var averageDurationHours = cycleCount > 0
            ? items.Average(item => TimeSpan.FromMilliseconds(item.CycleDurationMs).TotalHours)
            : 0d;

        return new RegimeSegmentStat
        {
            Segment = segment,
            CycleCount = cycleCount,
            WinningCycles = winningCycles,
            LosingCycles = losingCycles,
            WinRate = winRate,
            AverageCyclePnl = averageCyclePnl,
            TotalCyclePnl = Math.Round(totalCyclePnl, 2),
            AverageCycleDurationHours = Math.Round(averageDurationHours, 2),
        };
    }

    private static string ClassifyTrend(CandleEvaluationEntry entry)
    {
        if (entry.Close <= 0m)
        {
            return "Unavailable";
        }

        var emaSpreadPercent = Math.Abs(entry.EmaFast - entry.EmaSlow) / entry.Close;
        var atrPercent = entry.Atr > 0m ? entry.Atr / entry.Close : 0m;
        var directionalAlignment = (entry.Close >= entry.EmaFast && entry.EmaFast >= entry.EmaSlow)
            || (entry.Close <= entry.EmaFast && entry.EmaFast <= entry.EmaSlow);
        var trendingThreshold = Math.Max(0.0020m, atrPercent * 0.60m);

        return directionalAlignment && emaSpreadPercent >= trendingThreshold
            ? "Trending"
            : "Ranging";
    }

    private static string ClassifyAtrPercentile(decimal atrValue, IReadOnlyList<decimal> orderedAtrValues)
    {
        var percentile = CalculatePercentileRank(atrValue, orderedAtrValues);
        return percentile switch
        {
            < 0m => "Unavailable",
            < 33.34m => "Low ATR (0-33rd pct)",
            <= 66.67m => "Mid ATR (34-66th pct)",
            _ => "High ATR (67-100th pct)",
        };
    }

    private static string ClassifyVolatility(
        CandleEvaluationEntry entry,
        IReadOnlyList<decimal> orderedNormalizedAtrValues)
    {
        if (entry.Close <= 0m)
        {
            return "Unavailable";
        }

        var percentile = CalculatePercentileRank(entry.Atr / entry.Close, orderedNormalizedAtrValues);
        return percentile switch
        {
            < 0m => "Unavailable",
            < 33.34m => "Low Volatility",
            <= 66.67m => "Medium Volatility",
            _ => "High Volatility",
        };
    }

    private static decimal CalculatePercentileRank(decimal value, IReadOnlyList<decimal> orderedValues)
    {
        if (orderedValues.Count == 0)
        {
            return -1m;
        }

        var lessThanOrEqualCount = orderedValues.Count(candidate => candidate <= value);
        return Math.Round((decimal)lessThanOrEqualCount / orderedValues.Count * 100m, 2);
    }

    private static FundingRate? FindFundingRateAtOrBefore(
        IReadOnlyList<FundingRate> fundingRates,
        long timestampUtc)
    {
        if (fundingRates.Count == 0)
        {
            return null;
        }

        var low = 0;
        var high = fundingRates.Count - 1;
        FundingRate? result = null;

        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            var candidate = fundingRates[mid];

            if (candidate.Timestamp <= timestampUtc)
            {
                result = candidate;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return result;
    }

    private static string ClassifyFundingBucket(decimal? fundingRate)
    {
        if (!fundingRate.HasValue)
        {
            return "Unavailable";
        }

        return fundingRate.Value switch
        {
            <= -0.0001m => "Strongly Negative Funding",
            < -0.000025m => "Negative Funding",
            <= 0.000025m => "Neutral Funding",
            < 0.0001m => "Positive Funding",
            _ => "Strongly Positive Funding",
        };
    }

    private static string ClassifySession(long timestampUtc)
    {
        var hour = DateTimeOffset.FromUnixTimeMilliseconds(timestampUtc).UtcDateTime.Hour;
        return hour switch
        {
            >= 0 and < 8 => "Asia Session",
            >= 8 and < 16 => "Europe Session",
            _ => "US Session",
        };
    }

    private static readonly string[] TrendOrder = ["Trending", "Ranging", "Unavailable"];
    private static readonly string[] AtrPercentileOrder = ["Low ATR (0-33rd pct)", "Mid ATR (34-66th pct)", "High ATR (67-100th pct)", "Unavailable"];
    private static readonly string[] VolatilityOrder = ["Low Volatility", "Medium Volatility", "High Volatility", "Unavailable"];
    private static readonly string[] FundingOrder = ["Strongly Negative Funding", "Negative Funding", "Neutral Funding", "Positive Funding", "Strongly Positive Funding", "Unavailable"];
    private static readonly string[] SessionOrder = ["Asia Session", "Europe Session", "US Session", "Unavailable"];

    private sealed record TradeMetrics(
        decimal ProfitFactor,
        decimal SharpeRatio,
        int MaxConsecutiveLosses,
        decimal AverageWinSize,
        decimal AverageLossSize,
        decimal RewardRiskRatio,
        decimal FeeToGrossProfitRatio,
        decimal LargestWin,
        decimal LargestLoss)
    {
        public static TradeMetrics Empty => new(0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    private sealed record GridCycleRegimeObservation(
        string Trend,
        string AtrPercentile,
        string Volatility,
        string Funding,
        string Session,
        decimal CyclePnl,
        long CycleDurationMs);
}

public sealed record DrawdownEpisode(
    DateTime StartDate,
    DateTime TroughDate,
    DateTime? RecoveryDate,
    decimal DepthPercent,
    int? RecoveryCandles);
