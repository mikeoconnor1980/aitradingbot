using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradePilot.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStrategyLiveAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedAgentId",
                table: "Strategies",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LastStartedAtUtc",
                table: "Strategies",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LastStoppedAtUtc",
                table: "Strategies",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedAgentId",
                table: "Strategies");

            migrationBuilder.DropColumn(
                name: "LastStartedAtUtc",
                table: "Strategies");

            migrationBuilder.DropColumn(
                name: "LastStoppedAtUtc",
                table: "Strategies");
        }
    }
}
