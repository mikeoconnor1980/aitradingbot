using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradePilot.Persistence.Migrations;

/// <summary>Adds normalized, indexed deterministic strategy-evaluation evidence.</summary>
[DbContext(typeof(TradePilotDbContext))]
[Migration("20260814173000_AddStrategyEvaluations")]
public partial class AddStrategyEvaluations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "StrategyEvaluations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StrategyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                StrategyName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                StrategyVersion = table.Column<int>(type: "int", nullable: true),
                ConfigurationIdentity = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Timeframe = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                EvaluatedAtUtc = table.Column<long>(type: "bigint", nullable: false),
                Decision = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                SetupDetected = table.Column<bool>(type: "bit", nullable: false),
                PrimaryRejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                MarketContextTimestampUtc = table.Column<long>(type: "bigint", nullable: false),
                ReferencePrice = table.Column<double>(type: "float", nullable: false),
                MarketRegime = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                SignalType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                SignalReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                EvaluationShortCircuited = table.Column<bool>(type: "bit", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StrategyEvaluations", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "RuleEvaluations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StrategyEvaluationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EvaluationOrder = table.Column<int>(type: "int", nullable: false),
                RuleId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Passed = table.Column<bool>(type: "bit", nullable: false),
                ActualValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                ActualNumericValue = table.Column<double>(type: "float", nullable: true),
                ExpectedValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                ExpectedNumericValue = table.Column<double>(type: "float", nullable: true),
                Unit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                IsBlocking = table.Column<bool>(type: "bit", nullable: false),
                Kind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RuleEvaluations", x => x.Id);
                table.ForeignKey(
                    name: "FK_RuleEvaluations_StrategyEvaluations_StrategyEvaluationId",
                    column: x => x.StrategyEvaluationId,
                    principalTable: "StrategyEvaluations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_StrategyEvaluations_EvaluatedAtUtc",
            table: "StrategyEvaluations",
            column: "EvaluatedAtUtc");
        migrationBuilder.CreateIndex(
            name: "IX_StrategyEvaluations_StrategyId_Symbol_EvaluatedAtUtc",
            table: "StrategyEvaluations",
            columns: new[] { "StrategyId", "Symbol", "EvaluatedAtUtc" });
        migrationBuilder.CreateIndex(
            name: "IX_StrategyEvaluations_StrategyName_Symbol_EvaluatedAtUtc",
            table: "StrategyEvaluations",
            columns: new[] { "StrategyName", "Symbol", "EvaluatedAtUtc" });
        migrationBuilder.CreateIndex(
            name: "IX_RuleEvaluations_RuleId_Passed_IsBlocking",
            table: "RuleEvaluations",
            columns: new[] { "RuleId", "Passed", "IsBlocking" });
        migrationBuilder.CreateIndex(
            name: "IX_RuleEvaluations_StrategyEvaluationId_EvaluationOrder",
            table: "RuleEvaluations",
            columns: new[] { "StrategyEvaluationId", "EvaluationOrder" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "RuleEvaluations");
        migrationBuilder.DropTable(name: "StrategyEvaluations");
    }
}
