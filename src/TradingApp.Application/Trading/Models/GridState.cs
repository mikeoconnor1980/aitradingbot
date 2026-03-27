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
}
