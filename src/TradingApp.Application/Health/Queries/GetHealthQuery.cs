using TradingApp.Application.Abstractions.Queries;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Health.Models;

namespace TradingApp.Application.Health.Queries;

public sealed record GetHealthQuery : Query<HealthDto>;

public sealed class GetHealthQueryHandler : QueryHandler<GetHealthQuery, HealthDto>
{
    private readonly IHyperliquidRestClient _restClient;
    private readonly IHyperliquidSigner _signer;
    private readonly INetworkProvider _networkProvider;

    public GetHealthQueryHandler(
        IHyperliquidRestClient restClient,
        IHyperliquidSigner signer,
        INetworkProvider networkProvider)
    {
        _restClient = restClient;
        _signer = signer;
        _networkProvider = networkProvider;
    }

    public override async Task<HealthDto> Handle(GetHealthQuery request, CancellationToken cancellationToken)
    {
        var walletAddress = GetWalletAddressSafe();
        var displayAddress = walletAddress is not null ? TruncateAddress(walletAddress) : "Not configured";
        var network = await _networkProvider.GetNetworkAsync(cancellationToken);

        try
        {
            var isConnected = await _restClient.CheckConnectivityAsync(cancellationToken);

            return new HealthDto
            {
                Status = isConnected ? "connected" : "disconnected",
                WalletAddress = displayAddress,
                Network = network,
                Timestamp = DateTimeOffset.UtcNow,
                Error = isConnected ? null : $"Hyperliquid {network} API did not respond successfully"
            };
        }
        catch (TaskCanceledException)
        {
            return new HealthDto
            {
                Status = "disconnected",
                WalletAddress = displayAddress,
                Network = network,
                Timestamp = DateTimeOffset.UtcNow,
                Error = $"Hyperliquid {network} API request timed out"
            };
        }
        catch (HttpRequestException)
        {
            return new HealthDto
            {
                Status = "disconnected",
                WalletAddress = displayAddress,
                Network = network,
                Timestamp = DateTimeOffset.UtcNow,
                Error = $"Failed to reach Hyperliquid {network} API"
            };
        }
    }

    private string? GetWalletAddressSafe()
    {
        try
        {
            return _signer.WalletAddress;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static string TruncateAddress(string address)
    {
        return address.Length > 10
            ? $"{address[..6]}...{address[^4..]}"
            : address;
    }
}
