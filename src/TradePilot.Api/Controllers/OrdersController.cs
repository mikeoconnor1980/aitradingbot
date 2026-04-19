using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Api.Infrastructure;
using TradePilot.Api.Models;
using TradePilot.Api.Services;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Subscriptions.Services;
using System.Linq;

namespace TradePilot.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Produces("application/json")]
[Authorize]
public sealed class OrdersController : ControllerBase
{
    private readonly IHyperliquidOrderService _orderService;
    private readonly IHyperliquidAccountService _accountService;
    private readonly IHyperliquidRestClient _restClient;
    private readonly IHyperliquidAssetMetadataCache _metadataCache;
    private readonly IUserWalletAddressRepository _walletRepo;
    private readonly ISignerProvider _signerProvider;
    private readonly ISubscriptionFeatureService _subscriptionFeatureService;

    public OrdersController(
        IHyperliquidOrderService orderService,
        IHyperliquidAccountService accountService,
        IHyperliquidRestClient restClient,
        IHyperliquidAssetMetadataCache metadataCache,
        IUserWalletAddressRepository walletRepo,
        ISignerProvider signerProvider,
        ISubscriptionFeatureService subscriptionFeatureService)
    {
        _orderService = orderService;
        _accountService = accountService;
        _restClient = restClient;
        _metadataCache = metadataCache;
        _walletRepo = walletRepo;
        _signerProvider = signerProvider;
        _subscriptionFeatureService = subscriptionFeatureService;
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (claim is null || !Guid.TryParse(claim, out var userId))
        {
            throw new DomainException("Unable to resolve the current user.");
        }

        return userId;
    }

    private async Task EnsureAssetAllowedAsync(string asset, CancellationToken ct)
    {
        var policy = await _subscriptionFeatureService.GetPolicyAsync(GetUserId(), ct)
            ?? throw new DomainException("An active subscription is required to trade.");

        if (!_subscriptionFeatureService.IsAssetAllowed(policy.AllowedAssets, asset))
        {
            throw new DomainException($"Your current tier only supports {string.Join(", ", policy.AllowedAssets)} assets.");
        }
    }

    private async Task EnsureLeverageAllowedAsync(string asset, int leverage, CancellationToken ct)
    {
        var policy = await _subscriptionFeatureService.GetPolicyAsync(GetUserId(), ct)
            ?? throw new DomainException("An active subscription is required to trade.");

        if (!_subscriptionFeatureService.IsAssetAllowed(policy.AllowedAssets, asset))
        {
            throw new DomainException($"Your current tier only supports {string.Join(", ", policy.AllowedAssets)} assets.");
        }

        if (policy.MaxLeverage.HasValue && leverage > policy.MaxLeverage.Value)
        {
            throw new DomainException($"Your current tier supports a maximum of {policy.MaxLeverage.Value}x leverage.");
        }
    }

    private async Task<string?> GetWalletAddressAsync(CancellationToken ct)
    {
        // 1. Try user's wallet from JWT claims + DB
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (claim is not null && Guid.TryParse(claim, out var userId))
        {
            var wallet = await _walletRepo.GetActiveByUserIdAsync(userId, ct);
            if (wallet?.WalletAddress is not null)
                return wallet.WalletAddress;
        }

        // 2. Fall back to configured signer (local dev with private key)
        if (_signerProvider.IsConfigured)
            return _signerProvider.WalletAddress;

        return null;
    }

