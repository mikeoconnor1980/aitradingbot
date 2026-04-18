using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.FearGreed.Models;
using TradePilot.Application.Trading.Models;

namespace TradePilot.Worker.Services;

public sealed class ControlPlaneFearGreedSnapshotProvider : IFearGreedSnapshotProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ControlPlaneFearGreedSnapshotProvider> _logger;

    public ControlPlaneFearGreedSnapshotProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<ControlPlaneFearGreedSnapshotProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<FearGreedSnapshot?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient(AgentCheckInService.HttpClientName);
            var status = await httpClient.GetFromJsonAsync<FearGreedStatusDto>(
                "/api/fear-greed/status",
                cancellationToken);

            if (status?.LatestValue is null || status.LatestTimestamp is null)
            {
                return null;
            }

            return new FearGreedSnapshot(
                status.LatestValue.Value,
                FearGreedSnapshot.Classify(status.LatestValue.Value),
                status.LatestTimestamp.Value.ToUnixTimeSeconds());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to fetch Fear & Greed status from control plane.");
            return null;
        }
    }
}