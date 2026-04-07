using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingApp.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLiveTradingEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GridCycles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GridCycleId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    StrategyName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AnchorPrice = table.Column<double>(type: "REAL", nullable: false),
                    TotalLevels = table.Column<int>(type: "INTEGER", nullable: false),
                    FilledLevels = table.Column<int>(type: "INTEGER", nullable: false),
                    Lifecycle = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CloseReason = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    RealisedPnl = table.Column<double>(type: "REAL", nullable: true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GridCycles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LiveFills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Side = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Direction = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Price = table.Column<double>(type: "REAL", nullable: false),
                    Size = table.Column<double>(type: "REAL", nullable: false),
                    Fee = table.Column<double>(type: "REAL", nullable: false),
                    ClosedPnl = table.Column<double>(type: "REAL", nullable: false),
                    FilledAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveFills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LiveOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    GridCycleId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Side = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    OrderType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Price = table.Column<double>(type: "REAL", nullable: false),
                    Size = table.Column<double>(type: "REAL", nullable: false),
                    TradeType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PlacedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FilledAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveOrders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GridCycles_GridCycleId",
                table: "GridCycles",
                column: "GridCycleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GridCycles_Strategy_Symbol_Lifecycle",
                table: "GridCycles",
                columns: new[] { "StrategyName", "Symbol", "Lifecycle" });

            migrationBuilder.CreateIndex(
                name: "IX_GridCycles_UserId",
                table: "GridCycles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LiveFills_OrderId",
                table: "LiveFills",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_LiveFills_Symbol_FilledAtUtc",
                table: "LiveFills",
                columns: new[] { "Symbol", "FilledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LiveFills_UserId",
                table: "LiveFills",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LiveOrders_GridCycleId",
                table: "LiveOrders",
                column: "GridCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_LiveOrders_OrderId",
                table: "LiveOrders",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LiveOrders_UserId",
                table: "LiveOrders",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GridCycles");

            migrationBuilder.DropTable(
                name: "LiveFills");

            migrationBuilder.DropTable(
                name: "LiveOrders");
        }
    }
}
