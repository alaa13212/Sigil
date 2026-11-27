using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sigil.Infrastructure.Migrations
{
    /// <inheritdoc />
    internal partial class AddSamlSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SamlConnectionId",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SamlNameId",
                table: "Users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SamlStatus",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "SamlConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EntityId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    MetadataUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MetadataXml = table.Column<string>(type: "text", nullable: true),
                    MetadataFetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MetadataError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    EmailClaimType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NameClaimType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    GroupsClaimType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DefaultTeamName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SamlConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SamlGroupMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SamlConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TeamName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    GrantsGlobalAdmin = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SamlGroupMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SamlGroupMappings_SamlConnections_SamlConnectionId",
                        column: x => x.SamlConnectionId,
                        principalTable: "SamlConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_SamlConnectionId",
                table: "Users",
                column: "SamlConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_SamlGroupMappings_SamlConnectionId",
                table: "SamlGroupMappings",
                column: "SamlConnectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_SamlConnections_SamlConnectionId",
                table: "Users",
                column: "SamlConnectionId",
                principalTable: "SamlConnections",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_SamlConnections_SamlConnectionId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "SamlGroupMappings");

            migrationBuilder.DropTable(
                name: "SamlConnections");

            migrationBuilder.DropIndex(
                name: "IX_Users_SamlConnectionId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SamlConnectionId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SamlNameId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SamlStatus",
                table: "Users");
        }
    }
}
