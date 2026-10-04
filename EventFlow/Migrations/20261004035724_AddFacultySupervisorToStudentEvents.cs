using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventFlow.Migrations
{
    /// <inheritdoc />
    public partial class AddFacultySupervisorToStudentEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FacultySupervisorId",
                table: "Events",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Events_FacultySupervisorId",
                table: "Events",
                column: "FacultySupervisorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Events_AspNetUsers_FacultySupervisorId",
                table: "Events",
                column: "FacultySupervisorId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Events_AspNetUsers_FacultySupervisorId",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_Events_FacultySupervisorId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "FacultySupervisorId",
                table: "Events");
        }
    }
}
