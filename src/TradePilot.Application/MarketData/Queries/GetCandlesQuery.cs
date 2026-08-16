using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;
using TradePilot.Application.Trading.Services;

namespace TradePilot.Application.MarketData.Queries;

/// <summary>
/// Requests a bounded set of recent candles through the exchange-independent historical-data capability.
/// </summary>
/// <param name="Asset">The exchange-facing market symbol.</param>
/// <param name="Timeframe">The requested candle timeframe.</param>
/// <param name="Exchange">The exchange from which candles should be retrieved.</param>
/// <param name="EndTime">The optional query end as Unix time in milliseconds.</param>
/// <param name="Limit">The maximum number of candles to return, from 1 through 500.</param>
/// <param name="IncludeIndicators">Whether to enrich each candle with chart indicator values.</param>
public sealed record GetCandlesQuery(
    string Asset,
    string Timeframe,
    Exchange Exchange = Exchange.Hyperliquid,
    long? EndTime = null,
    int Limit = 500,
    bool IncludeIndicators = true) : Query<List<CandleDto>>;

/// <summary>
/// Retrieves recent candles through exchange-independent historical-data and symbol abstractions.
/// </summary>
public sealed class GetCandlesQueryHandler : QueryHandler<GetCandlesQuery, List<CandleDto>>
{
    private readonly IReadOnlyList<IExchangeHistoricalDataClient> _historicalDataClients;
    private readonly IReadOnlyList<IExchangeSymbolMapper> _symbolMappers;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetCandlesQueryHandler"/> class.
    /// </summary>
    /// <param name="historicalDataClients">The registered exchange historical-data clients.</param>
    /// <param name="symbolMappers">The registered exchange symbol mappers.</param>
    public GetCandlesQueryHandler(
        IEnumerable<IExchangeHistoricalDataClient> historicalDataClients,
        IEnumerable<IExchangeSymbolMapper> symbolMappers)
    {
        _historicalDataClients = historicalDataClients.ToList();
        _symbolMappers = symbolMappers.ToList();
    }

    /// <inheritdoc />
    public override async Task<List<CandleDto>> Handle(GetCandlesQuery request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Asset);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Timeframe);

        if (request.Limit is <= 0 or > 500)
        {
            throw new DomainException("Limit must be between 1 and 500.");
        }

        var intervalMs = MarketTimeframe.GetDurationMilliseconds(request.Timeframe);
        var historicalDataClient = ResolveHistoricalDataClient(_historicalDataClients, request.Exchange);
        var symbolMapper = ResolveSymbolMapper(_symbolMappers, request.Exchange);
        var pair = symbolMapper.FromExchangeSymbol(request.Asset);
        var endTime = request.EndTime ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var startTime = endTime - (request.Limit * intervalMs);

        var snapshots = await historicalDataClient.GetCandleSnapshotsAsync(
            pair,
            request.Timeframe,
            startTime,
            endTime,
            cancellationToken);

        var candles = snapshots
            .Select(candle => new CandleDto
            {
                Timestamp = candle.Timestamp,
                Open = candle.Open,
                High = candle.High,
                Low = candle.Low,
                Close = candle.Close,
                Volume = candle.Volume,
            })
            .OrderByDescending(candle => candle.Timestamp)
            .Take(request.Limit)
            .ToList();

        return request.IncludeIndicators ? EnrichCandles(candles) : candles;
    }

    private static IExchangeHistoricalDataClient ResolveHistoricalDataClient(
        IEnumerable<IExchangeHistoricalDataClient> historicalDataClients,
        Exchange exchange)
    {
        return historicalDataClients.FirstOrDefault(client => client.Exchange == exchange)
            ?? throw new InvalidOperationException($"No historical data client is registered for exchange '{exchange}'.");
    }

    private static IExchangeSymbolMapper ResolveSymbolMapper(
        IEnumerable<IExchangeSymbolMapper> symbolMappers,
        Exchange exchange)
    {
        return symbolMappers.FirstOrDefault(mapper => mapper.Exchange == exchange)
            ?? throw new InvalidOperationException($"No symbol mapper is registered for exchange '{exchange}'.");
    }

    internal static List<CandleDto> EnrichCandles(IReadOnlyList<CandleDto> candles)
    {
        if (candles.Count == 0)
        {
            return [];
        }

        var indexed = candles
            .Select((candle, index) => new { Candle = candle, Index = index })
            .OrderBy(entry => entry.Candle.Timestamp)
            .ToList();

        var indicators = ChartIndicatorSeriesCalculator.Calculate(indexed
            .Select(entry => (entry.Candle.High, entry.Candle.Low, entry.Candle.Close))
            .ToList());

        var enriched = new CandleDto[candles.Count];
        for (var sortedIndex = 0; sortedIndex < indexed.Count; sortedIndex++)
        {
            var entry = indexed[sortedIndex];
            enriched[entry.Index] = new CandleDto
            {
                Timestamp = entry.Candle.Timestamp,
                Open = entry.Candle.Open,
                High = entry.Candle.High,
                Low = entry.Candle.Low,
                Close = entry.Candle.Close,
                Volume = entry.Candle.Volume,
                Indicators = indicators[sortedIndex],
            };
        }

        return enriched.ToList();
    }
}
