namespace TradePilot.Application.Agent.Models;

/// <summary>
/// Configuration for serving installer files. Bound from appsettings "Installer" section.
/// When <see cref="BlobConnectionString"/> is set, files are served from Azure Blob Storage.
/// Otherwise falls back to the local <see cref="InstallerDirectory"/>.
/// </summary>
public sealed class InstallerOptions
{
    public const string SectionName = "Installer";

    /// <summary>
    /// Absolute or relative path to the directory containing installer artifacts (local dev only).
    /// </summary>
    public string InstallerDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Azure Blob Storage connection string. When set, the blob store is used instead of local disk.
    /// </summary>
    public string? BlobConnectionString { get; set; }

    /// <summary>
    /// Blob container name for installer artifacts.
    /// </summary>
    public string BlobContainerName { get; set; } = "installers";
}
