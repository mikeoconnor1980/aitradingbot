using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TradingApp.Api.Infrastructure;
using TradingApp.Api.Models;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Abstractions.Models;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Backtesting;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.StrategyAuthoring.Serialization;
using TradingApp.Domain.Entities;
using TradingApp.Domain.Trading;
using TradingApp.Infrastructure.Binance;

namespace TradingApp.Api.Controllers;

[Route("api/backtests")]
public sealed class BacktestsController : ApiController
{
    private static readonly string[] RequiredBacktestIntervals = ["15m", "1h", "4h"];
    private readonly IStrategyRepository _strategyRepository;

    public BacktestsController(
        IMediator mediator,
        IdentityService identityService,
        IStrategyRepository strategyRepository)
        : base(mediator, identityService)
    {
        _strategyRepository = strategyRepository;
    }

    [HttpPost]
    [ProducesResponseType(typeof(BacktestRunResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RunAsync([FromBody] RunBacktestRequest request, CancellationToken cancellationToken)
    {
        ValidateRequestDates(request);

        StrategyConfig strategyConfig;
        string symbol;
        string[] intervals;

        if (request.StrategyId.HasValue)
        {
            var strategy = await GetOwnedStrategyAsync(request.StrategyId.Value, cancellationToken);

            strategyConfig = JsonSerializer.Deserialize<StrategyConfig>(strategy.ConfigJson, StrategyJsonOptions.Default)
                ?? throw new DomainException("Failed to deserialize strategy configuration.");
            symbol = BinanceAssetMapper.NormalizeSymbol(strategyConfig.Market);
            intervals = RequiredBacktestIntervals;
        }
        else
        {
            if (request.StrategyConfig is null)
            {
                throw new DomainException("Either strategyId or strategyConfig must be provided.");
            }

            ValidateManualRequest(request);

            strategyConfig = MapStrategyConfig(request.StrategyConfig);
            symbol = BinanceAssetMapper.NormalizeSymbol(request.Symbol!);
            intervals = request.Intervals!;
        }

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
                symbol,
                intervals,
                request.StartDate!.Value,
                request.EndDate!.Value,
                strategyConfig,
                executionConfig,
                request.InitialCapital!.Value,
                request.EnableAuditLog,
                IdentityService.Identity,
                request.StrategyId),
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
        var strategyNames = await GetStrategyNamesByIdAsync(result.Items.Select(summary => summary.StrategyId), cancellationToken);

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
                    StrategyId = summary.StrategyId,
                    StrategyRevisionId = summary.StrategyRevisionId,
                    StrategyName = summary.StrategyId.HasValue && strategyNames.TryGetValue(summary.StrategyId.Value, out var strategyName)
                        ? strategyName
                        : summary.StrategyName,
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

        var normalizedSymbol = BinanceAssetMapper.NormalizeSymbol(symbol);

        if (!BinanceAssetMapper.IsValidSymbol(normalizedSymbol))
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

        var result = await Mediator.Send(new GetCandleCoverageQuery(normalizedSymbol, intervalArray), cancellationToken);
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

    private static void ValidateManualRequest(RunBacktestRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Symbol))
        {
            throw new DomainException("symbol is required");
        }

        if (request.Intervals is null || request.Intervals.Length == 0)
        {
            throw new DomainException("intervals is required");
        }

        request.Symbol = BinanceAssetMapper.NormalizeSymbol(request.Symbol);

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

        if (request.StrategyConfig is null)
        {
            throw new DomainException("strategyConfig is required");
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

    private static void ValidateRequestDates(RunBacktestRequest request)
    {
        if (!request.StartDate.HasValue)
        {
            throw new DomainException("startDate is required");
        }

        if (!request.EndDate.HasValue)
        {
            throw new DomainException("endDate is required");
        }

        var utcNow = DateTime.UtcNow;

        if (request.StartDate.Value.ToUniversalTime() > utcNow)
        {
            throw new DomainException("startDate cannot be in the future");
        }

        if (request.EndDate.Value.ToUniversalTime() > utcNow)
        {
            throw new DomainException("endDate cannot be in the future");
        }
    }

    private async Task<Strategy> GetOwnedStrategyAsync(Guid strategyId, CancellationToken cancellationToken)
    {
        var strategy = await _strategyRepository.GetByIdAsync(strategyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Strategy), strategyId);

        if (strategy.UserId != IdentityService.Identity.UserId || !strategy.IsActive)
        {
            throw new NotFoundException(nameof(Strategy), strategyId);
        }

        return strategy;
    }

    private static StrategyConfig MapStrategyConfig(StrategyConfigRequest request)
    {
        return new StrategyConfig
        {
            SchemaVersion = 1,
            StrategyMode = StrategyMode.Grid,
            StrategyName = request.StrategyName,
            Exchange = "Hyperliquid",
            Market = request.Market,
            Timeframe = request.Timeframe,
            Direction = ParseSchemaEnum(request.Direction, Direction.Long),
            Enabled = request.Enabled,
            Grid = request.Grid is not null
                ? new GridConfig
                {
                    Levels = request.Grid.Levels,
                    Spacing = request.Grid.Spacing,
                    EntryMode = NormalizeEntryMode(request.Grid.EntryMode),
                    AnchorPrice = request.Grid.AnchorPrice,
                    BreakdownThreshold = request.Grid.BreakdownThreshold,
                }
                : null,
            Exit = new ExitConfig
            {
                TakeProfit = new ExitRuleConfig
                {
                    Enabled = request.Exit.TakeProfit.Enabled,
                    Type = ParseSchemaEnum(request.Exit.TakeProfit.Type, ExitRuleType.FixedPercent),
                    Value = request.Exit.TakeProfit.Value,
                    Lookback = request.Exit.TakeProfit.Lookback,
                },
                StopLoss = new ExitRuleConfig
                {
                    Enabled = request.Exit.StopLoss.Enabled,
                    Type = ParseSchemaEnum(request.Exit.StopLoss.Type, ExitRuleType.FixedPercent),
                    Value = request.Exit.StopLoss.Value,
                    Lookback = request.Exit.StopLoss.Lookback,
                },
                ExitOnOppositeSignal = request.Exit.ExitOnOppositeSignal,
            },
            Risk = new RiskConfig
            {
                PositionSizeType = ParseSchemaEnum(request.Risk.PositionSizeType, PositionSizeType.PercentWallet),
                PositionSizeValue = request.Risk.PositionSizeValue,
                Leverage = request.Risk.Leverage,
                MaxOpenTrades = request.Risk.MaxOpenTrades,
                CooldownValue = request.Risk.CooldownValue,
                CooldownUnit = ParseSchemaEnum(request.Risk.CooldownUnit, CooldownUnit.Candles),
                AllowSameCandleReentry = request.Risk.AllowSameCandleReentry,
            },
            Source = new SourceMetadata
            {
                EntryPoint = StrategyEntryPoint.UiBuilder,
                Summary = $"Backtest: {request.StrategyName}",
            },
        };
    }

    private async Task<Dictionary<Guid, string?>> GetStrategyNamesByIdAsync(
        IEnumerable<Guid?> strategyIds,
        CancellationToken cancellationToken)
    {
        var ids = strategyIds
            .Where(strategyId => strategyId.HasValue)
            .Select(strategyId => strategyId!.Value)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return [];
        }

        var strategies = await _strategyRepository.GetByIdsAsync(ids, cancellationToken);

        return strategies.ToDictionary(
            strategy => strategy.Id,
            strategy => (string?)(strategy.IsActive
                ? strategy.Name
                : $"{strategy.Name} (deleted)"));
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