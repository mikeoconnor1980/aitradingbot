using System.Text.Json.Serialization;
using TradingApp.Application.StrategyAuthoring.Serialization;

namespace TradingApp.Application.StrategyAuthoring.Models;

[JsonConverter(typeof(EntryConditionConfigConverter))]
public sealed record EntryConditionConfig
{
    public string Id { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public EntryConditionType Type { get; init; }
    public string Label { get; init; } = string.Empty;
    public IEntryConditionParams? Params { get; init; }
}