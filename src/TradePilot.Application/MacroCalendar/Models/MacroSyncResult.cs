namespace TradePilot.Application.MacroCalendar.Models;

public sealed class MacroSyncResult
{
    public int Fetched { get; set; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
}
