using System.ComponentModel.DataAnnotations;

namespace TradePilot.Api.Models;

public sealed class IngestFundingRatesRequest
{
    [Required]
    public string Symbol { get; set; } = default!;

    public long? StartTime { get; set; }

    public long? EndTime { get; set; }
}