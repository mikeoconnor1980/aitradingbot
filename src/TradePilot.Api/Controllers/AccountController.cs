using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradePilot.Api.Infrastructure;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;
using TradePilot.Application.MarketData.Queries;
using TradePilot.Domain.Enums;

namespace TradePilot.Api.Controllers;

[ApiController]
[Route("api/account")]
[Produces("application/json")]
[Authorize]
public sealed class AccountController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IExchangeResolver _exchangeResolver;
    private readonly IUserWalletAddressRepository _walletRepo;
    private readonly IUserExchangeCredentialRepository _credentialRepository;

    public AccountController(
        IMediator mediator,
        IExchangeResolver exchangeResolver,
        IUserWalletAddressRepository walletRepo,
        IUserExchangeCredentialRepository credentialRepository)
    {
        _mediator = mediator;
        _exchangeResolver = exchangeResolver;
        _walletRepo = walletRepo;
        _credentialRepository = credentialRepository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(AccountSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetAccountSummaryAsync(CancellationToken cancellationToken)
    {
        var exchange = await _exchangeResolver.GetCurrentExchangeAsync(cancellationToken);
        if (!await HasConfiguredAccessAsync(exchange, cancellationToken))
            return Ok(new AccountSummaryDto());

        var summary = await _mediator.Send(
            new GetAccountSummaryQuery(exchange, await GetWalletAddressAsync(exchange, cancellationToken)),
            cancellationToken);
        return Ok(summary);
    }

    [HttpGet("positions")]
    [ProducesResponseType(typeof(IReadOnlyList<PositionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetPositionsAsync(CancellationToken cancellationToken)
    {
        var exchange = await _exchangeResolver.GetCurrentExchangeAsync(cancellationToken);
        if (!await HasConfiguredAccessAsync(exchange, cancellationToken))
            return Ok(Array.Empty<PositionDto>());

        var positions = await _mediator.Send(
            new GetOpenPositionsQuery(exchange, await GetWalletAddressAsync(exchange, cancellationToken)),
            cancellationToken);
        return Ok(positions);
    }

    [HttpGet("orders")]
    [ProducesResponseType(typeof(IReadOnlyList<OpenOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetOpenOrdersAsync(CancellationToken cancellationToken)
    {
        var exchange = await _exchangeResolver.GetCurrentExchangeAsync(cancellationToken);
        if (!await HasConfiguredAccessAsync(exchange, cancellationToken))
            return Ok(Array.Empty<OpenOrderDto>());

        var orders = await _mediator.Send(
            new GetOpenOrdersQuery(exchange, await GetWalletAddressAsync(exchange, cancellationToken)),
            cancellationToken);
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
        var exchange = await _exchangeResolver.GetCurrentExchangeAsync(cancellationToken);
        if (!await HasConfiguredAccessAsync(exchange, cancellationToken))
            return Ok(Array.Empty<FillEventDto>());

        var fills = await _mediator.Send(
            new GetRecentFillsQuery(exchange, asset, await GetWalletAddressAsync(exchange, cancellationToken)),
            cancellationToken);
        return Ok(fills);
    }

    private async Task<bool> HasConfiguredAccessAsync(Exchange exchange, CancellationToken cancellationToken)
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (claim is null || !Guid.TryParse(claim, out var userId))
            return false;

        return exchange switch
        {
            Exchange.Hyperliquid => (await _walletRepo.GetActiveByUserIdAndExchangeAsync(userId, exchange, cancellationToken))?.WalletAddress is not null,
            Exchange.Binance => await _credentialRepository.GetActiveByUserIdAndExchangeAsync(userId, exchange, cancellationToken) is not null,
            _ => false,
        };
    }

    private async Task<string?> GetWalletAddressAsync(Exchange exchange, CancellationToken cancellationToken)
    {
        if (exchange != Exchange.Hyperliquid)
        {
            return null;
        }

        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (claim is null || !Guid.TryParse(claim, out var userId))
            return null;

        var wallet = await _walletRepo.GetActiveByUserIdAndExchangeAsync(userId, exchange, cancellationToken);
        return wallet?.WalletAddress;
    }

}
