using Microsoft.EntityFrameworkCore;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Domain.Entities;

namespace TradePilot.Persistence.Repositories;

public sealed class WebhookConfigRepository : IWebhookConfigRepository
{
    private readonly TradePilotDbContext _db;

    public WebhookConfigRepository(TradePilotDbContext db)
    {
        _db = db;
    }

    public async Task<WebhookConfig?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.WebhookConfigs.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    public async Task<WebhookConfig?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return await _db.WebhookConfigs.FirstOrDefaultAsync(w => w.Token == token, cancellationToken);
    }

    public async Task<List<WebhookConfig>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.WebhookConfigs
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.UpdatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(WebhookConfig webhookConfig, CancellationToken cancellationToken = default)
    {
        await _db.WebhookConfigs.AddAsync(webhookConfig, cancellationToken);
    }

    public void Remove(WebhookConfig webhookConfig)
    {
        _db.WebhookConfigs.Remove(webhookConfig);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _db.SaveChangesAsync(cancellationToken);
    }
}