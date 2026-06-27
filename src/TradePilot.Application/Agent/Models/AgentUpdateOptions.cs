namespace TradePilot.Application.Agent.Models;

/// <summary>
/// Fallback configuration for agent auto-update. Bound from appsettings "AgentUpdate" section.
/// The API prefers manifest-backed release metadata and only uses these values when no
/// release manifest is available.
/// </summary>
public sealed class AgentUpdateOptions
{
    public const string SectionName = "AgentUpdate";

    /// <summary>The fallback latest available agent version (semver, e.g. "0.2.0").</summary>
    public string LatestVersion { get; set; } = string.Empty;

    /// <summary>Fallback URL to download the installer EXE for the latest version.</summary>
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>Fallback SHA256 hash of the installer binary (lowercase hex).</summary>
    public string Sha256Hash { get; set; } = string.Empty;

    /// <summary>
    /// Fallback minimum agent version still supported. Agents below this version
    /// should be force-notified (future use).
    /// </summary>
    public string MinimumVersion { get; set; } = "0.0.0";

    /// <summary>Fallback release notes shown to the operator (optional).</summary>
    public string? ReleaseNotes { get; set; }
}
