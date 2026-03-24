using MediatR;
using Microsoft.AspNetCore.Mvc;
using TradingApp.Api.Infrastructure;
using TradingApp.Application.Health.Models;
using TradingApp.Application.Health.Queries;

namespace TradingApp.Api.Controllers;

[Route("api/health")]
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
