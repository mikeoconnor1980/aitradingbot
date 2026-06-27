namespace TradePilot.Application.Agent;

using TradePilot.Application.Agent.Models;

/// <summary>
/// Abstraction for reading installer artifacts from either local disk or Azure Blob Storage.
/// </summary>
public interface IInstallerStore
{
    /// <summary>
    /// Load the latest installer release manifest.
    /// Returns null when no manifest is available.
    /// </summary>
    Task<InstallerReleaseManifest?> GetLatestReleaseManifestAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check whether the given installer file exists and return its size.
    /// </summary>
    Task<InstallerFileInfo> GetFileInfoAsync(string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Open a read stream for the given installer file.
    /// Returns null if the file does not exist.
    /// </summary>
    Task<Stream?> OpenReadAsync(string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Open a read stream for a file referenced by the release manifest.
    /// Returns null if the file does not exist.
    /// </summary>
    Task<Stream?> OpenReadAsync(InstallerReleaseFile releaseFile, CancellationToken cancellationToken = default);
}
