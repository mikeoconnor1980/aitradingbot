using MediatR;
using Microsoft.AspNetCore.Mvc;
using TradingApp.Api.Infrastructure;
using TradingApp.Api.Models;
using TradingApp.Infrastructure.Hyperliquid;

namespace TradingApp.Api.Controllers;

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