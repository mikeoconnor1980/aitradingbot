using System.Text.Json;
using Microsoft.Extensions.Options;
using TradePilot.Application.Agent;
using TradePilot.Application.Agent.Models;

namespace TradePilot.Infrastructure.Storage;

/// <summary>
/// Reads installer artifacts from the local filesystem (development only).
/// </summary>
public sealed class LocalInstallerStore : IInstallerStore
{
    private readonly string? _directory;

    public LocalInstallerStore(IOptions<InstallerOptions> options)
    {
        var dir = options.Value.InstallerDirectory;
        if (!string.IsNullOrEmpty(dir))
        {
            _directory = Path.IsPathRooted(dir)
                ? dir
                : Path.GetFullPath(dir);
        }
    }

    public async Task<InstallerReleaseManifest?> GetLatestReleaseManifestAsync(CancellationToken cancellationToken = default)
    {
        var filePath = ResolveExistingPath("latest.json");
        if (filePath is null)
        {
            return null;
        }

        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<InstallerReleaseManifest>(stream, cancellationToken: cancellationToken);
    }

    public Task<InstallerFileInfo> GetFileInfoAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var filePath = ResolveExistingPath(fileName);
        if (filePath is null)
            return Task.FromResult(new InstallerFileInfo(false, null));

        var info = new FileInfo(filePath);
        return Task.FromResult(new InstallerFileInfo(true, info.Length));
    }

    public Task<Stream?> OpenReadAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var filePath = ResolveExistingPath(fileName);
        if (filePath is null)
            return Task.FromResult<Stream?>(null);

        return Task.FromResult<Stream?>(File.OpenRead(filePath));
    }

    public Task<Stream?> OpenReadAsync(InstallerReleaseFile releaseFile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(releaseFile);

        var fileName = !string.IsNullOrWhiteSpace(releaseFile.BlobName)
            ? releaseFile.BlobName
            : releaseFile.FileName;

        return OpenReadAsync(fileName, cancellationToken);
    }

    private string? ResolveExistingPath(string fileName)
    {
        if (_directory is null)
        {
            return null;
        }

        var requestedPath = ResolveSafePath(fileName);
        if (requestedPath is not null && File.Exists(requestedPath))
        {
            return requestedPath;
        }

        if (fileName.Contains('/') || fileName.Contains('\\'))
        {
            var fallbackPath = ResolveSafePath(Path.GetFileName(fileName));
            if (fallbackPath is not null && File.Exists(fallbackPath))
            {
                return fallbackPath;
            }
        }

        return null;
    }

    private string? ResolveSafePath(string fileName)
    {
        var directory = _directory;
        if (directory is null)
        {
            return null;
        }

        var filePath = Path.Combine(directory, fileName);

        // Prevent path traversal
        return Path.GetFullPath(filePath).StartsWith(directory, StringComparison.OrdinalIgnoreCase)
            ? filePath
            : null;
    }
}
