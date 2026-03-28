using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingApp.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceToCandles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Candles_Symbol_Interval_Timestamp",
                table: "Candles");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Candles",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "Hyperliquid");

            migrationBuilder.CreateIndex(
                name: "IX_Candles_Source_Symbol_Interval_Timestamp",
                table: "Candles",
                columns: new[] { "Source", "Symbol", "Interval", "Timestamp" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Candles_Source_Symbol_Interval_Timestamp",
                table: "Candles");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Candles");

            migrationBuilder.CreateIndex(
                name: "IX_Candles_Symbol_Interval_Timestamp",
                table: "Candles",
                columns: new[] { "Symbol", "Interval", "Timestamp" },
                unique: true);
        }
    }
}
