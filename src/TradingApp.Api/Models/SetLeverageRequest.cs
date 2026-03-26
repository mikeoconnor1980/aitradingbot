using System.ComponentModel.DataAnnotations;

namespace TradingApp.Api.Models;

public sealed class SetLeverageRequest
{
    [Required]
    public string Asset { get; set; } = default!;

    [Required]
    [Range(1, 100)]
    public int Leverage { get; set; }

    public bool IsCross { get; set; } = true;
}
