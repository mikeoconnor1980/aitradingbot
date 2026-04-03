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

    public void SetMacd(int fast, int slow, int signal, decimal currentValue, decimal? previousValue = null)
    {
        var key = CreateMacdKey(fast, slow, signal);
        _current[key] = currentValue;

        if (previousValue.HasValue)
        {
            _previous[key] = previousValue.Value;
        }
    }

    public decimal? GetRsi(int period) => GetValue(_current, CreateRsiKey(period));

    public decimal? GetPreviousRsi(int period) => GetValue(_previous, CreateRsiKey(period));

    public decimal? GetEma(int period) => GetValue(_current, CreateEmaKey(period));

    public decimal? GetPreviousEma(int period) => GetValue(_previous, CreateEmaKey(period));

    public decimal? GetMacd(int fast, int slow, int signal) => GetValue(_current, CreateMacdKey(fast, slow, signal));

    public decimal? GetPreviousMacd(int fast, int slow, int signal) => GetValue(_previous, CreateMacdKey(fast, slow, signal));

    private static decimal? GetValue(IReadOnlyDictionary<string, decimal> source, string key)
    {
        return source.TryGetValue(key, out var value) ? value : null;
    }

    private static string CreateRsiKey(int period) => $"RSI:{period}";

    private static string CreateEmaKey(int period) => $"EMA:{period}";

    private static string CreateMacdKey(int fast, int slow, int signal) => $"MACD:{fast}:{slow}:{signal}";
}