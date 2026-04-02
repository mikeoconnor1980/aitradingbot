using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TradingApp.Api.Infrastructure;
using TradingApp.Api.Models;
using TradingApp.Application.Abstractions.Models;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Backtesting;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.StrategyAuthoring.Serialization;
using TradingApp.Domain.Trading;
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

        var strategyConfig = new StrategyConfig
        {
            SchemaVersion = 1,
            StrategyMode = StrategyMode.Grid,
            StrategyName = request.StrategyConfig.StrategyName,
            Exchange = "Hyperliquid",
            Market = request.StrategyConfig.Market,
            Timeframe = request.StrategyConfig.Timeframe,
            Direction = ParseSchemaEnum(request.StrategyConfig.Direction, Direction.Long),
            Enabled = request.StrategyConfig.Enabled,
            Grid = request.StrategyConfig.Grid is not null
                ? new GridConfig
                {
                    Levels = request.StrategyConfig.Grid.Levels,
                    Spacing = request.StrategyConfig.Grid.Spacing,
                    EntryMode = NormalizeEntryMode(request.StrategyConfig.Grid.EntryMode),
                    AnchorPrice = request.StrategyConfig.Grid.AnchorPrice,
                    BreakdownThreshold = request.StrategyConfig.Grid.BreakdownThreshold,
                }
                : null,
            Exit = new ExitConfig
            {
                TakeProfit = new ExitRuleConfig
                {
                    Enabled = request.StrategyConfig.Exit.TakeProfit.Enabled,
                    Type = ParseSchemaEnum(request.StrategyConfig.Exit.TakeProfit.Type, ExitRuleType.FixedPercent),
                    Value = request.StrategyConfig.Exit.TakeProfit.Value,
                    Lookback = request.StrategyConfig.Exit.TakeProfit.Lookback,
                },
                StopLoss = new ExitRuleConfig
                {
                    Enabled = request.StrategyConfig.Exit.StopLoss.Enabled,
                    Type = ParseSchemaEnum(request.StrategyConfig.Exit.StopLoss.Type, ExitRuleType.FixedPercent),
                    Value = request.StrategyConfig.Exit.StopLoss.Value,
                    Lookback = request.StrategyConfig.Exit.StopLoss.Lookback,
                },
                ExitOnOppositeSignal = request.StrategyConfig.Exit.ExitOnOppositeSignal,
            },
            Risk = new RiskConfig
            {
                PositionSizeType = ParseSchemaEnum(request.StrategyConfig.Risk.PositionSizeType, PositionSizeType.PercentWallet),
                PositionSizeValue = request.StrategyConfig.Risk.PositionSizeValue,
                Leverage = request.StrategyConfig.Risk.Leverage,
                MaxOpenTrades = request.StrategyConfig.Risk.MaxOpenTrades,
                CooldownValue = request.StrategyConfig.Risk.CooldownValue,
                CooldownUnit = ParseSchemaEnum(request.StrategyConfig.Risk.CooldownUnit, CooldownUnit.Candles),
                AllowSameCandleReentry = request.StrategyConfig.Risk.AllowSameCandleReentry,
            },
            Source = new SourceMetadata
            {
                EntryPoint = StrategyEntryPoint.UiBuilder,
                Summary = $"Backtest: {request.StrategyConfig.StrategyName}",
            },
        };

        var executionConfig = new ExecutionConfig
        {
            FeeModel = new FeeModel
            {
                MakerFeeRate = request.ExecutionConfig.MakerFee,
                TakerFeeRate = request.ExecutionConfig.TakerFee,
                SlippageRate = request.ExecutionConfig.Slippage,
            },
        };

        var result = await Mediator.Send(
            new RunBacktestCommand(
                request.Symbol,
                request.Intervals,
                request.StartDate!.Value,
                request.EndDate!.Value,
                strategyConfig,
                executionConfig,
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

        if (request.StrategyConfig.Grid is null)
        {
            throw new DomainException("grid configuration is required");
        }

        var entryMode = NormalizeEntryMode(request.StrategyConfig.Grid.EntryMode);

        if (!EntryModes.IsValid(entryMode))
        {
            throw new DomainException($"entryMode must be one of: {EntryModes.AutoFromSignalCandle}, {EntryModes.InitialMarketThenGrid}, {EntryModes.WaitForLimitPrice}");
        }

        if (string.Equals(entryMode, EntryModes.WaitForLimitPrice, StringComparison.Ordinal)
            && request.StrategyConfig.Grid.AnchorPrice is null)
        {
            throw new DomainException("manualAnchorPrice is required when entryMode is WaitForLimitPrice");
        }
    }

    private static TEnum ParseSchemaEnum<TEnum>(string? value, TEnum fallback)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        try
        {
            return JsonSerializer.Deserialize<TEnum>($"\"{value}\"", StrategyJsonOptions.Default);
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    private static string NormalizeEntryMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return EntryModes.AutoFromSignalCandle;
        }

        return value switch
        {
            "auto_from_signal_candle" => EntryModes.AutoFromSignalCandle,
            "initial_market_then_grid" => EntryModes.InitialMarketThenGrid,
            "wait_for_limit_price" => EntryModes.WaitForLimitPrice,
            _ => value,
        };
    }

    private const string GetBacktestByIdRouteName = "GetBacktestById";
}