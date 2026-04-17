using MediatR;
using Microsoft.AspNetCore.Mvc;
using TradePilot.Api.Infrastructure;
using TradePilot.Api.Models;
using TradePilot.Infrastructure.Hyperliquid;

namespace TradePilot.Api.Controllers;

[Route("api/reference-data")]
public sealed class ReferenceDataController : ApiController
{
    public ReferenceDataController(IMediator mediator, IdentityService identityService)
        : base(mediator, identityService)
    {
    }

    [HttpGet("markets")]
    [ProducesResponseType(typeof(ReferenceDataResponse), StatusCodes.Status200OK)]
    public IActionResult GetMarkets()
    {
        var markets = HyperliquidAssetMapper.GetSupportedCoins()
            .Select(coin => $"{coin}-USD")
            .ToList();

        return Ok(new ReferenceDataResponse
        {
            Markets = markets,
            Timeframes = HyperliquidAssetMapper.GetSupportedTimeframes().ToList(),
        });
    }
}