using MediatR;
using Microsoft.AspNetCore.Mvc;
using TradingApp.Api.Infrastructure;
using TradingApp.Application.MacroCalendar.Models;
using TradingApp.Application.MacroCalendar.Services;
using TradingApp.Domain.Enums;

namespace TradingApp.Api.Controllers;

[Route("api/macro-calendar")]
public sealed class MacroCalendarController : ApiController
{
    private readonly IMacroCalendarQueryService _queryService;
    private readonly IMacroCalendarIngestionService _ingestionService;

    public MacroCalendarController(
        IMediator mediator,
        IdentityService identityService,
        IMacroCalendarQueryService queryService,
        IMacroCalendarIngestionService ingestionService)
        : base(mediator, identityService)
    {
        _queryService = queryService;
        _ingestionService = ingestionService;
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
        var result = await _queryService.GetUpcomingEventsAsync(
            fromUtc, toUtc, currency, minimumImportance, cancellationToken);

        return Ok(result);
    }

    [HttpGet("active-blocks")]
    [ProducesResponseType(typeof(IReadOnlyCollection<MacroEventListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveBlocks(CancellationToken cancellationToken)
    {
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var result = await _queryService.GetActiveBlockWindowsAsync(nowMs, cancellationToken);
        return Ok(result);
    }

    [HttpPost("sync")]
    [ProducesResponseType(typeof(MacroSyncResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Sync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var result = await _ingestionService.SyncAsync(
            now.AddDays(-1), now.AddDays(7), cancellationToken);

        return Ok(result);
    }
}
