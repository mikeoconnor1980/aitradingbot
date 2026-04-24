using Microsoft.Extensions.Logging;
using TradePilot.Worker.Services;

namespace TradePilot.Worker.Tests.Services;

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
    public void GivenDisconnectedWebSocket_WhenEvaluateHealth_ThenLogsWarning()
    {
        var snapshot = new TradingHealthSnapshot(
            IsWebSocketConnected: false,
            IsTradingSessionActive: true,
            LastTradeReceived: DateTimeOffset.UtcNow,
            LastCandleClosed: DateTimeOffset.UtcNow,
            ServiceStartedUtc: DateTimeOffset.UtcNow.AddMinutes(-10),
            TradingSessionStartedUtc: DateTimeOffset.UtcNow.AddMinutes(-10),
            Uptime: TimeSpan.FromMinutes(10),
            TradingSessionUptime: TimeSpan.FromMinutes(10),
            TimeSinceLastTrade: TimeSpan.FromSeconds(30),
            TimeSinceLastCandle: TimeSpan.FromMinutes(2));

        _sut.EvaluateHealth(snapshot);

        VerifyLogLevel(LogLevel.Warning);
    }

    [TestMethod]
    public void GivenNoTradesAfterStartup_WhenEvaluateHealth_ThenLogsWarning()
    {
        var snapshot = new TradingHealthSnapshot(
            IsWebSocketConnected: true,
            IsTradingSessionActive: true,
            LastTradeReceived: null,
            LastCandleClosed: null,
            ServiceStartedUtc: DateTimeOffset.UtcNow.AddMinutes(-10),
            TradingSessionStartedUtc: DateTimeOffset.UtcNow.AddMinutes(-10),
            Uptime: TimeSpan.FromMinutes(10),
            TradingSessionUptime: TimeSpan.FromMinutes(10),
            TimeSinceLastTrade: null,
            TimeSinceLastCandle: null);

        _sut.EvaluateHealth(snapshot);

        VerifyLogLevel(LogLevel.Warning);
    }

    [TestMethod]
    public void GivenStaleTrades_WhenEvaluateHealth_ThenLogsWarning()
    {
        var snapshot = new TradingHealthSnapshot(
            IsWebSocketConnected: true,
            IsTradingSessionActive: true,
            LastTradeReceived: DateTimeOffset.UtcNow.AddMinutes(-10),
            LastCandleClosed: DateTimeOffset.UtcNow,
            ServiceStartedUtc: DateTimeOffset.UtcNow.AddMinutes(-20),
            TradingSessionStartedUtc: DateTimeOffset.UtcNow.AddMinutes(-20),
            Uptime: TimeSpan.FromMinutes(20),
            TradingSessionUptime: TimeSpan.FromMinutes(20),
            TimeSinceLastTrade: TimeSpan.FromMinutes(10),
            TimeSinceLastCandle: TimeSpan.FromMinutes(1));

        _sut.EvaluateHealth(snapshot);

        VerifyLogLevel(LogLevel.Warning);
    }

    [TestMethod]
    public void GivenStaleCandles_WhenEvaluateHealth_ThenLogsWarning()
    {
        var snapshot = new TradingHealthSnapshot(
            IsWebSocketConnected: true,
            IsTradingSessionActive: true,
            LastTradeReceived: DateTimeOffset.UtcNow,
            LastCandleClosed: DateTimeOffset.UtcNow.AddMinutes(-25),
            ServiceStartedUtc: DateTimeOffset.UtcNow.AddMinutes(-30),
            TradingSessionStartedUtc: DateTimeOffset.UtcNow.AddMinutes(-30),
            Uptime: TimeSpan.FromMinutes(30),
            TradingSessionUptime: TimeSpan.FromMinutes(30),
            TimeSinceLastTrade: TimeSpan.FromSeconds(5),
            TimeSinceLastCandle: TimeSpan.FromMinutes(25));

        _sut.EvaluateHealth(snapshot);

        VerifyLogLevel(LogLevel.Warning);
    }

    [TestMethod]
    public void GivenHealthySnapshot_WhenEvaluateHealth_ThenLogsInformationEvery10Checks()
    {
        var snapshot = new TradingHealthSnapshot(
            IsWebSocketConnected: true,
            IsTradingSessionActive: true,
            LastTradeReceived: DateTimeOffset.UtcNow,
            LastCandleClosed: DateTimeOffset.UtcNow,
            ServiceStartedUtc: DateTimeOffset.UtcNow.AddMinutes(-10),
            TradingSessionStartedUtc: DateTimeOffset.UtcNow.AddMinutes(-10),
            Uptime: TimeSpan.FromMinutes(10),
            TradingSessionUptime: TimeSpan.FromMinutes(10),
            TimeSinceLastTrade: TimeSpan.FromSeconds(5),
            TimeSinceLastCandle: TimeSpan.FromMinutes(2));

        for (var index = 0; index < 9; index++)
        {
            _sut.EvaluateHealth(snapshot);
        }

        VerifyNoLogLevel(LogLevel.Information);

        _sut.EvaluateHealth(snapshot);

        VerifyLogLevel(LogLevel.Information);
    }

    [TestMethod]
    public void GivenNoTradesWithinGracePeriod_WhenEvaluateHealth_ThenNoWarningLogged()
    {
        var snapshot = new TradingHealthSnapshot(
            IsWebSocketConnected: true,
            IsTradingSessionActive: true,
            LastTradeReceived: null,
            LastCandleClosed: null,
            ServiceStartedUtc: DateTimeOffset.UtcNow.AddMinutes(-2),
            TradingSessionStartedUtc: DateTimeOffset.UtcNow.AddMinutes(-2),
            Uptime: TimeSpan.FromMinutes(2),
            TradingSessionUptime: TimeSpan.FromMinutes(2),
            TimeSinceLastTrade: null,
            TimeSinceLastCandle: null);

        _sut.EvaluateHealth(snapshot);

        VerifyNoLogLevel(LogLevel.Warning);
    }

    [TestMethod]
    public void GivenIdleSnapshot_WhenEvaluateHealth_ThenLogsIdleInfoOnly()
    {
        var snapshot = new TradingHealthSnapshot(
            IsWebSocketConnected: false,
            IsTradingSessionActive: false,
            LastTradeReceived: null,
            LastCandleClosed: null,
            ServiceStartedUtc: DateTimeOffset.UtcNow.AddMinutes(-10),
            TradingSessionStartedUtc: null,
            Uptime: TimeSpan.FromMinutes(10),
            TradingSessionUptime: null,
            TimeSinceLastTrade: null,
            TimeSinceLastCandle: null);

        _sut.EvaluateHealth(snapshot);

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
