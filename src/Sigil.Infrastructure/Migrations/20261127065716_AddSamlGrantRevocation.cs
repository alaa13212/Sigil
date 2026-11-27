using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sigil.Infrastructure.Migrations
{
    /// <inheritdoc />
    internal partial class AddSamlGrantRevocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SamlAdminConnectionId",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SamlGroups",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SamlConnectionId",
                table: "TeamMemberships",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamMemberships_SamlConnectionId",
                table: "TeamMemberships",
                column: "SamlConnectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_TeamMemberships_SamlConnections_SamlConnectionId",
                table: "TeamMemberships",
                column: "SamlConnectionId",
                principalTable: "SamlConnections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TeamMemberships_SamlConnections_SamlConnectionId",
                table: "TeamMemberships");

            migrationBuilder.DropIndex(
                name: "IX_TeamMemberships_SamlConnectionId",
                table: "TeamMemberships");

            migrationBuilder.DropColumn(
                name: "SamlAdminConnectionId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SamlGroups",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SamlConnectionId",
                table: "TeamMemberships");
        }
    }
}
