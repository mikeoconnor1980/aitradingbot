namespace TradingApp.Application.Abstractions.Services;

public interface INonceProvider
{
    long GetNextNonce();
}