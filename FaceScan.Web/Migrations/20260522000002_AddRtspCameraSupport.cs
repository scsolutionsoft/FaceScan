using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaceScan.Web.Migrations;

/// <inheritdoc />
public partial class AddRtspCameraSupport : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "RtspStreamUrl",
            table: "ScanDevices",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "RtspPort",
            table: "ScanDevices",
            type: "int",
            nullable: false,
            defaultValue: 554);

        migrationBuilder.AddColumn<string>(
            name: "RtspUsername",
            table: "ScanDevices",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RtspPassword",
            table: "ScanDevices",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "RtspTestPassed",
            table: "ScanDevices",
            type: "bit",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "RtspStreamUrl",
            table: "ScanDevices");

        migrationBuilder.DropColumn(
            name: "RtspPort",
            table: "ScanDevices");

        migrationBuilder.DropColumn(
            name: "RtspUsername",
            table: "ScanDevices");

        migrationBuilder.DropColumn(
            name: "RtspPassword",
            table: "ScanDevices");

        migrationBuilder.DropColumn(
            name: "RtspTestPassed",
            table: "ScanDevices");
    }
}
