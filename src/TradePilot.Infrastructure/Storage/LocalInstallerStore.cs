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

    public Task<InstallerFileInfo> GetFileInfoAsync(string fileName, CancellationToken cancellationToken = default)
    {
        if (_directory is null)
            return Task.FromResult(new InstallerFileInfo(false, null));

        var filePath = Path.Combine(_directory, fileName);

        // Prevent path traversal
        if (!Path.GetFullPath(filePath).StartsWith(_directory, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(new InstallerFileInfo(false, null));

        if (!File.Exists(filePath))
            return Task.FromResult(new InstallerFileInfo(false, null));

        var info = new FileInfo(filePath);
        return Task.FromResult(new InstallerFileInfo(true, info.Length));
    }

    public Task<Stream?> OpenReadAsync(string fileName, CancellationToken cancellationToken = default)
    {
        if (_directory is null)
            return Task.FromResult<Stream?>(null);

        var filePath = Path.Combine(_directory, fileName);

        // Prevent path traversal
        if (!Path.GetFullPath(filePath).StartsWith(_directory, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<Stream?>(null);

        if (!File.Exists(filePath))
            return Task.FromResult<Stream?>(null);

        return Task.FromResult<Stream?>(File.OpenRead(filePath));
    }
}
