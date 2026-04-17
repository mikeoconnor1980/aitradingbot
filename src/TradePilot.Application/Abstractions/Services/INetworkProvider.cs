namespace TradePilot.Application.Abstractions.Services;

/// <summary>
/// Resolves the active Hyperliquid network (mainnet / testnet) for the current request scope.
/// </summary>
public interface INetworkProvider
{
    Task<string> GetNetworkAsync(CancellationToken cancellationToken = default);

    async Task<bool> IsMainnetAsync(CancellationToken cancellationToken = default)
    {
        var network = await GetNetworkAsync(cancellationToken);
        return network.Equals("mainnet", StringComparison.OrdinalIgnoreCase);
    }
}
