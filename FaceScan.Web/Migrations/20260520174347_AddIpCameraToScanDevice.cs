using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaceScan.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddIpCameraToScanDevice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CameraIntervalMs",
                table: "ScanDevices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CameraPassword",
                table: "ScanDevices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CameraSnapshotUrl",
                table: "ScanDevices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CameraType",
                table: "ScanDevices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CameraUsername",
                table: "ScanDevices",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CameraIntervalMs",
                table: "ScanDevices");

            migrationBuilder.DropColumn(
                name: "CameraPassword",
                table: "ScanDevices");

            migrationBuilder.DropColumn(
                name: "CameraSnapshotUrl",
                table: "ScanDevices");

            migrationBuilder.DropColumn(
                name: "CameraType",
                table: "ScanDevices");

            migrationBuilder.DropColumn(
                name: "CameraUsername",
                table: "ScanDevices");
        }
    }
}
