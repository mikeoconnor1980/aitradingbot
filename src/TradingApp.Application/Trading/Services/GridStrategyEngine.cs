using System.Text.Json;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.Trading.Services;

public sealed class GridStrategyEngine : IStrategyEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public Task<StrategyEvaluation> EvaluateAsync(MarketContext context, string strategyConfigJson, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(strategyConfigJson);

        var config = JsonSerializer.Deserialize<GridStrategyConfig>(strategyConfigJson, JsonOptions)
            ?? throw new ArgumentException("Strategy config JSON is invalid.", nameof(strategyConfigJson));

        if (config.GridLevels <= 0 || config.GridSpacing <= 0m || config.PositionSize <= 0m)
        {
            return Task.FromResult(new StrategyEvaluation
            {
                SetupDetected = false,
                Reason = "Grid configuration is incomplete."
            });
        }

        if (context.LatestOneHourCandle is null || context.LatestFourHourCandle is null)
        {
            return Task.FromResult(new StrategyEvaluation
            {
                SetupDetected = false,
                Reason = "Higher timeframe context is not available yet."
            });
        }

        return Task.FromResult(new StrategyEvaluation
        {
            SetupDetected = true,
            Reason = "Grid setup available."
        });
    }
}