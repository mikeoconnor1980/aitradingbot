using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingApp.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Candles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Interval = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Timestamp = table.Column<long>(type: "INTEGER", nullable: false),
                    Open = table.Column<double>(type: "REAL", nullable: false),
                    High = table.Column<double>(type: "REAL", nullable: false),
                    Low = table.Column<double>(type: "REAL", nullable: false),
                    Close = table.Column<double>(type: "REAL", nullable: false),
                    Volume = table.Column<double>(type: "REAL", nullable: false),
                    NumTrades = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Candles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Candles_Symbol_Interval_Timestamp",
                table: "Candles",
                columns: new[] { "Symbol", "Interval", "Timestamp" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Candles");
        }
    }
}
