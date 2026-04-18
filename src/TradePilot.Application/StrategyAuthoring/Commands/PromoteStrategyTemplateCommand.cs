using System.Text.Json;
using System.Text.RegularExpressions;
using TradePilot.Application.Abstractions.Commands;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Identity;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Serialization;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.StrategyAuthoring.Commands;

public sealed record PromoteStrategyTemplateCommand(
    Guid StrategyId,
    string Name,
    string Description,
    string[] Tags,
    AppIdentity Identity) : CreateCommand;

public sealed class PromoteStrategyTemplateCommandHandler : CreateCommandHandler<PromoteStrategyTemplateCommand>
{
    private static readonly Regex NonAlphaNumericPattern = new("[^a-z0-9]+", RegexOptions.Compiled);
    private static readonly Regex MultiDashPattern = new("-+", RegexOptions.Compiled);

    private readonly IStrategyRepository _strategyRepository;
    private readonly IStrategyTemplateRepository _templateRepository;

    public PromoteStrategyTemplateCommandHandler(
        IStrategyRepository strategyRepository,
        IStrategyTemplateRepository templateRepository)
    {
        _strategyRepository = strategyRepository;
        _templateRepository = templateRepository;
    }

    public override async Task<Guid> Handle(PromoteStrategyTemplateCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.Identity);

        var strategy = await _strategyRepository.GetByIdAsync(request.StrategyId, cancellationToken);
        if (strategy is null || strategy.UserId != request.Identity.UserId || !strategy.IsActive)
        {
            throw new NotFoundException(nameof(Strategy), request.StrategyId);
        }

        var name = NormalizeRequiredText(request.Name, 100, "Library name");
        var description = NormalizeRequiredText(request.Description, 500, "Library description");

        if (await _templateRepository.ExistsWithNameAsync(name, cancellationToken))
        {
            throw new DuplicateStrategyTemplateNameException(name);
        }

        var config = JsonSerializer.Deserialize<StrategyConfig>(strategy.ConfigJson, StrategyJsonOptions.Default)
            ?? throw new DomainException("Saved strategy configuration is invalid.");

        var allowedTags = await GetAllowedTagsAsync(cancellationToken);
        var normalizedTags = NormalizeTags(request.Tags, allowedTags);
        var tagsJson = JsonSerializer.Serialize(normalizedTags);

        if (tagsJson.Length > 500)
        {
            throw new DomainException("Selected tags exceed the maximum allowed length.");
        }

        var slug = await GenerateUniqueSlugAsync(name, cancellationToken);
        var promotedConfig = config with
        {
            StrategyName = name,
            TemplateId = slug,
            Metadata = new StrategyMetadata
            {
                Tags = normalizedTags,
                Notes = config.Metadata?.Notes ?? string.Empty,
            },
        };

        var sortOrder = await _templateRepository.GetNextSortOrderAsync(cancellationToken);
        var configJson = JsonSerializer.Serialize(promotedConfig, StrategyJsonOptions.Default);
        var template = StrategyTemplate.Create(
            slug,
            name,
            description,
            promotedConfig.StrategyMode.ToString().ToLowerInvariant(),
            promotedConfig.Direction.ToString().ToLowerInvariant(),
            promotedConfig.Market,
            tagsJson,
            configJson,
            sortOrder,
            isSystemTemplate: false);

        await _templateRepository.AddAsync(template, cancellationToken);
        return template.Id;
    }

    private async Task<Dictionary<string, string>> GetAllowedTagsAsync(CancellationToken cancellationToken)
    {
        var templates = await _templateRepository.GetActiveOrderedAsync(cancellationToken);
        var allowedTags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var template in templates)
        {
            try
            {
                var tags = JsonSerializer.Deserialize<string[]>(template.TagsJson) ?? [];

                foreach (var tag in tags)
                {
                    var normalizedTag = tag.Trim();
                    if (normalizedTag.Length == 0 || allowedTags.ContainsKey(normalizedTag))
                    {
                        continue;
                    }

                    allowedTags[normalizedTag] = normalizedTag;
                }
            }
            catch (JsonException)
            {
                continue;
            }
        }

        return allowedTags;
    }

    private static string[] NormalizeTags(string[]? tags, IReadOnlyDictionary<string, string> allowedTags)
    {
        if (tags is null || tags.Length == 0)
        {
            return [];
        }

        var normalizedTags = new List<string>();

        foreach (var rawTag in tags)
        {
            var tag = rawTag?.Trim();
            if (string.IsNullOrWhiteSpace(tag))
            {
                continue;
            }

            if (!allowedTags.TryGetValue(tag, out var canonicalTag))
            {
                throw new DomainException($"Unknown strategy tag '{tag}'.");
            }

            if (normalizedTags.Contains(canonicalTag, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            normalizedTags.Add(canonicalTag);
        }

        return normalizedTags.ToArray();
    }

    private async Task<string> GenerateUniqueSlugAsync(string name, CancellationToken cancellationToken)
    {
        var baseSlug = CreateSlug(name);
        if (!await _templateRepository.ExistsWithSlugAsync(baseSlug, cancellationToken))
        {
            return baseSlug;
        }

        var attempt = 2;
        while (true)
        {
            var suffix = $"-{attempt}";
            var truncatedBase = baseSlug.Length > 100 - suffix.Length
                ? baseSlug[..(100 - suffix.Length)]
                : baseSlug;
            var candidateSlug = $"{truncatedBase.TrimEnd('-')}{suffix}";

            if (!await _templateRepository.ExistsWithSlugAsync(candidateSlug, cancellationToken))
            {
                return candidateSlug;
            }

            attempt++;
        }
    }

    private static string CreateSlug(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        normalized = NonAlphaNumericPattern.Replace(normalized, "-");
        normalized = MultiDashPattern.Replace(normalized, "-").Trim('-');

        if (normalized.Length == 0)
        {
            normalized = "strategy-template";
        }

        return normalized.Length > 100 ? normalized[..100].TrimEnd('-') : normalized;
    }

    private static string NormalizeRequiredText(string value, int maxLength, string fieldName)
    {
        var normalized = value?.Trim() ?? string.Empty;

        if (normalized.Length == 0)
        {
            throw new DomainException($"{fieldName} is required.");
        }

        if (normalized.Length > maxLength)
        {
            throw new DomainException($"{fieldName} must be {maxLength} characters or fewer.");
        }

        return normalized;
    }
}