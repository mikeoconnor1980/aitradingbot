using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingApp.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOptimizationRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OptimizationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    StartDateUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    EndDateUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    InitialCapital = table.Column<double>(type: "REAL", nullable: false),
                    SweepConfigJson = table.Column<string>(type: "TEXT", nullable: false),
                    ThresholdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    TotalCombinations = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    QualifiedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ElapsedMs = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptimizationRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OptimizationResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OptimizationRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Rank = table.Column<int>(type: "INTEGER", nullable: false),
                    FitnessScore = table.Column<double>(type: "REAL", nullable: false),
                    StrategyConfigJson = table.Column<string>(type: "TEXT", nullable: false),
                    SignalDescription = table.Column<string>(type: "TEXT", nullable: false),
                    TotalPnl = table.Column<double>(type: "REAL", nullable: false),
                    WinRate = table.Column<double>(type: "REAL", nullable: false),
                    MaxDrawdown = table.Column<double>(type: "REAL", nullable: false),
                    TotalTrades = table.Column<int>(type: "INTEGER", nullable: false),
                    WinningTrades = table.Column<int>(type: "INTEGER", nullable: false),
                    LosingTrades = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalFeesPaid = table.Column<double>(type: "REAL", nullable: false),
                    AverageTradePnl = table.Column<double>(type: "REAL", nullable: false),
                    AverageHoldTimeMinutes = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptimizationResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OptimizationResults_OptimizationRuns_OptimizationRunId",
                        column: x => x.OptimizationRunId,
                        principalTable: "OptimizationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OptimizationResults_RunId",
                table: "OptimizationResults",
                column: "OptimizationRunId");

            migrationBuilder.CreateIndex(
                name: "IX_OptimizationResults_RunId_Rank",
                table: "OptimizationResults",
                columns: new[] { "OptimizationRunId", "Rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OptimizationRuns_CreatedAtUtc",
                table: "OptimizationRuns",
                column: "CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OptimizationResults");

            migrationBuilder.DropTable(
                name: "OptimizationRuns");
        }
    }
}
