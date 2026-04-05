using System.Threading.Channels;
using TradingApp.Application.Optimization.Models;

namespace TradingApp.Application.Optimization;

public sealed record OptimizationJob(Guid OptimizationRunId, SweepConfig Config);

public sealed class OptimizationJobQueue
{
    private readonly Channel<OptimizationJob> _channel = Channel.CreateBounded<OptimizationJob>(
        new BoundedChannelOptions(20)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });

    public async ValueTask EnqueueAsync(OptimizationJob job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        await _channel.Writer.WriteAsync(job, cancellationToken);
    }

    public IAsyncEnumerable<OptimizationJob> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}