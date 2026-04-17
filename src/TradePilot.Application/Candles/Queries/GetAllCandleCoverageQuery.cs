using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Candles.Models;

namespace TradePilot.Application.Candles.Queries;

public sealed record GetAllCandleCoverageQuery(
    IReadOnlyList<string> Symbols,
    IReadOnlyList<string> Intervals) : Query<AllCandleCoverageResponse>;

public sealed class GetAllCandleCoverageQueryHandler
    : QueryHandler<GetAllCandleCoverageQuery, AllCandleCoverageResponse>
{
    private readonly ICandleRepository _candleRepository;

    public GetAllCandleCoverageQueryHandler(ICandleRepository candleRepository)
    {
        _candleRepository = candleRepository;
    }

    public override async Task<AllCandleCoverageResponse> Handle(
        GetAllCandleCoverageQuery request,
        CancellationToken cancellationToken)
    {
        var symbols = new List<SymbolCoverage>(request.Symbols.Count);

        foreach (var symbol in request.Symbols)
        {
            var intervals = new List<IntervalCoverageDetail>(request.Intervals.Count);

            foreach (var interval in request.Intervals)
            {
                var (fromTs, toTs, count) = await _candleRepository.GetCoverageAsync(
                    symbol, interval, cancellationToken: cancellationToken);

                intervals.Add(new IntervalCoverageDetail
                {
                    Interval = interval,
                    From = fromTs.HasValue
                        ? DateTimeOffset.FromUnixTimeMilliseconds(fromTs.Value).UtcDateTime
                        : null,
                    To = toTs.HasValue
                        ? DateTimeOffset.FromUnixTimeMilliseconds(toTs.Value).UtcDateTime
                        : null,
                    CandleCount = count,
                });
            }

            symbols.Add(new SymbolCoverage
            {
                Symbol = symbol,
                Intervals = intervals,
            });
        }

        return new AllCandleCoverageResponse { Symbols = symbols };
    }
}
