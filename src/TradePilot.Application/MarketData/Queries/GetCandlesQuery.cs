using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;
using TradePilot.Application.Trading.Services;

namespace TradePilot.Application.MarketData.Queries;

public sealed record GetCandlesQuery(string Asset, string Timeframe, long? EndTime = null) : Query<List<CandleDto>>;

public sealed class GetCandlesQueryHandler : QueryHandler<GetCandlesQuery, List<CandleDto>>
{
    private readonly IHyperliquidRestClient _restClient;

    public GetCandlesQueryHandler(IHyperliquidRestClient restClient)
    {
        _restClient = restClient;
    }

    public override async Task<List<CandleDto>> Handle(GetCandlesQuery request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Asset);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Timeframe);

        var candles = await _restClient.GetCandlesAsync(request.Asset, request.Timeframe, request.EndTime, cancellationToken);
        return EnrichCandles(candles);
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