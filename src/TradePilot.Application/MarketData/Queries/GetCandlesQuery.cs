using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;
using TradePilot.Application.Trading.Services;

namespace TradePilot.Application.MarketData.Queries;

public sealed record GetCandlesQuery(string Asset, string Timeframe, Exchange Exchange = Exchange.Hyperliquid, long? EndTime = null) : Query<List<CandleDto>>;

public sealed class GetCandlesQueryHandler : QueryHandler<GetCandlesQuery, List<CandleDto>>
{
    private readonly IReadOnlyList<IExchangeHistoricalDataClient> _historicalDataClients;
    private readonly IReadOnlyList<IExchangeSymbolMapper> _symbolMappers;

    public GetCandlesQueryHandler(
        IEnumerable<IExchangeHistoricalDataClient> historicalDataClients,
        IEnumerable<IExchangeSymbolMapper> symbolMappers)
    {
        _historicalDataClients = historicalDataClients.ToList();
        _symbolMappers = symbolMappers.ToList();
    }

    public override async Task<List<CandleDto>> Handle(GetCandlesQuery request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Asset);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Timeframe);

        var historicalDataClient = ResolveHistoricalDataClient(_historicalDataClients, request.Exchange);
        var symbolMapper = ResolveSymbolMapper(_symbolMappers, request.Exchange);
        var pair = symbolMapper.FromExchangeSymbol(request.Asset);
        var intervalMs = GetIntervalMs(request.Timeframe);
        var endTime = request.EndTime ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var startTime = endTime - (500L * intervalMs);

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
            .Take(500)
            .ToList();

        return EnrichCandles(candles);
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

    private static long GetIntervalMs(string timeframe)
    {
        return timeframe.Trim().ToLowerInvariant() switch
        {
            "5m" => 300_000L,
            "15m" => 900_000L,
            "1h" => 3_600_000L,
            "4h" => 14_400_000L,
            "1d" => 86_400_000L,
            var unsupported => throw new DomainException($"Invalid timeframe '{unsupported}'. Supported: 5m, 15m, 1h, 4h, 1d"),
        };
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