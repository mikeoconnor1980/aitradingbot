namespace TradingApp.Application.Trading.Models;

/// <summary>
/// Represents the current state of a grid deployment.
/// Minimal definition and will be expanded by GridController work.
/// </summary>
public sealed class GridState
{
    public GridLifecycle Lifecycle { get; set; } = GridLifecycle.Inactive;
    public string? GridCycleId { get; set; }
    public int FilledLevels { get; set; }
    public int TotalLevels { get; set; }

    /// <summary>
    /// Highest price observed since position was opened.
    /// Used by ATR trailing stop to compute the dynamic stop level.
    /// Reset to null when position is closed.
    /// </summary>
    public decimal? TrailingStopHighWatermark { get; set; }

    /// <summary>
    /// Number of candles elapsed since position was opened.
    /// Used by ATR trailing stop warmup to delay exit checks.
    /// Reset to 0 when position is closed.
    /// </summary>
    public int CandlesSinceEntry { get; set; }
}
