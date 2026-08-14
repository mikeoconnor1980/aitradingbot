using TradePilot.Application.Analyst.Models;

namespace TradePilot.AI.Analyst;

/// <summary>Provides the explicit read-only tool allow-list used by the native Analyst.</summary>
public interface IAnalystToolCatalog
{
    /// <summary>Gets the immutable tool definitions supplied to the LLM.</summary>
    IReadOnlyList<AnalystToolDefinition> Definitions { get; }

    /// <summary>Executes one allow-listed tool through TradePilot.Application.</summary>
    Task<AnalystToolResult> ExecuteAsync(
        string toolName,
        string argumentsJson,
        AnalystToolContext context,
        CancellationToken cancellationToken);
}

/// <summary>Contains request-scoped identity required by account tools.</summary>
public sealed record AnalystToolContext(Guid? UserId);
