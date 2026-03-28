using System.Text.Json;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Backtesting;

internal static class BacktestRunResponseMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static string SerializeStrategyConfig(GridStrategyConfig strategyConfig)
    {
        ArgumentNullException.ThrowIfNull(strategyConfig);

        return JsonSerializer.Serialize(strategyConfig, JsonOptions);
    }

    public static string SerializeTrades(IReadOnlyList<BacktestTrade> trades)
    {
        ArgumentNullException.ThrowIfNull(trades);

        return JsonSerializer.Serialize(trades, JsonOptions);
    }

    public static BacktestRunResponse ToResponse(BacktestRun entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var strategyConfig = JsonSerializer.Deserialize<GridStrategyConfig>(entity.StrategyConfigJson, JsonOptions)
            ?? throw new JsonException("Stored strategy config is invalid.");
        var trades = JsonSerializer.Deserialize<List<BacktestTrade>>(entity.TradesJson, JsonOptions)
            ?? [];
        var intervals = JsonSerializer.Deserialize<string[]>(entity.IntervalsJson, JsonOptions)
            ?? [];

        return new BacktestRunResponse
        {
            Id = entity.Id,
            Symbol = entity.Symbol,
            Intervals = intervals,
            StartDate = DateTimeOffset.FromUnixTimeMilliseconds(entity.StartDateUtc).UtcDateTime,
            EndDate = DateTimeOffset.FromUnixTimeMilliseconds(entity.EndDateUtc).UtcDateTime,
            StrategyConfig = strategyConfig,
            InitialCapital = entity.InitialCapital,
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
            Trades = MapTrades(trades),
            CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(entity.CreatedAtUtc).UtcDateTime
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
                TradeType = trade.TradeType.ToString()
            })
            .ToList();
    }
}