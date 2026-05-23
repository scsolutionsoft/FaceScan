using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaceScan.Web.Migrations;

/// <inheritdoc />
public partial class AddOnvifCameraSupport : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "OnvifDeviceUri",
            table: "ScanDevices",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "OnvifPort",
            table: "ScanDevices",
            type: "int",
            nullable: false,
            defaultValue: 8080);

        migrationBuilder.AddColumn<string>(
            name: "OnvifManufacturer",
            table: "ScanDevices",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "OnvifModel",
            table: "ScanDevices",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "OnvifSerialNumber",
            table: "ScanDevices",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "OnvifFirmwareVersion",
            table: "ScanDevices",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "OnvifLastDiscoveredAtUtc",
            table: "ScanDevices",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "OnvifTestPassed",
            table: "ScanDevices",
            type: "bit",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "OnvifDeviceUri",
            table: "ScanDevices");

        migrationBuilder.DropColumn(
            name: "OnvifPort",
            table: "ScanDevices");

        migrationBuilder.DropColumn(
            name: "OnvifManufacturer",
            table: "ScanDevices");

        migrationBuilder.DropColumn(
            name: "OnvifModel",
            table: "ScanDevices");

        migrationBuilder.DropColumn(
            name: "OnvifSerialNumber",
            table: "ScanDevices");

        migrationBuilder.DropColumn(
            name: "OnvifFirmwareVersion",
            table: "ScanDevices");

        migrationBuilder.DropColumn(
            name: "OnvifLastDiscoveredAtUtc",
            table: "ScanDevices");

        migrationBuilder.DropColumn(
            name: "OnvifTestPassed",
            table: "ScanDevices");
    }
}
