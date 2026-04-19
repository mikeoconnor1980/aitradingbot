using MediatR;
using Microsoft.AspNetCore.Mvc;
using TradePilot.Api.Infrastructure;
using TradePilot.Api.Models;
using TradePilot.Application.Subscriptions.Services;
using TradePilot.Infrastructure.Hyperliquid;

namespace TradePilot.Api.Controllers;

[Route("api/reference-data")]
public sealed class ReferenceDataController : ApiController
{
    private readonly ISubscriptionFeatureService _subscriptionFeatureService;

    public ReferenceDataController(
        IMediator mediator,
        IdentityService identityService,
        ISubscriptionFeatureService subscriptionFeatureService)
        : base(mediator, identityService)
    {
        _subscriptionFeatureService = subscriptionFeatureService;
    }

    [HttpGet("markets")]
    [ProducesResponseType(typeof(ReferenceDataResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMarkets(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(IdentityService.Identity.UserId);
        var allowedAssets = await _subscriptionFeatureService.GetAllowedAssetsAsync(userId, cancellationToken);
        var coins = allowedAssets.Count > 0
            ? allowedAssets
            : HyperliquidAssetMapper.GetSupportedCoins().ToList();

        var markets = coins
            .Select(coin => $"{coin}-USD")
            .ToList();

        return Ok(new ReferenceDataResponse
        {
            Markets = markets,
            Timeframes = HyperliquidAssetMapper.GetSupportedTimeframes().ToList(),
        });
    }
}