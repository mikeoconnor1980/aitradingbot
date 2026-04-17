using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradePilot.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tier = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAtUtc = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subscriptions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_UserId",
                table: "Subscriptions",
                column: "UserId");

            // Seed existing active users with a 30-day free tier subscription.
            // Uses NEWID() for SQL Server.
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var expiresMs = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeMilliseconds();

            migrationBuilder.Sql($"""
                INSERT INTO Subscriptions (Id, UserId, Tier, Status, StartedAtUtc, ExpiresAtUtc, CreatedAtUtc)
                SELECT
                    NEWID(),
                    Id,
                    0,
                    0,
                    {nowMs},
                    {expiresMs},
                    {nowMs}
                FROM Users
                WHERE IsActive = 1
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Subscriptions");
        }
    }
}
