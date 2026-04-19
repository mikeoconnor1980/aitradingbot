using MediatR;
using Microsoft.AspNetCore.Mvc;
using TradePilot.Api.Infrastructure;
using TradePilot.Application.MacroCalendar.Models;
using TradePilot.Application.MacroCalendar.Services;
using TradePilot.Application.Subscriptions.Services;
using TradePilot.Domain.Enums;
using TradePilot.Domain.Subscriptions;

namespace TradePilot.Api.Controllers;

[Route("api/macro-calendar")]
public sealed class MacroCalendarController : ApiController
{
    private readonly IMacroCalendarQueryService _queryService;
    private readonly IMacroCalendarIngestionService _ingestionService;
    private readonly ISubscriptionFeatureService _subscriptionFeatureService;

    public MacroCalendarController(
        IMediator mediator,
        IdentityService identityService,
        IMacroCalendarQueryService queryService,
        IMacroCalendarIngestionService ingestionService,
        ISubscriptionFeatureService subscriptionFeatureService)
        : base(mediator, identityService)
    {
        _queryService = queryService;
        _ingestionService = ingestionService;
        _subscriptionFeatureService = subscriptionFeatureService;
    }

    [HttpGet("events")]
    [ProducesResponseType(typeof(IReadOnlyCollection<MacroEventListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvents(
        [FromQuery] long fromUtc,
        [FromQuery] long toUtc,
        [FromQuery] string? currency,
        [FromQuery] MacroEventImportance? minimumImportance,
        CancellationToken cancellationToken)
    {
        await EnsureFeatureAsync(Feature.MacroCalendar, cancellationToken);

        var result = await _queryService.GetUpcomingEventsAsync(
            fromUtc, toUtc, currency, minimumImportance, cancellationToken);

        return Ok(result);
    }

    [HttpGet("active-blocks")]
    [ProducesResponseType(typeof(IReadOnlyCollection<MacroEventListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveBlocks(CancellationToken cancellationToken)
    {
        await EnsureFeatureAsync(Feature.MacroCalendar, cancellationToken);

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var result = await _queryService.GetActiveBlockWindowsAsync(nowMs, cancellationToken);
        return Ok(result);
    }

    [HttpPost("sync")]
    [ProducesResponseType(typeof(MacroSyncResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Sync(CancellationToken cancellationToken)
    {
        await EnsureFeatureAsync(Feature.MacroCalendar, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var result = await _ingestionService.SyncAsync(
            now.AddDays(-1), now.AddDays(7), cancellationToken);

        return Ok(result);
    }

    private async Task EnsureFeatureAsync(Feature feature, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(IdentityService.Identity.UserId, out var userId)
            && !await _subscriptionFeatureService.CanAccessFeatureAsync(userId, feature, cancellationToken))
        {
            throw new UnauthorizedAccessException("This feature requires a Pro subscription.");
        }
    }
}
