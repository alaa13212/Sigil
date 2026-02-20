using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sigil.Infrastructure.Migrations
{
    /// <inheritdoc />
    internal partial class AddIssueIgnoreFilter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IgnoreFilterId",
                table: "Issues",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Issues_IgnoreFilterId",
                table: "Issues",
                column: "IgnoreFilterId");

            migrationBuilder.AddForeignKey(
                name: "FK_Issues_EventFilters_IgnoreFilterId",
                table: "Issues",
                column: "IgnoreFilterId",
                principalTable: "EventFilters",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Issues_EventFilters_IgnoreFilterId",
                table: "Issues");

            migrationBuilder.DropIndex(
                name: "IX_Issues_IgnoreFilterId",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "IgnoreFilterId",
                table: "Issues");
        }
    }
}
