using Microsoft.AspNetCore.SignalR;

namespace TradingApp.Api.Hubs;

/// <summary>
/// SignalR hub for real-time market data streaming.
/// This hub is a thin relay and receives broadcasts from MarketDataStreamService.
/// </summary>
public sealed class MarketDataHub : Hub
{
    private readonly ILogger<MarketDataHub> _logger;

    public MarketDataHub(ILogger<MarketDataHub> logger)
    {
        _logger = logger;
    }

    public override Task OnConnectedAsync()
    {
        _logger.LogInformation("SignalR client connected: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation(
            "SignalR client disconnected: {ConnectionId}, Error: {Error}",
            Context.ConnectionId,
            exception?.Message);
        return base.OnDisconnectedAsync(exception);
    }
}
