using System.ComponentModel.DataAnnotations;

namespace TradePilot.Api.Models;

public sealed class InterpretStrategyRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Please enter a strategy description")]
    [RegularExpression(@".*\S.*", ErrorMessage = "Please enter a strategy description")]
    [MaxLength(2000, ErrorMessage = "Strategy description must be 2000 characters or fewer")]
    public string Text { get; set; } = default!;
}