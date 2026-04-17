using TradePilot.Domain.Entities;

namespace TradePilot.Application.Abstractions.Services;

/// <summary>
/// HTTP client for the alternative.me Crypto Fear &amp; Greed Index API.
/// </summary>
public interface IFearGreedClient
{
    /// <summary>
    /// Fetches Fear &amp; Greed readings from the API.
    /// </summary>
    /// <param name="limit">Number of readings to fetch. 0 = all available history.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Parsed readings ordered newest-first as returned by the API.</returns>
    Task<IReadOnlyList<FearGreedReading>> FetchAsync(int limit, CancellationToken cancellationToken = default);
}
