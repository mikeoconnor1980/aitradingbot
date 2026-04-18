using TradePilot.Domain.Entities;

namespace TradePilot.Persistence.Seeding;

public static class AdminUserGrantSeeder
{
    private static readonly string[] DefaultAdminEmails =
    [
        "mike.oconnor@hotmail.co.uk",
    ];

    public static async Task SeedAsync(TradePilotDbContext db, CancellationToken cancellationToken = default)
    {
        foreach (var email in DefaultAdminEmails)
        {
            var normalizedEmail = AdminUserGrant.NormalizeEmail(email);
            var exists = db.AdminUserGrants.Any(grant => grant.Email == normalizedEmail);
            if (exists)
            {
                continue;
            }

            db.AdminUserGrants.Add(AdminUserGrant.Create(normalizedEmail));
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}