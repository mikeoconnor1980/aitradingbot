namespace TradePilot.Api.Models;

public sealed class PlaceOrderResponse
{
    public bool Success { get; set; }
    public string? OrderId { get; set; }
    public string? Status { get; set; }
    public string? Detail { get; set; }
    public List<string> Warnings { get; set; } = [];
}
