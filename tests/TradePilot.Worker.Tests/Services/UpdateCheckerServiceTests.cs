using Microsoft.Extensions.Logging.Abstractions;
using TradePilot.Worker.Services;

namespace TradePilot.Worker.Tests.Services;

[TestClass]
public sealed class UpdateCheckerServiceTests
{
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
}