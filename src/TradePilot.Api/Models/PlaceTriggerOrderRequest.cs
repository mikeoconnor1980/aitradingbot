using System.ComponentModel.DataAnnotations;

namespace TradePilot.Api.Models;

public sealed class PlaceTriggerOrderRequest
{
    [Required]
    public string Asset { get; set; } = default!;

    [Required]
    [RegularExpression("^(buy|sell)$", ErrorMessage = "Side must be 'buy' or 'sell'.")]
    public string Side { get; set; } = default!;

    [Required]
    [Range(0.000001, double.MaxValue, ErrorMessage = "Size must be positive.")]
    public decimal Size { get; set; }

    [Required]
    [Range(0.000001, double.MaxValue, ErrorMessage = "Trigger price must be positive.")]
    public decimal TriggerPrice { get; set; }

    [Required]
    [RegularExpression("^(sl|tp)$", ErrorMessage = "TpslType must be 'sl' or 'tp'.")]
    public string TpslType { get; set; } = default!;
}