    // Well-known token full names for display
    private static readonly Dictionary<string, string> CoinNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BTC"] = "Bitcoin",
        ["ETH"] = "Ethereum",
        ["SOL"] = "Solana",
        ["DOGE"] = "Dogecoin",
        ["AVAX"] = "Avalanche",
        ["ARB"] = "Arbitrum",
        ["LINK"] = "Chainlink",
        ["OP"] = "Optimism",
        ["SUI"] = "Sui",
        ["APT"] = "Aptos",
        ["ATOM"] = "Cosmos",
        ["MATIC"] = "Polygon",
        ["DOT"] = "Polkadot",
        ["ADA"] = "Cardano",
        ["XRP"] = "Ripple",
        ["LTC"] = "Litecoin",
        ["UNI"] = "Uniswap",
        ["AAVE"] = "Aave",
        ["MKR"] = "Maker",
        ["CRV"] = "Curve",
        ["FTM"] = "Fantom",
        ["NEAR"] = "NEAR Protocol",
        ["INJ"] = "Injective",
        ["TIA"] = "Celestia",
        ["SEI"] = "Sei",
        ["JUP"] = "Jupiter",
        ["WIF"] = "dogwifhat",
        ["PEPE"] = "Pepe",
        ["SHIB"] = "Shiba Inu",
        ["BONK"] = "Bonk",
        ["RENDER"] = "Render",
        ["FIL"] = "Filecoin",
        ["STX"] = "Stacks",
        ["IMX"] = "Immutable X",
        ["PENDLE"] = "Pendle",
        ["WLD"] = "Worldcoin",
        ["JTO"] = "Jito",
        ["PYTH"] = "Pyth Network",
        ["W"] = "Wormhole",
        ["ENA"] = "Ethena",
        ["ONDO"] = "Ondo Finance",
        ["HYPE"] = "Hyperliquid",
        ["PURR"] = "Purr",
    };

    // Priority order: top coins first, then rest alphabetically
    private static readonly List<string> PriorityCoins =
    [
        "BTC", "ETH", "SOL", "HYPE", "SUI", "XRP", "DOGE", "LINK",
        "AVAX", "ADA", "DOT", "ATOM", "APT", "ARB", "OP", "NEAR",
        "INJ", "TIA", "SEI", "AAVE", "UNI", "PEPE", "WIF", "BONK",
        "RENDER", "JUP", "PENDLE", "ONDO", "ENA", "PYTH",
    ];

    [HttpGet("assets")]
    [ProducesResponseType(typeof(IReadOnlyList<TradableAssetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailableAssetsAsync(CancellationToken ct)
    {
        var all = await _metadataCache.GetAllAsync(ct);
        var allowedAssets = await _subscriptionFeatureService.GetAllowedAssetsAsync(GetUserId(), ct);

        var priorityIndex = PriorityCoins
            .Select((coin, idx) => (coin, idx))
            .ToDictionary(x => x.coin, x => x.idx, StringComparer.OrdinalIgnoreCase);

        var sorted = all
            .Where(kvp => allowedAssets.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase))
            .OrderBy(kvp => priorityIndex.TryGetValue(kvp.Key, out var idx) ? idx : int.MaxValue)
            .ThenBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kvp => new TradableAssetDto
            {
                Symbol = $"{kvp.Key}-PERP",
                Name = CoinNames.TryGetValue(kvp.Key, out var name) ? name : kvp.Key,
                MaxLeverage = kvp.Value.MaxLeverage,
                SzDecimals = kvp.Value.SzDecimals,
            })
            .ToList();

        return Ok(sorted);
    }

    [HttpPost]
    [ProducesResponseType(typeof(PlaceOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> PlaceOrderAsync([FromBody] PlaceOrderRequest request, CancellationToken ct)
    {
        try
        {
            await EnsureAssetAllowedAsync(request.Asset, ct);
            var result = await _orderService.PlaceOrderAsync(request, ct);

            if (!result.Success)
            {
                return BadRequest(new Envelope(result.Detail ?? "Order rejected"));
            }

            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No private key configured"))
        {
            return BadRequest(new Envelope("All orders must be routed through an Execution Agent. Please start a Worker process and ensure it is selected in the UI."));
        }
    }

    [HttpPost("trigger")]
    [ProducesResponseType(typeof(PlaceOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> PlaceTriggerOrderAsync([FromBody] PlaceTriggerOrderRequest request, CancellationToken ct)
    {
        try
        {
            await EnsureAssetAllowedAsync(request.Asset, ct);
            var result = await _orderService.PlaceTriggerOrderAsync(request, ct);

            if (!result.Success)
            {
                return BadRequest(new Envelope(result.Detail ?? "Trigger order rejected"));
            }

            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No private key configured"))
        {
            return BadRequest(new Envelope("All orders must be routed through an Execution Agent. Please start a Worker process and ensure it is selected in the UI."));
        }
    }

    [HttpPost("test-sign")]
    [ProducesResponseType(typeof(TestSignResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> TestSignAsync(CancellationToken ct)
    {
        var result = await _orderService.TestSignAsync(ct);
        return Ok(result);
    }

    [HttpDelete("{orderId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CancelOrderAsync(string orderId, CancellationToken ct)
    {
        try
        {
            var walletAddress = await GetWalletAddressAsync(ct);
            var openOrders = await _accountService.GetOpenOrdersAsync(walletAddress, ct);
            var existingOrder = openOrders.FirstOrDefault(o => o.OrderId == orderId)
                ?? throw new NotFoundException($"Order {orderId} not found in open orders");

            await _orderService.CancelOrderAsync(orderId, existingOrder.Asset, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No private key configured"))
        {
            return BadRequest(new Envelope("All orders must be routed through an Execution Agent. Please start a Worker process and ensure it is selected in the UI."));
        }
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CancelAllOrdersAsync([FromQuery] string asset, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(asset))
        {
            throw new DomainException("Query parameter 'asset' is required.");
        }

        await _orderService.CancelAllOrdersAsync(asset, ct);
        return NoContent();
    }

    [HttpPut("{orderId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ModifyOrderAsync(string orderId, [FromBody] ModifyOrderDto dto, CancellationToken ct)
    {
        var walletAddress = await GetWalletAddressAsync(ct);
        var openOrders = await _accountService.GetOpenOrdersAsync(walletAddress, ct);
        var existingOrder = openOrders.FirstOrDefault(order => order.OrderId == orderId)
            ?? throw new NotFoundException($"Order {orderId} not found in open orders");

        await _orderService.ModifyOrderAsync(orderId, existingOrder.Asset, existingOrder.Side, dto.Price, dto.Size, ct);
        return NoContent();
    }

    [HttpPut("trigger/{orderId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ModifyTriggerOrderAsync(string orderId, [FromBody] ModifyTriggerOrderDto dto, CancellationToken ct)
    {
        var walletAddress = await GetWalletAddressAsync(ct);
        var openOrders = await _accountService.GetOpenOrdersAsync(walletAddress, ct);
        var existingOrder = openOrders.FirstOrDefault(order =>
                order.OrderId == orderId &&
                string.Equals(order.OrderType, "trigger", StringComparison.OrdinalIgnoreCase))
            ?? throw new NotFoundException($"Trigger order {orderId} not found in open orders");

        await _orderService.ModifyTriggerOrderAsync(
            orderId,
            existingOrder.Asset,
            existingOrder.Side,
            dto.TriggerPrice,
            dto.Size,
            existingOrder.TpslType ?? dto.TpslType ?? throw new DomainException($"Trigger order {orderId} is missing TP/SL type."),
            ct);

        return NoContent();
    }

    [HttpDelete("trigger/{orderId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CancelTriggerOrderAsync(string orderId, CancellationToken ct)
    {
        try
        {
            var walletAddress = await GetWalletAddressAsync(ct);
            var openOrders = await _accountService.GetOpenOrdersAsync(walletAddress, ct);
            var existingOrder = openOrders.FirstOrDefault(order =>
                    order.OrderId == orderId &&
                    string.Equals(order.OrderType, "trigger", StringComparison.OrdinalIgnoreCase))
                ?? throw new NotFoundException($"Trigger order {orderId} not found in open orders");

            await _orderService.CancelOrderAsync(orderId, existingOrder.Asset, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No private key configured"))
        {
            return BadRequest(new Envelope("All orders must be routed through an Execution Agent. Please start a Worker process and ensure it is selected in the UI."));
        }
    }

    [HttpPut("leverage")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SetLeverageAsync([FromBody] SetLeverageRequest request, CancellationToken ct)
    {
        try
        {
            await EnsureLeverageAllowedAsync(request.Asset, request.Leverage, ct);
            await _orderService.UpdateLeverageAsync(request.Asset, request.Leverage, request.IsCross, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No private key configured"))
        {
            return BadRequest(new Envelope("All orders must be routed through an Execution Agent. Please start a Worker process and ensure it is selected in the UI."));
        }
    }

#if DEBUG
    /// <summary>Debug endpoint: returns allMids response from Hyperliquid testnet.</summary>
    [HttpGet("debug/mids")]
    public async Task<IActionResult> DebugMidsAsync(CancellationToken ct)
    {
        var request = new { type = "allMids" };
        var response = await _restClient.PostInfoAsync<JsonElement>(request, ct);
        return Ok(response);
    }

    /// <summary>Debug endpoint: returns universe (asset index → coin name mapping).</summary>
    [HttpGet("debug/meta")]
    public async Task<IActionResult> DebugMetaAsync(CancellationToken ct)
    {
        var request = new { type = "meta" };
        var response = await _restClient.PostInfoAsync<JsonElement>(request, ct);

        // Extract just the asset index → name mapping for readability
        if (response.TryGetProperty("universe", out var universe))
        {
            var mapping = new Dictionary<int, string>();
            int idx = 0;
            foreach (var item in universe.EnumerateArray())
            {
                if (item.TryGetProperty("name", out var name))
                {
                    mapping[idx] = name.GetString() ?? "?";
                }
                idx++;
            }
            return Ok(new { assetIndexMapping = mapping, fullMeta = response });
        }

        return Ok(response);
    }

    /// <summary>Debug endpoint: returns raw clearinghouseState for inspecting position/leverage structure.</summary>
    [HttpGet("debug/clearinghouse")]
    public async Task<IActionResult> DebugClearinghouseAsync(CancellationToken ct)
    {
        var signer = HttpContext.RequestServices.GetRequiredService<IHyperliquidSigner>();
        var request = new { type = "clearinghouseState", user = signer.WalletAddress };
        var response = await _restClient.PostInfoAsync<JsonElement>(request, ct);
        return Ok(response);
    }
#endif
}