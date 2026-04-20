using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TradePilot.Application.Abstractions.Configuration;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Domain.Enums;

namespace TradePilot.Api.Infrastructure;

public sealed class BinanceSigningHandler : DelegatingHandler
{
    private readonly IExchangeCredentialAccessor _credentialAccessor;
    private readonly BinanceTradingOptions _options;

    public BinanceSigningHandler(
        IExchangeCredentialAccessor credentialAccessor,
        IOptions<BinanceTradingOptions> options)
    {
        _credentialAccessor = credentialAccessor;
        _options = options.Value;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var credential = await _credentialAccessor.GetActiveCredentialAsync(Exchange.Binance, cancellationToken);
        if (credential is null)
        {
            throw new DomainException("No active Binance API credential is configured for the current user.");
        }

        if (request.RequestUri is null)
        {
            throw new InvalidOperationException("Cannot sign a Binance request without a request URI.");
        }

        var query = RemoveTransientAuthParameters(request.RequestUri.Query.TrimStart('?'));
        query = AppendParameter(query, "timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture));
        query = AppendParameter(query, "recvWindow", _options.RecvWindowMs.ToString(CultureInfo.InvariantCulture));
        query = AppendParameter(query, "signature", ComputeSignature(query, credential.ApiSecret));

        var builder = new UriBuilder(request.RequestUri)
        {
            Query = query,
        };

        request.RequestUri = builder.Uri;
        request.Headers.Remove("X-MBX-APIKEY");
        request.Headers.Add("X-MBX-APIKEY", credential.ApiKey);

        return await base.SendAsync(request, cancellationToken);
    }

    private static string RemoveTransientAuthParameters(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return string.Empty;
        }

        var filtered = query
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(part =>
            {
                var key = part.Split('=', 2)[0];
                return !key.Equals("timestamp", StringComparison.OrdinalIgnoreCase)
                    && !key.Equals("recvWindow", StringComparison.OrdinalIgnoreCase)
                    && !key.Equals("signature", StringComparison.OrdinalIgnoreCase);
            });

        return string.Join('&', filtered);
    }

    private static string AppendParameter(string query, string key, string value)
    {
        var encoded = $"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}";
        return string.IsNullOrWhiteSpace(query) ? encoded : $"{query}&{encoded}";
    }

    private static string ComputeSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexStringLower(hash);
    }
}