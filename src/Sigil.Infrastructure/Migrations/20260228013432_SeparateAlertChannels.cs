using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sigil.Infrastructure.Migrations
{
    /// <inheritdoc />
    internal partial class SeparateAlertChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create AlertChannels table
            migrationBuilder.CreateTable(
                name: "AlertChannels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Config = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertChannels", x => x.Id);
                });

            // 2. Add nullable AlertChannelId column
            migrationBuilder.AddColumn<int>(
                name: "AlertChannelId",
                table: "AlertRules",
                type: "integer",
                nullable: true);

            // 3. Backfill: create one AlertChannel per distinct (Channel, ChannelConfig) pair, then point rules at them
            migrationBuilder.Sql(@"
                INSERT INTO ""AlertChannels"" (""Name"", ""Type"", ""Config"", ""CreatedAt"")
                SELECT DISTINCT ""Name"", ""Channel"", ""ChannelConfig"", ""CreatedAt""
                FROM ""AlertRules"";

                UPDATE ""AlertRules"" r
                SET ""AlertChannelId"" = c.""Id""
                FROM ""AlertChannels"" c
                WHERE c.""Name"" = r.""Name""
                  AND c.""Type"" = r.""Channel""
                  AND c.""Config"" = r.""ChannelConfig""
                  AND c.""CreatedAt"" = r.""CreatedAt"";
            ");

            // 4. Make AlertChannelId non-nullable
            migrationBuilder.AlterColumn<int>(
                name: "AlertChannelId",
                table: "AlertRules",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            // 5. Add index + FK
            migrationBuilder.CreateIndex(
                name: "IX_AlertRules_AlertChannelId",
                table: "AlertRules",
                column: "AlertChannelId");

            migrationBuilder.AddForeignKey(
                name: "FK_AlertRules_AlertChannels_AlertChannelId",
                table: "AlertRules",
                column: "AlertChannelId",
                principalTable: "AlertChannels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // 6. Drop old columns
            migrationBuilder.DropColumn(
                name: "Channel",
                table: "AlertRules");

            migrationBuilder.DropColumn(
                name: "ChannelConfig",
                table: "AlertRules");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Re-add old columns
            migrationBuilder.AddColumn<int>(
                name: "Channel",
                table: "AlertRules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ChannelConfig",
                table: "AlertRules",
                type: "text",
                nullable: false,
                defaultValue: "{}");

            // Copy data back from AlertChannels
            migrationBuilder.Sql(@"
                UPDATE ""AlertRules"" r
                SET ""Channel"" = c.""Type"",
                    ""ChannelConfig"" = c.""Config""
                FROM ""AlertChannels"" c
                WHERE c.""Id"" = r.""AlertChannelId"";
            ");

            // Drop FK, index, column
            migrationBuilder.DropForeignKey(
                name: "FK_AlertRules_AlertChannels_AlertChannelId",
                table: "AlertRules");

            migrationBuilder.DropIndex(
                name: "IX_AlertRules_AlertChannelId",
                table: "AlertRules");

            migrationBuilder.DropColumn(
                name: "AlertChannelId",
                table: "AlertRules");

            migrationBuilder.DropTable(
                name: "AlertChannels");
        }
    }
}
