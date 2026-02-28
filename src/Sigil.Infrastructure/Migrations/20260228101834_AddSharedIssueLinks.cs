using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sigil.Infrastructure.Migrations
{
    /// <inheritdoc />
    internal partial class AddSharedIssueLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SharedIssueLinks",
                columns: table => new
                {
                    Token = table.Column<Guid>(type: "uuid", nullable: false),
                    IssueId = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedIssueLinks", x => x.Token);
                    table.ForeignKey(
                        name: "FK_SharedIssueLinks_Issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "Issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SharedIssueLinks_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SharedIssueLinks_CreatedByUserId",
                table: "SharedIssueLinks",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedIssueLinks_ExpiresAt",
                table: "SharedIssueLinks",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_SharedIssueLinks_IssueId",
                table: "SharedIssueLinks",
                column: "IssueId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SharedIssueLinks");
        }
    }
}
