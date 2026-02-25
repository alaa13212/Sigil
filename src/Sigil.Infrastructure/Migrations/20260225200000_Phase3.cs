using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sigil.infrastructure.Migrations
{
    /// <inheritdoc />
    internal partial class Phase3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectConfigs",
                columns: table => new
                {
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectConfigs", x => new { x.ProjectId, x.Key });
                    table.ForeignKey(
                        name: "FK_ProjectConfigs_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddColumn<string>(
                name: "Culprit",
                table: "Events",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExceptionType",
                table: "Events",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Culprit",        table: "Events");
            migrationBuilder.DropColumn(name: "ExceptionType",  table: "Events");
            migrationBuilder.DropTable(name: "ProjectConfigs");
        }
    }
}
