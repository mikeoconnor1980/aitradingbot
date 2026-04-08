using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradingApp.Api.Infrastructure;
using TradingApp.Api.Models;
using TradingApp.Api.Services;
using TradingApp.Application.MarketData.Models;

namespace TradingApp.Api.Controllers;

[ApiController]
[Route("api/account")]
[Produces("application/json")]
[Authorize]
public sealed class AccountController : ControllerBase
{
    private readonly IHyperliquidAccountService _accountService;

    public AccountController(IHyperliquidAccountService accountService)
    {
        _accountService = accountService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(AccountSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetAccountSummaryAsync(CancellationToken cancellationToken)
    {
        var summary = await _accountService.GetAccountSummaryAsync(cancellationToken);
        return Ok(summary);
    }

    [HttpGet("positions")]
    [ProducesResponseType(typeof(IReadOnlyList<PositionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetPositionsAsync(CancellationToken cancellationToken)
    {
        var positions = await _accountService.GetPositionsAsync(cancellationToken);
        return Ok(positions);
    }

    [HttpGet("orders")]
    [ProducesResponseType(typeof(IReadOnlyList<OpenOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetOpenOrdersAsync(CancellationToken cancellationToken)
    {
        var orders = await _accountService.GetOpenOrdersAsync(cancellationToken);
        return Ok(orders);
    }

    [HttpGet("fills")]
    [ProducesResponseType(typeof(IReadOnlyList<FillEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetRecentFillsAsync(
        [FromQuery] string? asset,
        CancellationToken cancellationToken)
    {
        var fills = await _accountService.GetRecentFillsAsync(asset, cancellationToken);
        return Ok(fills);
    }
}