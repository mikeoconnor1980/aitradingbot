using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingApp.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFitnessMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CalmarRatio",
                table: "OptimizationResults",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ProfitFactor",
                table: "OptimizationResults",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SharpeRatio",
                table: "OptimizationResults",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SortinoRatio",
                table: "OptimizationResults",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CalmarRatio",
                table: "OptimizationResults");

            migrationBuilder.DropColumn(
                name: "ProfitFactor",
                table: "OptimizationResults");

            migrationBuilder.DropColumn(
                name: "SharpeRatio",
                table: "OptimizationResults");

            migrationBuilder.DropColumn(
                name: "SortinoRatio",
                table: "OptimizationResults");
        }
    }
}
