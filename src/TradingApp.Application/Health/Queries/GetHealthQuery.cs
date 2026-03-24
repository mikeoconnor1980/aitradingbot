using Microsoft.Extensions.Options;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Queries;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Health.Models;

namespace TradingApp.Application.Health.Queries;

public sealed record GetHealthQuery : Query<HealthDto>;

public sealed class GetHealthQueryHandler : QueryHandler<GetHealthQuery, HealthDto>
{
    private readonly IHyperliquidRestClient _restClient;
    private readonly IHyperliquidSigner _signer;
    private readonly HyperliquidOptions _options;

    public GetHealthQueryHandler(
        IHyperliquidRestClient restClient,
        IHyperliquidSigner signer,
        IOptions<HyperliquidOptions> options)
    {
        _restClient = restClient;
        _signer = signer;
        _options = options.Value;
    }

    public override async Task<HealthDto> Handle(GetHealthQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var isConnected = await _restClient.CheckConnectivityAsync(cancellationToken);

            return new HealthDto
            {
                Status = isConnected ? "connected" : "disconnected",
                WalletAddress = TruncateAddress(_signer.WalletAddress),
                Network = _options.Network,
                Timestamp = DateTimeOffset.UtcNow,
                Error = isConnected ? null : "Hyperliquid testnet API did not respond successfully"
            };
        }
        catch (TaskCanceledException)
        {
            return new HealthDto
            {
                Status = "disconnected",
                WalletAddress = TruncateAddress(_signer.WalletAddress),
                Network = _options.Network,
                Timestamp = DateTimeOffset.UtcNow,
                Error = "Hyperliquid testnet API request timed out"
            };
        }
        catch (HttpRequestException)
        {
            return new HealthDto
            {
                Status = "disconnected",
                WalletAddress = TruncateAddress(_signer.WalletAddress),
                Network = _options.Network,
                Timestamp = DateTimeOffset.UtcNow,
                Error = "Failed to reach Hyperliquid testnet API"
            };
        }
    }

    private static string TruncateAddress(string address)
    {
        return address.Length > 10
            ? $"{address[..6]}...{address[^4..]}"
            : address;
    }
}
