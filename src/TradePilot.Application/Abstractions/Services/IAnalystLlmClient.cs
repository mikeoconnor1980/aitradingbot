using TradePilot.Application.Analyst.Models;

namespace TradePilot.Application.Abstractions.Services;

/// <summary>Provides provider-independent chat and tool-calling for the TradePilot Analyst.</summary>
public interface IAnalystLlmClient
{
    /// <summary>Gets the provider label used for telemetry.</summary>
    string Provider { get; }

    /// <summary>Gets the configured provider model name.</summary>
    string Model { get; }

    /// <summary>Completes one Analyst conversation round.</summary>
    Task<AnalystLlmResponse> CompleteAsync(
        AnalystLlmRequest request,
        CancellationToken cancellationToken);
}
