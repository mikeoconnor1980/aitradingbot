using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Identity;
using Azure.Core;
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
    private static readonly Regex VersionedInstallerFileNamePattern = new(
        "^TradePilot-ExecutionAgent-v(?<version>.+?)-(?:(?:Setup\\.exe(?:\\.sha256)?)|(?:win-x64\\.zip))$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly BlobContainerClient _container;
    private readonly ILogger<BlobInstallerStore> _logger;

    public BlobInstallerStore(
        IOptions<InstallerOptions> options,
        ILogger<BlobInstallerStore> logger)
        : this(options, logger, new BlobClientOptions(), new DefaultAzureCredential())
    {
    }

    internal BlobInstallerStore(
        IOptions<InstallerOptions> options,
        ILogger<BlobInstallerStore> logger,
        BlobClientOptions blobClientOptions,
        TokenCredential tokenCredential)
    {
        _logger = logger;
        var blobServiceUri = options.Value.BlobServiceUri
            ?? throw new InvalidOperationException("Installer:BlobServiceUri is not configured.");
        var containerName = options.Value.BlobContainerName;
        var serviceClient = new BlobServiceClient(new Uri(blobServiceUri), tokenCredential, blobClientOptions);
        _container = serviceClient.GetBlobContainerClient(containerName);
    }

    public async Task<InstallerReleaseManifest?> GetLatestReleaseManifestAsync(CancellationToken cancellationToken = default)
    {
        await using var stream = await OpenReadAsync("latest.json", cancellationToken);
        if (stream is null)
        {
            return null;
        }

        var manifest = await JsonSerializer.DeserializeAsync<InstallerReleaseManifest>(stream, cancellationToken: cancellationToken);
        if (manifest is null)
        {
            _logger.LogWarning("Installer manifest 'latest.json' in container '{Container}' was empty.", _container.Name);
        }

        return manifest;
    }

    public async Task<InstallerFileInfo> GetFileInfoAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var blobName = NormalizeBlobName(fileName);
        var blob = _container.GetBlobClient(blobName);

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
        var blobName = NormalizeBlobName(fileName);
        var blob = _container.GetBlobClient(blobName);

        try
        {
            return await blob.OpenReadAsync(cancellationToken: cancellationToken);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Installer blob '{FileName}' not found in container '{Container}'.",
                blobName, _container.Name);
            return null;
        }
    }

    public Task<Stream?> OpenReadAsync(InstallerReleaseFile releaseFile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(releaseFile);

        var blobName = !string.IsNullOrWhiteSpace(releaseFile.BlobName)
            ? releaseFile.BlobName
            : releaseFile.FileName;

        return OpenReadAsync(blobName, cancellationToken);
    }

    internal static string NormalizeBlobName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return fileName;
        }

        if (fileName.Contains('/') || fileName.Contains('\\'))
        {
            return fileName;
        }

        if (string.Equals(fileName, "latest.json", StringComparison.OrdinalIgnoreCase))
        {
            return fileName;
        }

        var match = VersionedInstallerFileNamePattern.Match(fileName);
        if (!match.Success)
        {
            return fileName;
        }

        var version = match.Groups["version"].Value;
        return $"v{version}/{fileName}";
    }
}
