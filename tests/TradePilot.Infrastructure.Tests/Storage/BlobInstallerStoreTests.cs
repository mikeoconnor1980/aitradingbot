using Azure.Core;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradePilot.Application.Agent.Models;
using TradePilot.Infrastructure.Storage;

namespace TradePilot.Infrastructure.Tests.Storage;

[TestClass]
public sealed class BlobInstallerStoreTests
{
    [TestMethod]
    public void GivenVersionedSetupFileName_WhenNormalizingBlobName_ThenUsesVersionPrefix()
    {
        var blobName = BlobInstallerStore.NormalizeBlobName("TradePilot-ExecutionAgent-v0.1.0-Setup.exe");

        blobName.Should().Be("v0.1.0/TradePilot-ExecutionAgent-v0.1.0-Setup.exe");
    }

    [TestMethod]
    public void GivenVersionedZipOrShaFileName_WhenNormalizingBlobName_ThenUsesVersionPrefix()
    {
        BlobInstallerStore.NormalizeBlobName("TradePilot-ExecutionAgent-v0.1.0-win-x64.zip")
            .Should().Be("v0.1.0/TradePilot-ExecutionAgent-v0.1.0-win-x64.zip");

        BlobInstallerStore.NormalizeBlobName("TradePilot-ExecutionAgent-v0.1.0-Setup.exe.sha256")
            .Should().Be("v0.1.0/TradePilot-ExecutionAgent-v0.1.0-Setup.exe.sha256");
    }

    [TestMethod]
    public void GivenManifestOrNestedBlobName_WhenNormalizingBlobName_ThenLeavesNameUnchanged()
    {
        BlobInstallerStore.NormalizeBlobName("latest.json")
            .Should().Be("latest.json");

        BlobInstallerStore.NormalizeBlobName("v0.1.0/TradePilot-ExecutionAgent-v0.1.0-Setup.exe")
            .Should().Be("v0.1.0/TradePilot-ExecutionAgent-v0.1.0-Setup.exe");
    }

    [TestMethod]
    public void GivenBlobServiceUri_WhenStoreIsCreated_ThenUsesManagedIdentityConfiguration()
    {
        Action act = () => CreateStore(new BlobClientOptions());

        act.Should().NotThrow();
    }

    [TestMethod]
    public void GivenMissingBlobServiceUri_WhenStoreIsCreated_ThenThrowsConfigurationError()
    {
        var options = Options.Create(new InstallerOptions
        {
            BlobContainerName = "installers"
        });

        Action act = () => new BlobInstallerStore(
            options,
            NullLogger<BlobInstallerStore>.Instance,
            new BlobClientOptions(),
            new FakeTokenCredential());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Installer:BlobServiceUri*");
    }

    private static BlobInstallerStore CreateStore(BlobClientOptions blobClientOptions)
    {
        var options = Options.Create(new InstallerOptions
        {
            BlobServiceUri = "https://unitstorage.blob.core.windows.net/",
            BlobContainerName = "installers"
        });

        return new BlobInstallerStore(
            options,
            NullLogger<BlobInstallerStore>.Instance,
            blobClientOptions,
            new FakeTokenCredential());
    }

    private sealed class FakeTokenCredential : TokenCredential
    {
        private static readonly AccessToken AccessToken = new("token", DateTimeOffset.UtcNow.AddHours(1));

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => AccessToken;

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => ValueTask.FromResult(AccessToken);
    }
}