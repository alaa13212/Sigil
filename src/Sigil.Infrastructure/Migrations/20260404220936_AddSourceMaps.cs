using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sigil.Infrastructure.Migrations
{
    /// <inheritdoc />
    internal partial class AddSourceMaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExternalIssueLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IssueId = table.Column<int>(type: "integer", nullable: false),
                    TrackerType = table.Column<int>(type: "integer", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExternalUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ExternalStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalIssueLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalIssueLinks_Issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "Issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IssueTrackerConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    TrackerType = table.Column<int>(type: "integer", nullable: false),
                    EncryptedConfig = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    TwoWaySync = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueTrackerConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IssueTrackerConfigs_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SourceMaps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReleaseId = table.Column<int>(type: "integer", nullable: false),
                    MinifiedFilePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    OriginalSize = table.Column<long>(type: "bigint", nullable: false),
                    CompressedMapData = table.Column<byte[]>(type: "bytea", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceMaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourceMaps_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIssueLinks_ExternalId",
                table: "ExternalIssueLinks",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIssueLinks_IssueId_TrackerType",
                table: "ExternalIssueLinks",
                columns: new[] { "IssueId", "TrackerType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IssueTrackerConfigs_ProjectId_TrackerType",
                table: "IssueTrackerConfigs",
                columns: new[] { "ProjectId", "TrackerType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SourceMaps_ReleaseId_MinifiedFilePath",
                table: "SourceMaps",
                columns: new[] { "ReleaseId", "MinifiedFilePath" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalIssueLinks");

            migrationBuilder.DropTable(
                name: "IssueTrackerConfigs");

            migrationBuilder.DropTable(
                name: "SourceMaps");
        }
    }
}
