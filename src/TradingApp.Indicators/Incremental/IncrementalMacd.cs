namespace TradingApp.Indicators.Incremental;

/// <summary>
/// Incrementally calculates MACD (line, signal, histogram) using three internal EMAs.
/// Each new value is O(1) instead of O(n).
/// Matches TradingView ta.macd() implementation.
/// </summary>
public sealed class IncrementalMacd
{
    private readonly IncrementalEma _fastEma;
    private readonly IncrementalEma _slowEma;
    private readonly IncrementalEma _signalEma;

    public IncrementalMacd(int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fastPeriod);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(slowPeriod);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(signalPeriod);

        _fastEma = new IncrementalEma(fastPeriod);
        _slowEma = new IncrementalEma(slowPeriod);
        _signalEma = new IncrementalEma(signalPeriod);
    }

    public decimal? Line { get; private set; }

    public decimal? Signal { get; private set; }

    public decimal? Histogram { get; private set; }

    public void Add(decimal close)
    {
        _fastEma.Add(close);
        _slowEma.Add(close);

        if (_fastEma.Current.HasValue && _slowEma.Current.HasValue)
        {
            Line = _fastEma.Current.Value - _slowEma.Current.Value;
            _signalEma.Add(Line.Value);

            if (_signalEma.Current.HasValue)
            {
                Signal = _signalEma.Current.Value;
                Histogram = Line.Value - Signal.Value;
            }
        }
    }

    public void Reset()
    {
        _fastEma.Reset();
        _slowEma.Reset();
        _signalEma.Reset();
        Line = null;
        Signal = null;
        Histogram = null;
    }
}
