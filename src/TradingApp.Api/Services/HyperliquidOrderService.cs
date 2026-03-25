using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Options;
using Nethereum.ABI.EIP712;
using Nethereum.Util;
using TradingApp.Api.Models;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Infrastructure.Hyperliquid;
using TradingApp.Infrastructure.Hyperliquid.Models;

namespace TradingApp.Api.Services;

public sealed class HyperliquidOrderService : IHyperliquidOrderService
{
    private const int BtcAssetIndex = 0;

    private readonly IHyperliquidRestClient _restClient;
    private readonly IHyperliquidSigner _signer;
    private readonly INonceProvider _nonceProvider;
    private readonly HyperliquidOptions _options;
    private readonly ILogger<HyperliquidOrderService> _logger;

    public HyperliquidOrderService(
        IHyperliquidRestClient restClient,
        IHyperliquidSigner signer,
        INonceProvider nonceProvider,
        IOptions<HyperliquidOptions> options,
        ILogger<HyperliquidOrderService> logger)
    {
        _restClient = restClient;
        _signer = signer;
        _nonceProvider = nonceProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PlaceOrderResponse> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var coin = HyperliquidAssetMapper.ToCoin(request.Asset);
        if (!coin.Equals("BTC", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException($"Only BTC is supported in this POC. Received: {request.Asset}");
        }

        var isBuy = request.Side.Equals("buy", StringComparison.OrdinalIgnoreCase);
        var isMainnet = _options.Network.Equals("mainnet", StringComparison.OrdinalIgnoreCase);

        var price = request.OrderType.Equals("market", StringComparison.OrdinalIgnoreCase)
            ? (isBuy ? 999_999_999m : 0.01m)
            : request.Price ?? throw new DomainException("Price is required for limit orders.");

        var action = HyperliquidEip712.BuildOrderAction(
            assetIndex: BtcAssetIndex,
            isBuy: isBuy,
            price: price,
            size: request.Size);

        var nonce = _nonceProvider.GetNextNonce();
        var connectionId = HyperliquidEip712.ComputeActionHash(action, nonce, vaultAddress: null);
        var typedData = HyperliquidEip712.BuildPhantomAgentTypedData(connectionId, isMainnet);
        var (r, s, v) = _signer.SignTypedData(typedData);

        var payload = new
        {
            action,
            nonce,
            signature = new { r, s, v },
            vaultAddress = (string?)null,
        };

        var submitTimestamp = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var exchangeResponse = await _restClient
                .PostExchangeAsync<HyperliquidExchangeResponse>(payload, cancellationToken);
            stopwatch.Stop();

            var responseTimestamp = DateTimeOffset.UtcNow;
            _logger.LogInformation(
                "Order submitted. SubmitTimestampUtc={SubmitTimestampUtc}, ResponseTimestampUtc={ResponseTimestampUtc}, LatencyMs={LatencyMs}",
                submitTimestamp,
                responseTimestamp,
                stopwatch.ElapsedMilliseconds);

            return MapExchangeResponse(exchangeResponse);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("signature", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                ex,
                "EIP-712 signature rejected by Hyperliquid. WalletAddress={WalletAddress}, Nonce={Nonce}, V={V}",
                _signer.WalletAddress,
                nonce,
                v);

            return new PlaceOrderResponse
            {
                Success = false,
                Status = "signature_rejected",
                Detail = "The order signature was rejected by the exchange.",
            };
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();

            _logger.LogWarning(
                ex,
                "Order submission failed. SubmitTimestampUtc={SubmitTimestampUtc}, LatencyMs={LatencyMs}",
                submitTimestamp,
                stopwatch.ElapsedMilliseconds);

            return new PlaceOrderResponse
            {
                Success = false,
                Status = "rejected",
                Detail = "Order submission failed. Please retry.",
            };
        }
    }

    public Task<TestSignResponse> TestSignAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var isMainnet = _options.Network.Equals("mainnet", StringComparison.OrdinalIgnoreCase);
        var action = HyperliquidEip712.BuildOrderAction(
            assetIndex: BtcAssetIndex,
            isBuy: true,
            price: 65000m,
            size: 0.001m);

        var nonce = _nonceProvider.GetNextNonce();
        var connectionId = HyperliquidEip712.ComputeActionHash(action, nonce, vaultAddress: null);
        var typedData = HyperliquidEip712.BuildPhantomAgentTypedData(connectionId, isMainnet);
        var (r, s, v) = _signer.SignTypedData(typedData);

        var messageHash = "0x" + Convert.ToHexString(connectionId).ToLowerInvariant();

        var typeHashBytes = Sha3Keccack.Current.CalculateHash(
            Encoding.UTF8.GetBytes("Agent(string source,bytes32 connectionId)"));
        var typeHash = "0x" + Convert.ToHexString(typeHashBytes).ToLowerInvariant();

        var domainSeparatorBytes = Sha3Keccack.Current.CalculateHash(
            Encoding.UTF8.GetBytes(
                $"{typedData.Domain.Name}:{typedData.Domain.Version}:{typedData.Domain.ChainId}:{typedData.Domain.VerifyingContract}"));
        var domainSeparator = "0x" + Convert.ToHexString(domainSeparatorBytes).ToLowerInvariant();

        return Task.FromResult(new TestSignResponse
        {
            DomainSeparator = domainSeparator,
            TypeHash = typeHash,
            MessageHash = messageHash,
            Signature = new SignatureDto
            {
                V = v,
                R = r,
                S = s,
            },
        });
    }

    private static PlaceOrderResponse MapExchangeResponse(HyperliquidExchangeResponse exchangeResponse)
    {
        if (exchangeResponse.Status == "err")
        {
            return new PlaceOrderResponse
            {
                Success = false,
                Status = "rejected",
                Detail = exchangeResponse.Response?.ErrorMessage ?? "Unknown exchange error",
            };
        }

        if (exchangeResponse.Status == "ok" &&
            exchangeResponse.Response?.Data?.Statuses is { Count: > 0 } statuses)
        {
            var first = statuses[0];

            if (first.Resting is not null)
            {
                return new PlaceOrderResponse
                {
                    Success = true,
                    OrderId = first.Resting.Oid.ToString(),
                    Status = "open",
                };
            }

            if (first.Filled is not null)
            {
                return new PlaceOrderResponse
                {
                    Success = true,
                    OrderId = first.Filled.Oid.ToString(),
                    Status = "filled",
                };
            }

            if (first.Error is not null)
            {
                return new PlaceOrderResponse
                {
                    Success = false,
                    Status = "rejected",
                    Detail = first.Error,
                };
            }
        }

        return new PlaceOrderResponse
        {
            Success = false,
            Status = "rejected",
            Detail = $"Unexpected response: {exchangeResponse.Status}",
        };
    }
}
