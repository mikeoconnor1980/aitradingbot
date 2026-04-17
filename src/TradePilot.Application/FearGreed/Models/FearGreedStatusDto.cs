namespace TradePilot.Application.FearGreed.Models;

public sealed record FearGreedStatusDto(
    int? LatestValue,
    string? LatestClassification,
    DateTimeOffset? LatestTimestamp,
    int TotalReadings,
    DateTimeOffset? EarliestTimestamp);
