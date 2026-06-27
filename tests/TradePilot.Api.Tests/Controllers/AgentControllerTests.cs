using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TradePilot.Api.Controllers;
using TradePilot.Application.Agent;
using TradePilot.Api.Tests.Infrastructure;
using TradePilot.Application.Agent.Models;
using TradePilot.Application.Agent.Services;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Serialization;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;

namespace TradePilot.Api.Tests.Controllers;

[TestClass]
public sealed class AgentControllerTests : BaseControllerTests
{
    private const string AgentId = "worker-1";
    private static readonly Guid TestUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly TestInstallerStore _installerStore = new();
    private AgentUpdateOptions _agentUpdateOptions = new()
    {
        MinimumVersion = "0.0.0"
    };

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<AgentCommandStore>();
        services.AddSingleton(new AgentCommandStore());
        services.RemoveAll<IInstallerStore>();
        services.AddSingleton<IInstallerStore>(_installerStore);
        services.PostConfigure<AgentUpdateOptions>(options =>
        {
            options.LatestVersion = _agentUpdateOptions.LatestVersion;
            options.DownloadUrl = _agentUpdateOptions.DownloadUrl;
            options.Sha256Hash = _agentUpdateOptions.Sha256Hash;
            options.MinimumVersion = _agentUpdateOptions.MinimumVersion;
            options.ReleaseNotes = _agentUpdateOptions.ReleaseNotes;
        });
    }

    [TestMethod]
    public async Task GivenManifestBackedInstaller_WhenGettingInstallerInfo_ThenReturnsManifestMetadataAndAvailableStatus()
    {
        var manifest = CreateManifest();
        _installerStore.Manifest = manifest;
        _installerStore.AddFile(manifest.Files["exe"], [1, 2, 3]);
        _installerStore.AddFile(manifest.Files["zip"], [4, 5, 6, 7]);

        await SeedActiveSubscriptionAsync();
        var client = GetTestClient(userId: TestUserId.ToString(), email: "subscriber@tradepilot.dev");

        var response = await client.GetAsync("api/agent/installer/info");

        var payload = await response.ReadAndAssertSuccessAsync<InstallerInfoResponse>();
        payload.Status.Should().Be("Available");
        payload.Version.Should().Be("1.2.3");
        payload.ExeAvailable.Should().BeTrue();
        payload.ZipAvailable.Should().BeTrue();
        payload.ExeFileName.Should().Be(manifest.Files["exe"].FileName);
        payload.ZipFileName.Should().Be(manifest.Files["zip"].FileName);
        payload.ExeFileSizeBytes.Should().Be(3);
        payload.ZipFileSizeBytes.Should().Be(4);
        payload.Sha256Hash.Should().Be(manifest.Files["exe"].Sha256);
        payload.ExeSha256Hash.Should().Be(manifest.Files["exe"].Sha256);
        payload.ZipSha256Hash.Should().Be(manifest.Files["zip"].Sha256);
        payload.MinimumSupportedVersion.Should().Be("1.0.0");
    }

    [TestMethod]
    public async Task GivenManifestWithMissingBlob_WhenGettingInstallerInfo_ThenReturnsManifestFoundBlobMissingStatus()
    {
        var manifest = CreateManifest();
        _installerStore.Manifest = manifest;
        _installerStore.AddFile(manifest.Files["exe"], [1, 2, 3]);

        await SeedActiveSubscriptionAsync();
        var client = GetTestClient(userId: TestUserId.ToString(), email: "subscriber@tradepilot.dev");

        var response = await client.GetAsync("api/agent/installer/info");

        var payload = await response.ReadAndAssertSuccessAsync<InstallerInfoResponse>();
        payload.Status.Should().Be("ManifestFoundBlobMissing");
        payload.ExeAvailable.Should().BeTrue();
        payload.ZipAvailable.Should().BeFalse();
        payload.ZipFileName.Should().Be(manifest.Files["zip"].FileName);
    }

    [TestMethod]
    public async Task GivenNoManifestAndNoFallback_WhenGettingInstallerInfo_ThenReturnsNoManifestStatus()
    {
        _agentUpdateOptions = new AgentUpdateOptions
        {
            MinimumVersion = "0.0.0"
        };

        await SeedActiveSubscriptionAsync();
        var client = GetTestClient(userId: TestUserId.ToString(), email: "subscriber@tradepilot.dev");

        var response = await client.GetAsync("api/agent/installer/info");

        var payload = await response.ReadAndAssertSuccessAsync<InstallerInfoResponse>();
        payload.Status.Should().Be("NoManifest");
        payload.Version.Should().BeEmpty();
        payload.ExeAvailable.Should().BeFalse();
        payload.ZipAvailable.Should().BeFalse();
    }

    [TestMethod]
    public async Task GivenManifestBackedInstaller_WhenDownloadingZip_ThenStreamsManifestSelectedFile()
    {
        var manifest = CreateManifest();
        var zipPayload = new byte[] { 8, 9, 10, 11 };
        _installerStore.Manifest = manifest;
        _installerStore.AddFile(manifest.Files["exe"], [1, 2, 3]);
        _installerStore.AddFile(manifest.Files["zip"], zipPayload);

        await SeedActiveSubscriptionAsync();
        var client = GetTestClient(userId: TestUserId.ToString(), email: "subscriber@tradepilot.dev");

        var response = await client.GetAsync("api/agent/installer/download?format=zip");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/zip");
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(zipPayload);
    }

    [TestMethod]
    public async Task GivenManifestBackedInstaller_WhenHeartbeatHasOlderVersion_ThenReturnsManifestUpdateMetadata()
    {
        var manifest = CreateManifest();
        _installerStore.Manifest = manifest;
        _installerStore.AddFile(manifest.Files["exe"], [1, 2, 3]);
        _installerStore.AddFile(manifest.Files["zip"], [4, 5, 6]);

        var client = GetTestClient(authenticate: false);

        var response = await client.PostAsJsonAsync("api/agent/heartbeat", new AgentHeartbeat
        {
            AgentId = AgentId,
            State = AgentState.Idle,
            MachineName = "test-machine",
            AgentVersion = "1.0.0",
            TimestampUtc = DateTimeOffset.UtcNow,
        });

        var payload = await response.ReadAndAssertSuccessAsync<HeartbeatResponse>();
        payload.UpdateAvailable.Should().BeTrue();
        payload.LatestVersion.Should().Be("1.2.3");
        payload.UpdateDownloadUrl.Should().StartWith(client.BaseAddress!.ToString());
        payload.UpdateDownloadUrl.Should().Contain("/api/agent/installer/worker-download?format=exe");
        payload.UpdateDownloadUrl.Should().Contain("token=");
        payload.UpdateSha256Hash.Should().Be(manifest.Files["exe"].Sha256);
    }

    [TestMethod]
    public async Task GivenManifestBackedInstaller_WhenGettingLatestUpdate_ThenReturnsAbsoluteDownloadUrl()
    {
        var manifest = CreateManifest();
        _installerStore.Manifest = manifest;
        _installerStore.AddFile(manifest.Files["exe"], [1, 2, 3]);
        _installerStore.AddFile(manifest.Files["zip"], [4, 5, 6]);

        var client = GetTestClient(authenticate: false);

        var response = await client.GetAsync("api/agent/update/latest");

        var payload = await response.ReadAndAssertSuccessAsync<AgentUpdateInfo>();
        payload.Version.Should().Be("1.2.3");
        payload.DownloadUrl.Should().StartWith(client.BaseAddress!.ToString());
        payload.DownloadUrl.Should().Contain("/api/agent/installer/worker-download?format=exe");
        payload.DownloadUrl.Should().Contain("token=");
        payload.Sha256Hash.Should().Be(manifest.Files["exe"].Sha256);
    }

    [TestMethod]
    public async Task GivenUnauthenticatedBrowser_WhenGettingInstallerInfo_ThenReturnsUnauthorized()
    {
        var client = GetTestClient(authenticate: false);

        var response = await client.GetAsync("api/agent/installer/info");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task GivenUnauthenticatedBrowser_WhenDownloadingInstaller_ThenReturnsUnauthorized()
    {
        var client = GetTestClient(authenticate: false);

        var response = await client.GetAsync("api/agent/installer/download?format=exe");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task GivenAuthenticatedUserWithoutSubscription_WhenDownloadingInstaller_ThenReturnsForbidden()
    {
        var manifest = CreateManifest();
        _installerStore.Manifest = manifest;
        _installerStore.AddFile(manifest.Files["exe"], [1, 2, 3]);

        var client = GetTestClient(userId: TestUserId.ToString(), email: "subscriber@tradepilot.dev");

        var response = await client.GetAsync("api/agent/installer/download?format=exe");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task GivenHeartbeatWorkerDownloadToken_WhenDownloadingInstaller_ThenStreamsFileAnonymously()
    {
        var manifest = CreateManifest();
        var exePayload = new byte[] { 1, 3, 5, 7 };
        _installerStore.Manifest = manifest;
        _installerStore.AddFile(manifest.Files["exe"], exePayload);
        _installerStore.AddFile(manifest.Files["zip"], [2, 4]);

        var client = GetTestClient(authenticate: false);

        var heartbeatResponse = await client.PostAsJsonAsync("api/agent/heartbeat", new AgentHeartbeat
        {
            AgentId = AgentId,
            State = AgentState.Idle,
            MachineName = "test-machine",
            AgentVersion = "1.0.0",
            TimestampUtc = DateTimeOffset.UtcNow,
        });

        var heartbeat = await heartbeatResponse.ReadAndAssertSuccessAsync<HeartbeatResponse>();
        heartbeat.UpdateDownloadUrl.Should().NotBeNullOrWhiteSpace();

        var downloadResponse = await client.GetAsync(heartbeat.UpdateDownloadUrl!);

        downloadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await downloadResponse.Content.ReadAsByteArrayAsync()).Should().Equal(exePayload);
    }

    [TestMethod]
    public async Task GivenRunningAssignedStrategy_WhenIdleHeartbeat_ThenReturnsAutoResumeStartCommand()
    {
        var strategy = await SeedRunningStrategyAsync();
        var client = GetTestClient(authenticate: false);

        var response = await client.PostAsJsonAsync("api/agent/heartbeat", new AgentHeartbeat
        {
            AgentId = AgentId,
            State = AgentState.Idle,
            MachineName = "test-machine",
            TimestampUtc = DateTimeOffset.UtcNow,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        payload.Should().NotBeNull();

        var commands = payload!["pendingCommands"]!.AsArray();
        commands.Should().HaveCount(1);
        commands[0]!["strategyId"]!.GetValue<Guid>().Should().Be(strategy.Id);
    }

    [TestMethod]
    public async Task GivenStoppedStrategy_WhenIdleHeartbeat_ThenDoesNotAutoResume()
    {
        await SeedStoppedStrategyAsync();
        var client = GetTestClient(authenticate: false);

        var response = await client.PostAsJsonAsync("api/agent/heartbeat", new AgentHeartbeat
        {
            AgentId = AgentId,
            State = AgentState.Idle,
            MachineName = "test-machine",
            TimestampUtc = DateTimeOffset.UtcNow,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        payload.Should().NotBeNull();
        payload!["pendingCommands"]!.AsArray().Should().BeEmpty();
    }

    private async Task<Strategy> SeedRunningStrategyAsync()
    {
        await using var db = CreateTestDbContext();
        var config = CreateGridConfig();
        var strategy = Strategy.Create(
            "dev-user",
            config.StrategyName,
            "GridStrategy",
            JsonSerializer.Serialize(config, StrategyJsonOptions.Default));

        strategy.AssignToAgentAndStart(AgentId);
        db.Strategies.Add(strategy);
        await db.SaveChangesAsync();
        return strategy;
    }

    private async Task SeedStoppedStrategyAsync()
    {
        await using var db = CreateTestDbContext();
        var config = CreateGridConfig();
        var strategy = Strategy.Create(
            "dev-user",
            config.StrategyName,
            "GridStrategy",
            JsonSerializer.Serialize(config, StrategyJsonOptions.Default));

        strategy.AssignToAgentAndStart(AgentId);
        strategy.StopLiveTrading();
        db.Strategies.Add(strategy);
        await db.SaveChangesAsync();
    }

    private static StrategyConfig CreateGridConfig() => new()
    {
        SchemaVersion = 1,
        StrategyMode = StrategyMode.Grid,
        StrategyName = "BTC Grid",
        Exchange = "Hyperliquid",
        Market = "BTC-USD",
        Timeframe = "15m",
        Direction = Direction.Long,
        Grid = new GridConfig
        {
            Levels = 5,
            Spacing = 0.5m,
            BreakdownThreshold = 2m,
        },
        Exit = new ExitConfig(),
        Risk = new RiskConfig
        {
            PositionSizeType = PositionSizeType.FixedNotional,
            PositionSizeValue = 100m,
            Leverage = 1m,
            MaxOpenTrades = 1,
        },
    };

    private static InstallerReleaseManifest CreateManifest() => new()
    {
        Version = "1.2.3",
        PublishedAtUtc = DateTimeOffset.Parse("2026-06-27T06:00:00Z"),
        ReleaseNotes = "Manifest release",
        MinimumSupportedVersion = "1.0.0",
        Files = new Dictionary<string, InstallerReleaseFile>(StringComparer.OrdinalIgnoreCase)
        {
            ["exe"] = new InstallerReleaseFile
            {
                FileName = "TradePilot-ExecutionAgent-v1.2.3-Setup.exe",
                BlobName = "v1.2.3/TradePilot-ExecutionAgent-v1.2.3-Setup.exe",
                ContentType = "application/octet-stream",
                SizeBytes = 3,
                Sha256 = "exehash"
            },
            ["zip"] = new InstallerReleaseFile
            {
                FileName = "TradePilot-ExecutionAgent-v1.2.3-win-x64.zip",
                BlobName = "v1.2.3/TradePilot-ExecutionAgent-v1.2.3-win-x64.zip",
                ContentType = "application/zip",
                SizeBytes = 4,
                Sha256 = "ziphash"
            }
        }
    };

    private async Task SeedActiveSubscriptionAsync()
    {
        await using var db = CreateTestDbContext();

        if (await db.Users.FindAsync(TestUserId) is null)
        {
            var user = User.Create("subscriber@tradepilot.dev", "Subscriber", "password-hash");
            typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, TestUserId);
            db.Users.Add(user);
        }

        if (!await db.Subscriptions.AnyAsync(s => s.UserId == TestUserId))
        {
            db.Subscriptions.Add(Subscription.Create(TestUserId, SubscriptionTier.Pro, Subscription.TrialDurationDays));
        }

        await db.SaveChangesAsync();
    }

    private sealed class TestInstallerStore : IInstallerStore
    {
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

        public InstallerReleaseManifest? Manifest { get; set; }

        public void AddFile(InstallerReleaseFile releaseFile, byte[] content)
        {
            _files[releaseFile.FileName] = content;

            if (!string.IsNullOrWhiteSpace(releaseFile.BlobName))
            {
                _files[releaseFile.BlobName] = content;
            }
        }

        public Task<InstallerReleaseManifest?> GetLatestReleaseManifestAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Manifest);

        public Task<InstallerFileInfo> GetFileInfoAsync(string fileName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_files.TryGetValue(fileName, out var content)
                ? new InstallerFileInfo(true, content.LongLength)
                : new InstallerFileInfo(false, null));
        }

        public Task<Stream?> OpenReadAsync(string fileName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Stream?>(_files.TryGetValue(fileName, out var content)
                ? new MemoryStream(content, writable: false)
                : null);
        }

        public Task<Stream?> OpenReadAsync(InstallerReleaseFile releaseFile, CancellationToken cancellationToken = default)
        {
            var fileName = !string.IsNullOrWhiteSpace(releaseFile.BlobName)
                ? releaseFile.BlobName
                : releaseFile.FileName;

            return OpenReadAsync(fileName, cancellationToken);
        }
    }
}
