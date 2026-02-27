using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sigil.Infrastructure.Migrations
{
    /// <inheritdoc />
    internal partial class Phase5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DigestBatchMetrics",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    EventCount = table.Column<int>(type: "integer", nullable: false),
                    QueueDepthAtStart = table.Column<int>(type: "integer", nullable: false),
                    ParseElapsedMs = table.Column<int>(type: "integer", nullable: false),
                    DigestElapsedMs = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DigestBatchMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DigestBatchMetrics_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IngestionSpikes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DroppedCount = table.Column<int>(type: "integer", nullable: false),
                    AcceptedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngestionSpikes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngestionSpikes_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StackTraceFilters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    Field = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Operator = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StackTraceFilters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StackTraceFilters_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DigestBatchMetrics_ProjectId",
                table: "DigestBatchMetrics",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_DigestBatchMetrics_RecordedAt",
                table: "DigestBatchMetrics",
                column: "RecordedAt");

            migrationBuilder.CreateIndex(
                name: "IX_IngestionSpikes_ProjectId",
                table: "IngestionSpikes",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_IngestionSpikes_StartedAt",
                table: "IngestionSpikes",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_StackTraceFilters_ProjectId",
                table: "StackTraceFilters",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DigestBatchMetrics");

            migrationBuilder.DropTable(
                name: "IngestionSpikes");

            migrationBuilder.DropTable(
                name: "StackTraceFilters");
        }
    }
}
