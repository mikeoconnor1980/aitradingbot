using System.ComponentModel.DataAnnotations;

namespace TradingApp.Api.Models;

public sealed class IngestFundingRatesRequest
{
    [Required]
    public string Symbol { get; set; } = string.Empty;

    public long? StartTime { get; set; }

    public long? EndTime { get; set; }
}