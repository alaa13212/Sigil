using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sigil.Infrastructure.Migrations
{
    /// <inheritdoc />
    internal partial class AddProjectEventSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Events_ProjectId",
                table: "Events");

            migrationBuilder.CreateIndex(
                name: "IX_Events_ProjectId_Level_Timestamp",
                table: "Events",
                columns: new[] { "ProjectId", "Level", "Timestamp" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Events_ProjectId_Timestamp",
                table: "Events",
                columns: new[] { "ProjectId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Events_ProjectId_Level_Timestamp",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_Events_ProjectId_Timestamp",
                table: "Events");

            migrationBuilder.CreateIndex(
                name: "IX_Events_ProjectId",
                table: "Events",
                column: "ProjectId");
        }
    }
}
