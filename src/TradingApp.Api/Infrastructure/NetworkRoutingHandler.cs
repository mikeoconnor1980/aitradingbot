using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Services;

namespace TradingApp.Api.Infrastructure;

/// <summary>
/// DelegatingHandler that rewrites outgoing HTTP request URIs to match the
/// current user's preferred Hyperliquid network (mainnet / testnet).
/// </summary>
public sealed class NetworkRoutingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public NetworkRoutingHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var provider = _httpContextAccessor.HttpContext?.RequestServices.GetService<INetworkProvider>();

        if (provider is not null && request.RequestUri is not null)
        {
            var network = await provider.GetNetworkAsync(cancellationToken);
            var targetBase = new Uri(HyperliquidOptions.GetBaseUrlForNetwork(network));

            var builder = new UriBuilder(request.RequestUri)
            {
                Scheme = targetBase.Scheme,
                Host = targetBase.Host,
                Port = targetBase.Port,
            };

            request.RequestUri = builder.Uri;
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
