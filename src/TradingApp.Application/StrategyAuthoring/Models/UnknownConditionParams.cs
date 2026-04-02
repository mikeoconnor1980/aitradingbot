using System.Text.Json;

namespace TradingApp.Application.StrategyAuthoring.Models;

public sealed record UnknownConditionParams : IEntryConditionParams
{
    public Dictionary<string, JsonElement> RawProperties { get; init; } = [];
}