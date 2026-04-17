namespace TradePilot.Application.Agent.Models;

/// <summary>
/// Configuration for agent auto-update. Bound from appsettings "AgentUpdate" section.
/// The API compares the agent's reported version against <see cref="LatestVersion"/>
/// and signals the agent to download the new installer.
/// </summary>
public sealed class AgentUpdateOptions
{
    public const string SectionName = "AgentUpdate";

    /// <summary>The latest available agent version (semver, e.g. "0.2.0").</summary>
    public string LatestVersion { get; set; } = string.Empty;

    /// <summary>URL to download the installer EXE for the latest version.</summary>
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>SHA256 hash of the installer binary (lowercase hex).</summary>
    public string Sha256Hash { get; set; } = string.Empty;

    /// <summary>
    /// Minimum agent version still supported. Agents below this version
    /// should be force-notified (future use).
    /// </summary>
    public string MinimumVersion { get; set; } = "0.0.0";

    /// <summary>Brief release notes shown to the operator (optional).</summary>
    public string? ReleaseNotes { get; set; }
}
