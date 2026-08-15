using MediatR;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.MarketAnalysis.Models;
using TradePilot.Application.MarketData;
using TradePilot.Application.MarketData.Models;
using TradePilot.Application.MarketData.Queries;
using TradePilot.Application.Trading.Models;

namespace TradePilot.Application.MarketAnalysis.Queries;

/// <summary>Requests bounded deterministic evidence for an immutable chart range.</summary>
public sealed record AnalyseChartContextQuery(
    string Symbol,
    string Timeframe,
    Exchange Exchange,
    DateTimeOffset VisibleFromOpenTimeUtc,
    DateTimeOffset VisibleToOpenTimeUtc,
    DateTimeOffset? SelectedCandleOpenTimeUtc = null) : Query<AnalyseChartContextResult>;

/// <summary>Builds compact chart evidence through existing candle and market-analysis capabilities.</summary>
public sealed class AnalyseChartContextQueryHandler : QueryHandler<AnalyseChartContextQuery, AnalyseChartContextResult>
{
    public const int MaximumCandleCount = 500;

    private readonly ISender _sender;

    public AnalyseChartContextQueryHandler(ISender sender)
    {
        _sender = sender;
    }

    public override async Task<AnalyseChartContextResult> Handle(
        AnalyseChartContextQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Timeframe);
        var intervalMilliseconds = MarketTimeframe.GetDurationMilliseconds(request.Timeframe);
        if (request.VisibleFromOpenTimeUtc > request.VisibleToOpenTimeUtc)
        {
            throw new DomainException("The visible chart range must not be reversed.");
        }

        var requestedCount = ((request.VisibleToOpenTimeUtc.ToUnixTimeMilliseconds() - request.VisibleFromOpenTimeUtc.ToUnixTimeMilliseconds()) / intervalMilliseconds) + 1;
        if (requestedCount is <= 0 or > MaximumCandleCount)
        {
            throw new DomainException($"The visible chart range must contain between 1 and {MaximumCandleCount} candles.");
        }

        if (request.SelectedCandleOpenTimeUtc is { } selected &&
            (selected < request.VisibleFromOpenTimeUtc || selected > request.VisibleToOpenTimeUtc))
        {
            throw new DomainException("The selected candle must fall within the visible chart range.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var endTime = checked(request.VisibleToOpenTimeUtc.ToUnixTimeMilliseconds() + intervalMilliseconds);
        var fetched = await _sender.Send(new GetCandlesQuery(
            request.Symbol,
            request.Timeframe,
            request.Exchange,
            endTime,
            (int)requestedCount,
            IncludeIndicators: true), cancellationToken);
        var candles = fetched
            .Where(candle => candle.Timestamp >= request.VisibleFromOpenTimeUtc.ToUnixTimeMilliseconds()
                && candle.Timestamp <= request.VisibleToOpenTimeUtc.ToUnixTimeMilliseconds())
            .GroupBy(candle => candle.Timestamp)
            .Select(group => group.First())
            .OrderBy(candle => candle.Timestamp)
            .ToList();

        var requestedRange = new ChartRangeSummary(request.VisibleFromOpenTimeUtc, request.VisibleToOpenTimeUtc);
        if (candles.Count == 0)
        {
            return new AnalyseChartContextResult(
                request.Symbol.Trim(), request.Timeframe.Trim(), request.Exchange, requestedRange, null, false, 0,
                null, null, null, null, null, null, null, null, 0m, 0m, null, null);
        }

        var first = candles[0];
        var last = candles[^1];
        var highest = candles.MaxBy(candle => candle.High)!;
        var lowest = candles.MinBy(candle => candle.Low)!;
        var actualRange = new ChartRangeSummary(ToOpenTime(first), ToOpenTime(last));
        var selectedCandle = request.SelectedCandleOpenTimeUtc is { } selectedOpenTime
            ? CreateSelectedCandle(candles.FirstOrDefault(candle => candle.Timestamp == selectedOpenTime.ToUnixTimeMilliseconds()))
            : null;
        var endOfRangeAnalysis = await _sender.Send(new AnalyseMarketQuery(
            request.Symbol,
            request.Timeframe,
            request.Exchange,
            DateTimeOffset.FromUnixTimeMilliseconds(endTime)), cancellationToken);

        var absoluteChange = last.Close - first.Close;
        return new AnalyseChartContextResult(
            request.Symbol.Trim(),
            request.Timeframe.Trim(),
            request.Exchange,
            requestedRange,
            actualRange,
            candles.Count == requestedCount && actualRange == requestedRange,
            candles.Count,
            first.Close,
            last.Close,
            absoluteChange,
            first.Close == 0m ? null : absoluteChange / first.Close * 100m,
            highest.High,
            ToOpenTime(highest),
            lowest.Low,
            ToOpenTime(lowest),
            candles.Sum(candle => candle.Volume),
            candles.Average(candle => candle.Volume),
            endOfRangeAnalysis,
            selectedCandle);
    }

    private static DateTimeOffset ToOpenTime(CandleDto candle) => DateTimeOffset.FromUnixTimeMilliseconds(candle.Timestamp);

    private static SelectedChartCandle? CreateSelectedCandle(CandleDto? candle)
    {
        if (candle is null)
        {
            return null;
        }

        return new SelectedChartCandle(
            ToOpenTime(candle), candle.Open, candle.High, candle.Low, candle.Close, candle.Volume,
            CreateIndicatorValues(candle.Indicators));
    }

    private static IReadOnlyDictionary<string, decimal> CreateIndicatorValues(ChartIndicatorValues? indicators)
    {
        if (indicators is null)
        {
            return new Dictionary<string, decimal>();
        }

        var values = new Dictionary<string, decimal?>
        {
            ["ema20"] = indicators.EmaFast,
            ["ema50"] = indicators.EmaSlow,
            ["ema200"] = indicators.EmaTrend,
            ["rsi14"] = indicators.Rsi,
            ["macd"] = indicators.MacdLine,
            ["macdSignal"] = indicators.MacdSignal,
            ["macdHistogram"] = indicators.MacdHistogram,
            ["bollingerUpper"] = indicators.BollingerUpper,
            ["bollingerMiddle"] = indicators.BollingerMiddle,
            ["bollingerLower"] = indicators.BollingerLower,
        };

        return values
            .Where(pair => pair.Value.HasValue)
            .ToDictionary(pair => pair.Key, pair => pair.Value!.Value);
    }
}