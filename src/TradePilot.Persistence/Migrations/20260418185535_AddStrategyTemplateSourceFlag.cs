using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradePilot.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStrategyTemplateSourceFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSystemTemplate",
                table: "StrategyTemplates",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSystemTemplate",
                table: "StrategyTemplates");
        }
    }
}
