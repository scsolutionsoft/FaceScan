using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaceScan.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleAndPeriodAttendanceModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<TimeSpan>(
                name: "CheckOutStartTime",
                table: "SystemSettings",
                type: "time",
                nullable: false,
                defaultValue: new TimeSpan(0, 15, 30, 0, 0),
                oldClrType: typeof(TimeSpan),
                oldType: "time");

            migrationBuilder.AddColumn<int>(
                name: "AssignedClassroomId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClassPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    IsVisibleForCheck = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassPeriods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PeriodAttendanceSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClassroomId = table.Column<int>(type: "int", nullable: false),
                    ClassPeriodId = table.Column<int>(type: "int", nullable: false),
                    CheckedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CheckedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TeacherStatus = table.Column<int>(type: "int", nullable: false),
                    TeacherStatusNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodAttendanceSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PeriodAttendanceSessions_AspNetUsers_CheckedByUserId",
                        column: x => x.CheckedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PeriodAttendanceSessions_ClassPeriods_ClassPeriodId",
                        column: x => x.ClassPeriodId,
                        principalTable: "ClassPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PeriodAttendanceSessions_Classrooms_ClassroomId",
                        column: x => x.ClassroomId,
                        principalTable: "Classrooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PeriodAttendanceRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PeriodAttendanceSessionId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodAttendanceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PeriodAttendanceRecords_PeriodAttendanceSessions_PeriodAttendanceSessionId",
                        column: x => x.PeriodAttendanceSessionId,
                        principalTable: "PeriodAttendanceSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PeriodAttendanceRecords_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_AssignedClassroomId",
                table: "AspNetUsers",
                column: "AssignedClassroomId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassPeriods_SortOrder",
                table: "ClassPeriods",
                column: "SortOrder",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PeriodAttendanceRecords_PeriodAttendanceSessionId_StudentId",
                table: "PeriodAttendanceRecords",
                columns: new[] { "PeriodAttendanceSessionId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PeriodAttendanceRecords_StudentId",
                table: "PeriodAttendanceRecords",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodAttendanceSessions_CheckedByUserId",
                table: "PeriodAttendanceSessions",
                column: "CheckedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodAttendanceSessions_ClassPeriodId",
                table: "PeriodAttendanceSessions",
                column: "ClassPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodAttendanceSessions_ClassroomId",
                table: "PeriodAttendanceSessions",
                column: "ClassroomId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodAttendanceSessions_Date",
                table: "PeriodAttendanceSessions",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodAttendanceSessions_Date_ClassroomId_ClassPeriodId",
                table: "PeriodAttendanceSessions",
                columns: new[] { "Date", "ClassroomId", "ClassPeriodId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Classrooms_AssignedClassroomId",
                table: "AspNetUsers",
                column: "AssignedClassroomId",
                principalTable: "Classrooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Classrooms_AssignedClassroomId",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "PeriodAttendanceRecords");

            migrationBuilder.DropTable(
                name: "PeriodAttendanceSessions");

            migrationBuilder.DropTable(
                name: "ClassPeriods");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_AssignedClassroomId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AssignedClassroomId",
                table: "AspNetUsers");

            migrationBuilder.AlterColumn<TimeSpan>(
                name: "CheckOutStartTime",
                table: "SystemSettings",
                type: "time",
                nullable: false,
                oldClrType: typeof(TimeSpan),
                oldType: "time",
                oldDefaultValue: new TimeSpan(0, 15, 30, 0, 0));
        }
    }
}
