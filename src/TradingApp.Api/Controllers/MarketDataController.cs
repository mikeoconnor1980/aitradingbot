using System.ComponentModel.DataAnnotations;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TradingApp.Api.Infrastructure;
using TradingApp.Application.MarketData.Models;
using TradingApp.Application.MarketData.Queries;

namespace TradingApp.Api.Controllers;

[Route("api/market")]
public sealed class MarketDataController : ApiController
{
    public MarketDataController(IMediator mediator, IdentityService identityService)
        : base(mediator, identityService)
    {
    }

    [HttpGet("info")]
    [ProducesResponseType(typeof(MarketInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetMarketInfoAsync([FromQuery][Required] string asset, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetMarketInfoQuery(asset), cancellationToken);
        return Ok(result);
    }

    [HttpGet("candles")]
    [ProducesResponseType(typeof(List<CandleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetCandlesAsync(
        [FromQuery][Required] string asset,
        [FromQuery][Required] string timeframe,
        [FromQuery] long? endTime,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetCandlesQuery(asset, timeframe, endTime), cancellationToken);
        return Ok(result);
    }
}