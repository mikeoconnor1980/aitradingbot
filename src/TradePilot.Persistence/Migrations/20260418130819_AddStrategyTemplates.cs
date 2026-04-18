using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradePilot.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStrategyTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StrategyTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    StrategyMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Market = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TagsJson = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ConfigJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategyTemplates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StrategyTemplates_IsActive_SortOrder",
                table: "StrategyTemplates",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_StrategyTemplates_Slug",
                table: "StrategyTemplates",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StrategyTemplates");
        }
    }
}
