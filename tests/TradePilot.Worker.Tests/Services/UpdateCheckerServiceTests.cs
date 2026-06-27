using System.Net;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using TradePilot.Application.Agent.Models;
using TradePilot.Worker.Services;

namespace TradePilot.Worker.Tests.Services;

[TestClass]
public sealed class UpdateCheckerServiceTests
{
    [TestMethod]
    public async Task GivenRelativeDownloadUrl_WhenProcessingUpdate_ThenResolvesAgainstConfiguredBaseAddress()
    {
        var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Encoding.UTF8.GetBytes("installer-binary"))
        });
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://control-plane.test/")
        };
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(factory => factory.CreateClient(UpdateCheckerService.UpdateDownloadHttpClientName))
            .Returns(client);

        var sut = new UpdateCheckerService(
            httpClientFactory.Object,
            CreateSafeHealthProvider().Object,
            NullLogger<UpdateCheckerService>.Instance);

        await InvokeProcessUpdateAsync(
            sut,
            version: "1.2.3",
            downloadUrl: "/api/agent/installer/download?format=exe",
            sha256Hash: "expected-mismatch");

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.RequestUri.Should().Be(new Uri("https://control-plane.test/api/agent/installer/download?format=exe"));
        sut.CurrentState.Should().Be(UpdateState.Failed);
    }

    [TestMethod]
    public async Task GivenMissingSha256Hash_WhenProcessingUpdate_ThenFailsBeforeDownloading()
    {
        var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Encoding.UTF8.GetBytes("installer-binary"))
        });
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://control-plane.test/")
        };
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(factory => factory.CreateClient(UpdateCheckerService.UpdateDownloadHttpClientName))
            .Returns(client);

        var sut = new UpdateCheckerService(
            httpClientFactory.Object,
            CreateSafeHealthProvider().Object,
            NullLogger<UpdateCheckerService>.Instance);

        await InvokeProcessUpdateAsync(
            sut,
            version: "1.2.3",
            downloadUrl: "/api/agent/installer/download?format=exe",
            sha256Hash: string.Empty);

        handler.LastRequest.Should().BeNull();
        sut.CurrentState.Should().Be(UpdateState.Failed);
    }

    [TestMethod]
    public void GivenActiveTradingSession_WhenIsSafeToUpdate_ThenReturnsFalse()
    {
        var healthProvider = new Mock<ITradingHealthProvider>();
        healthProvider
            .Setup(provider => provider.GetSnapshot())
            .Returns(new TradingHealthSnapshot(
                IsWebSocketConnected: false,
                IsTradingSessionActive: true,
                LastTradeReceived: null,
                LastCandleClosed: null,
                ServiceStartedUtc: DateTimeOffset.UtcNow,
                TradingSessionStartedUtc: DateTimeOffset.UtcNow.AddMinutes(-10),
                Uptime: TimeSpan.FromHours(1),
                TradingSessionUptime: TimeSpan.FromMinutes(10),
                TimeSinceLastTrade: null,
                TimeSinceLastCandle: null));

        var sut = new UpdateCheckerService(
            Mock.Of<IHttpClientFactory>(),
            healthProvider.Object,
            NullLogger<UpdateCheckerService>.Instance);

        sut.IsSafeToUpdate().Should().BeFalse();
    }

    [TestMethod]
    public void GivenRecentTradeWithoutActiveSession_WhenIsSafeToUpdate_ThenReturnsFalse()
    {
        var healthProvider = new Mock<ITradingHealthProvider>();
        healthProvider
            .Setup(provider => provider.GetSnapshot())
            .Returns(new TradingHealthSnapshot(
                IsWebSocketConnected: true,
                IsTradingSessionActive: false,
                LastTradeReceived: DateTimeOffset.UtcNow.AddMinutes(-1),
                LastCandleClosed: null,
                ServiceStartedUtc: DateTimeOffset.UtcNow,
                TradingSessionStartedUtc: null,
                Uptime: TimeSpan.FromHours(1),
                TradingSessionUptime: null,
                TimeSinceLastTrade: TimeSpan.FromMinutes(1),
                TimeSinceLastCandle: null));

        var sut = new UpdateCheckerService(
            Mock.Of<IHttpClientFactory>(),
            healthProvider.Object,
            NullLogger<UpdateCheckerService>.Instance);

        sut.IsSafeToUpdate().Should().BeFalse();
    }

    private static async Task InvokeProcessUpdateAsync(
        UpdateCheckerService sut,
        string version,
        string downloadUrl,
        string sha256Hash)
    {
        var updateInfoType = typeof(UpdateCheckerService).GetNestedType("UpdateInfo", BindingFlags.Instance | BindingFlags.NonPublic);
        updateInfoType.Should().NotBeNull();

        var updateInfo = Activator.CreateInstance(
            updateInfoType!,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            args: [version, downloadUrl, sha256Hash],
            culture: null);
        updateInfo.Should().NotBeNull();

        var method = typeof(UpdateCheckerService).GetMethod("ProcessUpdateAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        var task = method!.Invoke(sut, [updateInfo!, CancellationToken.None]) as Task;
        task.Should().NotBeNull();
        await task!;
    }

    private static Mock<ITradingHealthProvider> CreateSafeHealthProvider()
    {
        var healthProvider = new Mock<ITradingHealthProvider>();
        healthProvider
            .Setup(provider => provider.GetSnapshot())
            .Returns(new TradingHealthSnapshot(
                IsWebSocketConnected: false,
                IsTradingSessionActive: false,
                LastTradeReceived: null,
                LastCandleClosed: null,
                ServiceStartedUtc: DateTimeOffset.UtcNow,
                TradingSessionStartedUtc: null,
                Uptime: TimeSpan.FromHours(1),
                TradingSessionUptime: null,
                TimeSinceLastTrade: null,
                TimeSinceLastCandle: null));

        return healthProvider;
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public CapturingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_responseFactory(request));
        }
    }
}