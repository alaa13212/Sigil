using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sigil.infrastructure.Migrations
{
    /// <inheritdoc />
    internal partial class AddIssueLastChangedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastChangedAt",
                table: "Issues",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Backfill: use the latest activity timestamp per issue, or FirstSeen if no activities exist
            migrationBuilder.Sql("""
                UPDATE "Issues" i
                SET "LastChangedAt" = COALESCE(
                    (SELECT MAX(a."Timestamp") FROM "IssueActivities" a WHERE a."IssueId" = i."Id"),
                    i."FirstSeen"
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastChangedAt",
                table: "Issues");
        }
    }
}
