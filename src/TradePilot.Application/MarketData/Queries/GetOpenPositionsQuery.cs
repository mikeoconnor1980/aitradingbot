using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;

namespace TradePilot.Application.MarketData.Queries;

/// <summary>
/// Requests the open derivatives positions for an exchange account.
/// </summary>
/// <param name="Exchange">The exchange whose positions should be queried.</param>
/// <param name="WalletAddress">The optional public wallet address required by wallet-based exchanges.</param>
public sealed record GetOpenPositionsQuery(
    Exchange Exchange,
    string? WalletAddress = null) : Query<IReadOnlyList<PositionDto>>;

/// <summary>
/// Retrieves open positions through the exchange-independent account abstraction.
/// </summary>
public sealed class GetOpenPositionsQueryHandler : QueryHandler<GetOpenPositionsQuery, IReadOnlyList<PositionDto>>
{
    private readonly IReadOnlyList<IExchangeAccountClient> _accountClients;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetOpenPositionsQueryHandler"/> class.
    /// </summary>
    /// <param name="accountClients">The registered exchange account clients.</param>
    public GetOpenPositionsQueryHandler(IEnumerable<IExchangeAccountClient> accountClients)
    {
        _accountClients = accountClients.ToList();
    }

    /// <inheritdoc />
    public override Task<IReadOnlyList<PositionDto>> Handle(
        GetOpenPositionsQuery request,
        CancellationToken cancellationToken)
    {
        return ExchangeAccountClientResolver.Resolve(_accountClients, request.Exchange)
            .GetPositionsAsync(request.WalletAddress, cancellationToken);
    }
}
