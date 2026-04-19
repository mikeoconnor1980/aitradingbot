using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradePilot.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBeginnerVisibilityAndSubscriptionTiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBeginnerVisible",
                table: "StrategyTemplates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE StrategyTemplates
                SET IsBeginnerVisible = 1
                WHERE Slug IN ('trend-pullback-ema-long', 'vwap-intraday-pullback-long');
                """);

            migrationBuilder.Sql("""
                UPDATE Subscriptions
                SET Tier = 1
                WHERE Tier = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsBeginnerVisible",
                table: "StrategyTemplates");
        }
    }
}
