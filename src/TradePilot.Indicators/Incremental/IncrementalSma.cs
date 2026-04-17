namespace TradePilot.Indicators.Incremental;

/// <summary>
/// Incrementally calculates SMA using a sliding window.
/// Maintains a circular buffer so each new value is O(1).
/// </summary>
public sealed class IncrementalSma
{
    private readonly int _period;
    private readonly decimal[] _buffer;
    private int _writeIndex;
    private int _count;
    private decimal _sum;

    public IncrementalSma(int period)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);
        _period = period;
        _buffer = new decimal[period];
    }

    public decimal Current => _count > 0 ? _sum / Math.Min(_count, _period) : 0m;

    public void Add(decimal value)
    {
        if (_count >= _period)
        {
            _sum -= _buffer[_writeIndex];
        }

        _buffer[_writeIndex] = value;
        _sum += value;
        _count++;
        _writeIndex = (_writeIndex + 1) % _period;
    }

    public void Reset()
    {
        _writeIndex = 0;
        _count = 0;
        _sum = 0m;
        Array.Clear(_buffer);
    }
}
