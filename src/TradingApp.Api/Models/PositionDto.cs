namespace TradingApp.Api.Models;

public sealed class PositionDto
{
    public string Asset { get; set; } = string.Empty;
    public decimal Size { get; set; }
    public string Side { get; set; } = string.Empty;
    public decimal EntryPrice { get; set; }
    public decimal MarkPrice { get; set; }
    public decimal UnrealisedPnl { get; set; }
    public decimal UnrealisedPnlPercent { get; set; }
    public decimal LiquidationPrice { get; set; }
    public int Leverage { get; set; }
    public string MarginMode { get; set; } = string.Empty;
    public decimal MarginUsed { get; set; }
    public decimal FundingRate { get; set; }
    public decimal? StopLossPrice { get; set; }
    public string? StopLossOrderId { get; set; }
    public decimal? TakeProfitPrice { get; set; }
    public string? TakeProfitOrderId { get; set; }
}