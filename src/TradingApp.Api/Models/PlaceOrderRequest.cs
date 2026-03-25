using System.ComponentModel.DataAnnotations;

namespace TradingApp.Api.Models;

public sealed class PlaceOrderRequest
{
    [Required]
    public string Asset { get; set; } = default!;

    [Required]
    [RegularExpression("^(buy|sell)$", ErrorMessage = "Side must be 'buy' or 'sell'.")]
    public string Side { get; set; } = default!;

    [Required]
    [RegularExpression("^(market|limit)$", ErrorMessage = "OrderType must be 'market' or 'limit'.")]
    public string OrderType { get; set; } = default!;

    public decimal? Price { get; set; }

    [Required]
    [Range(0.000001, double.MaxValue)]
    public decimal Size { get; set; }
}
