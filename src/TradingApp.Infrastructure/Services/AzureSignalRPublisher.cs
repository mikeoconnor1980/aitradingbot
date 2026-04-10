using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Logging;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.MarketData.Models;

namespace TradingApp.Infrastructure.Services;

/// <summary>
/// Publishes SignalR messages via Azure SignalR Service Management SDK (REST API).
/// Used by the Worker process which does not host a SignalR hub.
/// </summary>
public sealed class AzureSignalRPublisher : ISignalRPublisher, IAsyncDisposable
{
    private const string HubName = "marketdata";

    private readonly ServiceManager _serviceManager;
    private readonly ILogger<AzureSignalRPublisher> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private ServiceHubContext? _hubContext;

    public AzureSignalRPublisher(
        ServiceManager serviceManager,
        ILogger<AzureSignalRPublisher> logger)
    {
        _serviceManager = serviceManager;
        _logger = logger;
    }

    private async Task<ServiceHubContext> GetHubContextAsync(CancellationToken cancellationToken)
    {
        if (_hubContext is not null)
            return _hubContext;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            _hubContext ??= await _serviceManager.CreateHubContextAsync(HubName, cancellationToken);
            return _hubContext;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task BroadcastPriceUpdateAsync(PriceUpdateDto update, CancellationToken cancellationToken = default)
    {
        var hub = await GetHubContextAsync(cancellationToken);
        await hub.Clients.All.SendCoreAsync("ReceivePriceUpdate", [update], cancellationToken);
    }

    public async Task BroadcastConnectionStatusAsync(ConnectionStatusDto status, CancellationToken cancellationToken = default)
    {
        var hub = await GetHubContextAsync(cancellationToken);
        await hub.Clients.All.SendCoreAsync("ReceiveConnectionStatus", [status], cancellationToken);
    }

    public async Task BroadcastFillEventAsync(FillEventDto fill, CancellationToken cancellationToken = default)
    {
        var hub = await GetHubContextAsync(cancellationToken);
        await hub.Clients.All.SendCoreAsync("ReceiveFillEvent", [fill], cancellationToken);
    }

    public async Task BroadcastOrderUpdateAsync(OrderUpdateDto orderUpdate, CancellationToken cancellationToken = default)
    {
        var hub = await GetHubContextAsync(cancellationToken);
        await hub.Clients.All.SendCoreAsync("ReceiveOrderUpdate", [orderUpdate], cancellationToken);
    }

    public async Task BroadcastUserConnectionStatusAsync(ConnectionStatusDto status, CancellationToken cancellationToken = default)
    {
        var hub = await GetHubContextAsync(cancellationToken);
        await hub.Clients.All.SendCoreAsync("ReceiveUserConnectionStatus", [status], cancellationToken);
    }

    public async Task BroadcastOrdersResyncAsync(IReadOnlyList<OpenOrderDto> orders, CancellationToken cancellationToken = default)
    {
        var hub = await GetHubContextAsync(cancellationToken);
        await hub.Clients.All.SendCoreAsync("ReceiveOrdersResync", [orders], cancellationToken);
    }

    public async Task BroadcastPositionsResyncAsync(IReadOnlyList<PositionDto> positions, CancellationToken cancellationToken = default)
    {
        var hub = await GetHubContextAsync(cancellationToken);
        await hub.Clients.All.SendCoreAsync("ReceivePositionsResync", [positions], cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubContext is not null)
        {
            await _hubContext.DisposeAsync();
        }
        _initLock.Dispose();
    }
}
