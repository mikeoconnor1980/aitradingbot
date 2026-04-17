using Microsoft.EntityFrameworkCore;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;
using TradePilot.Persistence.Repositories;

namespace TradePilot.Persistence.Tests.Repositories;

[TestClass]
public sealed class StrategyRevisionRepositoryTests
{
    private DbContextOptions<TradePilotDbContext> _contextOptions = null!;

    [TestInitialize]
    public void Setup()
    {
        _contextOptions = new DbContextOptionsBuilder<TradePilotDbContext>()
            .UseInMemoryDatabase(databaseName: $"StrategyRevisionTests-{Guid.NewGuid():N}")
            .Options;

        using var context = new TradePilotDbContext(_contextOptions);
        context.Database.EnsureCreated();
    }

    private TradePilotDbContext CreateContext() => new(_contextOptions);

    [TestMethod]
    public async Task GivenValidRevision_WhenAddAsync_ThenRevisionIsPersisted()
    {
        var strategy = await SeedStrategyAsync();
        var revision = StrategyRevision.Create(strategy.Id, 1, "{}", RevisionSource.Ui, "Initial version");

        await using (var context = CreateContext())
        {
            var sut = new StrategyRevisionRepository(context);
            await sut.AddAsync(revision);
        }

        await using var verifyContext = CreateContext();
        var stored = await verifyContext.StrategyRevisions.FirstOrDefaultAsync();

        stored.Should().NotBeNull();
        stored!.RevisionNumber.Should().Be(1);
    }

    [TestMethod]
    public async Task GivenExistingRevision_WhenGetByStrategyAndRevisionAsync_ThenReturnsRevision()
    {
        var strategy = await SeedStrategyAsync();
        await SeedRevisionAsync(strategy.Id, 1, "Initial version");

        await using var context = CreateContext();
        var sut = new StrategyRevisionRepository(context);

        var result = await sut.GetByStrategyAndRevisionAsync(strategy.Id, 1);

        result.Should().NotBeNull();
        result!.RevisionNumber.Should().Be(1);
    }

    [TestMethod]
    public async Task GivenNoRevision_WhenGetByStrategyAndRevisionAsync_ThenReturnsNull()
    {
        var strategy = await SeedStrategyAsync();

        await using var context = CreateContext();
        var sut = new StrategyRevisionRepository(context);

        var result = await sut.GetByStrategyAndRevisionAsync(strategy.Id, 99);

        result.Should().BeNull();
    }

    [TestMethod]
    public async Task GivenMultipleRevisions_WhenGetPagedByStrategyIdAsync_ThenReturnsPaginatedResultsInDescendingOrder()
    {
        var strategy = await SeedStrategyAsync();
        await SeedRevisionAsync(strategy.Id, 1, "v1");
        await SeedRevisionAsync(strategy.Id, 2, "v2");
        await SeedRevisionAsync(strategy.Id, 3, "v3");

        await using var context = CreateContext();
        var sut = new StrategyRevisionRepository(context);

        var result = await sut.GetPagedByStrategyIdAsync(strategy.Id, 1, 2);

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(3);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(2);
        result.Items[0].RevisionNumber.Should().Be(3);
        result.Items[1].RevisionNumber.Should().Be(2);
    }

    [TestMethod]
    public async Task GivenRevisions_WhenGetLatestRevisionNumberAsync_ThenReturnsMaxNumber()
    {
        var strategy = await SeedStrategyAsync();
        await SeedRevisionAsync(strategy.Id, 1, "v1");
        await SeedRevisionAsync(strategy.Id, 2, "v2");

        await using var context = CreateContext();
        var sut = new StrategyRevisionRepository(context);

        var latest = await sut.GetLatestRevisionNumberAsync(strategy.Id);

        latest.Should().Be(2);
    }

    [TestMethod]
    public async Task GivenNoRevisions_WhenGetLatestRevisionNumberAsync_ThenReturnsZero()
    {
        var strategy = await SeedStrategyAsync();

        await using var context = CreateContext();
        var sut = new StrategyRevisionRepository(context);

        var latest = await sut.GetLatestRevisionNumberAsync(strategy.Id);

        latest.Should().Be(0);
    }

    private async Task<Strategy> SeedStrategyAsync()
    {
        var strategy = Strategy.Create("user-1", "Test Strategy", "GridStrategy", "{}");

        await using var context = CreateContext();
        context.Strategies.Add(strategy);
        await context.SaveChangesAsync();

        return strategy;
    }

    private async Task SeedRevisionAsync(Guid strategyId, int revisionNumber, string changeSummary)
    {
        var revision = StrategyRevision.Create(strategyId, revisionNumber, "{}", RevisionSource.Ui, changeSummary);

        await using var context = CreateContext();
        context.StrategyRevisions.Add(revision);
        await context.SaveChangesAsync();
    }
}