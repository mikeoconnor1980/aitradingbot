using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradePilot.Api.Infrastructure;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;
using TradePilot.Domain.Enums;
using TradePilot.Domain.ValueObjects;

namespace TradePilot.Api.Controllers;

[ApiController]
[Route("api/account")]
[Produces("application/json")]
[Authorize]
public sealed class AccountController : ControllerBase
{
    private readonly IEnumerable<IExchangeAccountClient> _accountClients;
    private readonly IEnumerable<IExchangeSymbolMapper> _symbolMappers;
    private readonly IExchangeResolver _exchangeResolver;
    private readonly IUserWalletAddressRepository _walletRepo;
    private readonly IUserExchangeCredentialRepository _credentialRepository;

    public AccountController(
        IEnumerable<IExchangeAccountClient> accountClients,
        IEnumerable<IExchangeSymbolMapper> symbolMappers,
        IExchangeResolver exchangeResolver,
        IUserWalletAddressRepository walletRepo,
        IUserExchangeCredentialRepository credentialRepository)
    {
        _accountClients = accountClients;
        _symbolMappers = symbolMappers;
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

        var summary = await ResolveAccountClient(exchange)
            .GetAccountSummaryAsync(await GetWalletAddressAsync(exchange, cancellationToken), cancellationToken);
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

        var positions = await ResolveAccountClient(exchange)
            .GetPositionsAsync(await GetWalletAddressAsync(exchange, cancellationToken), cancellationToken);
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

        var orders = await ResolveAccountClient(exchange)
            .GetOpenOrdersAsync(await GetWalletAddressAsync(exchange, cancellationToken), cancellationToken);
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

        var pair = ResolveTradingPair(exchange, asset);
        var fills = await ResolveAccountClient(exchange)
            .GetRecentFillsAsync(pair, await GetWalletAddressAsync(exchange, cancellationToken), cancellationToken);
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

    private IExchangeAccountClient ResolveAccountClient(Exchange exchange)
    {
        return _accountClients.FirstOrDefault(client => client.Exchange == exchange)
            ?? throw new InvalidOperationException($"No account client is registered for exchange '{exchange}'.");
    }

    private TradingPair? ResolveTradingPair(Exchange exchange, string? asset)
    {
        if (string.IsNullOrWhiteSpace(asset))
        {
            return null;
        }

        var mapper = _symbolMappers.FirstOrDefault(candidate => candidate.Exchange == exchange)
            ?? throw new InvalidOperationException($"No symbol mapper is registered for exchange '{exchange}'.");

        return mapper.FromExchangeSymbol(asset);
    }
}