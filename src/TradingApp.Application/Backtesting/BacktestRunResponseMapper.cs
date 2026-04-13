using System.Text.Json;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.StrategyAuthoring.Serialization;
using TradingApp.Domain.Entities;
using TradingApp.Domain.Trading;

namespace TradingApp.Application.Backtesting;

public static class BacktestRunResponseMapper
{
    private static readonly JsonSerializerOptions JsonOptions = StrategyJsonOptions.Default;

    public static string SerializeStrategyConfig(StrategyConfig strategyConfig)
    {
        ArgumentNullException.ThrowIfNull(strategyConfig);

        return JsonSerializer.Serialize(strategyConfig, JsonOptions);
    }

    public static string SerializeExecutionConfig(ExecutionConfig executionConfig)
    {
        ArgumentNullException.ThrowIfNull(executionConfig);

        return JsonSerializer.Serialize(executionConfig, JsonOptions);
    }

    public static string SerializeTrades(IReadOnlyList<BacktestTrade> trades)
    {
        ArgumentNullException.ThrowIfNull(trades);

        return JsonSerializer.Serialize(trades, JsonOptions);
    }

    public static string SerializeEquityTimeSeries(IReadOnlyList<EquitySnapshot> equityTimeSeries)
    {
        ArgumentNullException.ThrowIfNull(equityTimeSeries);

        return JsonSerializer.Serialize(equityTimeSeries, JsonOptions);
    }

    public static string SerializeCandleLog(IReadOnlyList<CandleEvaluationEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return JsonSerializer.Serialize(entries, JsonOptions);
    }

    public static string SerializeOrderEventLog(IReadOnlyList<OrderEventEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return JsonSerializer.Serialize(entries, JsonOptions);
    }

    public static string SerializeGridCycleLog(IReadOnlyList<GridCycleEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return JsonSerializer.Serialize(entries, JsonOptions);
    }

    public static BacktestRunResponse ToResponse(BacktestRun entity, string? strategyName = null)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var strategyConfig = JsonSerializer.Deserialize<StrategyConfig>(entity.StrategyConfigJson, JsonOptions)
            ?? throw new JsonException("Stored strategy config is invalid.");
        var executionConfig = JsonSerializer.Deserialize<ExecutionConfig>(entity.ExecutionConfigJson, JsonOptions)
            ?? throw new JsonException("Stored execution config is invalid.");
        var trades = JsonSerializer.Deserialize<List<BacktestTrade>>(entity.TradesJson, JsonOptions)
            ?? [];
        var equityTimeSeries = string.IsNullOrWhiteSpace(entity.EquityTimeSeriesJson)
            ? []
            : JsonSerializer.Deserialize<List<EquitySnapshot>>(entity.EquityTimeSeriesJson, JsonOptions) ?? [];
        var intervals = JsonSerializer.Deserialize<string[]>(entity.IntervalsJson, JsonOptions)
            ?? [];
        var rMetrics = ComputeRMetrics(trades);

