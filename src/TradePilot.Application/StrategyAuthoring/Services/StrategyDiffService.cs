using System.Text.Json;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.StrategyAuthoring.Models;

namespace TradePilot.Application.StrategyAuthoring.Services;

public sealed class StrategyDiffService : IStrategyDiffService
{
    public IReadOnlyList<FieldChangeDto> ComputeDiff(string fromConfigJson, string toConfigJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fromConfigJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(toConfigJson);

        try
        {
            using var fromDocument = JsonDocument.Parse(fromConfigJson);
            using var toDocument = JsonDocument.Parse(toConfigJson);

            var changes = new List<FieldChangeDto>();
            CompareElements(fromDocument.RootElement, toDocument.RootElement, string.Empty, changes);
            return changes;
        }
        catch (JsonException ex)
        {
            throw new DomainException($"Unable to compute diff: invalid configuration JSON. {ex.Message}");
        }
    }

    private static void CompareElements(
        JsonElement from,
        JsonElement to,
        string path,
        List<FieldChangeDto> changes)
    {
        if (from.ValueKind != to.ValueKind)
        {
            AddChange(path, from, to, changes);
            return;
        }

        switch (to.ValueKind)
        {
            case JsonValueKind.Object:
                CompareObjects(from, to, path, changes);
                break;

            case JsonValueKind.Array:
                if (from.GetRawText() != to.GetRawText())
                {
                    AddChange(path, from, to, changes);
                }

                break;

            default:
                if (from.GetRawText() != to.GetRawText())
                {
                    AddChange(path, from, to, changes);
                }

                break;
        }
    }

    private static void CompareObjects(
        JsonElement from,
        JsonElement to,
        string parentPath,
        List<FieldChangeDto> changes)
    {
        var fromProperties = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in from.EnumerateObject())
        {
            fromProperties.Add(property.Name);
            var childPath = string.IsNullOrEmpty(parentPath) ? property.Name : $"{parentPath}.{property.Name}";

            if (to.TryGetProperty(property.Name, out var toValue))
            {
                CompareElements(property.Value, toValue, childPath, changes);
                continue;
            }

            changes.Add(new FieldChangeDto
            {
                Path = childPath,
                OldValue = FormatValue(property.Value),
                NewValue = null,
            });
        }

        foreach (var property in to.EnumerateObject())
        {
            if (fromProperties.Contains(property.Name))
            {
                continue;
            }

            var childPath = string.IsNullOrEmpty(parentPath) ? property.Name : $"{parentPath}.{property.Name}";
            changes.Add(new FieldChangeDto
            {
                Path = childPath,
                OldValue = null,
                NewValue = FormatValue(property.Value),
            });
        }
    }

    private static void AddChange(
        string path,
        JsonElement from,
        JsonElement to,
        List<FieldChangeDto> changes)
    {
        changes.Add(new FieldChangeDto
        {
            Path = path,
            OldValue = FormatValue(from),
            NewValue = FormatValue(to),
        });
    }

    private static string? FormatValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Null => null,
            _ => element.GetRawText(),
        };
    }
}