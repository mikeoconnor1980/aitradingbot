using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TradingApp.Worker.Services;

namespace TradingApp.Worker.Tests.Services;

[TestClass]
public sealed class HealthMonitorServiceTests
{
    private HealthMonitorService _sut = null!;
    private Mock<ITradingHealthProvider> _healthProvider = null!;
    private Mock<ILogger<HealthMonitorService>> _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _healthProvider = new Mock<ITradingHealthProvider>();
        _logger = new Mock<ILogger<HealthMonitorService>>();
        _sut = new HealthMonitorService(_healthProvider.Object, _logger.Object);
    }

    [TestMethod]
    public void EvaluateHealth_WhenDisconnected_LogsWarning()
    {
        var snapshot = new TradingHealthSnapshot(
            IsWebSocketConnected: false,
            LastTradeReceived: DateTimeOffset.UtcNow,
            LastCandleClosed: DateTimeOffset.UtcNow,
            ServiceStartedUtc: DateTimeOffset.UtcNow.AddMinutes(-10),
            Uptime: TimeSpan.FromMinutes(10),
            TimeSinceLastTrade: TimeSpan.FromSeconds(30),
            TimeSinceLastCandle: TimeSpan.FromMinutes(2));

        _sut.EvaluateHealth(snapshot);

        VerifyLogLevel(LogLevel.Warning);
    }

    [TestMethod]
    public void EvaluateHealth_WhenNoTradesAfterStartup_LogsWarning()
    {
        var snapshot = new TradingHealthSnapshot(
            IsWebSocketConnected: true,
            LastTradeReceived: null,
            LastCandleClosed: null,
            ServiceStartedUtc: DateTimeOffset.UtcNow.AddMinutes(-10),
            Uptime: TimeSpan.FromMinutes(10),
            TimeSinceLastTrade: null,
            TimeSinceLastCandle: null);

        _sut.EvaluateHealth(snapshot);

        VerifyLogLevel(LogLevel.Warning);
    }

    [TestMethod]
    public void EvaluateHealth_WhenTradesStale_LogsWarning()
    {
        var snapshot = new TradingHealthSnapshot(
            IsWebSocketConnected: true,
            LastTradeReceived: DateTimeOffset.UtcNow.AddMinutes(-10),
            LastCandleClosed: DateTimeOffset.UtcNow,
            ServiceStartedUtc: DateTimeOffset.UtcNow.AddMinutes(-20),
            Uptime: TimeSpan.FromMinutes(20),
            TimeSinceLastTrade: TimeSpan.FromMinutes(10),
            TimeSinceLastCandle: TimeSpan.FromMinutes(1));

        _sut.EvaluateHealth(snapshot);

        VerifyLogLevel(LogLevel.Warning);
    }

    [TestMethod]
    public void EvaluateHealth_WhenCandlesStale_LogsWarning()
    {
        var snapshot = new TradingHealthSnapshot(
            IsWebSocketConnected: true,
            LastTradeReceived: DateTimeOffset.UtcNow,
            LastCandleClosed: DateTimeOffset.UtcNow.AddMinutes(-25),
            ServiceStartedUtc: DateTimeOffset.UtcNow.AddMinutes(-30),
            Uptime: TimeSpan.FromMinutes(30),
            TimeSinceLastTrade: TimeSpan.FromSeconds(5),
            TimeSinceLastCandle: TimeSpan.FromMinutes(25));

        _sut.EvaluateHealth(snapshot);

        VerifyLogLevel(LogLevel.Warning);
    }

    [TestMethod]
    public void EvaluateHealth_WhenHealthy_LogsInformation()
    {
        var snapshot = new TradingHealthSnapshot(
            IsWebSocketConnected: true,
            LastTradeReceived: DateTimeOffset.UtcNow,
            LastCandleClosed: DateTimeOffset.UtcNow,
            ServiceStartedUtc: DateTimeOffset.UtcNow.AddMinutes(-10),
            Uptime: TimeSpan.FromMinutes(10),
            TimeSinceLastTrade: TimeSpan.FromSeconds(5),
            TimeSinceLastCandle: TimeSpan.FromMinutes(2));

        _sut.EvaluateHealth(snapshot);

        VerifyLogLevel(LogLevel.Information);
    }

    [TestMethod]
    public void EvaluateHealth_WhenNoTradesButWithinGracePeriod_NoWarning()
    {
        var snapshot = new TradingHealthSnapshot(
            IsWebSocketConnected: true,
            LastTradeReceived: null,
            LastCandleClosed: null,
            ServiceStartedUtc: DateTimeOffset.UtcNow.AddMinutes(-2),
            Uptime: TimeSpan.FromMinutes(2),
            TimeSinceLastTrade: null,
            TimeSinceLastCandle: null);

        _sut.EvaluateHealth(snapshot);

        // Should log healthy info (connected, within grace period)
        VerifyLogLevel(LogLevel.Information);
        VerifyNoLogLevel(LogLevel.Warning);
    }

    private void VerifyLogLevel(LogLevel level)
    {
        _logger.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    private void VerifyNoLogLevel(LogLevel level)
    {
        _logger.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }
}
