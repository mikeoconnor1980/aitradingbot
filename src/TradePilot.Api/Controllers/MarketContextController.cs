using MediatR;
using Microsoft.AspNetCore.Mvc;
using TradePilot.Api.Infrastructure;
using TradePilot.Application.LlmContextSnapshots.Models;
using TradePilot.Application.LlmContextSnapshots.Queries;

namespace TradePilot.Api.Controllers;

[Route("api/market-context")]
public sealed class MarketContextController : ApiController
{
    public MarketContextController(IMediator mediator, IdentityService identityService)
        : base(mediator, identityService)
    {
    }

    [HttpGet("current")]
    [ProducesResponseType(typeof(LlmContextDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrent(
        [FromQuery] string symbol,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetCurrentContextQuery(symbol), cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpGet("history")]
    [ProducesResponseType(typeof(IReadOnlyList<LlmContextDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(
        [FromQuery] string symbol,
        [FromQuery] long fromUtc,
        [FromQuery] long toUtc,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new GetContextHistoryQuery(symbol, fromUtc, toUtc),
            cancellationToken);

        return Ok(result);
    }
}
