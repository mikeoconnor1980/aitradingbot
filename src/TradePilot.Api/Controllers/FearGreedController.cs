using MediatR;
using Microsoft.AspNetCore.Mvc;
using TradePilot.Api.Infrastructure;
using TradePilot.Application.FearGreed.Commands;
using TradePilot.Application.FearGreed.Models;
using TradePilot.Application.FearGreed.Queries;

namespace TradePilot.Api.Controllers;

[Route("api/fear-greed")]
public sealed class FearGreedController : ApiController
{
    public FearGreedController(IMediator mediator, IdentityService identityService)
        : base(mediator, identityService)
    {
    }

    [HttpGet("status")]
    [ProducesResponseType(typeof(FearGreedStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatusAsync(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetFearGreedStatusQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("history")]
    [ProducesResponseType(typeof(IReadOnlyList<FearGreedReadingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistoryAsync(
        [FromQuery] long? from,
        [FromQuery] long? to,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new GetFearGreedHistoryQuery(from, to),
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("backfill")]
    [ProducesResponseType(typeof(FearGreedBackfillResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> BackfillAsync(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new BackfillFearGreedCommand(), cancellationToken);
        return Ok(result);
    }
}
