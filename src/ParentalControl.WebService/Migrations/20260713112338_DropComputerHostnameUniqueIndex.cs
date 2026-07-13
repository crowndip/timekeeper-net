using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParentalControl.WebService.Migrations
{
    /// <inheritdoc />
    public partial class DropComputerHostnameUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Computers_Hostname",
                table: "Computers");

            migrationBuilder.CreateIndex(
                name: "IX_Computers_Hostname",
                table: "Computers",
                column: "Hostname");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Computers_Hostname",
                table: "Computers");

            migrationBuilder.CreateIndex(
                name: "IX_Computers_Hostname",
                table: "Computers",
                column: "Hostname",
                unique: true);
        }
    }
}
