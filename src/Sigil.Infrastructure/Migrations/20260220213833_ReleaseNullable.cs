using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sigil.infrastructure.Migrations
{
    /// <inheritdoc />
    internal partial class ReleaseNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Events_Releases_ReleaseId",
                table: "Events");

            migrationBuilder.DropForeignKey(
                name: "FK_IssueActivities_Users_UserId",
                table: "IssueActivities");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "IssueActivities",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<int>(
                name: "ReleaseId",
                table: "Events",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_Events_Releases_ReleaseId",
                table: "Events",
                column: "ReleaseId",
                principalTable: "Releases",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_IssueActivities_Users_UserId",
                table: "IssueActivities",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Events_Releases_ReleaseId",
                table: "Events");

            migrationBuilder.DropForeignKey(
                name: "FK_IssueActivities_Users_UserId",
                table: "IssueActivities");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "IssueActivities",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ReleaseId",
                table: "Events",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Events_Releases_ReleaseId",
                table: "Events",
                column: "ReleaseId",
                principalTable: "Releases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_IssueActivities_Users_UserId",
                table: "IssueActivities",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
