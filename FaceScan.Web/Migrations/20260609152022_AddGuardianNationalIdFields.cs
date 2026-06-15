using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaceScan.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddGuardianNationalIdFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GuardianNationalId",
                table: "Students",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NationalId",
                table: "StudentGuardians",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_GuardianNationalId",
                table: "Students",
                column: "GuardianNationalId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentGuardians_NationalId",
                table: "StudentGuardians",
                column: "NationalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Students_GuardianNationalId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_StudentGuardians_NationalId",
                table: "StudentGuardians");

            migrationBuilder.DropColumn(
                name: "GuardianNationalId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "NationalId",
                table: "StudentGuardians");
        }
    }
}
