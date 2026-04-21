using TradePilot.Worker.Services;

namespace TradePilot.Worker.Tests.Services;

[TestClass]
public sealed class ProcessedFillKeyTrackerTests
{
    [TestMethod]
    public void GivenDuplicateKeyWithinRetention_WhenRegistered_ThenReturnsFalse()
    {
        var now = DateTimeOffset.UtcNow;
        var sut = new ProcessedFillKeyTracker(now);

        var first = sut.TryRegister("fill-1", now);
        var second = sut.TryRegister("fill-1", now.AddMinutes(10));

        first.Should().BeTrue();
        second.Should().BeFalse();
        sut.Count.Should().Be(1);
    }

    [TestMethod]
    public void GivenExpiredKeys_WhenCompacted_ThenOlderEntriesAreRemovedAndRecentEntriesRemain()
    {
        var start = DateTimeOffset.UtcNow;
        var sut = new ProcessedFillKeyTracker(start);

        sut.TryRegister("expired-fill", start);
        sut.TryRegister("recent-fill", start.AddHours(1).AddMinutes(45));

        sut.Compact(start.AddHours(2).AddMinutes(31));

        sut.Contains("expired-fill").Should().BeFalse();
        sut.Contains("recent-fill").Should().BeTrue();
        sut.Count.Should().Be(1);
    }
}