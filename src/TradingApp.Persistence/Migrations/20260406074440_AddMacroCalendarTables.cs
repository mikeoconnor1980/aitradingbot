using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingApp.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMacroCalendarTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MacroEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ProviderEventId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Country = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ScheduledAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    ReleasedAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    Importance = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Actual = table.Column<string>(type: "TEXT", nullable: true),
                    Forecast = table.Column<string>(type: "TEXT", nullable: true),
                    Previous = table.Column<string>(type: "TEXT", nullable: true),
                    Revised = table.Column<string>(type: "TEXT", nullable: true),
                    DefaultPreBlockMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultPostBlockMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    BlockStartUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    BlockEndUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    SourceUrl = table.Column<string>(type: "TEXT", nullable: true),
                    RawPayloadJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    LastSeenUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MacroEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MacroSyncRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    StartedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    CompletedAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    Succeeded = table.Column<bool>(type: "INTEGER", nullable: false),
                    EventsFetched = table.Column<int>(type: "INTEGER", nullable: false),
                    EventsInserted = table.Column<int>(type: "INTEGER", nullable: false),
                    EventsUpdated = table.Column<int>(type: "INTEGER", nullable: false),
                    Error = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MacroSyncRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MacroEvents_BlockEndUtc",
                table: "MacroEvents",
                column: "BlockEndUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MacroEvents_BlockStartUtc",
                table: "MacroEvents",
                column: "BlockStartUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MacroEvents_Importance",
                table: "MacroEvents",
                column: "Importance");

            migrationBuilder.CreateIndex(
                name: "IX_MacroEvents_Provider_ProviderEventId",
                table: "MacroEvents",
                columns: new[] { "Provider", "ProviderEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MacroEvents_ScheduledAtUtc",
                table: "MacroEvents",
                column: "ScheduledAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MacroSyncRuns_StartedAtUtc",
                table: "MacroSyncRuns",
                column: "StartedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MacroEvents");

            migrationBuilder.DropTable(
                name: "MacroSyncRuns");
        }
    }
}
