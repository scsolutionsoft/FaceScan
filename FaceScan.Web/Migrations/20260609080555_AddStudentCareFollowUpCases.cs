using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaceScan.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentCareFollowUpCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentCareFollowUpCases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Concern = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SupportPlan = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AssignedToUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    LastUpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentCareFollowUpCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentCareFollowUpCases_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentCareFollowUpCases_AspNetUsers_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudentCareFollowUpCases_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudentCareFollowUpCases_AspNetUsers_LastUpdatedByUserId",
                        column: x => x.LastUpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudentCareFollowUpCases_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentCareFollowUpCases_AcademicYearId",
                table: "StudentCareFollowUpCases",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCareFollowUpCases_AssignedToUserId",
                table: "StudentCareFollowUpCases",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCareFollowUpCases_CreatedByUserId",
                table: "StudentCareFollowUpCases",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCareFollowUpCases_DueDate",
                table: "StudentCareFollowUpCases",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCareFollowUpCases_LastUpdatedByUserId",
                table: "StudentCareFollowUpCases",
                column: "LastUpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCareFollowUpCases_OpenedAt",
                table: "StudentCareFollowUpCases",
                column: "OpenedAt");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCareFollowUpCases_Priority",
                table: "StudentCareFollowUpCases",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCareFollowUpCases_Status",
                table: "StudentCareFollowUpCases",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCareFollowUpCases_StudentId_AcademicYearId",
                table: "StudentCareFollowUpCases",
                columns: new[] { "StudentId", "AcademicYearId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentCareFollowUpCases");
        }
    }
}
