namespace TradePilot.Application.Agent.Models;

public sealed class ClosePositionPayload
{
    public required string Asset { get; init; }
    public decimal? Amount { get; init; }
}