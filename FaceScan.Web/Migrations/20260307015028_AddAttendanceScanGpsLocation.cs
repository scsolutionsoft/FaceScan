using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaceScan.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceScanGpsLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                table: "AttendanceTransactions",
                type: "decimal(9,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LocationAccuracyMeters",
                table: "AttendanceTransactions",
                type: "decimal(8,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                table: "AttendanceTransactions",
                type: "decimal(9,6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "AttendanceTransactions");

            migrationBuilder.DropColumn(
                name: "LocationAccuracyMeters",
                table: "AttendanceTransactions");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "AttendanceTransactions");
        }
    }
}
