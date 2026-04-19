using TradePilot.Worker.Services;

namespace TradePilot.Worker.Tests.Services;

[TestClass]
public sealed class TradingSessionTests
{
    [TestMethod]
    public void GivenTradeTickBeforeSessionStartOutsideSkew_WhenIsTradeTickCurrent_ThenReturnsFalse()
    {
        var sessionStartMs = 1_000_000L;
        var tradeTimestampMs = sessionStartMs - 30_001L;

        var result = TradingSession.IsTradeTickCurrent(tradeTimestampMs, sessionStartMs);

        result.Should().BeFalse();
    }

    [TestMethod]
    public void GivenTradeTickWithinSkewBeforeSessionStart_WhenIsTradeTickCurrent_ThenReturnsTrue()
    {
        var sessionStartMs = 1_000_000L;
        var tradeTimestampMs = sessionStartMs - 30_000L;

        var result = TradingSession.IsTradeTickCurrent(tradeTimestampMs, sessionStartMs);

        result.Should().BeTrue();
    }

    [TestMethod]
    public void GivenTradeTickAfterSessionStart_WhenIsTradeTickCurrent_ThenReturnsTrue()
    {
        var sessionStartMs = 1_000_000L;
        var tradeTimestampMs = sessionStartMs + 1L;

        var result = TradingSession.IsTradeTickCurrent(tradeTimestampMs, sessionStartMs);

        result.Should().BeTrue();
    }

    [TestMethod]
    public void GivenClosedBucketAfterSessionStart_WhenResolvingLatestEligibleClosedBucket_ThenReturnsBucketOpenTime()
    {
        var intervalMs = 5L * 60L * 1000L;
        var sessionStartMs = 1_000_000L;
        var nowMs = 1_200_000L;

        var result = TradingSession.GetLatestEligibleClosedBucketOpenTime(nowMs, sessionStartMs, intervalMs);

        result.Should().Be(900_000L);
    }

    [TestMethod]
    public void GivenLatestClosedBucketBeforeSessionStart_WhenResolvingLatestEligibleClosedBucket_ThenReturnsNull()
    {
        var intervalMs = 5L * 60L * 1000L;
        var sessionStartMs = 1_200_001L;
        var nowMs = 1_200_000L;

        var result = TradingSession.GetLatestEligibleClosedBucketOpenTime(nowMs, sessionStartMs, intervalMs);

        result.Should().BeNull();
    }
}