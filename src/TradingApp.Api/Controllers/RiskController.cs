using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TradingApp.Api.Infrastructure;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Application.Trading.Services;

namespace TradingApp.Api.Controllers;

[ApiController]
[Route("api/risk")]
[Produces("application/json")]
[Authorize]
public sealed class RiskController : ControllerBase
{
    private readonly IHyperliquidAccountService _accountService;
    private readonly IUserWalletAddressRepository _walletRepo;
    private readonly RiskLimitsConfig _limits;

    public RiskController(
        IHyperliquidAccountService accountService,
        IUserWalletAddressRepository walletRepo,
        IOptions<RiskLimitsConfig> limits)
    {
        _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
        _walletRepo = walletRepo ?? throw new ArgumentNullException(nameof(walletRepo));
        _limits = limits?.Value ?? throw new ArgumentNullException(nameof(limits));
    }

    [HttpGet("portfolio-heat")]
    [ProducesResponseType(typeof(PortfolioHeatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetPortfolioHeatAsync(CancellationToken cancellationToken)
    {
        var address = await GetWalletAddressAsync(cancellationToken);
        if (address is null)
        {
            return Ok(PortfolioHeatResponse.Empty(_limits.MaxPortfolioHeatPercent));
        }

        var summaryTask = _accountService.GetAccountSummaryAsync(address, cancellationToken);
        var positionsTask = _accountService.GetPositionsAsync(address, cancellationToken);

        await Task.WhenAll(summaryTask, positionsTask);

        var heat = PortfolioHeatCalculator.CalculateFromPositions(
            await positionsTask,
            (await summaryTask).Equity,
            _limits.MaxPortfolioHeatPercent);

        return Ok(new PortfolioHeatResponse
        {
            HeatPercent = heat.HeatPercent,
            MaxHeatPercent = heat.MaxHeatPercent,
            Equity = heat.Equity,
            Positions = heat.Entries
                .Select(entry => new PortfolioHeatPositionResponse
                {
                    Symbol = entry.Symbol,
                    RiskUsd = entry.RiskUsd,
                    RiskPercent = entry.RiskPercent,
                })
                .ToArray(),
        });
    }

    private async Task<string?> GetWalletAddressAsync(CancellationToken cancellationToken)
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (claim is null || !Guid.TryParse(claim, out var userId))
        {
            return null;
        }

        var wallet = await _walletRepo.GetActiveByUserIdAsync(userId, cancellationToken);
        return wallet?.WalletAddress;
    }
}