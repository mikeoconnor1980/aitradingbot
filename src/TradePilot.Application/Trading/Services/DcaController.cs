using System.Globalization;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Enums;
using TradePilot.Domain.Trading;

namespace TradePilot.Application.Trading.Services;

/// <summary>
/// Emits scheduled DCA buys for the current market using the shared signal pipeline.
/// </summary>
public sealed class DcaController : IDcaController
{
    public Task<IReadOnlyList<TradingSignal>> ProcessAsync(
        StrategyEvaluation evaluation,
        MarketContext context,
        GridState gridState,
        PositionState positionState,
        IStrategyConfig strategyConfig,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(evaluation);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(gridState);
        ArgumentNullException.ThrowIfNull(positionState);
        ArgumentNullException.ThrowIfNull(strategyConfig);

        if (strategyConfig is not StrategyConfig config)
        {
            throw new ArgumentException(
                $"Expected {nameof(StrategyConfig)} but received {strategyConfig.GetType().Name}.",
                nameof(strategyConfig));
        }

        if (!evaluation.SetupDetected || config.Dca is null || context.CurrentCandle.Close <= 0m)
        {
            return Task.FromResult<IReadOnlyList<TradingSignal>>(Array.Empty<TradingSignal>());
        }

        if (!IsDue(config.Dca, context.TimestampUtc))
        {
            return Task.FromResult<IReadOnlyList<TradingSignal>>(Array.Empty<TradingSignal>());
        }

        var allocation = ResolveAllocation(config, context.Symbol);
        if (allocation is null || allocation.WeightPercent <= 0m)
        {
            return Task.FromResult<IReadOnlyList<TradingSignal>>(Array.Empty<TradingSignal>());
        }

        var scheduleScalingFactor = ResolveScalingFactor(config.Dca.ScalingBands, context.CurrentCandle.Close);
        var notionalUsd = config.Dca.BaseAmountUsd * (allocation.WeightPercent / 100m);
        notionalUsd *= scheduleScalingFactor;
        notionalUsd *= context.DrawdownScalingFactor;

        if (notionalUsd <= 0m)
        {
            return Task.FromResult<IReadOnlyList<TradingSignal>>(Array.Empty<TradingSignal>());
        }

        var size = decimal.Round(notionalUsd / context.CurrentCandle.Close, 8, MidpointRounding.AwayFromZero);
        if (size <= 0m)
        {
            return Task.FromResult<IReadOnlyList<TradingSignal>>(Array.Empty<TradingSignal>());
        }

        var executionMarket = ResolveExecutionMarket(config, allocation, context.Symbol);

        return Task.FromResult<IReadOnlyList<TradingSignal>>(
        [
            new TradingSignal
            {
                SignalType = "OpenPosition",
                Symbol = executionMarket,
                Reason = $"Scheduled DCA buy ({config.Dca.Interval}).",
                Parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["entryPrice"] = context.CurrentCandle.Close,
                    ["size"] = size,
                    ["notionalUsd"] = notionalUsd,
                    ["assetType"] = config.AssetType.ToString(),
                    ["orderType"] = OrderType.Market.ToString(),
                    ["gridCycleId"] = "dca",
                    ["tradeType"] = TradeType.DcaBuy.ToString(),
                    ["scheduleInterval"] = config.Dca.Interval.ToString(),
                    ["scalingFactor"] = scheduleScalingFactor,
                }
            }
        ]);
    }

    private static string ResolveExecutionMarket(StrategyConfig config, DcaAllocation allocation, string symbol)
    {
        if (!string.IsNullOrWhiteSpace(allocation.Market))
        {
            return NormalizeExecutionMarket(allocation.Market, config.AssetType);
        }

        if (!string.IsNullOrWhiteSpace(config.Market))
        {
            return NormalizeExecutionMarket(config.Market, config.AssetType);
        }

        return NormalizeExecutionMarket(symbol, config.AssetType);
    }

    private static bool IsDue(DcaConfig dca, long timestampUtc)
    {
        if (!TimeOnly.TryParseExact(
                dca.TimeOfDayUtc,
                "HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var scheduledTime))
        {
            return false;
        }

        var utcTime = DateTimeOffset.FromUnixTimeMilliseconds(timestampUtc).UtcDateTime;

        return dca.Interval switch
        {
            DcaInterval.FiveMinutes => utcTime.Minute % 5 == scheduledTime.Minute % 5,
            DcaInterval.Hourly => utcTime.Minute == scheduledTime.Minute,
            DcaInterval.FourHourly =>
                utcTime.Minute == scheduledTime.Minute
                && utcTime.Hour % 4 == scheduledTime.Hour % 4,
            DcaInterval.Daily =>
                utcTime.Minute == scheduledTime.Minute
                && utcTime.Hour == scheduledTime.Hour,
            DcaInterval.Weekly =>
                dca.DayOfWeek is int weeklyDay
                && utcTime.Minute == scheduledTime.Minute
                && utcTime.Hour == scheduledTime.Hour
                && (int)utcTime.DayOfWeek == weeklyDay,
            DcaInterval.Biweekly =>
                dca.DayOfWeek is int biweeklyDay
                && utcTime.Minute == scheduledTime.Minute
                && utcTime.Hour == scheduledTime.Hour
                && (int)utcTime.DayOfWeek == biweeklyDay
                && ISOWeek.GetWeekOfYear(utcTime.Date) % 2 == 0,
            DcaInterval.Monthly =>
                dca.DayOfMonth is int monthlyDay
                && utcTime.Minute == scheduledTime.Minute
                && utcTime.Hour == scheduledTime.Hour
                && utcTime.Day == monthlyDay,
            _ => false,
        };
    }

    private static DcaAllocation? ResolveAllocation(StrategyConfig config, string symbol)
    {
        if (config.Dca is null)
        {
            return null;
        }

        foreach (var allocation in config.Dca.Allocations)
        {
            if (MatchesMarket(allocation.Market, symbol, config.Market))
            {
                return allocation;
            }
        }

        return config.Dca.Allocations.Count == 0
            ? new DcaAllocation
            {
                Market = config.Market,
                WeightPercent = 100m,
            }
            : null;
    }

    private static bool MatchesMarket(string allocationMarket, string symbol, string configuredMarket)
    {
        if (string.Equals(allocationMarket, symbol, StringComparison.OrdinalIgnoreCase)
            || string.Equals(allocationMarket, configuredMarket, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(NormalizeMarket(allocationMarket), NormalizeMarket(symbol), StringComparison.OrdinalIgnoreCase)
            || string.Equals(NormalizeMarket(allocationMarket), NormalizeMarket(configuredMarket), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeMarket(string value)
    {
        return value
            .Replace("-USD", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("-PERP", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private static string NormalizeExecutionMarket(string value, AssetType assetType)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        if (trimmed.EndsWith("-USD", StringComparison.OrdinalIgnoreCase)
            || trimmed.EndsWith("-PERP", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed.ToUpperInvariant();
        }

        return trimmed.Contains('-')
            ? trimmed.ToUpperInvariant()
            : assetType == AssetType.Perp
                ? $"{trimmed.ToUpperInvariant()}-PERP"
                : $"{trimmed.ToUpperInvariant()}-USD";
    }

    private static decimal ResolveScalingFactor(IReadOnlyList<DcaScalingBand>? scalingBands, decimal price)
    {
        if (scalingBands is null || scalingBands.Count == 0)
        {
            return 1m;
        }

        foreach (var band in scalingBands)
        {
            var lowerBoundMatched = !band.PriceLowerUsd.HasValue || price >= band.PriceLowerUsd.Value;
            var upperBoundMatched = !band.PriceUpperUsd.HasValue || price <= band.PriceUpperUsd.Value;

            if (lowerBoundMatched && upperBoundMatched)
            {
                return Math.Max(0m, 1m + (band.ScalingPercent / 100m));
            }
        }

        return 1m;
    }
}