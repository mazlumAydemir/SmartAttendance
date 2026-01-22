using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAttendance.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceMethodFixed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceSessions_Users_InstructorId",
                table: "AttendanceSessions");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceSessions_InstructorId",
                table: "AttendanceSessions");

            migrationBuilder.AddColumn<int>(
                name: "Method",
                table: "AttendanceSessions",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Method",
                table: "AttendanceSessions");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSessions_InstructorId",
                table: "AttendanceSessions",
                column: "InstructorId");

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceSessions_Users_InstructorId",
                table: "AttendanceSessions",
                column: "InstructorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
