using System.ComponentModel.DataAnnotations;

namespace TradingApp.Api.Models;

public sealed class InterpretStrategyRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Please enter a strategy description")]
    [RegularExpression(@".*\S.*", ErrorMessage = "Please enter a strategy description")]
    [MaxLength(500, ErrorMessage = "Strategy description must be 500 characters or fewer")]
    public string Text { get; set; } = default!;
}