namespace TradePilot.Application.Abstractions.Services;

public interface INonceProvider
{
    long GetNextNonce();
}