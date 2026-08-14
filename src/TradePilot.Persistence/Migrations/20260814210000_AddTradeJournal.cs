using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradePilot.Persistence.Migrations;

/// <summary>Adds durable logical trade evidence and links existing live fills to it.</summary>
[DbContext(typeof(TradePilotDbContext))]
[Migration("20260814210000_AddTradeJournal")]
public partial class AddTradeJournal : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TradeJournalRecords",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                StrategyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                StrategyName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                StrategyVersion = table.Column<int>(type: "int", nullable: true),
                ConfigurationIdentity = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Side = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                EntryTimeUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                ExitTimeUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                EntryPrice = table.Column<double>(type: "float", nullable: false),
                ExitPrice = table.Column<double>(type: "float", nullable: true),
                EntryQuantity = table.Column<double>(type: "float", nullable: false),
                ExitQuantity = table.Column<double>(type: "float", nullable: false),
                Leverage = table.Column<double>(type: "float", nullable: true),
                GrossPnl = table.Column<double>(type: "float", nullable: false),
                Fees = table.Column<double>(type: "float", nullable: false),
                Funding = table.Column<double>(type: "float", nullable: true),
                NetPnl = table.Column<double>(type: "float", nullable: false),
                DurationMilliseconds = table.Column<long>(type: "bigint", nullable: true),
                MfeAmount = table.Column<double>(type: "float", nullable: true),
                MfePercent = table.Column<double>(type: "float", nullable: true),
                MaeAmount = table.Column<double>(type: "float", nullable: true),
                MaePercent = table.Column<double>(type: "float", nullable: true),
                EntryStrategyEvaluationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ExitStrategyEvaluationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ExitReason = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                EntryMarketRegime = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                Timeframe = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                SourceExchange = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                SourceLifecycleId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TradeJournalRecords", x => x.Id);
                table.ForeignKey(
                    name: "FK_TradeJournalRecords_StrategyEvaluations_EntryStrategyEvaluationId",
                    column: x => x.EntryStrategyEvaluationId,
                    principalTable: "StrategyEvaluations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_TradeJournalRecords_StrategyEvaluations_ExitStrategyEvaluationId",
                    column: x => x.ExitStrategyEvaluationId,
                    principalTable: "StrategyEvaluations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.AddColumn<string>(
            name: "GridCycleId",
            table: "LiveFills",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: "");
        migrationBuilder.AddColumn<bool>(
            name: "IsEntry",
            table: "LiveFills",
            type: "bit",
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "TradeType",
            table: "LiveFills",
            type: "nvarchar(30)",
            maxLength: 30,
            nullable: false,
            defaultValue: "");
        migrationBuilder.AddColumn<Guid>(
            name: "TradeJournalRecordId",
            table: "LiveFills",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_LiveFills_TradeJournalRecordId",
            table: "LiveFills",
            column: "TradeJournalRecordId");
        migrationBuilder.CreateIndex(
            name: "IX_TradeJournalRecords_EntryStrategyEvaluationId",
            table: "TradeJournalRecords",
            column: "EntryStrategyEvaluationId");
        migrationBuilder.CreateIndex(
            name: "IX_TradeJournalRecords_ExitStrategyEvaluationId",
            table: "TradeJournalRecords",
            column: "ExitStrategyEvaluationId");
        migrationBuilder.CreateIndex(
            name: "IX_TradeJournalRecords_Strategy_Version_ExitTimeUtc",
            table: "TradeJournalRecords",
            columns: new[] { "StrategyId", "StrategyVersion", "ExitTimeUtc" });
        migrationBuilder.CreateIndex(
            name: "IX_TradeJournalRecords_User_Status_EntryTimeUtc",
            table: "TradeJournalRecords",
            columns: new[] { "UserId", "Status", "EntryTimeUtc" });
        migrationBuilder.CreateIndex(
            name: "IX_TradeJournalRecords_User_Symbol_ExitTimeUtc",
            table: "TradeJournalRecords",
            columns: new[] { "UserId", "Symbol", "ExitTimeUtc" });

        migrationBuilder.AddForeignKey(
            name: "FK_LiveFills_TradeJournalRecords_TradeJournalRecordId",
            table: "LiveFills",
            column: "TradeJournalRecordId",
            principalTable: "TradeJournalRecords",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_LiveFills_TradeJournalRecords_TradeJournalRecordId",
            table: "LiveFills");
        migrationBuilder.DropTable(name: "TradeJournalRecords");
        migrationBuilder.DropIndex(name: "IX_LiveFills_TradeJournalRecordId", table: "LiveFills");
        migrationBuilder.DropColumn(name: "GridCycleId", table: "LiveFills");
        migrationBuilder.DropColumn(name: "IsEntry", table: "LiveFills");
        migrationBuilder.DropColumn(name: "TradeType", table: "LiveFills");
        migrationBuilder.DropColumn(name: "TradeJournalRecordId", table: "LiveFills");
    }
}
