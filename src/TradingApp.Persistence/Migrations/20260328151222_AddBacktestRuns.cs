using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingApp.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBacktestRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BacktestRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IntervalsJson = table.Column<string>(type: "TEXT", nullable: false),
                    StartDateUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    EndDateUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    StrategyConfigJson = table.Column<string>(type: "TEXT", nullable: false),
                    InitialCapital = table.Column<double>(type: "REAL", nullable: false),
                    CandlesReplayed = table.Column<int>(type: "INTEGER", nullable: false),
                    ElapsedMs = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalTrades = table.Column<int>(type: "INTEGER", nullable: false),
                    WinningTrades = table.Column<int>(type: "INTEGER", nullable: false),
                    LosingTrades = table.Column<int>(type: "INTEGER", nullable: false),
                    WinRate = table.Column<double>(type: "REAL", nullable: false),
                    TotalPnl = table.Column<double>(type: "REAL", nullable: false),
                    MaxDrawdown = table.Column<double>(type: "REAL", nullable: false),
                    AverageTradePnl = table.Column<double>(type: "REAL", nullable: false),
                    AverageHoldTimeMinutes = table.Column<double>(type: "REAL", nullable: false),
                    HedgesOpened = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalFeesPaid = table.Column<double>(type: "REAL", nullable: false),
                    TradesJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BacktestRuns", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BacktestRuns");
        }
    }
}
