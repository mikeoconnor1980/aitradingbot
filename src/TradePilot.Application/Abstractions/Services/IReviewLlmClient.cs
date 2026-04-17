namespace TradePilot.Application.Abstractions.Services;

/// <summary>
/// Marker interface for the review-specific LLM client registration.
/// Uses independently-configured LlmReview options.
/// </summary>
public interface IReviewLlmClient : ILlmClient;