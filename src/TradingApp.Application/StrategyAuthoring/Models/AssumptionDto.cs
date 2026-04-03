using System.Text.Json.Serialization;
using TradingApp.Application.StrategyAuthoring.Serialization;

namespace TradingApp.Application.StrategyAuthoring.Models;

public sealed class AssumptionDto
{
    public string FieldName { get; init; } = string.Empty;

    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string AssumedValue { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;
}