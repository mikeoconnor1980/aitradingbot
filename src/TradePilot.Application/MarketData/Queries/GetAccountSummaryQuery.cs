using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;

namespace TradePilot.Application.MarketData.Queries;

/// <summary>
/// Requests the current derivatives account summary for an exchange account.
/// </summary>
/// <param name="Exchange">The exchange whose account state should be queried.</param>
/// <param name="WalletAddress">The optional public wallet address required by wallet-based exchanges.</param>
public sealed record GetAccountSummaryQuery(
    Exchange Exchange,
    string? WalletAddress = null) : Query<AccountSummaryDto>;

/// <summary>
/// Retrieves an account summary through the exchange-independent account abstraction.
/// </summary>
public sealed class GetAccountSummaryQueryHandler : QueryHandler<GetAccountSummaryQuery, AccountSummaryDto>
{
    private readonly IReadOnlyList<IExchangeAccountClient> _accountClients;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetAccountSummaryQueryHandler"/> class.
    /// </summary>
    /// <param name="accountClients">The registered exchange account clients.</param>
    public GetAccountSummaryQueryHandler(IEnumerable<IExchangeAccountClient> accountClients)
    {
        _accountClients = accountClients.ToList();
    }

    /// <inheritdoc />
    public override Task<AccountSummaryDto> Handle(
        GetAccountSummaryQuery request,
        CancellationToken cancellationToken)
    {
        return ExchangeAccountClientResolver.Resolve(_accountClients, request.Exchange)
            .GetAccountSummaryAsync(request.WalletAddress, cancellationToken);
    }
}
