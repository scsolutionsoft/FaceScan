using FaceScan.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaceScan.Web.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260521093000_AddScanDeviceRecognitionProfile")]
    public partial class AddScanDeviceRecognitionProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RecognitionProfile",
                table: "ScanDevices",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "auto");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecognitionProfile",
                table: "ScanDevices");
        }
    }
}