using System.Threading;
using TradingApp.Application.Abstractions.Services;

namespace TradingApp.Infrastructure.Services;

public sealed class NonceProvider : INonceProvider
{
    private long _lastNonce;

    public long GetNextNonce()
    {
        var currentMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long lastNonce;
        long nextNonce;

        do
        {
            lastNonce = Interlocked.Read(ref _lastNonce);
            nextNonce = Math.Max(currentMilliseconds, lastNonce + 1);
        }
        while (Interlocked.CompareExchange(ref _lastNonce, nextNonce, lastNonce) != lastNonce);

        return nextNonce;
    }
}