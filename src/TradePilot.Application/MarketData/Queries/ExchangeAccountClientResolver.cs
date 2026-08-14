using TradePilot.Application.Abstractions.Services;

namespace TradePilot.Application.MarketData.Queries;

internal static class ExchangeAccountClientResolver
{
    public static IExchangeAccountClient Resolve(
        IEnumerable<IExchangeAccountClient> accountClients,
        Exchange exchange)
    {
        return accountClients.FirstOrDefault(client => client.Exchange == exchange)
            ?? throw new InvalidOperationException($"No account client is registered for exchange '{exchange}'.");
    }
}
