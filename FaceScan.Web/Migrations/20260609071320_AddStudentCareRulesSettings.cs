using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaceScan.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentCareRulesSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequireStudentCareApproval",
                table: "SystemSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "StudentCareInitialBehaviorScore",
                table: "SystemSettings",
                type: "int",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.AddColumn<int>(
                name: "StudentCareLowBehaviorScoreThreshold",
                table: "SystemSettings",
                type: "int",
                nullable: false,
                defaultValue: 60);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequireStudentCareApproval",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "StudentCareInitialBehaviorScore",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "StudentCareLowBehaviorScoreThreshold",
                table: "SystemSettings");
        }
    }
}
