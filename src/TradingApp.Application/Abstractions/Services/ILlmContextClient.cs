namespace TradingApp.Application.Abstractions.Services;

/// <summary>
/// Marker interface for the market-context-specific LLM client registration.
/// Uses independently-configured LlmContext options.
/// </summary>
public interface ILlmContextClient : ILlmClient;
