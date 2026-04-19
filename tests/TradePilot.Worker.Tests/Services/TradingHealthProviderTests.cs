using FluentAssertions;
using TradePilot.Worker.Services;

namespace TradePilot.Worker.Tests.Services;

[TestClass]
public sealed class TradingHealthProviderTests
{
    private TradingHealthProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _sut = new TradingHealthProvider();
    }

    [TestMethod]
    public void InitialState_IsDisconnected_NoTradesOrCandles()
    {
        _sut.IsWebSocketConnected.Should().BeFalse();
        _sut.IsTradingSessionActive.Should().BeFalse();
        _sut.LastTradeReceived.Should().BeNull();
        _sut.LastCandleClosed.Should().BeNull();
    }

    [TestMethod]
    public void RecordTradingSessionStarted_SetsSessionActiveAndResetsTradingState()
    {
        _sut.RecordConnectionState(true);
        _sut.RecordTradeReceived();
        _sut.RecordCandleClosed("5m");

        _sut.RecordTradingSessionStarted();

        _sut.IsTradingSessionActive.Should().BeTrue();
        _sut.IsWebSocketConnected.Should().BeFalse();
        _sut.LastTradeReceived.Should().BeNull();
        _sut.LastCandleClosed.Should().BeNull();
        _sut.TradingSessionStartedUtc.Should().NotBeNull();
    }

    [TestMethod]
    public void RecordTradingSessionStopped_ClearsSessionState()
    {
        _sut.RecordTradingSessionStarted();
        _sut.RecordConnectionState(true);
        _sut.RecordTradeReceived();
        _sut.RecordCandleClosed("5m");

        _sut.RecordTradingSessionStopped();

        _sut.IsTradingSessionActive.Should().BeFalse();
        _sut.IsWebSocketConnected.Should().BeFalse();
        _sut.LastTradeReceived.Should().BeNull();
        _sut.LastCandleClosed.Should().BeNull();
        _sut.TradingSessionStartedUtc.Should().BeNull();
    }

    [TestMethod]
    public void RecordConnectionState_True_SetsConnected()
    {
        _sut.RecordConnectionState(true);

        _sut.IsWebSocketConnected.Should().BeTrue();
    }

    [TestMethod]
    public void RecordConnectionState_False_SetsDisconnected()
    {
        _sut.RecordConnectionState(true);
        _sut.RecordConnectionState(false);

        _sut.IsWebSocketConnected.Should().BeFalse();
    }

    [TestMethod]
    public void RecordTradeReceived_SetsTimestamp()
    {
        var before = DateTimeOffset.UtcNow;
        _sut.RecordTradeReceived();
        var after = DateTimeOffset.UtcNow;

        _sut.LastTradeReceived.Should().NotBeNull();
        _sut.LastTradeReceived!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [TestMethod]
    public void RecordCandleClosed_SetsTimestamp()
    {
        var before = DateTimeOffset.UtcNow;
        _sut.RecordCandleClosed("15m");
        var after = DateTimeOffset.UtcNow;

        _sut.LastCandleClosed.Should().NotBeNull();
        _sut.LastCandleClosed!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [TestMethod]
    public void RecordServiceStarted_SetsStartTime()
    {
        var before = DateTimeOffset.UtcNow;
        _sut.RecordServiceStarted();
        var after = DateTimeOffset.UtcNow;

        _sut.ServiceStartedUtc.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [TestMethod]
    public void GetSnapshot_ReturnsCorrectState()
    {
        _sut.RecordServiceStarted();
        _sut.RecordConnectionState(true);
        _sut.RecordTradeReceived();
        _sut.RecordCandleClosed("15m");

        var snapshot = _sut.GetSnapshot();

        snapshot.IsWebSocketConnected.Should().BeTrue();
        snapshot.IsTradingSessionActive.Should().BeFalse();
        snapshot.LastTradeReceived.Should().NotBeNull();
        snapshot.LastCandleClosed.Should().NotBeNull();
        snapshot.Uptime.Should().BeGreaterOrEqualTo(TimeSpan.Zero);
        snapshot.TimeSinceLastTrade.Should().NotBeNull();
        snapshot.TimeSinceLastCandle.Should().NotBeNull();
    }

    [TestMethod]
    public void GetSnapshot_WhenSessionActive_IncludesTradingSessionUptime()
    {
        _sut.RecordServiceStarted();
        _sut.RecordTradingSessionStarted();

        var snapshot = _sut.GetSnapshot();

        snapshot.IsTradingSessionActive.Should().BeTrue();
        snapshot.TradingSessionStartedUtc.Should().NotBeNull();
        snapshot.TradingSessionUptime.Should().NotBeNull();
    }

    [TestMethod]
    public void GetSnapshot_WhenNoTradesOrCandles_TimeSinceIsNull()
    {
        _sut.RecordServiceStarted();

        var snapshot = _sut.GetSnapshot();

        snapshot.TimeSinceLastTrade.Should().BeNull();
        snapshot.TimeSinceLastCandle.Should().BeNull();
    }

    [TestMethod]
    public void MultipleTradesReceived_LastTimestampWins()
    {
        _sut.RecordTradeReceived();
        var firstTrade = _sut.LastTradeReceived;

        // Small delay to ensure different timestamp
        Thread.Sleep(10);
        _sut.RecordTradeReceived();
        var secondTrade = _sut.LastTradeReceived;

        secondTrade.Should().BeOnOrAfter(firstTrade!.Value);
    }
}
