namespace TradePilot.Api.Models;

public sealed class ReferenceDataResponse
{
    public List<string> Markets { get; init; } = [];
    public List<string> Timeframes { get; init; } = [];
}