namespace TradingApp.Application.Trading.Models;

/// <summary>
/// Holds computed indicator values keyed by type and period.
/// Supports current and previous values for cross detection.
/// </summary>
public sealed class IndicatorContext
{
    private readonly Dictionary<string, decimal> _current = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, decimal> _previous = new(StringComparer.OrdinalIgnoreCase);

    public void SetRsi(int period, decimal currentValue, decimal? previousValue = null)
    {
        _current[CreateRsiKey(period)] = currentValue;

        if (previousValue.HasValue)
        {
            _previous[CreateRsiKey(period)] = previousValue.Value;
        }
    }

    public void SetEma(int period, decimal currentValue, decimal? previousValue = null)
    {
        _current[CreateEmaKey(period)] = currentValue;

        if (previousValue.HasValue)
        {
            _previous[CreateEmaKey(period)] = previousValue.Value;
        }
    }

    public void SetSma(int period, decimal currentValue, decimal? previousValue = null)
    {
        _current[CreateSmaKey(period)] = currentValue;

        if (previousValue.HasValue)
        {
            _previous[CreateSmaKey(period)] = previousValue.Value;
        }
    }

    public void SetMacd(
        int fast,
        int slow,
        int signal,
        decimal line,
        decimal signalLine,
        decimal histogram,
        decimal? previousLine = null,
        decimal? previousSignalLine = null,
        decimal? previousHistogram = null)
    {
        var lineKey = CreateMacdKey(fast, slow, signal);
        var signalKey = CreateMacdSignalKey(fast, slow, signal);
        var histogramKey = CreateMacdHistogramKey(fast, slow, signal);

        _current[lineKey] = line;
        _current[signalKey] = signalLine;
        _current[histogramKey] = histogram;

        if (previousLine.HasValue)
        {
            _previous[lineKey] = previousLine.Value;
        }

        if (previousSignalLine.HasValue)
        {
            _previous[signalKey] = previousSignalLine.Value;
        }

        if (previousHistogram.HasValue)
        {
            _previous[histogramKey] = previousHistogram.Value;
        }
    }

    public decimal? GetRsi(int period) => GetValue(_current, CreateRsiKey(period));

    public decimal? GetPreviousRsi(int period) => GetValue(_previous, CreateRsiKey(period));

    public decimal? GetEma(int period) => GetValue(_current, CreateEmaKey(period));

    public decimal? GetPreviousEma(int period) => GetValue(_previous, CreateEmaKey(period));

    public decimal? GetSma(int period) => GetValue(_current, CreateSmaKey(period));

    public decimal? GetPreviousSma(int period) => GetValue(_previous, CreateSmaKey(period));

    public decimal? GetMacd(int fast, int slow, int signal) => GetValue(_current, CreateMacdKey(fast, slow, signal));

    public decimal? GetPreviousMacd(int fast, int slow, int signal) => GetValue(_previous, CreateMacdKey(fast, slow, signal));

    public decimal? GetMacdSignal(int fast, int slow, int signal) => GetValue(_current, CreateMacdSignalKey(fast, slow, signal));

    public decimal? GetPreviousMacdSignal(int fast, int slow, int signal) => GetValue(_previous, CreateMacdSignalKey(fast, slow, signal));

    public decimal? GetMacdHistogram(int fast, int slow, int signal) => GetValue(_current, CreateMacdHistogramKey(fast, slow, signal));

    public decimal? GetPreviousMacdHistogram(int fast, int slow, int signal) => GetValue(_previous, CreateMacdHistogramKey(fast, slow, signal));

    private static decimal? GetValue(IReadOnlyDictionary<string, decimal> source, string key)
    {
        return source.TryGetValue(key, out var value) ? value : null;
    }

    private static string CreateRsiKey(int period) => $"RSI:{period}";

    private static string CreateEmaKey(int period) => $"EMA:{period}";

    private static string CreateSmaKey(int period) => $"SMA:{period}";

    private static string CreateMacdKey(int fast, int slow, int signal) => $"MACD:{fast}:{slow}:{signal}";

    private static string CreateMacdSignalKey(int fast, int slow, int signal) => $"MACD:{fast}:{slow}:{signal}:signal";

    private static string CreateMacdHistogramKey(int fast, int slow, int signal) => $"MACD:{fast}:{slow}:{signal}:histogram";
}