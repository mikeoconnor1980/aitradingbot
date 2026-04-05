namespace TradingApp.Indicators.Incremental;

/// <summary>
/// Incrementally calculates EMA using SMA-seeded initialisation.
/// Maintains running state so each new value is O(1) instead of O(n).
/// Matches TradingView ta.ema() implementation.
/// </summary>
public sealed class IncrementalEma
{
    private readonly int _period;
    private readonly decimal _smoothing;
    private decimal _sum;
    private int _count;
    private decimal? _value;
    private bool _seeded;

    public IncrementalEma(int period)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);
        _period = period;
        _smoothing = 2m / (period + 1m);
    }

    public decimal? Current => _value;

    public void Add(decimal value)
    {
        _count++;

        if (!_seeded)
        {
            _sum += value;

            if (_count == _period)
            {
                _value = _sum / _period;
                _seeded = true;
            }

            return;
        }

        _value = ((value - _value!.Value) * _smoothing) + _value.Value;
    }

    public void Reset()
    {
        _sum = 0m;
        _count = 0;
        _value = null;
        _seeded = false;
    }
}
