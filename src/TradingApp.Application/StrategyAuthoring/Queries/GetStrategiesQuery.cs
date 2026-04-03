using System.Text.Json;
using TradingApp.Application.Abstractions.Identity;
using TradingApp.Application.Abstractions.Queries;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.StrategyAuthoring.Serialization;

namespace TradingApp.Application.StrategyAuthoring.Queries;

public sealed record GetStrategiesQuery(AppIdentity Identity) : Query<List<StrategySummaryDto>>;

public sealed class GetStrategiesQueryHandler : QueryHandler<GetStrategiesQuery, List<StrategySummaryDto>>
{
    private readonly IStrategyRepository _repository;

    public GetStrategiesQueryHandler(IStrategyRepository repository)
    {
        _repository = repository;
    }

    public override async Task<List<StrategySummaryDto>> Handle(GetStrategiesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.Identity);

        var strategies = await _repository.GetActiveByUserIdAsync(request.Identity.UserId, cancellationToken);

        return strategies.Select(strategy =>
        {
            StrategyConfig config;
            try
            {
                config = JsonSerializer.Deserialize<StrategyConfig>(strategy.ConfigJson, StrategyJsonOptions.Default)
                    ?? new StrategyConfig();
            }
            catch (JsonException)
            {
                config = new StrategyConfig();
            }

            return new StrategySummaryDto
            {
                Id = strategy.Id,
                Name = strategy.Name,
                Market = config.Market,
                Timeframe = config.Timeframe,
                Direction = config.Direction.ToString().ToLowerInvariant(),
                StrategyMode = config.StrategyMode.ToString().ToLowerInvariant(),
                Version = strategy.Version,
                CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(strategy.CreatedAtUtc).UtcDateTime,
                UpdatedAt = DateTimeOffset.FromUnixTimeMilliseconds(strategy.UpdatedAtUtc).UtcDateTime,
            };
        }).ToList();
    }
}