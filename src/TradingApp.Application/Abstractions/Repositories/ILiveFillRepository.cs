using TradingApp.Domain.Entities;

namespace TradingApp.Application.Abstractions.Repositories;

public interface ILiveFillRepository
{
    Task AddAsync(LiveFill fill, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LiveFill>> GetBySymbolAsync(string symbol, DateTime? since = null, int limit = 100, CancellationToken cancellationToken = default);
}
