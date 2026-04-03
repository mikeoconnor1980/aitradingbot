using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingApp.Persistence.Migrations
{
    public partial class CleanOldBacktestData : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM BacktestRuns;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}