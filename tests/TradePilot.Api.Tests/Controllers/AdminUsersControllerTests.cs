using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TradePilot.Api.Models;
using TradePilot.Api.Tests.Infrastructure;
using TradePilot.Application.Administration.Models;
using TradePilot.Domain.Entities;

namespace TradePilot.Api.Tests.Controllers;

[TestClass]
public sealed class AdminUsersControllerTests : BaseControllerTests
{
    private const string BaseUrl = "api/admin/users";

    [TestMethod]
    public async Task GivenAdminGrant_WhenGetAdminUsers_ThenReturnsConfiguredAdmins()
    {
        await SeedAdminGrantAsync("test@tradepilot.dev");
        await SeedAdminGrantAsync("ops@tradepilot.dev");

        var client = GetTestClient();

        var response = await client.GetAsync(BaseUrl);

        var admins = await response.ReadAndAssertSuccessAsync<List<AdminUserDto>>();
        admins.Should().Contain(admin => admin.Email == "test@tradepilot.dev");
        admins.Should().Contain(admin => admin.Email == "ops@tradepilot.dev");
        admins.Should().Contain(admin => admin.Email == "mike.oconnor@hotmail.co.uk");
    }

    [TestMethod]
    public async Task GivenAdminGrant_WhenAdminUserCreated_ThenReturnsCreatedAndPersistsGrant()
    {
        await SeedAdminGrantAsync("test@tradepilot.dev");
        var client = GetTestClient();

        var response = await client.PostAsync(
            BaseUrl,
            GetStringContent(new CreateAdminUserRequest { Email = "new.admin@tradepilot.dev" }));

        response.AssertStatusCode(HttpStatusCode.Created);

        await using var context = CreateTestDbContext();
        var createdGrant = await context.AdminUserGrants
            .AsNoTracking()
            .SingleOrDefaultAsync(grant => grant.Email == "new.admin@tradepilot.dev");

        createdGrant.Should().NotBeNull();
    }

    [TestMethod]
    public async Task GivenDuplicateAdminGrant_WhenAdminUserCreated_ThenReturnsConflict()
    {
        await SeedAdminGrantAsync("test@tradepilot.dev");
        await SeedAdminGrantAsync("duplicate@tradepilot.dev");
        var client = GetTestClient();

        var response = await client.PostAsync(
            BaseUrl,
            GetStringContent(new CreateAdminUserRequest { Email = "duplicate@tradepilot.dev" }));

        response.AssertStatusCode(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorCode").GetString().Should().Be("conflict");
    }

    [TestMethod]
    public async Task GivenMultipleAdminGrants_WhenAdminUserRemoved_ThenReturnsNoContent()
    {
        await SeedAdminGrantAsync("test@tradepilot.dev");
        var removableGrantId = await SeedAdminGrantAsync("remove.me@tradepilot.dev");
        var client = GetTestClient();

        var response = await client.DeleteAsync($"{BaseUrl}/{removableGrantId}");

        response.AssertStatusCode(HttpStatusCode.NoContent);

        await using var context = CreateTestDbContext();
        var deletedGrant = await context.AdminUserGrants
            .AsNoTracking()
            .SingleOrDefaultAsync(grant => grant.Id == removableGrantId);

        deletedGrant.Should().BeNull();
    }

    [TestMethod]
    public async Task GivenSingleAdminGrant_WhenAdminUserRemoved_ThenReturnsConflict()
    {
        var soleGrantId = await SeedAdminGrantAsync("test@tradepilot.dev");
        var client = GetTestClient();

        await using (var setupContext = CreateTestDbContext())
        {
            var extraGrants = await setupContext.AdminUserGrants
                .Where(grant => grant.Email != "test@tradepilot.dev")
                .ToListAsync();
            setupContext.AdminUserGrants.RemoveRange(extraGrants);
            await setupContext.SaveChangesAsync();
        }

        var response = await client.DeleteAsync($"{BaseUrl}/{soleGrantId}");

        response.AssertStatusCode(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorCode").GetString().Should().Be("conflict");
    }

    [TestMethod]
    public async Task GivenNonAdmin_WhenGetAdminUsers_ThenReturnsForbidden()
    {
        var client = GetTestClient();

        var response = await client.GetAsync(BaseUrl);

        response.AssertStatusCode(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task GivenAdminGrant_WhenGetCurrentUser_ThenAuthMeReturnsIsAdminTrue()
    {
        await SeedAdminGrantAsync("test@tradepilot.dev");
        var client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GenerateTestToken(
                userId: Guid.NewGuid().ToString(),
                email: "test@tradepilot.dev",
                displayName: "Admin Test User"));

        var response = await client.GetAsync("api/auth/me");

        var me = await response.ReadAndAssertSuccessAsync<JsonElement>();
        me.GetProperty("isAdmin").GetBoolean().Should().BeTrue();
    }

    private async Task<Guid> SeedAdminGrantAsync(string email)
    {
        await using var context = CreateTestDbContext();

        var normalizedEmail = AdminUserGrant.NormalizeEmail(email);
        var existingGrant = await context.AdminUserGrants
            .SingleOrDefaultAsync(grant => grant.Email == normalizedEmail);

        if (existingGrant is not null)
        {
            return existingGrant.Id;
        }

        var grant = AdminUserGrant.Create(normalizedEmail);
        context.AdminUserGrants.Add(grant);
        await context.SaveChangesAsync();

        return grant.Id;
    }
}