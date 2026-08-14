using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;
using TradePilot.Domain.ValueObjects;

namespace TradePilot.Application.MarketData.Queries;

/// <summary>
/// Requests recent fills for an exchange account, optionally filtered by asset.
/// </summary>
/// <param name="Exchange">The exchange whose fills should be queried.</param>
/// <param name="Asset">An optional exchange-facing asset symbol.</param>
/// <param name="WalletAddress">The optional public wallet address required by wallet-based exchanges.</param>
public sealed record GetRecentFillsQuery(
    Exchange Exchange,
    string? Asset = null,
    string? WalletAddress = null) : Query<IReadOnlyList<FillEventDto>>;

/// <summary>
/// Retrieves recent fills through exchange-independent account and symbol abstractions.
/// </summary>
public sealed class GetRecentFillsQueryHandler : QueryHandler<GetRecentFillsQuery, IReadOnlyList<FillEventDto>>
{
    private readonly IReadOnlyList<IExchangeAccountClient> _accountClients;
    private readonly IReadOnlyList<IExchangeSymbolMapper> _symbolMappers;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetRecentFillsQueryHandler"/> class.
    /// </summary>
    /// <param name="accountClients">The registered exchange account clients.</param>
    /// <param name="symbolMappers">The registered exchange symbol mappers.</param>
    public GetRecentFillsQueryHandler(
        IEnumerable<IExchangeAccountClient> accountClients,
        IEnumerable<IExchangeSymbolMapper> symbolMappers)
    {
        _accountClients = accountClients.ToList();
        _symbolMappers = symbolMappers.ToList();
    }

    /// <inheritdoc />
    public override Task<IReadOnlyList<FillEventDto>> Handle(
        GetRecentFillsQuery request,
        CancellationToken cancellationToken)
    {
        var pair = ResolveTradingPair(request.Exchange, request.Asset);

        return ExchangeAccountClientResolver.Resolve(_accountClients, request.Exchange)
            .GetRecentFillsAsync(pair, request.WalletAddress, cancellationToken);
    }

    private TradingPair? ResolveTradingPair(Exchange exchange, string? asset)
    {
        if (string.IsNullOrWhiteSpace(asset))
        {
            return null;
        }

        var mapper = _symbolMappers.FirstOrDefault(candidate => candidate.Exchange == exchange)
            ?? throw new InvalidOperationException($"No symbol mapper is registered for exchange '{exchange}'.");

        return mapper.FromExchangeSymbol(asset);
    }
}
