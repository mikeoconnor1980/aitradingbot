using System.ComponentModel.DataAnnotations;

namespace TradingApp.Api.Models;

public sealed class ModifyTriggerOrderDto
{
    [Required]
    [Range(0.000001, double.MaxValue, ErrorMessage = "Trigger price must be positive.")]
    public decimal TriggerPrice { get; set; }

    [Required]
    [Range(0.000001, double.MaxValue, ErrorMessage = "Size must be positive.")]
    public decimal Size { get; set; }

    [RegularExpression("^(tp|sl)$", ErrorMessage = "TpslType must be 'tp' or 'sl'.")]
    public string? TpslType { get; set; }
}