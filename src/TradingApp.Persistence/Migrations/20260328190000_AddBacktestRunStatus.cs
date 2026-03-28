using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingApp.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBacktestRunStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "BacktestRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 2); // Completed — existing rows are completed runs

            migrationBuilder.AddColumn<int>(
                name: "Progress",
                table: "BacktestRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 100); // Existing rows are 100% complete

            migrationBuilder.AddColumn<int>(
                name: "TotalCandles",
                table: "BacktestRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "BacktestRuns",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Status", table: "BacktestRuns");
            migrationBuilder.DropColumn(name: "Progress", table: "BacktestRuns");
            migrationBuilder.DropColumn(name: "TotalCandles", table: "BacktestRuns");
            migrationBuilder.DropColumn(name: "ErrorMessage", table: "BacktestRuns");
        }
    }
}
