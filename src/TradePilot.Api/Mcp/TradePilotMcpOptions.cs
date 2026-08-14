namespace TradePilot.Api.Mcp;

/// <summary>
/// Configures TradePilot's read-only MCP transport endpoint.
/// </summary>
public sealed class TradePilotMcpOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "Mcp";

    /// <summary>Gets or sets whether the MCP endpoint is mapped.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the Streamable HTTP endpoint path.</summary>
    public string Path { get; set; } = "/mcp";
}
