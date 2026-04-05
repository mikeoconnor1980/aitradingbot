using MediatR;
using Microsoft.AspNetCore.Mvc;
using TradingApp.Api.Infrastructure;
using TradingApp.Api.Models;
using TradingApp.Application.Abstractions.Models;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Optimization;
using TradingApp.Application.Optimization.Models;
using TradingApp.Infrastructure.Binance;

namespace TradingApp.Api.Controllers;

[Route("api/optimizations")]
public sealed class OptimizationsController : ApiController
{
    private const string GetOptimizationByIdRouteName = "GetOptimizationById";

    public OptimizationsController(IMediator mediator, IdentityService identityService)
        : base(mediator, identityService)
    {
    }

    [HttpPost]
    [ProducesResponseType(typeof(OptimizationRunResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RunAsync([FromBody] RunOptimizationRequest request, CancellationToken cancellationToken)
    {
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
        var response = await Mediator.Send(new GetOptimizationResultQuery(id), cancellationToken);
        return Ok(response);
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

        return defaults with
        {
            StopLossMin = stopLossMin,
            StopLossMax = stopLossMax,
            TakeProfitMin = takeProfitMin,
            TakeProfitMax = takeProfitMax,
            LeverageMin = leverageMin,
            LeverageMax = leverageMax,
        };
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
}