using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAttendance.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAutoAttendanceSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DefaultDurationMinutes",
                table: "Courses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DefaultMethod",
                table: "Courses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DefaultRadiusMeters",
                table: "Courses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsAutoAttendanceEnabled",
                table: "Courses",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultDurationMinutes",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "DefaultMethod",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "DefaultRadiusMeters",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "IsAutoAttendanceEnabled",
                table: "Courses");
        }
    }
}
