using System.Text.Json;
using TradePilot.Application.Abstractions.Identity;
using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Serialization;
using TradePilot.Application.Subscriptions.Services;
using TradePilot.Domain.Subscriptions;

namespace TradePilot.Application.StrategyAuthoring.Queries;

public sealed record GetStrategyTemplatesQuery(AppIdentity Identity, bool IncludeAll = false) : Query<IReadOnlyList<StrategyTemplateDto>>;

public sealed class GetStrategyTemplatesQueryHandler
    : QueryHandler<GetStrategyTemplatesQuery, IReadOnlyList<StrategyTemplateDto>>
{
    private readonly IStrategyTemplateRepository _repository;
    private readonly ISubscriptionFeatureService _subscriptionFeatureService;

    public GetStrategyTemplatesQueryHandler(
        IStrategyTemplateRepository repository,
        ISubscriptionFeatureService subscriptionFeatureService)
    {
        _repository = repository;
        _subscriptionFeatureService = subscriptionFeatureService;
    }

    public override async Task<IReadOnlyList<StrategyTemplateDto>> Handle(
        GetStrategyTemplatesQuery request,
        CancellationToken cancellationToken)
    {
        var templates = await _repository.GetActiveOrderedAsync(cancellationToken);

        var filteredTemplates = templates;
        if (!request.IncludeAll && Guid.TryParse(request.Identity.UserId, out var userId))
        {
            var policy = await _subscriptionFeatureService.GetPolicyAsync(userId, cancellationToken);
            if (policy?.HasFeature(Feature.FullStrategyLibrary) != true)
            {
                filteredTemplates = templates.Where(template => template.IsBeginnerVisible).ToList();
            }
        }

        return filteredTemplates.Select(t =>
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
                IsBeginnerVisible = t.IsBeginnerVisible,
                CreatedAtUtc = t.CreatedAtUtc,
                UpdatedAtUtc = t.UpdatedAtUtc,
            };
        }).ToList();
    }
}
