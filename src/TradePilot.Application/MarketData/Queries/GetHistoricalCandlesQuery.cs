using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.MarketData.Models;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.MarketData.Queries;

public sealed record GetHistoricalCandlesQuery(
    string Asset,
    string Timeframe,
    long? EndTime = null,
    int Limit = 500) : Query<List<CandleDto>>;

public sealed class GetHistoricalCandlesQueryHandler : QueryHandler<GetHistoricalCandlesQuery, List<CandleDto>>
{
    private static readonly Dictionary<string, long> TimeframeMs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["1m"] = 60_000L,
        ["3m"] = 180_000L,
        ["5m"] = 300_000L,
        ["15m"] = 900_000L,
        ["30m"] = 1_800_000L,
        ["1h"] = 3_600_000L,
        ["4h"] = 14_400_000L,
        ["1d"] = 86_400_000L,
    };

    private readonly ICandleRepository _candleRepository;

    public GetHistoricalCandlesQueryHandler(ICandleRepository candleRepository)
    {
        _candleRepository = candleRepository;
    }

    private const int MaxLimit = 5000;

    public override async Task<List<CandleDto>> Handle(GetHistoricalCandlesQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Asset))
        {
            throw new DomainException("Asset must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(request.Timeframe))
        {
            throw new DomainException("Timeframe must not be empty.");
        }

        if (request.Limit is <= 0 or > MaxLimit)
        {
            throw new DomainException($"Limit must be between 1 and {MaxLimit}.");
        }

        if (!TimeframeMs.TryGetValue(request.Timeframe, out var timeframeMs))
        {
            throw new DomainException($"Invalid timeframe '{request.Timeframe}'. Supported: {string.Join(", ", TimeframeMs.Keys)}");
        }

        var symbol = MapAssetToSymbol(request.Asset);
        var endTime = request.EndTime ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var startTime = endTime - (request.Limit * timeframeMs);

        var candles = await _candleRepository.GetCandlesAsync(
            symbol,
            request.Timeframe,
            startTime,
            endTime,
            cancellationToken: cancellationToken);

        var deduplicated = candles
            .GroupBy(candle => candle.Timestamp)
            .Select(group => group.First())
            .OrderBy(candle => candle.Timestamp)
            .ToList();

        var mapped = deduplicated
            .Skip(Math.Max(0, deduplicated.Count - request.Limit))
            .Select(MapToDto)
            .ToList();

        return GetCandlesQueryHandler.EnrichCandles(mapped);
    }

    private static string MapAssetToSymbol(string asset)
    {
        var trimmed = asset.Trim();
        return trimmed.EndsWith("-PERP", StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^5]
            : trimmed;
    }

    private static CandleDto MapToDto(Candle candle) => new()
    {
        Timestamp = candle.Timestamp,
        Open = candle.Open,
        High = candle.High,
        Low = candle.Low,
        Close = candle.Close,
        Volume = candle.Volume,
    };
}