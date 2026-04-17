using System.Threading.Channels;

namespace TradePilot.Application.Backtesting;

public sealed record BacktestJob(Guid BacktestRunId);

public sealed class BacktestJobQueue
{
    private readonly Channel<BacktestJob> _channel = Channel.CreateBounded<BacktestJob>(
        new BoundedChannelOptions(50)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });

    public async ValueTask EnqueueAsync(BacktestJob job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        await _channel.Writer.WriteAsync(job, cancellationToken);
    }

    public IAsyncEnumerable<BacktestJob> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
