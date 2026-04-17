using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradePilot.Api.Infrastructure;
using TradePilot.Application.Health.Models;
using TradePilot.Application.Health.Queries;

namespace TradePilot.Api.Controllers;

[Route("api/health")]
[AllowAnonymous]
public sealed class HealthController : ApiController
{
    public HealthController(IMediator mediator, IdentityService identityService)
        : base(mediator, identityService)
    {
    }

    [HttpGet]
    [ProducesResponseType(typeof(HealthDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHealthAsync(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetHealthQuery(), cancellationToken);
        return Ok(result);
    }
}
