using System.Text.Json;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Queries;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Backtesting.Models;

namespace TradingApp.Application.Backtesting;

public sealed record GetBacktestDebugQuery(Guid BacktestId, string CycleId) : Query<BacktestDebugResponse?>;

public sealed class GetBacktestDebugQueryHandler : QueryHandler<GetBacktestDebugQuery, BacktestDebugResponse?>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

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

        return new BacktestDebugResponse
        {
            CycleId = request.CycleId,
            CandleEvaluations = candleEvaluations
                .Where(entry => string.Equals(entry.GridCycleId, request.CycleId, StringComparison.Ordinal))
                .ToList(),
            OrderEvents = orderEvents
                .Where(entry => string.Equals(entry.GridCycleId, request.CycleId, StringComparison.Ordinal))
                .ToList(),
            GridCycleSummary = gridCycles.FirstOrDefault(entry => string.Equals(entry.GridCycleId, request.CycleId, StringComparison.Ordinal)),
        };
    }
}