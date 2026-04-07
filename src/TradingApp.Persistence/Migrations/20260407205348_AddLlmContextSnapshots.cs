using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingApp.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLlmContextSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LlmContextSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    MarketSentiment = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    MacroRegime = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    EventRisk = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: false),
                    DerivedRegime = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    GeneratedAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmContextSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LlmContextSnapshots_GeneratedAtUtc",
                table: "LlmContextSnapshots",
                column: "GeneratedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_LlmContextSnapshots_Symbol_GeneratedAtUtc",
                table: "LlmContextSnapshots",
                columns: new[] { "Symbol", "GeneratedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LlmContextSnapshots");
        }
    }
}
