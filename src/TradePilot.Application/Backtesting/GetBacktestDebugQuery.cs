using System.Text.Json;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Backtesting.Models;
using TradePilot.Application.StrategyAuthoring.Serialization;
using TradePilot.Application.Trading.Services;

namespace TradePilot.Application.Backtesting;

public sealed record GetBacktestDebugQuery(Guid BacktestId, string CycleId) : Query<BacktestDebugResponse?>;

public sealed class GetBacktestDebugQueryHandler : QueryHandler<GetBacktestDebugQuery, BacktestDebugResponse?>
{
    private static readonly JsonSerializerOptions JsonOptions = StrategyJsonOptions.Default;

    private readonly IBacktestRunRepository _repository;

    public GetBacktestDebugQueryHandler(IBacktestRunRepository repository)
    {
        _repository = repository;
    }

    public override async Task<BacktestDebugResponse?> Handle(GetBacktestDebugQuery request, CancellationToken cancellationToken)
    {
        var backtestRun = await _repository.GetByIdAsync(request.BacktestId, cancellationToken);

        if (backtestRun is null)
        {
            throw new NotFoundException("BacktestRun", request.BacktestId.ToString());
        }

        if (string.IsNullOrWhiteSpace(backtestRun.CandleLogJson))
        {
            return null;
        }

        var candleEvaluations = JsonSerializer.Deserialize<List<CandleEvaluationEntry>>(backtestRun.CandleLogJson, JsonOptions)
            ?? [];
        var orderEvents = string.IsNullOrWhiteSpace(backtestRun.OrderEventLogJson)
            ? []
            : JsonSerializer.Deserialize<List<OrderEventEntry>>(backtestRun.OrderEventLogJson, JsonOptions) ?? [];
        var gridCycles = string.IsNullOrWhiteSpace(backtestRun.GridCycleLogJson)
            ? []
            : JsonSerializer.Deserialize<List<GridCycleEntry>>(backtestRun.GridCycleLogJson, JsonOptions) ?? [];

        var filteredCandleEvaluations = candleEvaluations
            .Where(entry => string.Equals(entry.GridCycleId, request.CycleId, StringComparison.Ordinal))
            .OrderBy(entry => entry.TimestampUtc)
            .ToList();

        var indicatorSeries = ChartIndicatorSeriesCalculator.Calculate(filteredCandleEvaluations
            .Select(entry => (entry.High, entry.Low, entry.Close))
            .ToList());

        var enrichedCandleEvaluations = filteredCandleEvaluations
            .Select((entry, index) => entry with { Indicators = indicatorSeries[index] })
            .ToList();

        return new BacktestDebugResponse
        {
            CycleId = request.CycleId,
            CandleEvaluations = enrichedCandleEvaluations,
            OrderEvents = orderEvents
                .Where(entry => string.Equals(entry.GridCycleId, request.CycleId, StringComparison.Ordinal))
                .ToList(),
            GridCycleSummary = gridCycles.FirstOrDefault(entry => string.Equals(entry.GridCycleId, request.CycleId, StringComparison.Ordinal)),
        };
    }
}