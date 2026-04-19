using MediatR;
using Microsoft.AspNetCore.Mvc;
using TradePilot.Api.Infrastructure;
using TradePilot.Api.Models;
using TradePilot.Application.Abstractions.Models;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Optimization;
using TradePilot.Application.Optimization.Models;
using TradePilot.Application.Subscriptions.Services;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Domain.Subscriptions;
using TradePilot.Infrastructure.Binance;

namespace TradePilot.Api.Controllers;

[Route("api/optimizations")]
public sealed class OptimizationsController : ApiController
{
    private const string GetOptimizationByIdRouteName = "GetOptimizationById";
    private readonly ISubscriptionFeatureService _subscriptionFeatureService;

    public OptimizationsController(
        IMediator mediator,
        IdentityService identityService,
        ISubscriptionFeatureService subscriptionFeatureService)
        : base(mediator, identityService)
    {
        _subscriptionFeatureService = subscriptionFeatureService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(OptimizationRunResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RunAsync([FromBody] RunOptimizationRequest request, CancellationToken cancellationToken)
    {
        await EnsureFeatureAsync(cancellationToken);
        ValidateRequest(request);

        var normalizedSymbol = BinanceAssetMapper.NormalizeSymbol(request.Symbol.Trim());
        if (!BinanceAssetMapper.IsValidSymbol(normalizedSymbol))
        {
            throw new DomainException(
                $"Unknown symbol '{request.Symbol}'. Supported: {string.Join(", ", BinanceAssetMapper.ValidSymbols)}");
        }

        var startDateUtc = request.StartDate!.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(request.StartDate.Value, DateTimeKind.Utc)
            : request.StartDate.Value.ToUniversalTime();
        var endDateUtc = request.EndDate!.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(request.EndDate.Value, DateTimeKind.Utc)
            : request.EndDate.Value.ToUniversalTime();

        var bounds = BuildBounds(request);
        var thresholds = BuildThresholds(request);
        var walkForward = BuildWalkForwardConfig(request);
        var evolutionary = BuildEvolutionaryConfig(request);
        var config = new SweepConfig
        {
            Symbol = request.Symbol.Trim(),
            BacktestSymbol = normalizedSymbol,
            StartDateUtc = new DateTimeOffset(startDateUtc).ToUnixTimeMilliseconds(),
            EndDateUtc = new DateTimeOffset(endDateUtc).ToUnixTimeMilliseconds(),
            InitialCapital = request.InitialCapital,
            SampleSize = request.SampleSize,
            Bounds = bounds,
            Thresholds = thresholds,
            WalkForward = walkForward,
            Evolutionary = evolutionary,
        };

        var response = await Mediator.Send(new RunOptimizationCommand(config), cancellationToken);
        return AcceptedAtRoute(GetOptimizationByIdRouteName, new { id = response.Id }, response);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OptimizationRunSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetListAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        await EnsureFeatureAsync(cancellationToken);

        if (page < 1)
        {
            throw new DomainException("page must be greater than or equal to 1");
        }

        if (pageSize < 1 || pageSize > 100)
        {
            throw new DomainException("pageSize must be between 1 and 100");
        }

        var response = await Mediator.Send(new GetOptimizationListQuery(page, pageSize), cancellationToken);
        return Ok(response);
    }

    [HttpGet("{id:guid}", Name = GetOptimizationByIdRouteName)]
    [ProducesResponseType(typeof(OptimizationRunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await EnsureFeatureAsync(cancellationToken);
        var response = await Mediator.Send(new GetOptimizationResultQuery(id), cancellationToken);
        return Ok(response);
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        await EnsureFeatureAsync(cancellationToken);
        await Mediator.Send(new CancelOptimizationCommand(id), cancellationToken);
        return NoContent();
    }

    private async Task EnsureFeatureAsync(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(IdentityService.Identity.UserId);
        if (!await _subscriptionFeatureService.CanAccessFeatureAsync(userId, Feature.Optimizer, cancellationToken))
        {
            throw new UnauthorizedAccessException("This feature requires a Pro subscription.");
        }
    }

    private static void ValidateRequest(RunOptimizationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Symbol))
        {
            throw new DomainException("symbol is required");
        }

        if (!request.StartDate.HasValue)
        {
            throw new DomainException("startDate is required");
        }

        if (!request.EndDate.HasValue)
        {
            throw new DomainException("endDate is required");
        }

        if (request.EndDate.Value <= request.StartDate.Value)
        {
            throw new DomainException("endDate must be after startDate");
        }
    }

    private static ParameterBounds BuildBounds(RunOptimizationRequest request)
    {
        var defaults = new ParameterBounds();
        var stopLossMin = request.StopLossMin ?? defaults.StopLossMin;
        var stopLossMax = request.StopLossMax ?? defaults.StopLossMax;
        var takeProfitMin = request.TakeProfitMin ?? defaults.TakeProfitMin;
        var takeProfitMax = request.TakeProfitMax ?? defaults.TakeProfitMax;
        var leverageMin = request.LeverageMin ?? defaults.LeverageMin;
        var leverageMax = request.LeverageMax ?? defaults.LeverageMax;

        ValidateRange(stopLossMin, stopLossMax, "stopLoss");
        ValidateRange(takeProfitMin, takeProfitMax, "takeProfit");
        ValidateRange(leverageMin, leverageMax, "leverage");

        var directions = ParseDirections(request.Directions) ?? defaults.Directions;
        var timeframes = FilterValid(request.Timeframes, ["1m", "3m", "5m", "15m", "30m", "1h", "2h", "4h", "6h", "12h", "1d"]) ?? defaults.Timeframes;
        var positionSizeMode = defaults.PositionSizeMode;
        if (request.PositionSizeMode is not null)
        {
            if (!Enum.TryParse<PositionSizeMode>(request.PositionSizeMode, ignoreCase: true, out var sizeMode))
            {
                throw new DomainException(
                    $"Invalid positionSizeMode '{request.PositionSizeMode}'. Allowed: {string.Join(", ", Enum.GetNames<PositionSizeMode>())}");
            }

            positionSizeMode = sizeMode;
        }

        // Map operator string arrays, filtering to known valid ones
        var rsiOperators = FilterValid(request.RsiOperators, ["lt", "lte", "gt", "gte", "cross_above", "cross_below"]) ?? defaults.RsiOperators;
        var macdOperators = FilterValid(request.MacdOperators, ["cross_above_signal", "cross_below_signal", "above_zero", "below_zero", "histogram_rising", "histogram_falling"]) ?? defaults.MacdOperators;
        var priceVsEmaOperators = FilterValid(request.PriceVsEmaOperators, ["near", "above", "below", "cross_above", "cross_below", "touch"]) ?? defaults.PriceVsEmaOperators;

        // ExitOnOppositeSignal: if caller sends a specific bool, only sweep that value; otherwise use defaults (both)
        var exitOnOppositeSignalOptions = request.ExitOnOppositeSignal.HasValue
            ? [request.ExitOnOppositeSignal.Value]
            : defaults.ExitOnOppositeSignalOptions;

        var riskPerTradeOptions = request.RiskPerTradePercentOptions is { Length: > 0 }
            ? request.RiskPerTradePercentOptions
            : defaults.RiskPerTradePercentOptions;

        if (riskPerTradeOptions.Any(r => r <= 0m || r > 100m))
        {
            throw new DomainException("riskPerTradePercentOptions values must be between 0 (exclusive) and 100");
        }

        return defaults with
        {
            Directions = directions,
            Timeframes = timeframes,
            StopLossMin = stopLossMin,
            StopLossMax = stopLossMax,
            TakeProfitMin = takeProfitMin,
            TakeProfitMax = takeProfitMax,
            LeverageMin = leverageMin,
            LeverageMax = leverageMax,
            PositionSizeMode = positionSizeMode,
            RsiOperators = rsiOperators,
            RsiPeriods = request.RsiPeriods is { Length: > 0 } ? request.RsiPeriods : defaults.RsiPeriods,
            RsiThresholds = request.RsiThresholds is { Length: > 0 } ? request.RsiThresholds : defaults.RsiThresholds,
            MacdOperators = macdOperators,
            MacdFastPeriods = request.MacdFastPeriods is { Length: > 0 } ? request.MacdFastPeriods : defaults.MacdFastPeriods,
            MacdSlowPeriods = request.MacdSlowPeriods is { Length: > 0 } ? request.MacdSlowPeriods : defaults.MacdSlowPeriods,
            PriceVsEmaOperators = priceVsEmaOperators,
            EmaPeriods = request.EmaPeriods is { Length: > 0 } ? request.EmaPeriods : defaults.EmaPeriods,
            EmaProximityPercents = request.EmaProximityPercents is { Length: > 0 } ? request.EmaProximityPercents : defaults.EmaProximityPercents,
            ExitOnOppositeSignalOptions = exitOnOppositeSignalOptions,
            MaxOpenTradesOptions = request.MaxOpenTradesOptions is { Length: > 0 } ? request.MaxOpenTradesOptions : defaults.MaxOpenTradesOptions,
            CooldownCandlesOptions = request.CooldownCandlesOptions is { Length: > 0 } ? request.CooldownCandlesOptions : defaults.CooldownCandlesOptions,
            IncludeTrendFilter = request.IncludeTrendFilter ?? defaults.IncludeTrendFilter,
            PositionSizeOptions = request.PositionSizePercent.HasValue ? [request.PositionSizePercent.Value] : defaults.PositionSizeOptions,
            RiskPerTradePercentOptions = riskPerTradeOptions,
            IncludeAutoLeverage = request.IncludeAutoLeverage ?? defaults.IncludeAutoLeverage,
        };
    }

    private static Direction[]? ParseDirections(string[]? input)
    {
        if (input is not { Length: > 0 })
        {
            return null;
        }

        var parsed = input
            .Select(direction => Enum.TryParse<Direction>(direction, ignoreCase: true, out var result) ? result : (Direction?)null)
            .Where(direction => direction.HasValue)
            .Select(direction => direction!.Value)
            .Distinct()
            .ToArray();

        return parsed.Length > 0 ? parsed : null;
    }

    private static string[]? FilterValid(string[]? input, string[] allowed)
    {
        if (input is not { Length: > 0 })
        {
            return null;
        }

        var filtered = input
            .Select(value => value.Trim().ToLowerInvariant())
            .Where(value => allowed.Contains(value))
            .Distinct()
            .ToArray();

        return filtered.Length > 0 ? filtered : null;
    }

    private static FitnessThresholds BuildThresholds(RunOptimizationRequest request)
    {
        var defaults = new FitnessThresholds();
        var thresholds = defaults with
        {
            MinWinRate = request.MinWinRate ?? defaults.MinWinRate,
            MinTotalTrades = request.MinTotalTrades ?? defaults.MinTotalTrades,
            MaxDrawdownPercent = request.MaxDrawdownPercent ?? defaults.MaxDrawdownPercent,
        };

        if (thresholds.MinWinRate < 0m || thresholds.MinWinRate > 100m)
        {
            throw new DomainException("minWinRate must be between 0 and 100");
        }

        if (thresholds.MinTotalTrades < 1)
        {
            throw new DomainException("minTotalTrades must be greater than or equal to 1");
        }

        if (thresholds.MaxDrawdownPercent <= 0m || thresholds.MaxDrawdownPercent > 100m)
        {
            throw new DomainException("maxDrawdownPercent must be greater than 0 and less than or equal to 100");
        }

        return thresholds;
    }

    private static void ValidateRange(decimal min, decimal max, string label)
    {
        if (min <= 0m || max <= 0m)
        {
            throw new DomainException($"{label} bounds must be greater than 0");
        }

        if (max < min)
        {
            throw new DomainException($"{label} max must be greater than or equal to min");
        }
    }

    private static WalkForwardConfig BuildWalkForwardConfig(RunOptimizationRequest request)
    {
        var defaults = new WalkForwardConfig();

        var splitPercent = request.WalkForwardSplitPercent ?? defaults.ValidationSplitPercent;

        if (splitPercent is <= 0m or >= 100m)
        {
            throw new DomainException("walkForwardSplitPercent must be between 0 and 100 (exclusive)");
        }

        return new WalkForwardConfig
        {
            Enabled = request.WalkForwardEnabled ?? defaults.Enabled,
            ValidationSplitPercent = splitPercent,
        };
    }

    private static EvolutionaryConfig BuildEvolutionaryConfig(RunOptimizationRequest request)
    {
        var defaults = new EvolutionaryConfig();

        var generations = request.EvolutionaryGenerations ?? defaults.Generations;
        var eliteCount = request.EvolutionaryEliteCount ?? defaults.EliteCount;
        var mutationRate = request.EvolutionaryMutationRate ?? defaults.MutationRate;
        var crossoverRate = request.EvolutionaryCrossoverRate ?? defaults.CrossoverRate;

        if (generations is < 0 or > 20)
        {
            throw new DomainException("evolutionaryGenerations must be between 0 and 20");
        }

        if (eliteCount < 2)
        {
            throw new DomainException("evolutionaryEliteCount must be at least 2");
        }

        if (mutationRate is < 0m or > 1m)
        {
            throw new DomainException("evolutionaryMutationRate must be between 0 and 1");
        }

        if (crossoverRate is < 0m or > 1m)
        {
            throw new DomainException("evolutionaryCrossoverRate must be between 0 and 1");
        }

        return new EvolutionaryConfig
        {
            Enabled = request.EvolutionaryEnabled ?? defaults.Enabled,
            Generations = generations,
            EliteCount = eliteCount,
            MutationRate = mutationRate,
            CrossoverRate = crossoverRate,
        };
    }
}