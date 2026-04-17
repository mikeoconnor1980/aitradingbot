using TradePilot.Infrastructure.Services;

namespace TradePilot.Infrastructure.Tests.Services;

[TestClass]
public sealed class NonceProviderTests
{
    [TestMethod]
    public void GivenSequentialCalls_WhenGetNextNonce_ThenReturnsStrictlyIncreasingValues()
    {
        var provider = new NonceProvider();

        var firstNonce = provider.GetNextNonce();
        var secondNonce = provider.GetNextNonce();
        var thirdNonce = provider.GetNextNonce();

        secondNonce.Should().BeGreaterThan(firstNonce);
        thirdNonce.Should().BeGreaterThan(secondNonce);
    }

    [TestMethod]
    public void GivenConcurrentCalls_WhenGetNextNonce_ThenReturnsUniqueValues()
    {
        var provider = new NonceProvider();
        var nonces = new long[1000];

        Parallel.For(0, nonces.Length, index =>
        {
            nonces[index] = provider.GetNextNonce();
        });

        nonces.Distinct().Should().HaveCount(nonces.Length);
    }

    [TestMethod]
    public void GivenCurrentTime_WhenGetNextNonce_ThenReturnsRecentUtcMilliseconds()
    {
        var provider = new NonceProvider();
        var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var nonce = provider.GetNextNonce();

        var after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        nonce.Should().BeGreaterOrEqualTo(before);
        nonce.Should().BeLessOrEqualTo(after + 1);
    }
}