using System.Text.Json.Serialization;

namespace TradePilot.Application.Agent.Models;

/// <summary>
/// Release metadata published by CI to describe the latest execution agent artifacts.
/// </summary>
public sealed class InstallerReleaseManifest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("publishedAtUtc")]
    public DateTimeOffset PublishedAtUtc { get; set; }

    [JsonPropertyName("releaseNotes")]
    public string? ReleaseNotes { get; set; }

    [JsonPropertyName("minimumSupportedVersion")]
    public string MinimumSupportedVersion { get; set; } = string.Empty;

    [JsonPropertyName("files")]
    public Dictionary<string, InstallerReleaseFile> Files { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("artifacts")]
    public Dictionary<string, InstallerReleaseFile> Artifacts { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}