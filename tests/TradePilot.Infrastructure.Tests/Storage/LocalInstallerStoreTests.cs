using Microsoft.Extensions.Options;
using TradePilot.Application.Agent.Models;
using TradePilot.Infrastructure.Storage;

namespace TradePilot.Infrastructure.Tests.Storage;

[TestClass]
public sealed class LocalInstallerStoreTests
{
    [TestMethod]
    public async Task GivenLatestManifestFile_WhenReadingReleaseManifest_ThenReturnsManifest()
    {
        var tempDirectory = CreateTempDirectory();

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(tempDirectory, "latest.json"),
                """
                {
                  "version": "1.2.3",
                  "publishedAtUtc": "2026-06-27T06:00:00Z",
                  "releaseNotes": "Test release",
                  "minimumSupportedVersion": "1.0.0",
                  "files": {
                    "exe": {
                      "filename": "TradePilot-ExecutionAgent-v1.2.3-Setup.exe",
                      "blobName": "v1.2.3/TradePilot-ExecutionAgent-v1.2.3-Setup.exe",
                      "contentType": "application/octet-stream",
                      "sizeBytes": 123,
                      "sha256": "abc"
                    }
                  },
                  "artifacts": {}
                }
                """);

            var store = CreateStore(tempDirectory);

            var manifest = await store.GetLatestReleaseManifestAsync();

            manifest.Should().NotBeNull();
            manifest!.Version.Should().Be("1.2.3");
            manifest.Files["exe"].BlobName.Should().Be("v1.2.3/TradePilot-ExecutionAgent-v1.2.3-Setup.exe");
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task GivenManifestFileDescriptor_WhenOpeningRead_ThenUsesLocalFileNameFallback()
    {
        var tempDirectory = CreateTempDirectory();

        try
        {
            var fileName = "TradePilot-ExecutionAgent-v1.2.3-Setup.exe";
            var payload = new byte[] { 1, 2, 3, 4 };
            await File.WriteAllBytesAsync(Path.Combine(tempDirectory, fileName), payload);

            var store = CreateStore(tempDirectory);

            await using var stream = await store.OpenReadAsync(new InstallerReleaseFile
            {
                FileName = fileName,
                BlobName = $"v1.2.3/{fileName}",
                ContentType = "application/octet-stream",
                SizeBytes = payload.Length,
                Sha256 = "abc"
            });

            stream.Should().NotBeNull();
            using var memoryStream = new MemoryStream();
            await stream!.CopyToAsync(memoryStream);
            memoryStream.ToArray().Should().Equal(payload);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static LocalInstallerStore CreateStore(string installerDirectory)
    {
        return new LocalInstallerStore(Options.Create(new InstallerOptions
        {
            InstallerDirectory = installerDirectory
        }));
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"tradepilot-installer-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}