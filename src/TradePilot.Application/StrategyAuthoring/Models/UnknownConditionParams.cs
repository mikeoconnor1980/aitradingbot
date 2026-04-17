using System.Text.Json;

namespace TradePilot.Application.StrategyAuthoring.Models;

public sealed record UnknownConditionParams : IEntryConditionParams
{
    public Dictionary<string, JsonElement> RawProperties { get; init; } = [];
}