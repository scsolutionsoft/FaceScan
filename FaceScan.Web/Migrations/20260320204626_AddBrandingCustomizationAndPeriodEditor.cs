using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaceScan.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddBrandingCustomizationAndPeriodEditor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApplicationDisplayName",
                table: "SystemSettings",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "FaceScan");

            migrationBuilder.AddColumn<string>(
                name: "ApplicationTagline",
                table: "SystemSettings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "ระบบเช็กเวลาเข้า-ออกด้วยการสแกนใบหน้า");

            migrationBuilder.AddColumn<string>(
                name: "BrandLogoPath",
                table: "SystemSettings",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThemeAccentColor",
                table: "SystemSettings",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "#E9D5FF");

            migrationBuilder.AddColumn<string>(
                name: "ThemeBackgroundColor",
                table: "SystemSettings",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "#F7F3FF");

            migrationBuilder.AddColumn<string>(
                name: "ThemePrimaryColor",
                table: "SystemSettings",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "#7C3AED");

            migrationBuilder.AddColumn<string>(
                name: "ThemePrimarySoftColor",
                table: "SystemSettings",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "#A855F7");

            migrationBuilder.AddColumn<string>(
                name: "ThemeSidebarEndColor",
                table: "SystemSettings",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "#4C1D95");

            migrationBuilder.AddColumn<string>(
                name: "ThemeSidebarStartColor",
                table: "SystemSettings",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "#7C3AED");

            migrationBuilder.AddColumn<string>(
                name: "ThemeSurfaceColor",
                table: "SystemSettings",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "#FFFFFF");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplicationDisplayName",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "ApplicationTagline",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "BrandLogoPath",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "ThemeAccentColor",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "ThemeBackgroundColor",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "ThemePrimaryColor",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "ThemePrimarySoftColor",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "ThemeSidebarEndColor",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "ThemeSidebarStartColor",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "ThemeSurfaceColor",
                table: "SystemSettings");
        }
    }
}
