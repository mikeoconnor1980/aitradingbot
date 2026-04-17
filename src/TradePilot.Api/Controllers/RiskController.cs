using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TradePilot.Api.Infrastructure;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;
using TradePilot.Application.Trading.Services;

namespace TradePilot.Api.Controllers;

[ApiController]
[Route("api/risk")]
[Produces("application/json")]
[Authorize]
public sealed class RiskController : ControllerBase
{
    private readonly IHyperliquidAccountService _accountService;
    private readonly IStrategyRepository _strategyRepository;
    private readonly IUserWalletAddressRepository _walletRepo;
    private readonly RiskLimitsConfig _limits;

    public RiskController(
        IHyperliquidAccountService accountService,
        IStrategyRepository strategyRepository,
        IUserWalletAddressRepository walletRepo,
        IOptions<RiskLimitsConfig> limits)
    {
        _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
        _strategyRepository = strategyRepository ?? throw new ArgumentNullException(nameof(strategyRepository));
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

    [HttpGet("drawdown-state")]
    [ProducesResponseType(typeof(DrawdownStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetDrawdownStateAsync(CancellationToken cancellationToken)
    {
        var address = await GetWalletAddressAsync(cancellationToken);
        if (address is null)
        {
            return Ok(DrawdownStateResponse.Empty());
        }

        var summary = await _accountService.GetAccountSummaryAsync(address, cancellationToken);
        var highWaterMark = await GetHighWaterMarkAsync(summary.Equity, cancellationToken);
        var result = DrawdownEvaluator.Evaluate(summary.Equity, highWaterMark, _limits.DrawdownTiers);

        return Ok(new DrawdownStateResponse
        {
            DrawdownPercent = result.DrawdownPercent,
            HighWaterMark = result.NewHighWaterMark,
            ScalingFactor = result.ScalingFactor,
            IsCircuitBreakerActive = result.IsHalted,
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

    private async Task<decimal> GetHighWaterMarkAsync(decimal currentEquity, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return currentEquity;
        }

        var activeStrategies = await _strategyRepository.GetActiveByUserIdAsync(userId, cancellationToken);
        var highWaterMark = activeStrategies
            .Select(strategy => strategy.HighWaterMarkUsd)
            .FirstOrDefault(value => value.HasValue);

        return highWaterMark ?? currentEquity;
    }
}