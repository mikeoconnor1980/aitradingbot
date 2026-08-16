using MediatR;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.MarketAnalysis.Models;
using TradePilot.Application.MarketAnalysis.Services;
using TradePilot.Application.MarketData;
using TradePilot.Application.MarketData.Models;

namespace TradePilot.Application.MarketAnalysis.Queries;

/// <summary>
/// Requests deterministic Phase 2 market analysis for two or more explicit timeframes.
/// </summary>
/// <param name="Symbol">The exchange-facing market symbol preserved by Phase 2.</param>
/// <param name="Timeframes">Explicit candle timeframes; aliases and duplicates are canonicalized.</param>
/// <param name="Exchange">The exchange from which each Phase 2 analysis retrieves candles.</param>
/// <param name="AsOf">An optional shared UTC cutoff for all underlying Phase 2 analyses.</param>
public sealed record AnalyseMarketMultiTimeframeQuery(
    string Symbol,
    IReadOnlyCollection<string> Timeframes,
    Exchange Exchange = Exchange.Hyperliquid,
    DateTimeOffset? AsOf = null) : Query<MultiTimeframeMarketAnalysisResult>;

/// <summary>
/// Sequentially composes the existing Phase 2 capability once per distinct canonical timeframe.
/// </summary>
public sealed class AnalyseMarketMultiTimeframeQueryHandler
    : QueryHandler<AnalyseMarketMultiTimeframeQuery, MultiTimeframeMarketAnalysisResult>
{
    private readonly ISender _sender;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnalyseMarketMultiTimeframeQueryHandler"/> class.
    /// </summary>
    /// <param name="sender">The mediator used to invoke the existing Phase 2 capability.</param>
    /// <param name="timeProvider">The clock used for a shared cutoff and composite generation timestamp.</param>
    public AnalyseMarketMultiTimeframeQueryHandler(ISender sender, TimeProvider? timeProvider = null)
    {
        _sender = sender;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public override async Task<MultiTimeframeMarketAnalysisResult> Handle(
        AnalyseMarketMultiTimeframeQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Symbol);
        ArgumentNullException.ThrowIfNull(request.Timeframes);
        cancellationToken.ThrowIfCancellationRequested();

        var orderedTimeframes = request.Timeframes
            .Select(NormalizeTimeframe)
            .Distinct(StringComparer.Ordinal)
            .Select(timeframe => new
            {
                Timeframe = timeframe,
                Duration = MarketTimeframe.GetDurationMilliseconds(timeframe),
            })
            .OrderBy(item => item.Duration)
            .Select(item => item.Timeframe)
            .ToList();

        if (orderedTimeframes.Count < 2)
        {
            throw new DomainException("At least two distinct timeframes are required for multi-timeframe analysis.");
        }

        var analysisAsOf = request.AsOf ?? _timeProvider.GetUtcNow();
        var analyses = new List<TimeframeMarketAnalysis>(orderedTimeframes.Count);

        foreach (var timeframe in orderedTimeframes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var analysis = await _sender.Send(
                new AnalyseMarketQuery(request.Symbol, timeframe, request.Exchange, analysisAsOf),
                cancellationToken);
            analyses.Add(new TimeframeMarketAnalysis(timeframe, analysis));
        }

        cancellationToken.ThrowIfCancellationRequested();

        return MultiTimeframeAnalysisPolicy.Compose(
            request.Symbol.Trim(),
            _timeProvider.GetUtcNow(),
            analyses);
    }

    /// <summary>
    /// Canonicalizes casing after delegating supported-timeframe validation to the existing Phase 2 utility.
    /// </summary>
    private static string NormalizeTimeframe(string timeframe)
    {
        _ = MarketTimeframe.GetDurationMilliseconds(timeframe);
        var trimmed = timeframe.Trim();

        return trimmed == "1M" ? trimmed : trimmed.ToLowerInvariant();
    }
}
