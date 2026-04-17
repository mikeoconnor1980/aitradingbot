namespace TradePilot.Application.Abstractions.Services;

public interface ILlmClient
{
    Task<string> CompleteAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken);
}