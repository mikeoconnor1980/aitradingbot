using System.Text.Json;
using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Serialization;

namespace TradePilot.Application.StrategyAuthoring.Queries;

public sealed record GetStrategyTemplatesQuery : Query<IReadOnlyList<StrategyTemplateDto>>;

public sealed class GetStrategyTemplatesQueryHandler
    : QueryHandler<GetStrategyTemplatesQuery, IReadOnlyList<StrategyTemplateDto>>
{
    private readonly IStrategyTemplateRepository _repository;

    public GetStrategyTemplatesQueryHandler(IStrategyTemplateRepository repository)
    {
        _repository = repository;
    }

    public override async Task<IReadOnlyList<StrategyTemplateDto>> Handle(
        GetStrategyTemplatesQuery request,
        CancellationToken cancellationToken)
    {
        var templates = await _repository.GetActiveOrderedAsync(cancellationToken);

        return templates.Select(t =>
        {
            StrategyConfig config;
            try
            {
                config = JsonSerializer.Deserialize<StrategyConfig>(t.ConfigJson, StrategyJsonOptions.Default)
                    ?? new StrategyConfig();
            }
            catch (JsonException)
            {
                config = new StrategyConfig();
            }

            string[] tags;
            try
            {
                tags = JsonSerializer.Deserialize<string[]>(t.TagsJson) ?? [];
            }
            catch (JsonException)
            {
                tags = [];
            }

            return new StrategyTemplateDto
            {
                Id = t.Id,
                Slug = t.Slug,
                Name = t.Name,
                Description = t.Description,
                StrategyMode = t.StrategyMode,
                Direction = t.Direction,
                Market = t.Market,
                Tags = tags,
                Config = config,
                SortOrder = t.SortOrder,
                IsSystemTemplate = t.IsSystemTemplate,
                CreatedAtUtc = t.CreatedAtUtc,
                UpdatedAtUtc = t.UpdatedAtUtc,
            };
        }).ToList();
    }
}
