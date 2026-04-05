using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingApp.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutOfSampleMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "OosFitnessScore",
                table: "OptimizationResults",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "OosMaxDrawdown",
                table: "OptimizationResults",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "OosTotalPnl",
                table: "OptimizationResults",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OosTotalTrades",
                table: "OptimizationResults",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "OosWinRate",
                table: "OptimizationResults",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OosFitnessScore",
                table: "OptimizationResults");

            migrationBuilder.DropColumn(
                name: "OosMaxDrawdown",
                table: "OptimizationResults");

            migrationBuilder.DropColumn(
                name: "OosTotalPnl",
                table: "OptimizationResults");

            migrationBuilder.DropColumn(
                name: "OosTotalTrades",
                table: "OptimizationResults");

            migrationBuilder.DropColumn(
                name: "OosWinRate",
                table: "OptimizationResults");
        }
    }
}
