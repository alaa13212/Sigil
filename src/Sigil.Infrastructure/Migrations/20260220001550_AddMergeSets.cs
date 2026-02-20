using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sigil.Infrastructure.Migrations
{
    /// <inheritdoc />
    internal partial class AddMergeSets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MergeSetId",
                table: "Issues",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MergeSets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    PrimaryIssueId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FirstSeen = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeen = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OccurrenceCount = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MergeSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MergeSets_Issues_PrimaryIssueId",
                        column: x => x.PrimaryIssueId,
                        principalTable: "Issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MergeSets_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Issues_MergeSetId",
                table: "Issues",
                column: "MergeSetId");

            migrationBuilder.CreateIndex(
                name: "IX_MergeSets_PrimaryIssueId",
                table: "MergeSets",
                column: "PrimaryIssueId");

            migrationBuilder.CreateIndex(
                name: "IX_MergeSets_ProjectId",
                table: "MergeSets",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_Issues_MergeSets_MergeSetId",
                table: "Issues",
                column: "MergeSetId",
                principalTable: "MergeSets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Issues_MergeSets_MergeSetId",
                table: "Issues");

            migrationBuilder.DropTable(
                name: "MergeSets");

            migrationBuilder.DropIndex(
                name: "IX_Issues_MergeSetId",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "MergeSetId",
                table: "Issues");
        }
    }
}
