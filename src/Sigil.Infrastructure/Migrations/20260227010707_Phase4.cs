using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Sigil.infrastructure.Migrations
{
    /// <inheritdoc />
    internal partial class Phase4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IssueBookmarks");

            migrationBuilder.AddColumn<string>(
                name: "SuggestedEventMessage",
                table: "Issues",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuggestedFramesSummary",
                table: "Issues",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "search_vector",
                table: "Issues",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "\n                setweight(to_tsvector('simple', coalesce(\"Title\", '')), 'A') ||\n                setweight(to_tsvector('simple', coalesce(\"ExceptionType\", '')), 'A') ||\n                setweight(to_tsvector('simple', coalesce(\"Culprit\", '')), 'B') ||\n                setweight(to_tsvector('simple', coalesce(\"SuggestedEventMessage\", '')), 'B') ||\n                setweight(to_tsvector('simple', coalesce(\"SuggestedFramesSummary\", '')), 'C')\n            ",
                stored: true);

            migrationBuilder.CreateTable(
                name: "EventBuckets",
                columns: table => new
                {
                    IssueId = table.Column<int>(type: "integer", nullable: false),
                    BucketStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventBuckets", x => new { x.IssueId, x.BucketStart });
                    table.ForeignKey(
                        name: "FK_EventBuckets_Issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "Issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserIssueStates",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssueId = table.Column<int>(type: "integer", nullable: false),
                    IsBookmarked = table.Column<bool>(type: "boolean", nullable: false),
                    BookmarkedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastViewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserIssueStates", x => new { x.UserId, x.IssueId });
                    table.ForeignKey(
                        name: "FK_UserIssueStates_Issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "Issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserIssueStates_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPageViews",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    PageType = table.Column<int>(type: "integer", nullable: false),
                    LastViewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPageViews", x => new { x.UserId, x.ProjectId, x.PageType });
                    table.ForeignKey(
                        name: "FK_UserPageViews_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserPageViews_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Issues_search_vector",
                table: "Issues",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "IX_UserIssueStates_IssueId",
                table: "UserIssueStates",
                column: "IssueId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPageViews_ProjectId",
                table: "UserPageViews",
                column: "ProjectId");

            // Backfill SuggestedEventMessage for existing issues
            migrationBuilder.Sql("""
                UPDATE "Issues" i
                SET "SuggestedEventMessage" = e."Message"
                FROM "Events" e
                WHERE i."SuggestedEventId" = e."Id"
                  AND e."Message" IS NOT NULL;
                """);

            // Backfill EventBuckets from historical events
            migrationBuilder.Sql("""
                INSERT INTO "EventBuckets" ("IssueId", "BucketStart", "Count")
                SELECT
                    "IssueId",
                    date_trunc('hour', "Timestamp") AS "BucketStart",
                    COUNT(*)::integer AS "Count"
                FROM "Events"
                WHERE "IssueId" IS NOT NULL
                GROUP BY "IssueId", date_trunc('hour', "Timestamp")
                ON CONFLICT ("IssueId", "BucketStart")
                    DO UPDATE SET "Count" = EXCLUDED."Count";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventBuckets");

            migrationBuilder.DropTable(
                name: "UserIssueStates");

            migrationBuilder.DropTable(
                name: "UserPageViews");

            migrationBuilder.DropIndex(
                name: "IX_Issues_search_vector",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "search_vector",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "SuggestedEventMessage",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "SuggestedFramesSummary",
                table: "Issues");

            migrationBuilder.CreateTable(
                name: "IssueBookmarks",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssueId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueBookmarks", x => new { x.UserId, x.IssueId });
                    table.ForeignKey(
                        name: "FK_IssueBookmarks_Issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "Issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IssueBookmarks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IssueBookmarks_IssueId",
                table: "IssueBookmarks",
                column: "IssueId");
        }
    }
}
