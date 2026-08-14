using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.FundingRates.Models;

namespace TradePilot.Application.FundingRates.Queries;

/// <summary>
/// Requests historical funding observations for a market and time range.
/// </summary>
/// <param name="Asset">The exchange-facing asset symbol.</param>
/// <param name="StartTime">The inclusive range start as Unix time in milliseconds.</param>
/// <param name="EndTime">The inclusive range end as Unix time in milliseconds.</param>
/// <param name="Exchange">The exchange whose funding history should be queried.</param>
public sealed record GetFundingRatesQuery(
    string Asset,
    long StartTime,
    long EndTime,
    Exchange Exchange = Exchange.Hyperliquid) : Query<IReadOnlyList<FundingRateDto>>;

/// <summary>
/// Retrieves funding history through exchange-independent historical-data and symbol abstractions.
/// </summary>
public sealed class GetFundingRatesQueryHandler : QueryHandler<GetFundingRatesQuery, IReadOnlyList<FundingRateDto>>
{
    private readonly IReadOnlyList<IExchangeHistoricalDataClient> _historicalDataClients;
    private readonly IReadOnlyList<IExchangeSymbolMapper> _symbolMappers;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetFundingRatesQueryHandler"/> class.
    /// </summary>
    /// <param name="historicalDataClients">The registered exchange historical-data clients.</param>
    /// <param name="symbolMappers">The registered exchange symbol mappers.</param>
    public GetFundingRatesQueryHandler(
        IEnumerable<IExchangeHistoricalDataClient> historicalDataClients,
        IEnumerable<IExchangeSymbolMapper> symbolMappers)
    {
        _historicalDataClients = historicalDataClients.ToList();
        _symbolMappers = symbolMappers.ToList();
    }

    /// <inheritdoc />
    public override async Task<IReadOnlyList<FundingRateDto>> Handle(
        GetFundingRatesQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Asset);

        if (request.EndTime < request.StartTime)
        {
            throw new DomainException("EndTime must be greater than or equal to StartTime.");
        }

        var client = _historicalDataClients.FirstOrDefault(candidate => candidate.Exchange == request.Exchange)
            ?? throw new InvalidOperationException($"No historical data client is registered for exchange '{request.Exchange}'.");
        var mapper = _symbolMappers.FirstOrDefault(candidate => candidate.Exchange == request.Exchange)
            ?? throw new InvalidOperationException($"No symbol mapper is registered for exchange '{request.Exchange}'.");
        var pair = mapper.FromExchangeSymbol(request.Asset);
        var rates = await client.GetFundingRatesAsync(
            pair,
            request.StartTime,
            request.EndTime,
            cancellationToken);

        return rates.OrderBy(rate => rate.FundingTime).ToList();
    }
}
