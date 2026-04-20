using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParentalControl.WebService.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAliases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PrimaryUserId",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_PrimaryUserId",
                table: "Users",
                column: "PrimaryUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Users_PrimaryUserId",
                table: "Users",
                column: "PrimaryUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Users_PrimaryUserId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_PrimaryUserId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PrimaryUserId",
                table: "Users");
        }
    }
}
