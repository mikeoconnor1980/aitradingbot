using TradePilot.Domain.Entities;

namespace TradePilot.Application.Abstractions.Repositories;

public interface IWebhookConfigRepository
{
    Task<WebhookConfig?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WebhookConfig?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<List<WebhookConfig>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(WebhookConfig webhookConfig, CancellationToken cancellationToken = default);
    void Remove(WebhookConfig webhookConfig);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}