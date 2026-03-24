namespace TradingApp.Application.Abstractions.Services;

public interface IHyperliquidRestClient
{
    Task<bool> CheckConnectivityAsync(CancellationToken cancellationToken = default);
}
