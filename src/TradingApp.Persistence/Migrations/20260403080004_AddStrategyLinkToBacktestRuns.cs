using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingApp.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStrategyLinkToBacktestRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StrategyId",
                table: "BacktestRuns",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StrategyRevisionId",
                table: "BacktestRuns",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BacktestRuns_StrategyId",
                table: "BacktestRuns",
                column: "StrategyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BacktestRuns_StrategyId",
                table: "BacktestRuns");

            migrationBuilder.DropColumn(
                name: "StrategyId",
                table: "BacktestRuns");

            migrationBuilder.DropColumn(
                name: "StrategyRevisionId",
                table: "BacktestRuns");
        }
    }
}
