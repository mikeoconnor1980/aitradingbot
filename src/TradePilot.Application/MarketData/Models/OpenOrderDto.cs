namespace TradePilot.Application.MarketData.Models;

public sealed class OpenOrderDto
{
    public string OrderId { get; set; } = string.Empty;
    public string Asset { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Size { get; set; }
    public string OrderType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? TriggerPrice { get; set; }
    public string? TpslType { get; set; }
    public bool IsReduceOnly { get; set; }
}
