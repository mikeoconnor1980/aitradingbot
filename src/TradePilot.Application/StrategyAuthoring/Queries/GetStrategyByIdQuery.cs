using System.Text.Json;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Identity;
using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Serialization;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.StrategyAuthoring.Queries;

public sealed record GetStrategyByIdQuery(Guid Id, AppIdentity Identity) : Query<StrategyDto>;

public sealed class GetStrategyByIdQueryHandler : QueryHandler<GetStrategyByIdQuery, StrategyDto>
{
    private readonly IStrategyRepository _repository;

    public GetStrategyByIdQueryHandler(IStrategyRepository repository)
    {
        _repository = repository;
    }

    public override async Task<StrategyDto> Handle(GetStrategyByIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.Identity);

        var strategy = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (strategy is null || strategy.UserId != request.Identity.UserId || !strategy.IsActive)
        {
            throw new NotFoundException(nameof(Strategy), request.Id);
        }

        var config = JsonSerializer.Deserialize<StrategyConfig>(strategy.ConfigJson, StrategyJsonOptions.Default)
            ?? new StrategyConfig();

        return new StrategyDto
        {
            Id = strategy.Id,
            Name = strategy.Name,
            StrategyType = strategy.StrategyType,
            Config = config,
            Version = strategy.Version,
            CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(strategy.CreatedAtUtc).UtcDateTime,
            UpdatedAt = DateTimeOffset.FromUnixTimeMilliseconds(strategy.UpdatedAtUtc).UtcDateTime,
        };
    }
}