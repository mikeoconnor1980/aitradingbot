using System.Text.Json;

namespace TradingApp.Application.StrategyAuthoring.Services;

public sealed class ChangeSummaryGenerator : IChangeSummaryGenerator
{
    private const int MaxSummaryLength = 2000;
    private const string InitialVersionSummary = "Initial version";
    private const string NoChangesSummary = "No changes detected";

    public string Generate(string? previousConfigJson, string currentConfigJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentConfigJson);

        if (string.IsNullOrWhiteSpace(previousConfigJson))
        {
            return InitialVersionSummary;
        }

        try
        {
            using var previousDocument = JsonDocument.Parse(previousConfigJson);
            using var currentDocument = JsonDocument.Parse(currentConfigJson);

            var changes = new List<string>();
            CompareElements(previousDocument.RootElement, currentDocument.RootElement, string.Empty, changes);

            if (changes.Count == 0)
            {
                return NoChangesSummary;
            }

            var summary = string.Join(", ", changes);

            if (summary.Length > MaxSummaryLength)
            {
                summary = string.Concat(summary.AsSpan(0, MaxSummaryLength - 3), "...");
            }

            return summary;
        }
        catch (JsonException)
        {
            return "Configuration changed (diff unavailable)";
        }
    }

    private static void CompareElements(
        JsonElement previous,
        JsonElement current,
        string path,
        List<string> changes)
    {
        if (previous.ValueKind != current.ValueKind)
        {
            changes.Add(FormatChange(path, FormatValue(previous), FormatValue(current)));
            return;
        }

        switch (current.ValueKind)
        {
            case JsonValueKind.Object:
                CompareObjects(previous, current, path, changes);
                break;
            case JsonValueKind.Array:
                if (previous.GetRawText() != current.GetRawText())
                {
                    changes.Add(FormatChange(path, "[array]", "[array]"));
                }

                break;
            default:
                if (previous.GetRawText() != current.GetRawText())
                {
                    changes.Add(FormatChange(path, FormatValue(previous), FormatValue(current)));
                }

                break;
        }
    }

    private static void CompareObjects(
        JsonElement previous,
        JsonElement current,
        string parentPath,
        List<string> changes)
    {
        var previousProperties = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in previous.EnumerateObject())
        {
            previousProperties.Add(property.Name);
            var childPath = string.IsNullOrEmpty(parentPath) ? property.Name : $"{parentPath}.{property.Name}";

            if (current.TryGetProperty(property.Name, out var currentValue))
            {
                CompareElements(property.Value, currentValue, childPath, changes);
                continue;
            }

            changes.Add(FormatChange(childPath, FormatValue(property.Value), "[removed]"));
        }

        foreach (var property in current.EnumerateObject())
        {
            if (previousProperties.Contains(property.Name))
            {
                continue;
            }

            var childPath = string.IsNullOrEmpty(parentPath) ? property.Name : $"{parentPath}.{property.Name}";
            changes.Add(FormatChange(childPath, "[added]", FormatValue(property.Value)));
        }
    }

    private static string FormatChange(string path, string oldValue, string newValue)
    {
        return $"{path}: {oldValue} → {newValue}";
    }

    private static string FormatValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? "null",
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            JsonValueKind.Object => "[object]",
            JsonValueKind.Array => "[array]",
            _ => element.GetRawText(),
        };
    }
}