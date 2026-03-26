using System.ComponentModel.DataAnnotations;

namespace TradingApp.Api.Models;

public sealed class ModifyOrderDto
{
    [Range(0.000001, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
    public decimal Price { get; set; }

    [Range(0.000001, double.MaxValue, ErrorMessage = "Size must be greater than 0")]
    public decimal Size { get; set; }
}