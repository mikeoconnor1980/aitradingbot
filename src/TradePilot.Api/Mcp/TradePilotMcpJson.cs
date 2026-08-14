using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradePilot.Api.Mcp;

internal static class TradePilotMcpJson
{
    /// <summary>
    /// Creates the serializer settings shared by MCP schemas and structured results.
    /// </summary>
    internal static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));

        return options;
    }
}
