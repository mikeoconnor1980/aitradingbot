using System.Globalization;
using Microsoft.Extensions.Logging;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.StrategyAuthoring.Services;

public sealed class TrendFilterEvaluator : ITrendFilterEvaluator
{
    private readonly ILogger<TrendFilterEvaluator> _logger;

    public TrendFilterEvaluator(ILogger<TrendFilterEvaluator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public TrendFilterResult Evaluate(
        TrendFilterConfig? filter,
        Direction strategyDirection,
        IndicatorContext indicatorContext,
        MarketContext marketContext)
    {
        ArgumentNullException.ThrowIfNull(indicatorContext);
        ArgumentNullException.ThrowIfNull(marketContext);

        if (filter is null || !filter.Enabled)
        {
            return TrendFilterResult.Pass("Trend filter disabled - skipped.");
        }

        if (!AppliesToDirection(filter.AppliesTo, strategyDirection))
        {
            return TrendFilterResult.Pass(
                $"Trend filter appliesTo={filter.AppliesTo} does not match direction={strategyDirection} - skipped.");
        }

        return filter.Type switch
        {
            TrendFilterType.EmaCross => EvaluateMovingAverageCross(filter, indicatorContext, isEma: true),
            TrendFilterType.SmaCross => EvaluateMovingAverageCross(filter, indicatorContext, isEma: false),
            TrendFilterType.PriceAboveEma => EvaluatePriceAboveEma(filter, indicatorContext, marketContext),
            _ => HandleUnknownType(filter),
        };
    }

    private static bool AppliesToDirection(Direction appliesTo, Direction strategyDirection)
    {
        return appliesTo == Direction.Both || appliesTo == strategyDirection;
    }

    private static TrendFilterResult EvaluateMovingAverageCross(
        TrendFilterConfig filter,
        IndicatorContext indicatorContext,
        bool isEma)
    {
        var movingAverageType = isEma ? "EMA" : "SMA";
        var fastCurrent = isEma
            ? indicatorContext.GetEma(filter.FastPeriod)
            : indicatorContext.GetSma(filter.FastPeriod);
        var fastPrevious = isEma
            ? indicatorContext.GetPreviousEma(filter.FastPeriod)
            : indicatorContext.GetPreviousSma(filter.FastPeriod);
        var slowCurrent = isEma
            ? indicatorContext.GetEma(filter.SlowPeriod)
            : indicatorContext.GetSma(filter.SlowPeriod);
        var slowPrevious = isEma
            ? indicatorContext.GetPreviousEma(filter.SlowPeriod)
            : indicatorContext.GetPreviousSma(filter.SlowPeriod);

        if (!fastCurrent.HasValue || !slowCurrent.HasValue)
        {
            return TrendFilterResult.Fail(
                $"{movingAverageType}({filter.FastPeriod}) or {movingAverageType}({filter.SlowPeriod}) not available - insufficient data.");
        }

        return filter.Operator switch
        {
            TrendOperator.Gt => EvaluateComparison(
                fastCurrent.Value,
                slowCurrent.Value,
                $"{movingAverageType}({filter.FastPeriod})",
                $"{movingAverageType}({filter.SlowPeriod})",
                ">",
                (fast, slow) => fast > slow),
            TrendOperator.Lt => EvaluateComparison(
                fastCurrent.Value,
                slowCurrent.Value,
                $"{movingAverageType}({filter.FastPeriod})",
                $"{movingAverageType}({filter.SlowPeriod})",
                "<",
                (fast, slow) => fast < slow),
            TrendOperator.Gte => EvaluateComparison(
                fastCurrent.Value,
                slowCurrent.Value,
                $"{movingAverageType}({filter.FastPeriod})",
                $"{movingAverageType}({filter.SlowPeriod})",
                ">=",
                (fast, slow) => fast >= slow),
            TrendOperator.Lte => EvaluateComparison(
                fastCurrent.Value,
                slowCurrent.Value,
                $"{movingAverageType}({filter.FastPeriod})",
                $"{movingAverageType}({filter.SlowPeriod})",
                "<=",
                (fast, slow) => fast <= slow),
            TrendOperator.CrossAbove => EvaluateCross(
                fastCurrent,
                fastPrevious,
                slowCurrent,
                slowPrevious,
                $"{movingAverageType}({filter.FastPeriod})",
                $"{movingAverageType}({filter.SlowPeriod})",
                "cross_above",
                (previousFast, previousSlow, currentFast, currentSlow) => previousFast <= previousSlow && currentFast > currentSlow),
            TrendOperator.CrossBelow => EvaluateCross(
                fastCurrent,
                fastPrevious,
                slowCurrent,
                slowPrevious,
                $"{movingAverageType}({filter.FastPeriod})",
                $"{movingAverageType}({filter.SlowPeriod})",
                "cross_below",
                (previousFast, previousSlow, currentFast, currentSlow) => previousFast >= previousSlow && currentFast < currentSlow),
            _ => TrendFilterResult.Fail(
                $"Unknown operator '{filter.Operator}' for {movingAverageType.ToLowerInvariant()} trend filter."),
        };
    }

    private static TrendFilterResult EvaluatePriceAboveEma(
        TrendFilterConfig filter,
        IndicatorContext indicatorContext,
        MarketContext marketContext)
    {
        if (filter.Period is null or <= 0)
        {
            return TrendFilterResult.Fail("PriceAboveEma filter has an invalid period.");
        }

        var period = filter.Period.Value;
        var currentEma = indicatorContext.GetEma(period);
        if (!currentEma.HasValue)
        {
            return TrendFilterResult.Fail($"EMA({period}) not available - insufficient data.");
        }

        var currentClose = marketContext.CurrentCandle.Close;

        return filter.Operator switch
        {
            TrendOperator.Above => EvaluateComparison(currentClose, currentEma.Value, "Price", $"EMA({period})", ">", (price, ema) => price > ema),
            TrendOperator.Below => EvaluateComparison(currentClose, currentEma.Value, "Price", $"EMA({period})", "<", (price, ema) => price < ema),
            TrendOperator.Gt => EvaluateComparison(currentClose, currentEma.Value, "Price", $"EMA({period})", ">", (price, ema) => price > ema),
            TrendOperator.Lt => EvaluateComparison(currentClose, currentEma.Value, "Price", $"EMA({period})", "<", (price, ema) => price < ema),
            TrendOperator.Gte => EvaluateComparison(currentClose, currentEma.Value, "Price", $"EMA({period})", ">=", (price, ema) => price >= ema),
            TrendOperator.Lte => EvaluateComparison(currentClose, currentEma.Value, "Price", $"EMA({period})", "<=", (price, ema) => price <= ema),
            TrendOperator.CrossAbove => EvaluatePriceCross(marketContext, indicatorContext, period, crossAbove: true),
            TrendOperator.CrossBelow => EvaluatePriceCross(marketContext, indicatorContext, period, crossAbove: false),
            _ => TrendFilterResult.Fail($"Unknown operator '{filter.Operator}' for price_above_ema trend filter."),
        };
    }

    private static TrendFilterResult EvaluateComparison(
        decimal left,
        decimal right,
        string leftLabel,
        string rightLabel,
        string operatorSymbol,
        Func<decimal, decimal, bool> compare)
    {
        var passed = compare(left, right);
        var status = passed ? "filter passed" : "filter failed";

        return new TrendFilterResult
        {
            Passed = passed,
            Reason = $"{leftLabel} = {Format(left)} {operatorSymbol} {rightLabel} = {Format(right)} - {status}",
        };
    }

    private static TrendFilterResult EvaluateCross(
        decimal? currentLeft,
        decimal? previousLeft,
        decimal? currentRight,
        decimal? previousRight,
        string leftLabel,
        string rightLabel,
        string operatorLabel,
        Func<decimal, decimal, decimal, decimal, bool> compare)
    {
        if (!previousLeft.HasValue || !previousRight.HasValue || !currentLeft.HasValue || !currentRight.HasValue)
        {
            return TrendFilterResult.Fail(
                $"{leftLabel} or {rightLabel} previous values not available for {operatorLabel} detection.");
        }

        var passed = compare(previousLeft.Value, previousRight.Value, currentLeft.Value, currentRight.Value);
        var status = passed ? "filter passed" : "filter failed";

        return new TrendFilterResult
        {
            Passed = passed,
            Reason =
                $"{leftLabel} prev={Format(previousLeft.Value)} curr={Format(currentLeft.Value)} {operatorLabel} {rightLabel} prev={Format(previousRight.Value)} curr={Format(currentRight.Value)} - {status}",
        };
    }

    private static TrendFilterResult EvaluatePriceCross(
        MarketContext marketContext,
        IndicatorContext indicatorContext,
        int period,
        bool crossAbove)
    {
        var previousCandle = marketContext.PreviousCandle;
        var currentEma = indicatorContext.GetEma(period);
        var previousEma = indicatorContext.GetPreviousEma(period);

        if (previousCandle is null)
        {
            return TrendFilterResult.Fail("Previous candle not available for price/EMA cross detection.");
        }

        if (!currentEma.HasValue || !previousEma.HasValue)
        {
            return TrendFilterResult.Fail($"EMA({period}) previous value not available for cross detection.");
        }

        var previousClose = previousCandle.Close;
        var currentClose = marketContext.CurrentCandle.Close;
        var passed = crossAbove
            ? previousClose < previousEma.Value && currentClose > currentEma.Value
            : previousClose > previousEma.Value && currentClose < currentEma.Value;
        var direction = crossAbove ? "cross_above" : "cross_below";
        var status = passed ? "filter passed" : "filter failed";

        return new TrendFilterResult
        {
            Passed = passed,
            Reason =
                $"Price prev={Format(previousClose)} curr={Format(currentClose)} {direction} EMA({period}) prev={Format(previousEma.Value)} curr={Format(currentEma.Value)} - {status}",
        };
    }

    private TrendFilterResult HandleUnknownType(TrendFilterConfig filter)
    {
        _logger.LogWarning("Unknown trend filter type {TrendFilterType}. Trend filter fails closed.", filter.Type);
        return TrendFilterResult.Fail($"Unknown trend filter type '{filter.Type}' - filter fails closed.");
    }

    private static string Format(decimal value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}