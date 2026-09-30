using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventFlow.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestedClubId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ClubPresidentId",
                table: "Clubs",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<int>(
                name: "RequestedClubId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_RequestedClubId",
                table: "AspNetUsers",
                column: "RequestedClubId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Clubs_RequestedClubId",
                table: "AspNetUsers",
                column: "RequestedClubId",
                principalTable: "Clubs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Clubs_RequestedClubId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_RequestedClubId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RequestedClubId",
                table: "AspNetUsers");

            migrationBuilder.AlterColumn<string>(
                name: "ClubPresidentId",
                table: "Clubs",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);
        }
    }
}
