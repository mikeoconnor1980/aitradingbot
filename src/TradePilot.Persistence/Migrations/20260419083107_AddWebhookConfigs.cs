using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradePilot.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WebhookConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Token = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DefaultAsset = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TargetAgentId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "bigint", nullable: false),
                    LastTriggeredAtUtc = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WebhookConfigs_Token",
                table: "WebhookConfigs",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebhookConfigs_UserId",
                table: "WebhookConfigs",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WebhookConfigs");
        }
    }
}
