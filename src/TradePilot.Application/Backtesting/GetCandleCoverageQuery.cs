using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Backtesting.Models;

namespace TradePilot.Application.Backtesting;

public sealed record GetCandleCoverageQuery(string Symbol, string[] Intervals) : Query<CandleCoverageResponse>;

public sealed class GetCandleCoverageQueryHandler : QueryHandler<GetCandleCoverageQuery, CandleCoverageResponse>
{
    private readonly ICandleRepository _candleRepository;

    public GetCandleCoverageQueryHandler(ICandleRepository candleRepository)
    {
        _candleRepository = candleRepository;
    }

    public override async Task<CandleCoverageResponse> Handle(GetCandleCoverageQuery request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Symbol);
        ArgumentNullException.ThrowIfNull(request.Intervals);

        var coverage = new Dictionary<string, IntervalCoverage>(StringComparer.OrdinalIgnoreCase);

        foreach (var interval in request.Intervals)
        {
            var intervalCoverage = await _candleRepository.GetCoverageAsync(
                request.Symbol,
                interval,
                cancellationToken: cancellationToken);

            coverage[$"{request.Symbol}/{interval}"] = new IntervalCoverage
            {
                From = intervalCoverage.FromTimestampUtc.HasValue
                    ? DateTimeOffset.FromUnixTimeMilliseconds(intervalCoverage.FromTimestampUtc.Value).UtcDateTime
                    : null,
                To = intervalCoverage.ToTimestampUtc.HasValue
                    ? DateTimeOffset.FromUnixTimeMilliseconds(intervalCoverage.ToTimestampUtc.Value).UtcDateTime
                    : null,
                CandleCount = intervalCoverage.CandleCount
            };
        }

        return new CandleCoverageResponse
        {
            Coverage = coverage
        };
    }
}