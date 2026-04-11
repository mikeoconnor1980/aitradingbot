using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingApp.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPreferredNetwork : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreferredNetwork",
                table: "Users",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "mainnet");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreferredNetwork",
                table: "Users");
        }
    }
}
