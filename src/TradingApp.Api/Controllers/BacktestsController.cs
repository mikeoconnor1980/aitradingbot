using System.ComponentModel.DataAnnotations;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TradingApp.Api.Infrastructure;
using TradingApp.Api.Models;
using TradingApp.Application.Abstractions.Models;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Backtesting;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Infrastructure.Binance;

namespace TradingApp.Api.Controllers;

[Route("api/backtests")]
public sealed class BacktestsController : ApiController
{
    public BacktestsController(IMediator mediator, IdentityService identityService)
        : base(mediator, identityService)
    {
    }

    [HttpPost]
    [ProducesResponseType(typeof(BacktestRunResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RunAsync([FromBody] RunBacktestRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        var strategyConfig = new GridStrategyConfig
        {
            GridLevels = request.StrategyConfig.GridLevels,
            GridSpacing = request.StrategyConfig.GridSpacing,
            TakeProfitPercent = request.StrategyConfig.TakeProfitPercent,
            BreakdownThreshold = request.StrategyConfig.BreakdownThreshold,
            MakerFee = request.StrategyConfig.MakerFee,
            TakerFee = request.StrategyConfig.TakerFee,
            Slippage = request.StrategyConfig.Slippage,
            PositionSize = request.StrategyConfig.PositionSize,
            Leverage = request.StrategyConfig.Leverage,
            StopLossPercent = request.StrategyConfig.StopLossPercent,
        };

        var result = await Mediator.Send(
            new RunBacktestCommand(
                request.Symbol,
                request.Intervals,
                request.StartDate!.Value,
                request.EndDate!.Value,
                strategyConfig,
                request.InitialCapital!.Value,
                request.EnableAuditLog),
            cancellationToken);

        return AcceptedAtRoute(GetBacktestByIdRouteName, new { id = result.Id }, result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<BacktestSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetBacktestsAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            throw new DomainException("page must be greater than or equal to 1");
        }

        if (pageSize < 1 || pageSize > 100)
        {
            throw new DomainException("pageSize must be between 1 and 100");
        }

        var result = await Mediator.Send(new GetBacktestListQuery(page, pageSize), cancellationToken);

        return Ok(new PagedResult<BacktestSummaryDto>
        {
            Items = result.Items
                .Select(summary => new BacktestSummaryDto
                {
                    Id = summary.Id,
                    Symbol = summary.Symbol,
                    Intervals = summary.Intervals,
                    StartDate = summary.StartDate,
                    EndDate = summary.EndDate,
                    TotalTrades = summary.TotalTrades,
                    WinRate = summary.WinRate,
                    TotalPnl = summary.TotalPnl,
                    MaxDrawdown = summary.MaxDrawdown,
                    CreatedAt = summary.CreatedAt,
                })
                .ToList(),
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
        });
    }

    [HttpGet("validate")]
    [ProducesResponseType(typeof(CandleCoverageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ValidateAsync(
        [FromQuery] string? symbol,
        [FromQuery] string? intervals,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new DomainException("symbol is required");
        }

        if (string.IsNullOrWhiteSpace(intervals))
        {
            throw new DomainException("intervals is required");
        }

        if (!BinanceAssetMapper.IsValidSymbol(symbol))
        {
            throw new DomainException(
                $"Unknown symbol '{symbol}'. Supported: {string.Join(", ", BinanceAssetMapper.ValidSymbols)}");
        }

        var intervalArray = intervals
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var interval in intervalArray)
        {
            if (!BinanceAssetMapper.IsValidInterval(interval))
            {
                throw new DomainException(
                    $"Invalid interval '{interval}'. Valid: {string.Join(", ", BinanceAssetMapper.ValidIntervals)}");
            }
        }

        var result = await Mediator.Send(new GetCandleCoverageQuery(symbol, intervalArray), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}", Name = GetBacktestByIdRouteName)]
    [ProducesResponseType(typeof(BacktestRunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetBacktestResultQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}/debug")]
    [ProducesResponseType(typeof(BacktestDebugResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDebugDataAsync(
        Guid id,
        [FromQuery][Required] string cycleId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetBacktestDebugQuery(id, cycleId), cancellationToken);
        return result is not null ? Ok(result) : NoContent();
    }

    private static void ValidateRequest(RunBacktestRequest request)
    {
        if (!BinanceAssetMapper.IsValidSymbol(request.Symbol))
        {
            throw new DomainException(
                $"Unknown symbol '{request.Symbol}'. Supported: {string.Join(", ", BinanceAssetMapper.ValidSymbols)}");
        }

        foreach (var interval in request.Intervals)
        {
            if (!BinanceAssetMapper.IsValidInterval(interval))
            {
                throw new DomainException(
                    $"Invalid interval '{interval}'. Valid: {string.Join(", ", BinanceAssetMapper.ValidIntervals)}");
            }
        }

        if (request.EndDate!.Value <= request.StartDate!.Value)
        {
            throw new DomainException("endDate must be after startDate");
        }
    }

    private const string GetBacktestByIdRouteName = "GetBacktestById";
}