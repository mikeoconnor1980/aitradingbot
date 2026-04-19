using MediatR;
using Microsoft.AspNetCore.Mvc;
using TradePilot.Api.Infrastructure;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Subscriptions.Commands;
using TradePilot.Application.Subscriptions.Queries;
using TradePilot.Domain.Enums;

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
        return await Subscribe(new SubscribeRequest("beginner"), cancellationToken);
    }

    [HttpPost("subscribe")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest request, CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(IdentityService.Identity.UserId);
        if (!Enum.TryParse<SubscriptionTier>(request.Tier, true, out var tier)
            || tier is not (SubscriptionTier.Beginner or SubscriptionTier.Pro or SubscriptionTier.Free))
        {
            return BadRequest(new Envelope("Tier must be Beginner or Pro.", "invalid_tier"));
        }

        try
        {
            var subscriptionId = await Mediator.Send(new SubscribeCommand(userId, tier), cancellationToken);
            return StatusCode(StatusCodes.Status201Created, new { id = subscriptionId });
        }
        catch (DomainException ex)
        {
            return BadRequest(new Envelope(ex.Message, "already_subscribed"));
        }
    }

    [HttpPost("cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Cancel(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(IdentityService.Identity.UserId);

        try
        {
            await Mediator.Send(new CancelSubscriptionCommand(userId), cancellationToken);
            return NoContent();
        }
        catch (DomainException ex)
        {
            return BadRequest(new Envelope(ex.Message, "subscription_not_found"));
        }
    }
}

public sealed record SubscribeRequest(string Tier);
