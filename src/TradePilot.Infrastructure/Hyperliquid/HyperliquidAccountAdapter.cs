using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;
using TradePilot.Domain.ValueObjects;

namespace TradePilot.Infrastructure.Hyperliquid;

public sealed class HyperliquidAccountAdapter : IExchangeAccountClient
{
    private readonly IHyperliquidAccountService _accountService;

    public HyperliquidAccountAdapter(IHyperliquidAccountService accountService)
    {
        _accountService = accountService;
    }

    public Exchange Exchange => Exchange.Hyperliquid;

    public Task<AccountSummaryDto> GetAccountSummaryAsync(string? walletAddress = null, CancellationToken cancellationToken = default)
    {
        return _accountService.GetAccountSummaryAsync(walletAddress, cancellationToken);
    }

    public Task<IReadOnlyList<PositionDto>> GetPositionsAsync(string? walletAddress = null, CancellationToken cancellationToken = default)
    {
        return _accountService.GetPositionsAsync(walletAddress, cancellationToken);
    }

    public Task<IReadOnlyList<OpenOrderDto>> GetOpenOrdersAsync(string? walletAddress = null, CancellationToken cancellationToken = default)
    {
        return _accountService.GetOpenOrdersAsync(walletAddress, cancellationToken);
    }

    public Task<IReadOnlyList<FillEventDto>> GetRecentFillsAsync(
        TradingPair? pair = null,
        string? walletAddress = null,
        CancellationToken cancellationToken = default)
    {
        return _accountService.GetRecentFillsAsync(pair?.Base, walletAddress, cancellationToken);
    }
}