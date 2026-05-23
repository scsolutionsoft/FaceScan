using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaceScan.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherDutyAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "TeacherCheckOutStartTime",
                table: "SystemSettings",
                type: "time",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<TimeSpan>(
                name: "TeacherLateAfterTime",
                table: "SystemSettings",
                type: "time",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.CreateTable(
                name: "TeacherAttendanceDailySummaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FirstCheckInTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastCheckOutTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AttendanceStatus = table.Column<int>(type: "int", nullable: false),
                    TotalScans = table.Column<int>(type: "int", nullable: false),
                    IsPresent = table.Column<bool>(type: "bit", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherAttendanceDailySummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherAttendanceDailySummaries_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeacherAttendanceTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ScanType = table.Column<int>(type: "int", nullable: false),
                    ScanTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScanDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeviceId = table.Column<int>(type: "int", nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    LocationAccuracyMeters = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    ConfidenceScore = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    RecognitionProvider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SnapshotPath = table.Column<string>(type: "nvarchar(350)", maxLength: 350, nullable: true),
                    IsDuplicate = table.Column<bool>(type: "bit", nullable: false),
                    RawResponseJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherAttendanceTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherAttendanceTransactions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeacherAttendanceTransactions_ScanDevices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "ScanDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TeacherFacePhotos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(350)", maxLength: 350, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    QualityScore = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherFacePhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherFacePhotos_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeacherFaceProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    EnrollmentStatus = table.Column<int>(type: "int", nullable: false),
                    TemplateVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LastTrainedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EmbeddingJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    QualityNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherFaceProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherFaceProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAttendanceDailySummaries_Date",
                table: "TeacherAttendanceDailySummaries",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAttendanceDailySummaries_UserId_Date",
                table: "TeacherAttendanceDailySummaries",
                columns: new[] { "UserId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAttendanceTransactions_DeviceId",
                table: "TeacherAttendanceTransactions",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAttendanceTransactions_ScanDate",
                table: "TeacherAttendanceTransactions",
                column: "ScanDate");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAttendanceTransactions_UserId_ScanDate",
                table: "TeacherAttendanceTransactions",
                columns: new[] { "UserId", "ScanDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherFacePhotos_UserId",
                table: "TeacherFacePhotos",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherFaceProfiles_UserId",
                table: "TeacherFaceProfiles",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeacherAttendanceDailySummaries");

            migrationBuilder.DropTable(
                name: "TeacherAttendanceTransactions");

            migrationBuilder.DropTable(
                name: "TeacherFacePhotos");

            migrationBuilder.DropTable(
                name: "TeacherFaceProfiles");

            migrationBuilder.DropColumn(
                name: "TeacherCheckOutStartTime",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "TeacherLateAfterTime",
                table: "SystemSettings");
        }
    }
}
