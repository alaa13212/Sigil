using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sigil.Infrastructure.Migrations
{
    /// <inheritdoc />
    internal partial class AddIndexOnEventsReceivedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Events_ReceivedAt",
                table: "Events",
                column: "ReceivedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Events_ReceivedAt",
                table: "Events");
        }
    }
}
