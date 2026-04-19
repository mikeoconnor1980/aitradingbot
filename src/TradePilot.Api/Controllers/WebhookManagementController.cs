using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradePilot.Api.Models;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Subscriptions.Services;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Subscriptions;

namespace TradePilot.Api.Controllers;

[ApiController]
[Route("api/webhooks")]
[Produces("application/json")]
[Authorize]
public sealed class WebhookManagementController : ControllerBase
{
    private readonly IWebhookConfigRepository _webhookRepository;
    private readonly ISubscriptionFeatureService _subscriptionFeatureService;

    public WebhookManagementController(
        IWebhookConfigRepository webhookRepository,
        ISubscriptionFeatureService subscriptionFeatureService)
    {
        _webhookRepository = webhookRepository;
        _subscriptionFeatureService = subscriptionFeatureService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<WebhookConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        if (!await _subscriptionFeatureService.CanAccessFeatureAsync(userId.Value, Feature.Webhooks, cancellationToken))
        {
            return Forbid();
        }

        var webhooks = await _webhookRepository.GetByUserIdAsync(userId.Value, cancellationToken);
        return Ok(webhooks.Select(Map).ToList());
    }

    [HttpPost]
    [ProducesResponseType(typeof(WebhookConfigDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateWebhookRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        if (!await _subscriptionFeatureService.CanAccessFeatureAsync(userId.Value, Feature.Webhooks, cancellationToken))
        {
            return Forbid();
        }

        var webhook = WebhookConfig.Create(userId.Value, request.Label, request.DefaultAsset, request.TargetAgentId);
        await _webhookRepository.AddAsync(webhook, cancellationToken);
        await _webhookRepository.SaveChangesAsync(cancellationToken);

        return Created($"api/webhooks/{webhook.Id}", Map(webhook));
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(WebhookConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWebhookRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        if (!await _subscriptionFeatureService.CanAccessFeatureAsync(userId.Value, Feature.Webhooks, cancellationToken))
        {
            return Forbid();
        }

        var webhook = await GetOwnedWebhookAsync(id, cancellationToken);
        if (webhook is null)
        {
            return NotFound();
        }

        webhook.Update(request.Label, request.DefaultAsset, request.TargetAgentId, request.IsEnabled);
        await _webhookRepository.SaveChangesAsync(cancellationToken);

        return Ok(Map(webhook));
    }

    [HttpPost("{id:guid}/regenerate")]
    [ProducesResponseType(typeof(WebhookConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Regenerate(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        if (!await _subscriptionFeatureService.CanAccessFeatureAsync(userId.Value, Feature.Webhooks, cancellationToken))
        {
            return Forbid();
        }

        var webhook = await GetOwnedWebhookAsync(id, cancellationToken);
        if (webhook is null)
        {
            return NotFound();
        }

        webhook.RegenerateToken();
        await _webhookRepository.SaveChangesAsync(cancellationToken);

        return Ok(Map(webhook));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        if (!await _subscriptionFeatureService.CanAccessFeatureAsync(userId.Value, Feature.Webhooks, cancellationToken))
        {
            return Forbid();
        }

        var webhook = await GetOwnedWebhookAsync(id, cancellationToken);
        if (webhook is null)
        {
            return NotFound();
        }

        _webhookRepository.Remove(webhook);
        await _webhookRepository.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task<WebhookConfig?> GetOwnedWebhookAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            return null;
        }

        var webhook = await _webhookRepository.GetByIdAsync(id, cancellationToken);
        return webhook is not null && webhook.UserId == userId.Value ? webhook : null;
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim is not null && Guid.TryParse(claim, out var userId) ? userId : null;
    }

    private static WebhookConfigDto Map(WebhookConfig webhook)
    {
        return new WebhookConfigDto
        {
            Id = webhook.Id.ToString(),
            Label = webhook.Label,
            Token = webhook.Token,
            DefaultAsset = webhook.DefaultAsset,
            TargetAgentId = webhook.TargetAgentId,
            IsEnabled = webhook.IsEnabled,
            CreatedAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(webhook.CreatedAtUtc).UtcDateTime.ToString("O"),
            UpdatedAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(webhook.UpdatedAtUtc).UtcDateTime.ToString("O"),
            LastTriggeredAtUtc = webhook.LastTriggeredAtUtc.HasValue
                ? DateTimeOffset.FromUnixTimeMilliseconds(webhook.LastTriggeredAtUtc.Value).UtcDateTime.ToString("O")
                : null,
        };
    }
}