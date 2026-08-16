using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;

namespace TradePilot.Application.TradeJournal.Services;

/// <summary>Calculates normalized price excursion from inclusive historical candles.</summary>
public static class TradeExcursionCalculator
{
    /// <summary>
    /// Uses the final quantity-weighted entry and total entry quantity. Entry and exit fill prices are included
    /// alongside the high/low of every candle whose timestamp falls inclusively within the trade lifetime.
    /// </summary>
    public static TradeExcursion Calculate(
        TradeSide side,
        decimal entryPrice,
        decimal quantity,
        decimal exitPrice,
        IReadOnlyList<Candle> candles)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(entryPrice);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exitPrice);
        ArgumentNullException.ThrowIfNull(candles);

        var highest = Math.Max(entryPrice, exitPrice);
        var lowest = Math.Min(entryPrice, exitPrice);
        foreach (var candle in candles)
        {
            highest = Math.Max(highest, candle.High);
            lowest = Math.Min(lowest, candle.Low);
        }

        var favorablePerUnit = side == TradeSide.Long
            ? Math.Max(0m, highest - entryPrice)
            : Math.Max(0m, entryPrice - lowest);
        var adversePerUnit = side == TradeSide.Long
            ? Math.Min(0m, lowest - entryPrice)
            : Math.Min(0m, entryPrice - highest);

        return new TradeExcursion(
            favorablePerUnit * quantity,
            favorablePerUnit / entryPrice * 100m,
            adversePerUnit * quantity,
            adversePerUnit / entryPrice * 100m);
    }
}

/// <summary>Normalized favorable and adverse price excursion.</summary>
public sealed record TradeExcursion(
    decimal MfeAmount,
    decimal MfePercent,
    decimal MaeAmount,
    decimal MaePercent);
