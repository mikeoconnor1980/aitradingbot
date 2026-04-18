using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradePilot.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStrategyTemplateNameUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_StrategyTemplates_Name",
                table: "StrategyTemplates",
                column: "Name",
                unique: true,
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StrategyTemplates_Name",
                table: "StrategyTemplates");
        }
    }
}
