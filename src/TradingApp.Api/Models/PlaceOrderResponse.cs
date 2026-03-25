namespace TradingApp.Api.Models;

public sealed class PlaceOrderResponse
{
    public bool Success { get; set; }
    public string? OrderId { get; set; }
    public string? Status { get; set; }
    public string? Detail { get; set; }
}
