namespace TradingApp.Indicators.Incremental;

/// <summary>
/// Incrementally calculates ATR using Wilder smoothing.
/// Maintains running state so each new bar is O(1).
/// Matches TradingView ta.atr() implementation.
/// </summary>
public sealed class IncrementalAtr
{
    private readonly int _period;
    private int _count;
    private decimal? _previousClose;
    private decimal _trueRangeSum;
    private decimal? _value;
    private bool _seeded;

    public IncrementalAtr(int period)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);
        _period = period;
    }

    public decimal? Current => _value;

    public void Add(decimal high, decimal low, decimal close)
    {
        if (_previousClose is null)
        {
            _previousClose = close;
            return;
        }

        var trueRange = Math.Max(
            high - low,
            Math.Max(Math.Abs(high - _previousClose.Value), Math.Abs(low - _previousClose.Value)));

        _previousClose = close;
        _count++;

        if (!_seeded)
        {
            _trueRangeSum += trueRange;

            if (_count == _period)
            {
                _value = _trueRangeSum / _period;
                _seeded = true;
            }

            return;
        }

        _value = ((_value!.Value * (_period - 1)) + trueRange) / _period;
    }

    public void Reset()
    {
        _count = 0;
        _previousClose = null;
        _trueRangeSum = 0m;
        _value = null;
        _seeded = false;
    }
}
