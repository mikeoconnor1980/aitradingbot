using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingApp.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKellyMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "HalfKellyPercent",
                table: "BacktestRuns",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "KellyPercent",
                table: "BacktestRuns",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "WinLossRRatio",
                table: "BacktestRuns",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HalfKellyPercent",
                table: "BacktestRuns");

            migrationBuilder.DropColumn(
                name: "KellyPercent",
                table: "BacktestRuns");

            migrationBuilder.DropColumn(
                name: "WinLossRRatio",
                table: "BacktestRuns");
        }
    }
}