        return new BacktestRunResponse
        {
            Id = entity.Id,
            Symbol = entity.Symbol,
            Intervals = intervals,
            StartDate = DateTimeOffset.FromUnixTimeMilliseconds(entity.StartDateUtc).UtcDateTime,
            EndDate = DateTimeOffset.FromUnixTimeMilliseconds(entity.EndDateUtc).UtcDateTime,
            StrategyConfig = strategyConfig,
            ExecutionConfig = executionConfig,
            InitialCapital = entity.InitialCapital,
            Status = entity.Status.ToString(),
            Progress = entity.Progress,
            ErrorMessage = entity.ErrorMessage,
            CandlesReplayed = entity.CandlesReplayed,
            ElapsedMs = entity.ElapsedMs,
            TotalTrades = entity.TotalTrades,
            WinningTrades = entity.WinningTrades,
            LosingTrades = entity.LosingTrades,
            WinRate = entity.WinRate,
            TotalPnl = entity.TotalPnl,
            MaxDrawdown = entity.MaxDrawdown,
            AverageTradePnl = entity.AverageTradePnl,
            AverageHoldTimeMinutes = entity.AverageHoldTimeMinutes,
            HedgesOpened = entity.HedgesOpened,
            TotalFeesPaid = entity.TotalFeesPaid,
            Expectancy = entity.Expectancy ?? rMetrics.Expectancy,
            ProfitFactor = entity.ProfitFactor ?? rMetrics.ProfitFactor,
            Sqn = entity.Sqn ?? rMetrics.Sqn,
            KellyPercent = entity.KellyPercent ?? rMetrics.KellyPercent,
            HalfKellyPercent = entity.HalfKellyPercent ?? rMetrics.HalfKellyPercent,
            WinLossRRatio = entity.WinLossRRatio ?? rMetrics.WinLossRRatio,
            AvgWinR = rMetrics.AvgWinR,
            AvgLossR = rMetrics.AvgLossR,
            RWinRate = rMetrics.RWinRate,
            RDistribution = rMetrics.RDistribution,
            Trades = MapTrades(trades),
            EquityTimeSeries = MapEquityTimeSeries(equityTimeSeries),
            CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(entity.CreatedAtUtc).UtcDateTime,
            HasAuditLog = entity.CandleLogJson is not null || entity.OrderEventLogJson is not null || entity.GridCycleLogJson is not null,
            StrategyId = entity.StrategyId,
            StrategyRevisionId = entity.StrategyRevisionId,
            StrategyName = strategyName,
        };
    }

    private static IReadOnlyList<BacktestTradeResponse> MapTrades(IReadOnlyList<BacktestTrade> trades)
    {
        return trades
            .Select(trade => new BacktestTradeResponse
            {
                EntryTime = DateTimeOffset.FromUnixTimeMilliseconds(trade.EntryTimeUtc).UtcDateTime,
                ExitTime = trade.ExitTimeUtc.HasValue
                    ? DateTimeOffset.FromUnixTimeMilliseconds(trade.ExitTimeUtc.Value).UtcDateTime
                    : null,
                EntryPrice = trade.EntryPrice,
                ExitPrice = trade.ExitPrice,
                Side = trade.Side.ToString(),
                Size = trade.Size,
                Pnl = trade.PnL,
                Fees = trade.Fees,
                TradeType = trade.TradeType.ToString(),
                GridCycleId = trade.GridCycleId,
                ExitReason = trade.ExitReason,
                InitialRDollars = trade.InitialRDollars,
                RMultipleResult = trade.RMultipleResult,
                Mfe = trade.MFE,
                Mae = trade.MAE
            })
            .ToList();
    }

    private static IReadOnlyList<EquitySnapshotResponse> MapEquityTimeSeries(IReadOnlyList<EquitySnapshot> snapshots)
    {
        return snapshots
            .Select(s => new EquitySnapshotResponse
            {
                TimestampUtc = s.TimestampUtc,
                Equity = s.Equity
            })
            .ToList();
    }

    private static RMetricsSummary ComputeRMetrics(IReadOnlyList<BacktestTrade> trades)
    {
        var rValues = trades
            .Where(trade => trade.RMultipleResult.HasValue)
            .Select(trade => trade.RMultipleResult!.Value)
            .ToList();

        if (rValues.Count == 0)
        {
            return new RMetricsSummary();
        }

        var winners = rValues.Where(value => value > 0m).ToList();
        var losers = rValues.Where(value => value < 0m).ToList();
        var expectancy = rValues.Average();
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
            var mean = (double)expectancy;
            var variance = rValues.Sum(value => Math.Pow((double)value - mean, 2d)) / (rValues.Count - 1);
            var standardDeviation = Math.Sqrt(variance);

            if (standardDeviation > 0d)
            {
                sqn = Math.Round((decimal)(mean / standardDeviation * Math.Sqrt(rValues.Count)), 4);
            }
        }

        var grossLoss = Math.Abs(losers.Sum());

        return new RMetricsSummary
        {
            Expectancy = Math.Round(expectancy, 4),
            ProfitFactor = grossLoss > 0m ? Math.Round(winners.Sum() / grossLoss, 4) : null,
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