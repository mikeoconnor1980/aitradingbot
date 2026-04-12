using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingApp.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRMultipleMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Expectancy",
                table: "BacktestRuns",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ProfitFactor",
                table: "BacktestRuns",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Sqn",
                table: "BacktestRuns",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Expectancy",
                table: "BacktestRuns");

            migrationBuilder.DropColumn(
                name: "ProfitFactor",
                table: "BacktestRuns");

            migrationBuilder.DropColumn(
                name: "Sqn",
                table: "BacktestRuns");
        }
    }
}
