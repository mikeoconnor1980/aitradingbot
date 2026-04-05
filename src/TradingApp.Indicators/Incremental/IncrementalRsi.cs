namespace TradingApp.Indicators.Incremental;

/// <summary>
/// Incrementally calculates RSI using Wilder smoothing.
/// Maintains running average gain/loss so each new value is O(1).
/// Matches TradingView ta.rsi() implementation.
/// </summary>
public sealed class IncrementalRsi
{
    private readonly int _period;
    private int _count;
    private decimal _averageGain;
    private decimal _averageLoss;
    private decimal? _previousClose;
    private decimal? _value;
    private bool _seeded;

    // Accumulate initial deltas for seed calculation
    private decimal _seedGainSum;
    private decimal _seedLossSum;

    public IncrementalRsi(int period)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);
        _period = period;
    }

    public decimal? Current => _value;

    public void Add(decimal close)
    {
        if (_previousClose is null)
        {
            _previousClose = close;
            return;
        }

        var delta = close - _previousClose.Value;
        _previousClose = close;
        _count++;

        if (!_seeded)
        {
            if (delta >= 0m)
            {
                _seedGainSum += delta;
            }
            else
            {
                _seedLossSum += Math.Abs(delta);
            }

            if (_count == _period)
            {
                _averageGain = _seedGainSum / _period;
                _averageLoss = _seedLossSum / _period;
                _value = CalculateRsiValue(_averageGain, _averageLoss);
                _seeded = true;
            }

            return;
        }

        var gain = delta >= 0m ? delta : 0m;
        var loss = delta < 0m ? Math.Abs(delta) : 0m;

        _averageGain = ((_averageGain * (_period - 1)) + gain) / _period;
        _averageLoss = ((_averageLoss * (_period - 1)) + loss) / _period;
        _value = CalculateRsiValue(_averageGain, _averageLoss);
    }

    public void Reset()
    {
        _count = 0;
        _averageGain = 0m;
        _averageLoss = 0m;
        _previousClose = null;
        _value = null;
        _seeded = false;
        _seedGainSum = 0m;
        _seedLossSum = 0m;
    }

    private static decimal CalculateRsiValue(decimal averageGain, decimal averageLoss)
    {
        if (averageGain == 0m && averageLoss == 0m)
        {
            return 50m;
        }

        if (averageLoss == 0m)
        {
            return 100m;
        }

        var relativeStrength = averageGain / averageLoss;
        return 100m - (100m / (1m + relativeStrength));
    }
}
