using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;

namespace TradePilot.Application.MarketData.Queries;

/// <summary>
/// Requests active orders for an exchange account.
/// </summary>
/// <param name="Exchange">The exchange whose orders should be queried.</param>
/// <param name="WalletAddress">The optional public wallet address required by wallet-based exchanges.</param>
public sealed record GetOpenOrdersQuery(
    Exchange Exchange,
    string? WalletAddress = null) : Query<IReadOnlyList<OpenOrderDto>>;

/// <summary>
/// Retrieves active orders through the exchange-independent account abstraction.
/// </summary>
public sealed class GetOpenOrdersQueryHandler : QueryHandler<GetOpenOrdersQuery, IReadOnlyList<OpenOrderDto>>
{
    private readonly IReadOnlyList<IExchangeAccountClient> _accountClients;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetOpenOrdersQueryHandler"/> class.
    /// </summary>
    /// <param name="accountClients">The registered exchange account clients.</param>
    public GetOpenOrdersQueryHandler(IEnumerable<IExchangeAccountClient> accountClients)
    {
        _accountClients = accountClients.ToList();
    }

    /// <inheritdoc />
    public override Task<IReadOnlyList<OpenOrderDto>> Handle(
        GetOpenOrdersQuery request,
        CancellationToken cancellationToken)
    {
        return ExchangeAccountClientResolver.Resolve(_accountClients, request.Exchange)
            .GetOpenOrdersAsync(request.WalletAddress, cancellationToken);
    }
}
