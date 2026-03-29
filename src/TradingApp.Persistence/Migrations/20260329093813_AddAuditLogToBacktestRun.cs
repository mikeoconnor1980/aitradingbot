using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingApp.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogToBacktestRun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AuditLogEnabled",
                table: "BacktestRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CandleLogJson",
                table: "BacktestRuns",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GridCycleLogJson",
                table: "BacktestRuns",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrderEventLogJson",
                table: "BacktestRuns",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuditLogEnabled",
                table: "BacktestRuns");

            migrationBuilder.DropColumn(
                name: "CandleLogJson",
                table: "BacktestRuns");

            migrationBuilder.DropColumn(
                name: "GridCycleLogJson",
                table: "BacktestRuns");

            migrationBuilder.DropColumn(
                name: "OrderEventLogJson",
                table: "BacktestRuns");
        }
    }
}
