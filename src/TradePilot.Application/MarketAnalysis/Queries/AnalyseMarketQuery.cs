using MediatR;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.MarketAnalysis.Models;
using TradePilot.Application.MarketAnalysis.Services;
using TradePilot.Application.MarketData;
using TradePilot.Application.MarketData.Models;
using TradePilot.Application.MarketData.Queries;
using TradePilot.Indicators;

namespace TradePilot.Application.MarketAnalysis.Queries;

/// <summary>
/// Requests deterministic technical analysis for one market and one timeframe.
/// </summary>
/// <param name="Symbol">The exchange-facing market symbol, such as BTC or BTC-PERP.</param>
/// <param name="Timeframe">A timeframe supported by the existing candle capability, such as 4h.</param>
/// <param name="Exchange">The exchange from which candles should be retrieved.</param>
/// <param name="AsOf">The optional UTC analysis cutoff. Candles closing after this instant are excluded.</param>
public sealed record AnalyseMarketQuery(
    string Symbol,
    string Timeframe,
    Exchange Exchange = Exchange.Hyperliquid,
    DateTimeOffset? AsOf = null) : Query<MarketAnalysisResult>;

/// <summary>
/// Composes the existing candle capability with TradePilot indicators and deterministic classifications.
/// </summary>
public sealed class AnalyseMarketQueryHandler : QueryHandler<AnalyseMarketQuery, MarketAnalysisResult>
{
    internal const int RequestedCandleCount = 250;
    internal const int MinimumCandleCount = 200;
    internal const int MaximumStalenessIntervals = 2;
    private const int RsiPeriod = 14;
    private const int AtrPeriod = 14;

    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnalyseMarketQueryHandler"/> class.
    /// </summary>
    /// <param name="sender">The mediator used to invoke the existing candle application capability.</param>
    public AnalyseMarketQueryHandler(ISender sender)
    {
        _sender = sender;
    }

    /// <inheritdoc />
    public override async Task<MarketAnalysisResult> Handle(
        AnalyseMarketQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Timeframe);
        cancellationToken.ThrowIfCancellationRequested();

        var timeframeMilliseconds = MarketTimeframe.GetDurationMilliseconds(request.Timeframe);
        var asOf = request.AsOf ?? DateTimeOffset.UtcNow;
        var asOfMilliseconds = asOf.ToUnixTimeMilliseconds();
        var fetchedCandles = await _sender.Send(
            new GetCandlesQuery(
                request.Symbol,
                request.Timeframe,
                request.Exchange,
                asOfMilliseconds,
                RequestedCandleCount,
                IncludeIndicators: false),
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        var completedCandles = fetchedCandles
            .Where(candle => IsCompleted(candle, timeframeMilliseconds, asOfMilliseconds))
            .GroupBy(candle => candle.Timestamp)
            .Select(group => group.First())
            .OrderBy(candle => candle.Timestamp)
            .ToList();

        if (completedCandles.Count < MinimumCandleCount)
        {
            throw new DomainException(
                $"Insufficient completed candle history for {request.Symbol.Trim()}/{request.Timeframe.Trim()}. " +
                $"At least {MinimumCandleCount} candles are required; {completedCandles.Count} were available.");
        }

        var latestCandle = completedCandles[^1];
        var latestCloseTimeMilliseconds = checked(latestCandle.Timestamp + timeframeMilliseconds);
        if (request.AsOf is null && latestCloseTimeMilliseconds < checked(
                asOfMilliseconds - (MaximumStalenessIntervals * timeframeMilliseconds)))
        {
            throw new DomainException(
                $"The latest completed {request.Timeframe.Trim()} candle for {request.Symbol.Trim()} closed at " +
                $"{DateTimeOffset.FromUnixTimeMilliseconds(latestCloseTimeMilliseconds):O}, which is stale for analysis as of {asOf:O}.");
        }

        if (latestCandle.Close <= 0m)
        {
            throw new DomainException("The latest completed candle close must be greater than zero.");
        }

        var closes = completedCandles.Select(candle => candle.Close).ToList();
        var bars = completedCandles
            .Select(candle => (candle.High, candle.Low, candle.Close))
            .ToList();
        var ema20 = RequireIndicator(EmaCalculator.Calculate(closes, 20), "EMA20");
        var ema50 = RequireIndicator(EmaCalculator.Calculate(closes, 50), "EMA50");
        var ema200 = RequireIndicator(EmaCalculator.Calculate(closes, 200), "EMA200");
        var rsi = RequireIndicator(RsiCalculator.Calculate(closes, RsiPeriod), "RSI14");
        var atr = RequireIndicator(AtrCalculator.Calculate(bars, AtrPeriod), "ATR14");
        var atrPercent = CalculatePercent(atr, latestCandle.Close);
        var swings = ConfirmedSwingDetector.Detect(completedCandles);

        var indicators = new MarketIndicatorValues(
            ema20,
            ema50,
            ema200,
            rsi,
            atr,
            atrPercent,
            CalculatePercent(latestCandle.Close - ema20, ema20),
            CalculatePercent(latestCandle.Close - ema50, ema50),
            CalculatePercent(latestCandle.Close - ema200, ema200));

        return new MarketAnalysisResult(
            request.Symbol.Trim(),
            request.Timeframe.Trim(),
            DateTimeOffset.FromUnixTimeMilliseconds(checked(latestCandle.Timestamp + timeframeMilliseconds)),
            latestCandle.Close,
            indicators,
            MarketAnalysisPolicy.ClassifyTrend(latestCandle.Close, ema20, ema50, ema200),
            MarketAnalysisPolicy.ClassifyMomentum(rsi),
            MarketAnalysisPolicy.ClassifyVolatility(atrPercent),
            MarketAnalysisPolicy.ClassifyStructure(swings),
            swings.Highs.Count == 0 ? null : swings.Highs[^1],
            swings.Lows.Count == 0 ? null : swings.Lows[^1]);
    }

    private static bool IsCompleted(CandleDto candle, long timeframeMilliseconds, long asOfMilliseconds)
    {
        return candle.Timestamp > 0
            && candle.Timestamp <= asOfMilliseconds - timeframeMilliseconds;
    }

    private static decimal RequireIndicator(decimal? value, string name)
    {
        return value ?? throw new DomainException($"{name} is unavailable for the completed candle history.");
    }

    private static decimal CalculatePercent(decimal numerator, decimal denominator)
    {
        if (denominator == 0m)
        {
            throw new DomainException("A percentage calculation denominator must be greater than zero.");
        }

        return numerator / denominator * 100m;
    }
}
