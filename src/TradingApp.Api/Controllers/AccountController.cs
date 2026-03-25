using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using TradingApp.Api.Models;
using TradingApp.Api.Services;

namespace TradingApp.Api.Controllers;

[ApiController]
[Route("api/account")]
[Produces("application/json")]
public sealed class AccountController : ControllerBase
{
    private readonly IHyperliquidAccountService _accountService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        IHyperliquidAccountService accountService,
        ILogger<AccountController> logger)
    {
        _accountService = accountService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(AccountSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetAccountSummaryAsync(CancellationToken cancellationToken)
    {
        try
        {
            var summary = await _accountService.GetAccountSummaryAsync(cancellationToken);
            return Ok(summary);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch account summary from Hyperliquid");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Hyperliquid API is unavailable" });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid response from Hyperliquid API");
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "Unexpected response from Hyperliquid API" });
        }
    }

    [HttpGet("positions")]
    [ProducesResponseType(typeof(IReadOnlyList<PositionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetPositionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var positions = await _accountService.GetPositionsAsync(cancellationToken);
            return Ok(positions);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch positions from Hyperliquid");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Hyperliquid API is unavailable" });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid response from Hyperliquid API");
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "Unexpected response from Hyperliquid API" });
        }
    }

    [HttpGet("orders")]
    [ProducesResponseType(typeof(IReadOnlyList<OpenOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetOpenOrdersAsync(CancellationToken cancellationToken)
    {
        try
        {
            var orders = await _accountService.GetOpenOrdersAsync(cancellationToken);
            return Ok(orders);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch open orders from Hyperliquid");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Hyperliquid API is unavailable" });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid response from Hyperliquid API");
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "Unexpected response from Hyperliquid API" });
        }
    }
}