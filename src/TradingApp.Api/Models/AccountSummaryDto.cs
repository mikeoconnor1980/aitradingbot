namespace TradingApp.Api.Models;

public sealed class AccountSummaryDto
{
    public decimal Equity { get; set; }
    public decimal AvailableMargin { get; set; }
    public decimal CrossMarginRatio { get; set; }
    public decimal MaintenanceMargin { get; set; }
    public decimal UnrealisedPnl { get; set; }
}