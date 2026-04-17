using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradePilot.Api.Infrastructure;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;

namespace TradePilot.Api.Controllers;

[ApiController]
[Route("api/account")]
[Produces("application/json")]
[Authorize]
public sealed class AccountController : ControllerBase
{
    private readonly IHyperliquidAccountService _accountService;
    private readonly IUserWalletAddressRepository _walletRepo;

    public AccountController(IHyperliquidAccountService accountService, IUserWalletAddressRepository walletRepo)
    {
        _accountService = accountService;
        _walletRepo = walletRepo;
    }

    [HttpGet]
    [ProducesResponseType(typeof(AccountSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetAccountSummaryAsync(CancellationToken cancellationToken)
    {
        var address = await GetWalletAddressAsync(cancellationToken);
        if (address is null)
            return Ok(new AccountSummaryDto());

        var summary = await _accountService.GetAccountSummaryAsync(address, cancellationToken);
        return Ok(summary);
    }

    [HttpGet("positions")]
    [ProducesResponseType(typeof(IReadOnlyList<PositionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetPositionsAsync(CancellationToken cancellationToken)
    {
        var address = await GetWalletAddressAsync(cancellationToken);
        if (address is null)
            return Ok(Array.Empty<PositionDto>());

        var positions = await _accountService.GetPositionsAsync(address, cancellationToken);
        return Ok(positions);
    }

    [HttpGet("orders")]
    [ProducesResponseType(typeof(IReadOnlyList<OpenOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetOpenOrdersAsync(CancellationToken cancellationToken)
    {
        var address = await GetWalletAddressAsync(cancellationToken);
        if (address is null)
            return Ok(Array.Empty<OpenOrderDto>());

        var orders = await _accountService.GetOpenOrdersAsync(address, cancellationToken);
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
        var address = await GetWalletAddressAsync(cancellationToken);
        if (address is null)
            return Ok(Array.Empty<FillEventDto>());

        var fills = await _accountService.GetRecentFillsAsync(asset, address, cancellationToken);
        return Ok(fills);
    }

    private async Task<string?> GetWalletAddressAsync(CancellationToken cancellationToken)
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (claim is null || !Guid.TryParse(claim, out var userId))
            return null;

        var wallet = await _walletRepo.GetActiveByUserIdAsync(userId, cancellationToken);
        return wallet?.WalletAddress;
    }
}