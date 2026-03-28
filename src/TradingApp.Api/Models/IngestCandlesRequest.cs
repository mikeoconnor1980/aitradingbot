using System.ComponentModel.DataAnnotations;

namespace TradingApp.Api.Models;

public sealed class IngestCandlesRequest
{
    [Required]
    public string Symbol { get; set; } = default!;

    [Required]
    [MinLength(1)]
    public string[] Intervals { get; set; } = default!;

    public long? StartTime { get; set; }

    public long? EndTime { get; set; }

    public bool IncludeMarkPrice { get; set; }
}