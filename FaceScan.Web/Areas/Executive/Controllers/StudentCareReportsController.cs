using System.Security.Claims;
using System.Globalization;
using FaceScan.Web.Data;
using FaceScan.Web.Helpers;
using FaceScan.Web.Models.Entities;
using FaceScan.Web.Models.Enums;
using FaceScan.Web.Services.Interfaces;
using FaceScan.Web.ViewModels;
using FaceScan.Web.ViewModels.Executive;
using FaceScan.Web.ViewModels.Teacher;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FaceScan.Web.Areas.Executive.Controllers;

[Area("Executive")]
[Authorize(Roles = "SuperAdmin,Admin,Executive,StudentCareAdmin")]
public class StudentCareReportsController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ISystemSettingService _systemSettingService;
    private readonly IAuditLogService _auditLogService;

    public StudentCareReportsController(
        ApplicationDbContext dbContext,
        ISystemSettingService systemSettingService,
        IAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _systemSettingService = systemSettingService;
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] ExecutiveStudentCareFilterViewModel filter, CancellationToken cancellationToken)
    {
        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);
        if (!settings.EnableStudentCareModule)
        {
            return NotFound();
        }

        var model = await BuildModelAsync(filter, settings, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ExportCsv([FromQuery] ExecutiveStudentCareFilterViewModel filter, CancellationToken cancellationToken)
    {
        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);
        if (!settings.EnableStudentCareModule)
        {
            return NotFound();
        }

        var model = await BuildModelAsync(filter, settings, cancellationToken);
        var records = model.Rows.Select(x => new StudentCareCsvRow
        {
            StudentCode = x.StudentCode,
            StudentName = x.StudentName,
            GradeLevel = x.GradeLevel,
            Classroom = x.Classroom,
            BehaviorScore = x.BehaviorScore,
            GoodnessPoint = x.GoodnessPoint,
            RiskLevel = x.RiskLevel.ToString(),
            LastHomeVisitAt = x.LastHomeVisitAt,
            HasHomeLocation = x.HasHomeLocation,
            WasteWeightKg = x.WasteWeightKg,
            WasteAmount = x.WasteAmount,
            ProblemsFound = x.ProblemsFound
        });

        return File(CsvExportHelper.Export(records), "text/csv", $"student-care-report-{DateTime.Today:yyyyMMdd}.csv");
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int studentId, CancellationToken cancellationToken)
    {
        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);
        if (!settings.EnableStudentCareModule)
        {
            return NotFound();
        }

        var student = await _dbContext.Students
            .AsNoTracking()
            .Include(x => x.GradeLevel)
            .Include(x => x.Classroom)
            .FirstOrDefaultAsync(x => x.Id == studentId && x.IsActive, cancellationToken);
        if (student is null)
        {
            return NotFound();
        }

        var model = await BuildStudentCareProfileDetailAsync(student, settings, false, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Print(int studentId, CancellationToken cancellationToken)
    {
        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);
        if (!settings.EnableStudentCareModule)
        {
            return NotFound();
        }

        var student = await _dbContext.Students
            .AsNoTracking()
            .Include(x => x.GradeLevel)
            .Include(x => x.Classroom)
            .FirstOrDefaultAsync(x => x.Id == studentId && x.IsActive, cancellationToken);
        if (student is null)
        {
            return NotFound();
        }

        var model = await BuildStudentCareProfileDetailAsync(student, settings, true, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(string itemType, int id, CancellationToken cancellationToken)
    {
        var handled = await SetApprovalStatusAsync(itemType, id, approve: true, cancellationToken);
        TempData[handled ? "Success" : "Error"] = handled ? "อนุมัติรายการเรียบร้อย" : "ไม่พบรายการที่ต้องการอนุมัติ";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(string itemType, int id, CancellationToken cancellationToken)
    {
        var handled = await SetApprovalStatusAsync(itemType, id, approve: false, cancellationToken);
        TempData[handled ? "Success" : "Error"] = handled ? "ปฏิเสธรายการเรียบร้อย" : "ไม่พบรายการที่ต้องการปฏิเสธ";
        return RedirectToAction(nameof(Index));
    }

    private async Task<ExecutiveStudentCareDashboardViewModel> BuildModelAsync(
        ExecutiveStudentCareFilterViewModel filter,
        SystemSetting settings,
        CancellationToken cancellationToken)
    {
        filter.AcademicYearId ??= settings.AcademicYearCurrentId;
        var academicYearId = filter.AcademicYearId;

        var academicYears = await _dbContext.AcademicYears
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.StartDate)
            .Select(x => new SelectOptionViewModel { Value = x.Id, Text = x.Name })
            .ToListAsync(cancellationToken);
        var gradeLevels = await _dbContext.GradeLevels
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .Select(x => new SelectOptionViewModel { Value = x.Id, Text = x.Name })
            .ToListAsync(cancellationToken);
        var classrooms = await _dbContext.Classrooms
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SelectOptionViewModel { Value = x.Id, Text = x.Name })
            .ToListAsync(cancellationToken);

        var studentsQuery = _dbContext.Students
            .AsNoTracking()
            .Include(x => x.GradeLevel)
            .Include(x => x.Classroom)
            .Where(x => x.IsActive);

        if (academicYearId.HasValue)
        {
            studentsQuery = studentsQuery.Where(x => x.AcademicYearId == academicYearId.Value);
        }

        if (filter.GradeLevelId.HasValue)
        {
            studentsQuery = studentsQuery.Where(x => x.GradeLevelId == filter.GradeLevelId.Value);
        }

        if (filter.ClassroomId.HasValue)
        {
            studentsQuery = studentsQuery.Where(x => x.ClassroomId == filter.ClassroomId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.StudentKeyword))
        {
            var keyword = filter.StudentKeyword.Trim();
            studentsQuery = studentsQuery.Where(x =>
                x.StudentCode.Contains(keyword) ||
                (x.Prefix + x.FirstName + " " + x.LastName).Contains(keyword));
        }

        var students = await studentsQuery
            .OrderBy(x => x.Classroom!.Name)
            .ThenBy(x => x.StudentCode)
            .ToListAsync(cancellationToken);
        var studentIds = students.Select(x => x.Id).ToList();

        var profiles = await _dbContext.StudentCareProfiles
            .AsNoTracking()
            .Where(x => studentIds.Contains(x.StudentId))
            .ToDictionaryAsync(x => x.StudentId, cancellationToken);
        var behaviorScores = await _dbContext.BehaviorScoreTransactions
            .AsNoTracking()
            .Where(x => studentIds.Contains(x.StudentId) && x.Status == StudentCareRecordStatus.Approved)
            .Where(x => !academicYearId.HasValue || x.AcademicYearId == academicYearId.Value)
            .GroupBy(x => x.StudentId)
            .Select(x => new { StudentId = x.Key, Score = settings.StudentCareInitialBehaviorScore + x.Sum(t => t.ScoreChange) })
            .ToDictionaryAsync(x => x.StudentId, x => x.Score, cancellationToken);
        var goodnessPoints = await _dbContext.GoodnessBankTransactions
            .AsNoTracking()
            .Where(x => studentIds.Contains(x.StudentId) && x.Status == StudentCareRecordStatus.Approved)
            .Where(x => !academicYearId.HasValue || x.AcademicYearId == academicYearId.Value)
            .GroupBy(x => x.StudentId)
            .Select(x => new { StudentId = x.Key, Point = x.Sum(t => t.Point) })
            .ToDictionaryAsync(x => x.StudentId, x => x.Point, cancellationToken);
        var wasteTotals = await _dbContext.WasteBankTransactions
            .AsNoTracking()
            .Where(x => studentIds.Contains(x.StudentId))
            .Where(x => !academicYearId.HasValue || x.AcademicYearId == academicYearId.Value)
            .GroupBy(x => x.StudentId)
            .Select(x => new { StudentId = x.Key, Weight = x.Sum(t => t.WeightKg), Amount = x.Sum(t => t.Amount) })
            .ToDictionaryAsync(x => x.StudentId, cancellationToken);

        var rows = students.Select(student =>
        {
            profiles.TryGetValue(student.Id, out var profile);
            wasteTotals.TryGetValue(student.Id, out var waste);
            return new ExecutiveStudentCareRowViewModel
            {
                StudentId = student.Id,
                StudentCode = student.StudentCode,
                StudentName = student.FullName,
                GradeLevel = student.GradeLevel?.Name ?? "-",
                Classroom = student.Classroom?.Name ?? "-",
                BehaviorScore = behaviorScores.TryGetValue(student.Id, out var score) ? score : settings.StudentCareInitialBehaviorScore,
                GoodnessPoint = goodnessPoints.TryGetValue(student.Id, out var point) ? point : 0,
                RiskLevel = profile?.RiskLevel ?? StudentCareRiskLevel.Normal,
                LastHomeVisitAt = profile?.LastHomeVisitAt,
                HasHomeLocation = profile?.HomeLatitude.HasValue == true && profile.HomeLongitude.HasValue,
                HomeLatitude = profile?.HomeLatitude,
                HomeLongitude = profile?.HomeLongitude,
                HomeLocationSharedAt = profile?.HomeLocationSharedAt,
                WasteWeightKg = waste?.Weight ?? 0m,
                WasteAmount = waste?.Amount ?? 0m,
                ProblemsFound = profile?.ProblemsFound
            };
        }).ToList();

        if (filter.RiskLevel.HasValue)
        {
            rows = rows.Where(x => x.RiskLevel == filter.RiskLevel.Value).ToList();
        }

        return new ExecutiveStudentCareDashboardViewModel
        {
            Filter = filter,
            AcademicYears = academicYears,
            GradeLevels = gradeLevels,
            Classrooms = classrooms,
            Rows = rows,
            TotalStudents = rows.Count,
            ProfileCount = rows.Count(x => profiles.ContainsKey(x.StudentId)),
            HomeVisitedCount = rows.Count(x => x.LastHomeVisitAt.HasValue),
            RiskStudentCount = rows.Count(x => x.RiskLevel is StudentCareRiskLevel.Risk or StudentCareRiskLevel.Urgent),
            WasteBankTotalWeightKg = rows.Sum(x => x.WasteWeightKg),
            WasteBankTotalAmount = rows.Sum(x => x.WasteAmount),
            ClassroomSummaries = rows
                .GroupBy(x => x.Classroom)
                .OrderBy(x => x.Key)
                .Select(x => new ExecutiveStudentCareGroupSummaryViewModel
                {
                    Classroom = x.Key,
                    StudentCount = x.Count(),
                    HomeVisitedCount = x.Count(r => r.LastHomeVisitAt.HasValue),
                    RiskStudentCount = x.Count(r => r.RiskLevel is StudentCareRiskLevel.Risk or StudentCareRiskLevel.Urgent),
                    WasteWeightKg = x.Sum(r => r.WasteWeightKg),
                    WasteAmount = x.Sum(r => r.WasteAmount)
                })
                .ToList(),
            PendingApprovals = await BuildPendingApprovalsAsync(cancellationToken),
            PendingApprovalCount = await CountPendingApprovalsAsync(cancellationToken)
        };
    }

    private async Task<List<ExecutiveStudentCareApprovalItemViewModel>> BuildPendingApprovalsAsync(CancellationToken cancellationToken)
    {
        var behavior = await _dbContext.BehaviorScoreTransactions
            .AsNoTracking()
            .Include(x => x.Student)!.ThenInclude(x => x!.Classroom)
            .Where(x => x.Status == StudentCareRecordStatus.Submitted)
            .OrderByDescending(x => x.RecordedAt)
            .Take(20)
            .Select(x => new ExecutiveStudentCareApprovalItemViewModel
            {
                ItemType = "behavior",
                Id = x.Id,
                StudentCode = x.Student != null ? x.Student.StudentCode : "-",
                StudentName = x.Student != null ? x.Student.FullName : "-",
                Classroom = x.Student != null && x.Student.Classroom != null ? x.Student.Classroom.Name : "-",
                Title = x.Category,
                Detail = $"{x.ScoreChange} คะแนน | {x.Reason}",
                SubmittedAt = x.RecordedAt
            })
            .ToListAsync(cancellationToken);

        var goodness = await _dbContext.GoodnessBankTransactions
            .AsNoTracking()
            .Include(x => x.Student)!.ThenInclude(x => x!.Classroom)
            .Where(x => x.Status == StudentCareRecordStatus.Submitted)
            .OrderByDescending(x => x.RecordedAt)
            .Take(20)
            .Select(x => new ExecutiveStudentCareApprovalItemViewModel
            {
                ItemType = "goodness",
                Id = x.Id,
                StudentCode = x.Student != null ? x.Student.StudentCode : "-",
                StudentName = x.Student != null ? x.Student.FullName : "-",
                Classroom = x.Student != null && x.Student.Classroom != null ? x.Student.Classroom.Name : "-",
                Title = x.GoodnessType,
                Detail = $"+{x.Point} คะแนน | {x.Description}",
                SubmittedAt = x.RecordedAt
            })
            .ToListAsync(cancellationToken);

        return behavior.Concat(goodness)
            .OrderByDescending(x => x.SubmittedAt)
            .Take(20)
            .ToList();
    }

    private async Task<StudentCareProfileDetailViewModel> BuildStudentCareProfileDetailAsync(
        Student student,
        SystemSetting settings,
        bool isPrintMode,
        CancellationToken cancellationToken)
    {
        var academicYearId = settings.AcademicYearCurrentId ?? student.AcademicYearId;
        var profile = await _dbContext.StudentCareProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.StudentId == student.Id, cancellationToken);
        var guardians = await _dbContext.StudentGuardians
            .AsNoTracking()
            .Where(x => x.StudentId == student.Id)
            .OrderByDescending(x => x.IsPrimaryContact)
            .ThenBy(x => x.FullName)
            .Select(x => new StudentCareGuardianDetailViewModel
            {
                FullName = x.FullName,
                Relationship = x.Relationship,
                PhoneNumber = x.PhoneNumber,
                Occupation = x.Occupation,
                MonthlyIncome = x.MonthlyIncome,
                Address = x.Address,
                PhotoPath = x.PhotoPath,
                IsPrimaryContact = x.IsPrimaryContact
            })
            .ToListAsync(cancellationToken);
        var behaviorTransactions = await _dbContext.BehaviorScoreTransactions
            .AsNoTracking()
            .Where(x => x.StudentId == student.Id && x.AcademicYearId == academicYearId)
            .OrderByDescending(x => x.RecordedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => new StudentCareBehaviorDetailViewModel
            {
                RecordedAt = x.RecordedAt,
                Category = x.Category,
                ScoreChange = x.ScoreChange,
                Reason = x.Reason,
                Status = x.Status
            })
            .Take(30)
            .ToListAsync(cancellationToken);
        var goodnessTransactions = await _dbContext.GoodnessBankTransactions
            .AsNoTracking()
            .Where(x => x.StudentId == student.Id && x.AcademicYearId == academicYearId)
            .OrderByDescending(x => x.RecordedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => new StudentCareGoodnessDetailViewModel
            {
                RecordedAt = x.RecordedAt,
                GoodnessType = x.GoodnessType,
                Point = x.Point,
                Description = x.Description,
                Status = x.Status
            })
            .Take(30)
            .ToListAsync(cancellationToken);
        var homeVisits = await _dbContext.HomeVisitRecords
            .AsNoTracking()
            .Where(x => x.StudentId == student.Id && x.AcademicYearId == academicYearId)
            .OrderByDescending(x => x.VisitDate)
            .ThenByDescending(x => x.Id)
            .Select(x => new StudentCareHomeVisitDetailViewModel
            {
                VisitDate = x.VisitDate,
                VisitStatus = x.VisitStatus,
                LivesWith = x.LivesWith,
                HouseholdIncome = x.HouseholdIncome,
                HouseCondition = x.HouseCondition,
                FamilyRelationship = x.FamilyRelationship,
                LearningSupportAtHome = x.LearningSupportAtHome,
                RiskBehaviors = x.RiskBehaviors,
                ProblemsFound = x.ProblemsFound,
                TeacherObservation = x.TeacherObservation,
                SupportPlan = x.SupportPlan,
                ParentPhotoPath = x.ParentPhotoPath,
                HousePhotoPath = x.HousePhotoPath,
                Latitude = x.Latitude,
                Longitude = x.Longitude
            })
            .Take(10)
            .ToListAsync(cancellationToken);
        var wasteBankTransactions = await _dbContext.WasteBankTransactions
            .AsNoTracking()
            .Where(x => x.StudentId == student.Id && x.AcademicYearId == academicYearId)
            .OrderByDescending(x => x.RecordedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => new StudentCareWasteBankDetailViewModel
            {
                RecordedAt = x.RecordedAt,
                WasteType = x.WasteType,
                WeightKg = x.WeightKg,
                PricePerKg = x.PricePerKg,
                Amount = x.Amount,
                Note = x.Note
            })
            .Take(30)
            .ToListAsync(cancellationToken);
        var followUpCases = await _dbContext.StudentCareFollowUpCases
            .AsNoTracking()
            .Where(x => x.StudentId == student.Id && x.AcademicYearId == academicYearId)
            .OrderBy(x => x.Status == StudentCareFollowUpStatus.Completed || x.Status == StudentCareFollowUpStatus.Canceled)
            .ThenByDescending(x => x.Priority)
            .ThenBy(x => x.DueDate ?? DateTime.MaxValue)
            .ThenByDescending(x => x.OpenedAt)
            .Select(x => new StudentCareFollowUpCaseDetailViewModel
            {
                Id = x.Id,
                Title = x.Title,
                Concern = x.Concern,
                SupportPlan = x.SupportPlan,
                Priority = x.Priority,
                Status = x.Status,
                OpenedAt = x.OpenedAt,
                DueDate = x.DueDate,
                CompletedAt = x.CompletedAt,
                Outcome = x.Outcome
            })
            .ToListAsync(cancellationToken);

        var approvedBehaviorScoreChange = await _dbContext.BehaviorScoreTransactions
            .AsNoTracking()
            .Where(x => x.StudentId == student.Id && x.AcademicYearId == academicYearId && x.Status == StudentCareRecordStatus.Approved)
            .SumAsync(x => (int?)x.ScoreChange, cancellationToken) ?? 0;
        var approvedGoodnessPoint = await _dbContext.GoodnessBankTransactions
            .AsNoTracking()
            .Where(x => x.StudentId == student.Id && x.AcademicYearId == academicYearId && x.Status == StudentCareRecordStatus.Approved)
            .SumAsync(x => (int?)x.Point, cancellationToken) ?? 0;
        var wasteTotals = await _dbContext.WasteBankTransactions
            .AsNoTracking()
            .Where(x => x.StudentId == student.Id && x.AcademicYearId == academicYearId)
            .GroupBy(x => x.StudentId)
            .Select(x => new { Weight = x.Sum(t => t.WeightKg), Amount = x.Sum(t => t.Amount), Count = x.Count() })
            .FirstOrDefaultAsync(cancellationToken);
        var mapUrl = profile?.HomeLatitude.HasValue == true && profile.HomeLongitude.HasValue
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"https://www.google.com/maps?q={profile.HomeLatitude.Value},{profile.HomeLongitude.Value}")
            : null;

        return new StudentCareProfileDetailViewModel
        {
            StudentId = student.Id,
            StudentCode = student.StudentCode,
            StudentName = student.FullName,
            GradeLevel = student.GradeLevel?.Name ?? "-",
            Classroom = student.Classroom?.Name ?? "-",
            StudentNo = student.StudentNo,
            Address = student.Address,
            GuardianName = student.GuardianName,
            GuardianNationalId = student.GuardianNationalId,
            GuardianPhone = student.GuardianPhone,
            BehaviorScore = settings.StudentCareInitialBehaviorScore + approvedBehaviorScoreChange,
            BehaviorTransactionCount = behaviorTransactions.Count(x => x.Status == StudentCareRecordStatus.Approved),
            GoodnessPoint = approvedGoodnessPoint,
            GoodnessTransactionCount = goodnessTransactions.Count(x => x.Status == StudentCareRecordStatus.Approved),
            WasteBankWeightKg = wasteTotals?.Weight ?? 0m,
            WasteBankAmount = wasteTotals?.Amount ?? 0m,
            WasteBankTransactionCount = wasteTotals?.Count ?? 0,
            RiskLevel = profile?.RiskLevel ?? StudentCareRiskLevel.Normal,
            LivesWith = profile?.LivesWith,
            GuardianRelationship = profile?.GuardianRelationship,
            HouseholdIncome = profile?.HouseholdIncome,
            RiskBehaviors = profile?.RiskBehaviors,
            ProblemsFound = profile?.ProblemsFound,
            SupportRecommendation = profile?.SupportRecommendation,
            LastHomeVisitAt = profile?.LastHomeVisitAt,
            HomeLatitude = profile?.HomeLatitude,
            HomeLongitude = profile?.HomeLongitude,
            HomeLocationSharedAt = profile?.HomeLocationSharedAt,
            MapUrl = mapUrl,
            IsPrintMode = isPrintMode,
            BackArea = "Executive",
            BackController = "StudentCareReports",
            BackAction = "Index",
            Guardians = guardians,
            BehaviorTransactions = behaviorTransactions,
            GoodnessTransactions = goodnessTransactions,
            HomeVisits = homeVisits,
            WasteBankTransactions = wasteBankTransactions,
            FollowUpCases = followUpCases
        };
    }

    private async Task<int> CountPendingApprovalsAsync(CancellationToken cancellationToken)
        => await _dbContext.BehaviorScoreTransactions.CountAsync(x => x.Status == StudentCareRecordStatus.Submitted, cancellationToken)
           + await _dbContext.GoodnessBankTransactions.CountAsync(x => x.Status == StudentCareRecordStatus.Submitted, cancellationToken);

    private async Task<bool> SetApprovalStatusAsync(string itemType, int id, bool approve, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.Equals(itemType, "behavior", StringComparison.OrdinalIgnoreCase))
        {
            var item = await _dbContext.BehaviorScoreTransactions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (item is null) return false;
            item.Status = approve ? StudentCareRecordStatus.Approved : StudentCareRecordStatus.Rejected;
            item.ApprovedByUserId = approve ? userId : null;
            item.ApprovedAt = approve ? DateTime.UtcNow : null;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await LogApprovalAsync(itemType, id, approve, cancellationToken);
            return true;
        }

        if (string.Equals(itemType, "goodness", StringComparison.OrdinalIgnoreCase))
        {
            var item = await _dbContext.GoodnessBankTransactions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (item is null) return false;
            item.Status = approve ? StudentCareRecordStatus.Approved : StudentCareRecordStatus.Rejected;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await LogApprovalAsync(itemType, id, approve, cancellationToken);
            return true;
        }

        return false;
    }

    private Task LogApprovalAsync(string itemType, int id, bool approve, CancellationToken cancellationToken)
        => _auditLogService.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            approve ? "ApproveStudentCareItem" : "RejectStudentCareItem",
            itemType,
            id.ToString(),
            approve ? "Approved" : "Rejected",
            HttpContextHelper.GetIpAddress(HttpContext),
            cancellationToken);

    private sealed class StudentCareCsvRow
    {
        public string StudentCode { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;
        public string Classroom { get; set; } = string.Empty;
        public int BehaviorScore { get; set; }
        public int GoodnessPoint { get; set; }
        public string RiskLevel { get; set; } = string.Empty;
        public DateTime? LastHomeVisitAt { get; set; }
        public bool HasHomeLocation { get; set; }
        public decimal WasteWeightKg { get; set; }
        public decimal WasteAmount { get; set; }
        public string? ProblemsFound { get; set; }
    }
}
