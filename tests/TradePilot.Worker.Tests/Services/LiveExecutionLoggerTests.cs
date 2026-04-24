using TradePilot.Application.Agent.Models;
using TradePilot.Worker.Services;

namespace TradePilot.Worker.Tests.Services;

[TestClass]
public sealed class LiveExecutionLoggerTests
{
    [TestMethod]
    public void GivenMoreThanMaxEntries_WhenLogged_ThenOldestEntriesAreDropped()
    {
        var sut = new LiveExecutionLogger();

        for (var index = 0; index < LiveExecutionLogger.MaxQueueSize + 5; index++)
        {
            sut.Log(new ExecutionLogEntry
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                Category = ExecutionLogCategory.Signal,
                Level = ExecutionLogLevel.Summary,
                Message = $"entry-{index}",
            });
        }

        var drained = sut.Drain();

        drained.Should().HaveCount(LiveExecutionLogger.MaxQueueSize);
        drained.First().Message.Should().Be("entry-5");
        drained.Last().Message.Should().Be($"entry-{LiveExecutionLogger.MaxQueueSize + 4}");
    }
}