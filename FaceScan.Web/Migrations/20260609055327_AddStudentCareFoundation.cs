using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaceScan.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentCareFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnableBehaviorScoreModule",
                table: "SystemSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableGoodnessBankModule",
                table: "SystemSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableHomeVisitModule",
                table: "SystemSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableStudentCareModule",
                table: "SystemSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableWasteBankModule",
                table: "SystemSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "BehaviorScoreTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    ScoreChange = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    EvidencePhotoPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RecordedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BehaviorScoreTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BehaviorScoreTransactions_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BehaviorScoreTransactions_AspNetUsers_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BehaviorScoreTransactions_AspNetUsers_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BehaviorScoreTransactions_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GoodnessBankTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    GoodnessType = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Point = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EvidencePhotoPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RecordedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodnessBankTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoodnessBankTransactions_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoodnessBankTransactions_AspNetUsers_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GoodnessBankTransactions_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HomeVisitRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    VisitDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TeacherUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    VisitStatus = table.Column<int>(type: "int", nullable: false),
                    LivesWith = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    HouseCondition = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FamilyRelationship = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LearningSupportAtHome = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HouseholdIncome = table.Column<decimal>(type: "decimal(12,2)", nullable: true),
                    RiskBehaviors = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ProblemsFound = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TeacherObservation = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SupportPlan = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ParentPhotoPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    HousePhotoPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeVisitRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HomeVisitRecords_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HomeVisitRecords_AspNetUsers_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_HomeVisitRecords_AspNetUsers_TeacherUserId",
                        column: x => x.TeacherUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_HomeVisitRecords_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentCareProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    LivesWith = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    GuardianRelationship = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    HouseholdIncome = table.Column<decimal>(type: "decimal(12,2)", nullable: true),
                    IncomeRange = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    HomeLatitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    HomeLongitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    HomeLocationSharedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RiskLevel = table.Column<int>(type: "int", nullable: false),
                    RiskBehaviors = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ProblemsFound = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SupportRecommendation = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LastHomeVisitAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentCareProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentCareProfiles_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentGuardians",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Relationship = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Occupation = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    MonthlyIncome = table.Column<decimal>(type: "decimal(12,2)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PhotoPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsPrimaryContact = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentGuardians", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentGuardians_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WasteBankRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WasteType = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    PricePerKg = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WasteBankRates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WasteBankTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    WasteType = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    WeightKg = table.Column<decimal>(type: "decimal(10,3)", nullable: false),
                    PricePerKg = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    RecordedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WasteBankTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WasteBankTransactions_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WasteBankTransactions_AspNetUsers_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WasteBankTransactions_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BehaviorScoreTransactions_AcademicYearId",
                table: "BehaviorScoreTransactions",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_BehaviorScoreTransactions_ApprovedByUserId",
                table: "BehaviorScoreTransactions",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BehaviorScoreTransactions_RecordedAt",
                table: "BehaviorScoreTransactions",
                column: "RecordedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BehaviorScoreTransactions_RecordedByUserId",
                table: "BehaviorScoreTransactions",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BehaviorScoreTransactions_Status",
                table: "BehaviorScoreTransactions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BehaviorScoreTransactions_StudentId_AcademicYearId",
                table: "BehaviorScoreTransactions",
                columns: new[] { "StudentId", "AcademicYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_GoodnessBankTransactions_AcademicYearId",
                table: "GoodnessBankTransactions",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodnessBankTransactions_RecordedAt",
                table: "GoodnessBankTransactions",
                column: "RecordedAt");

            migrationBuilder.CreateIndex(
                name: "IX_GoodnessBankTransactions_RecordedByUserId",
                table: "GoodnessBankTransactions",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodnessBankTransactions_Status",
                table: "GoodnessBankTransactions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_GoodnessBankTransactions_StudentId_AcademicYearId",
                table: "GoodnessBankTransactions",
                columns: new[] { "StudentId", "AcademicYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_HomeVisitRecords_AcademicYearId",
                table: "HomeVisitRecords",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeVisitRecords_ApprovedByUserId",
                table: "HomeVisitRecords",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeVisitRecords_StudentId_AcademicYearId",
                table: "HomeVisitRecords",
                columns: new[] { "StudentId", "AcademicYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_HomeVisitRecords_TeacherUserId",
                table: "HomeVisitRecords",
                column: "TeacherUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeVisitRecords_VisitDate",
                table: "HomeVisitRecords",
                column: "VisitDate");

            migrationBuilder.CreateIndex(
                name: "IX_HomeVisitRecords_VisitStatus",
                table: "HomeVisitRecords",
                column: "VisitStatus");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCareProfiles_RiskLevel",
                table: "StudentCareProfiles",
                column: "RiskLevel");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCareProfiles_StudentId",
                table: "StudentCareProfiles",
                column: "StudentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentGuardians_StudentId",
                table: "StudentGuardians",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_WasteBankRates_EffectiveFrom",
                table: "WasteBankRates",
                column: "EffectiveFrom");

            migrationBuilder.CreateIndex(
                name: "IX_WasteBankRates_WasteType_IsActive",
                table: "WasteBankRates",
                columns: new[] { "WasteType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_WasteBankTransactions_AcademicYearId",
                table: "WasteBankTransactions",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_WasteBankTransactions_RecordedAt",
                table: "WasteBankTransactions",
                column: "RecordedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WasteBankTransactions_RecordedByUserId",
                table: "WasteBankTransactions",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WasteBankTransactions_StudentId_AcademicYearId",
                table: "WasteBankTransactions",
                columns: new[] { "StudentId", "AcademicYearId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BehaviorScoreTransactions");

            migrationBuilder.DropTable(
                name: "GoodnessBankTransactions");

            migrationBuilder.DropTable(
                name: "HomeVisitRecords");

            migrationBuilder.DropTable(
                name: "StudentCareProfiles");

            migrationBuilder.DropTable(
                name: "StudentGuardians");

            migrationBuilder.DropTable(
                name: "WasteBankRates");

            migrationBuilder.DropTable(
                name: "WasteBankTransactions");

            migrationBuilder.DropColumn(
                name: "EnableBehaviorScoreModule",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "EnableGoodnessBankModule",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "EnableHomeVisitModule",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "EnableStudentCareModule",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "EnableWasteBankModule",
                table: "SystemSettings");

        }
    }
}
