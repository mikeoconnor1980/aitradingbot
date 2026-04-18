using System.Net;
using Microsoft.Extensions.DependencyInjection;
using TradePilot.Api.Tests.Infrastructure;
using TradePilot.Domain.Entities;

namespace TradePilot.Api.Tests.Controllers;

[TestClass]
public sealed class FearGreedControllerTests : BaseControllerTests
{
    protected override void ConfigureTestServices(IServiceCollection services)
    {
        using var db = CreateTestDbContext();
        db.FearGreedReadings.Add(FearGreedReading.Create(
            value: 26,
            classification: "Fear",
            timestamp: new DateTimeOffset(2026, 4, 18, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(),
            fetchedAtUtc: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        db.SaveChanges();
    }

    [TestMethod]
    public async Task GivenAnonymousClient_WhenGetStatus_ThenReturnsOk()
    {
        var client = GetTestClient(authenticate: false);

        var response = await client.GetAsync("api/fear-greed/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}