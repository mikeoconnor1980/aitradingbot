using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradePilot.Application.Agent;
using TradePilot.Application.Agent.Models;

namespace TradePilot.Infrastructure.Storage;

/// <summary>
/// Reads installer artifacts from Azure Blob Storage.
/// </summary>
public sealed class BlobInstallerStore : IInstallerStore
{
    private readonly BlobContainerClient _container;
    private readonly ILogger<BlobInstallerStore> _logger;

    public BlobInstallerStore(
        IOptions<InstallerOptions> options,
        ILogger<BlobInstallerStore> logger)
    {
        _logger = logger;
        var connectionString = options.Value.BlobConnectionString
            ?? throw new InvalidOperationException("Installer:BlobConnectionString is not configured.");
        var containerName = options.Value.BlobContainerName;
        _container = new BlobContainerClient(connectionString, containerName);
    }

    public async Task<InstallerFileInfo> GetFileInfoAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var blob = _container.GetBlobClient(fileName);

        try
        {
            var props = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);
            return new InstallerFileInfo(true, props.Value.ContentLength);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return new InstallerFileInfo(false, null);
        }
    }

    public async Task<Stream?> OpenReadAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var blob = _container.GetBlobClient(fileName);

        try
        {
            return await blob.OpenReadAsync(cancellationToken: cancellationToken);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Installer blob '{FileName}' not found in container '{Container}'.",
                fileName, _container.Name);
            return null;
        }
    }
}
