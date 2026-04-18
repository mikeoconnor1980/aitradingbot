using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using TradePilot.Worker.Services;

namespace TradePilot.Worker.Tests.Services;

[TestClass]
public sealed class ControlPlaneFearGreedSnapshotProviderTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock = new();
    private readonly Mock<ILogger<ControlPlaneFearGreedSnapshotProvider>> _loggerMock = new();

    [TestMethod]
    public async Task GivenControlPlaneStatus_WhenGetLatestAsync_ThenReturnsMappedSnapshot()
    {
        var timestamp = new DateTimeOffset(2026, 4, 18, 0, 0, 0, TimeSpan.Zero);
        var responseBody = """
            {
              "latestValue": 26,
              "latestClassification": "Fear",
              "latestTimestamp": "2026-04-18T00:00:00+00:00",
              "totalReadings": 2,
              "earliestTimestamp": "2026-04-17T00:00:00+00:00"
            }
            """;

        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        });

        _httpClientFactoryMock
            .Setup(factory => factory.CreateClient(AgentCheckInService.HttpClientName))
            .Returns(new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost:5062")
            });

        var sut = new ControlPlaneFearGreedSnapshotProvider(
            _httpClientFactoryMock.Object,
            _loggerMock.Object);

        var snapshot = await sut.GetLatestAsync();

        snapshot.Should().NotBeNull();
        snapshot!.Value.Should().Be(26);
        snapshot.Classification.Should().Be(TradePilot.Application.Trading.Models.FearGreedClassification.Fear);
        snapshot.TimestampUtc.Should().Be(timestamp.ToUnixTimeSeconds());
    }

    [TestMethod]
    public async Task GivenControlPlaneStatusWithoutLatestReading_WhenGetLatestAsync_ThenReturnsNull()
    {
        var responseBody = """
            {
              "latestValue": null,
              "latestClassification": null,
              "latestTimestamp": null,
              "totalReadings": 0,
              "earliestTimestamp": null
            }
            """;

        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        });

        _httpClientFactoryMock
            .Setup(factory => factory.CreateClient(AgentCheckInService.HttpClientName))
            .Returns(new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost:5062")
            });

        var sut = new ControlPlaneFearGreedSnapshotProvider(
            _httpClientFactoryMock.Object,
            _loggerMock.Object);

        var snapshot = await sut.GetLatestAsync();

        snapshot.Should().BeNull();
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responseFactory(request));
        }
    }
}