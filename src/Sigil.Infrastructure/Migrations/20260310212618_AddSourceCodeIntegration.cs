using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sigil.infrastructure.Migrations
{
    /// <inheritdoc />
    internal partial class AddSourceCodeIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContextLine",
                table: "StackFrames",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "PostContext",
                table: "StackFrames",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "PreContext",
                table: "StackFrames",
                type: "text[]",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SourceCodeProviders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    BaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EncryptedAccessToken = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceCodeProviders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectRepositories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    ProviderId = table.Column<int>(type: "integer", nullable: false),
                    RepositoryOwner = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RepositoryName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DefaultBranch = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectRepositories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectRepositories_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectRepositories_SourceCodeProviders_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "SourceCodeProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectRepositories_ProjectId",
                table: "ProjectRepositories",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectRepositories_ProviderId",
                table: "ProjectRepositories",
                column: "ProviderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectRepositories");

            migrationBuilder.DropTable(
                name: "SourceCodeProviders");

            migrationBuilder.DropColumn(
                name: "ContextLine",
                table: "StackFrames");

            migrationBuilder.DropColumn(
                name: "PostContext",
                table: "StackFrames");

            migrationBuilder.DropColumn(
                name: "PreContext",
                table: "StackFrames");
        }
    }
}
