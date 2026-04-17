namespace TradePilot.Application.Trading.Models;

/// <summary>
/// Represents the current state of a grid deployment.
/// Minimal definition and will be expanded by GridController work.
/// Thread-safe: all mutations should be wrapped in lock(SyncRoot).
/// </summary>
public sealed class GridState
{
    /// <summary>Lock object for synchronising mutations across threads.</summary>
    public object SyncRoot { get; } = new();

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

    /// <summary>
    /// Dollar risk (1R) for the current grid cycle.
    /// Set during RiskBased position sizing at grid deployment.
    /// Null for non-RiskBased sizing modes.
    /// </summary>
    public decimal? InitialRDollars { get; set; }

    /// <summary>
    /// ATR value captured when the grid was deployed.
    /// Used by AtrInitial stop to compute the fixed stop price from entry-time volatility.
    /// Reset to null when position is closed.
    /// </summary>
    public decimal? AtrAtEntry { get; set; }

    /// <summary>
    /// Tracks exchange-native TP/SL trigger orders protecting the current position.
    /// In-memory only — rebuilt from exchange on worker restart.
    /// </summary>
    public ProtectionOrderState ProtectionOrders { get; } = new();
}
