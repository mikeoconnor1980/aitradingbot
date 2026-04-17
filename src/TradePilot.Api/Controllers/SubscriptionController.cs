using MediatR;
using Microsoft.AspNetCore.Mvc;
using TradePilot.Api.Infrastructure;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Subscriptions.Commands;
using TradePilot.Application.Subscriptions.Queries;

namespace TradePilot.Api.Controllers;

[Route("api/subscriptions")]
public sealed class SubscriptionController : ApiController
{
    public SubscriptionController(IMediator mediator, IdentityService identityService)
        : base(mediator, identityService)
    {
    }

    [HttpGet("status")]
    [ProducesResponseType(typeof(SubscriptionStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(IdentityService.Identity.UserId);
        var result = await Mediator.Send(new GetSubscriptionStatusQuery(userId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("free")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SubscribeToFreeTier(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(IdentityService.Identity.UserId);

        try
        {
            var subscriptionId = await Mediator.Send(new SubscribeToFreeTierCommand(userId), cancellationToken);
            return StatusCode(StatusCodes.Status201Created, new { id = subscriptionId });
        }
        catch (DomainException ex)
        {
            return BadRequest(new Envelope(ex.Message, "already_subscribed"));
        }
    }
}
