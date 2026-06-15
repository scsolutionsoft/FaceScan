using System.Security.Claims;
using System.Globalization;
using FaceScan.Web.Data;
using FaceScan.Web.Helpers;
using FaceScan.Web.Models.Entities;
using FaceScan.Web.Models.Enums;
using FaceScan.Web.Services.Interfaces;
using FaceScan.Web.ViewModels;
using FaceScan.Web.ViewModels.Teacher;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FaceScan.Web.Areas.Teacher.Controllers;

[Area("Teacher")]
[Authorize(Roles = "SuperAdmin,Admin,Teacher,HomeroomHead,StudentCareAdmin")]
public class StudentCareController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISystemSettingService _systemSettingService;
    private readonly IAuditLogService _auditLogService;
    private readonly IFileStorageService _fileStorageService;

    public StudentCareController(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ISystemSettingService systemSettingService,
        IAuditLogService auditLogService,
        IFileStorageService fileStorageService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _systemSettingService = systemSettingService;
        _auditLogService = auditLogService;
        _fileStorageService = fileStorageService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] StudentCareDashboardFilterViewModel filter, CancellationToken cancellationToken)
    {
        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);
        if (!settings.EnableStudentCareModule)
        {
            return NotFound();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var model = await BuildDashboardAsync(user, settings, filter, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordBehavior(StudentCareTransactionInputViewModel input, CancellationToken cancellationToken)
    {
        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);
        if (!settings.EnableStudentCareModule || !settings.EnableBehaviorScoreModule)
        {
            return NotFound();
        }

        if (!input.CategoryId.HasValue)
        {
            TempData["Error"] = "กรุณาระบุคะแนนเพิ่มหรือลด";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(input.Description))
        {
            TempData["Error"] = "กรุณาระบุประเภทและเหตุผลการบันทึกคะแนนพฤติกรรม";
            return RedirectToAction(nameof(Index));
        }

        var behaviorCategory = await _dbContext.BehaviorScoreCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == input.CategoryId.Value && x.IsActive, cancellationToken);
        if (behaviorCategory is null)
        {
            TempData["Error"] = "ไม่พบประเภทคะแนนพฤติกรรมที่เลือก";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var student = await GetAccessibleStudentAsync(user, input.StudentId, cancellationToken);
        if (student is null)
        {
            return Forbid();
        }

        var academicYearId = ResolveAcademicYearId(settings, student);
        if (!academicYearId.HasValue)
        {
            TempData["Error"] = "กรุณาตั้งค่าปีการศึกษาปัจจุบันหรือตรวจสอบข้อมูลปีการศึกษาของนักเรียน";
            return RedirectToAction(nameof(Index));
        }

        var transaction = new BehaviorScoreTransaction
        {
            StudentId = student.Id,
            AcademicYearId = academicYearId.Value,
            ScoreChange = behaviorCategory.ScoreChange,
            Category = behaviorCategory.Name,
            Reason = input.Description.Trim(),
            RecordedByUserId = user.Id,
            RecordedAt = DateTime.UtcNow,
            ApprovedByUserId = settings.RequireStudentCareApproval ? null : user.Id,
            ApprovedAt = settings.RequireStudentCareApproval ? null : DateTime.UtcNow,
            Status = settings.RequireStudentCareApproval ? StudentCareRecordStatus.Submitted : StudentCareRecordStatus.Approved
        };

        _dbContext.BehaviorScoreTransactions.Add(transaction);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            "RecordBehaviorScore",
            "BehaviorScoreTransaction",
            transaction.Id.ToString(),
            $"{student.StudentCode} {student.FullName}: {transaction.ScoreChange} ({transaction.Category})",
            HttpContextHelper.GetIpAddress(HttpContext),
            cancellationToken);

        TempData["Success"] = "บันทึกคะแนนพฤติกรรมเรียบร้อย";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordGoodness(StudentCareTransactionInputViewModel input, CancellationToken cancellationToken)
    {
        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);
        if (!settings.EnableStudentCareModule || !settings.EnableGoodnessBankModule)
        {
            return NotFound();
        }

        if (!input.CategoryId.HasValue)
        {
            TempData["Error"] = "กรุณาระบุประเภทความดีและคะแนน";
            return RedirectToAction(nameof(Index));
        }

        var goodnessCategory = await _dbContext.GoodnessBankCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == input.CategoryId.Value && x.IsActive, cancellationToken);
        if (goodnessCategory is null)
        {
            TempData["Error"] = "ไม่พบประเภทธนาคารความดีที่เลือก";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var student = await GetAccessibleStudentAsync(user, input.StudentId, cancellationToken);
        if (student is null)
        {
            return Forbid();
        }

        var academicYearId = ResolveAcademicYearId(settings, student);
        if (!academicYearId.HasValue)
        {
            TempData["Error"] = "กรุณาตั้งค่าปีการศึกษาปัจจุบันหรือตรวจสอบข้อมูลปีการศึกษาของนักเรียน";
            return RedirectToAction(nameof(Index));
        }

        var transaction = new GoodnessBankTransaction
        {
            StudentId = student.Id,
            AcademicYearId = academicYearId.Value,
            GoodnessType = goodnessCategory.Name,
            Point = goodnessCategory.Point,
            Description = input.Description?.Trim(),
            RecordedByUserId = user.Id,
            RecordedAt = DateTime.UtcNow,
            Status = settings.RequireStudentCareApproval ? StudentCareRecordStatus.Submitted : StudentCareRecordStatus.Approved
        };

        _dbContext.GoodnessBankTransactions.Add(transaction);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            "RecordGoodnessBank",
            "GoodnessBankTransaction",
            transaction.Id.ToString(),
            $"{student.StudentCode} {student.FullName}: +{transaction.Point} ({transaction.GoodnessType})",
            HttpContextHelper.GetIpAddress(HttpContext),
            cancellationToken);

        TempData["Success"] = "บันทึกธนาคารความดีเรียบร้อย";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveWasteRate(WasteBankRateInputViewModel input, CancellationToken cancellationToken)
    {
        if (!CanManageWasteBank())
        {
            return Forbid();
        }

        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);
        if (!settings.EnableWasteBankModule)
        {
            return NotFound();
        }

        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(input.WasteType) || input.PricePerKg <= 0)
        {
            TempData["Error"] = "กรุณาระบุชนิดขยะและราคาต่อกิโลกรัมให้ถูกต้อง";
            return RedirectToAction(nameof(Index));
        }

        var wasteType = input.WasteType.Trim();
        var now = DateTime.UtcNow;
        var activeRates = await _dbContext.WasteBankRates
            .Where(x => x.IsActive && x.WasteType == wasteType)
            .ToListAsync(cancellationToken);

        foreach (var rate in activeRates)
        {
            rate.IsActive = false;
            rate.EffectiveTo = now;
        }

        var newRate = new WasteBankRate
        {
            WasteType = wasteType,
            PricePerKg = input.PricePerKg,
            EffectiveFrom = now,
            IsActive = true
        };

        _dbContext.WasteBankRates.Add(newRate);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            "UpdateWasteBankRate",
            "WasteBankRate",
            newRate.Id.ToString(),
            $"{newRate.WasteType}: {newRate.PricePerKg:N2} per kg",
            HttpContextHelper.GetIpAddress(HttpContext),
            cancellationToken);

        TempData["Success"] = "บันทึกราคารับซื้อขยะเรียบร้อย";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordWasteBank(WasteBankTransactionInputViewModel input, CancellationToken cancellationToken)
    {
        if (!CanManageWasteBank())
        {
            return Forbid();
        }

        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);
        if (!settings.EnableWasteBankModule)
        {
            return NotFound();
        }

        if (!ModelState.IsValid || input.WeightKg <= 0 || string.IsNullOrWhiteSpace(input.WasteType))
        {
            TempData["Error"] = "กรุณาระบุนักเรียน ชนิดขยะ และน้ำหนักให้ถูกต้อง";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var student = await GetAccessibleStudentAsync(user, input.StudentId, cancellationToken);
        if (student is null)
        {
            return Forbid();
        }

        var academicYearId = ResolveAcademicYearId(settings, student);
        if (!academicYearId.HasValue)
        {
            TempData["Error"] = "กรุณาตั้งค่าปีการศึกษาปัจจุบันหรือตรวจสอบข้อมูลปีการศึกษาของนักเรียน";
            return RedirectToAction(nameof(Index));
        }

        var wasteType = input.WasteType.Trim();
        var rate = await _dbContext.WasteBankRates
            .AsNoTracking()
            .Where(x => x.IsActive && x.WasteType == wasteType)
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

        if (rate is null)
        {
            TempData["Error"] = "ยังไม่ได้ตั้งราคารับซื้อของขยะชนิดนี้";
            return RedirectToAction(nameof(Index));
        }

        var amount = Math.Round(input.WeightKg * rate.PricePerKg, 2, MidpointRounding.AwayFromZero);
        var transaction = new WasteBankTransaction
        {
            StudentId = student.Id,
            AcademicYearId = academicYearId.Value,
            WasteType = wasteType,
            WeightKg = input.WeightKg,
            PricePerKg = rate.PricePerKg,
            Amount = amount,
            RecordedByUserId = user.Id,
            RecordedAt = DateTime.UtcNow,
            Note = input.Note?.Trim()
        };

        _dbContext.WasteBankTransactions.Add(transaction);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            "RecordWasteBank",
            "WasteBankTransaction",
            transaction.Id.ToString(),
            $"{student.StudentCode} {student.FullName}: {transaction.WasteType} {transaction.WeightKg:N3} kg x {transaction.PricePerKg:N2} = {transaction.Amount:N2}",
            HttpContextHelper.GetIpAddress(HttpContext),
            cancellationToken);

        TempData["Success"] = $"บันทึกธนาคารขยะเรียบร้อย ยอดเงิน {amount:N2} บาท";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Reports(CancellationToken cancellationToken)
    {
        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);
        if (!settings.EnableStudentCareModule)
        {
            return NotFound();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var (students, assignedClassroomName) = await GetAccessibleStudentsAsync(user, cancellationToken);
        var studentIds = students.Select(x => x.Id).ToList();
        var academicYearId = settings.AcademicYearCurrentId;

        var behaviorScores = await BuildBehaviorScoreMapAsync(studentIds, academicYearId, settings.StudentCareInitialBehaviorScore, cancellationToken);
        var behaviorCounts = await _dbContext.BehaviorScoreTransactions
            .AsNoTracking()
            .Where(x => studentIds.Contains(x.StudentId) && x.Status == StudentCareRecordStatus.Approved)
            .Where(x => !academicYearId.HasValue || x.AcademicYearId == academicYearId.Value)
            .GroupBy(x => x.StudentId)
            .Select(x => new { StudentId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.StudentId, x => x.Count, cancellationToken);

        var goodnessPoints = await BuildGoodnessPointMapAsync(studentIds, academicYearId, cancellationToken);
        var goodnessCounts = await _dbContext.GoodnessBankTransactions
            .AsNoTracking()
            .Where(x => studentIds.Contains(x.StudentId) && x.Status == StudentCareRecordStatus.Approved)
            .Where(x => !academicYearId.HasValue || x.AcademicYearId == academicYearId.Value)
            .GroupBy(x => x.StudentId)
            .Select(x => new { StudentId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.StudentId, x => x.Count, cancellationToken);
        var profiles = await _dbContext.StudentCareProfiles
            .AsNoTracking()
            .Where(x => studentIds.Contains(x.StudentId))
            .ToDictionaryAsync(x => x.StudentId, cancellationToken);
        var wasteBankRows = await BuildWasteBankReportRowsAsync(students, academicYearId, cancellationToken);
        var openFollowUpCases = await _dbContext.StudentCareFollowUpCases
            .AsNoTracking()
            .Include(x => x.Student)!.ThenInclude(x => x!.Classroom)
            .Where(x => studentIds.Contains(x.StudentId))
            .Where(x => !academicYearId.HasValue || x.AcademicYearId == academicYearId.Value)
            .Where(x => x.Status == StudentCareFollowUpStatus.Open || x.Status == StudentCareFollowUpStatus.InProgress)
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.DueDate ?? DateTime.MaxValue)
            .ThenByDescending(x => x.OpenedAt)
            .Select(x => new StudentCareFollowUpReportRowViewModel
            {
                StudentId = x.StudentId,
                StudentCode = x.Student != null ? x.Student.StudentCode : "-",
                FullName = x.Student != null ? x.Student.FullName : "-",
                Classroom = x.Student != null && x.Student.Classroom != null ? x.Student.Classroom.Name : "-",
                Title = x.Title,
                Priority = x.Priority,
                Status = x.Status,
                OpenedAt = x.OpenedAt,
                DueDate = x.DueDate,
                IsOverdue = x.DueDate.HasValue && x.DueDate.Value.Date < DateTime.Today
            })
            .Take(30)
            .ToListAsync(cancellationToken);
        var behaviorDeductionTransactions = await _dbContext.BehaviorScoreTransactions
            .AsNoTracking()
            .Include(x => x.Student)!.ThenInclude(x => x!.Classroom)
            .Where(x => studentIds.Contains(x.StudentId) && x.ScoreChange < 0)
            .Where(x => !academicYearId.HasValue || x.AcademicYearId == academicYearId.Value)
            .OrderByDescending(x => x.RecordedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => new StudentCareScoreTransactionReportRowViewModel
            {
                StudentId = x.StudentId,
                StudentCode = x.Student != null ? x.Student.StudentCode : "-",
                FullName = x.Student != null ? x.Student.FullName : "-",
                Classroom = x.Student != null && x.Student.Classroom != null ? x.Student.Classroom.Name : "-",
                RecordedAt = x.RecordedAt,
                Category = x.Category,
                Point = x.ScoreChange,
                Description = x.Reason,
                Status = x.Status
            })
            .Take(30)
            .ToListAsync(cancellationToken);
        var recentGoodnessTransactions = await _dbContext.GoodnessBankTransactions
            .AsNoTracking()
            .Include(x => x.Student)!.ThenInclude(x => x!.Classroom)
            .Where(x => studentIds.Contains(x.StudentId))
            .Where(x => !academicYearId.HasValue || x.AcademicYearId == academicYearId.Value)
            .OrderByDescending(x => x.RecordedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => new StudentCareScoreTransactionReportRowViewModel
            {
                StudentId = x.StudentId,
                StudentCode = x.Student != null ? x.Student.StudentCode : "-",
                FullName = x.Student != null ? x.Student.FullName : "-",
                Classroom = x.Student != null && x.Student.Classroom != null ? x.Student.Classroom.Name : "-",
                RecordedAt = x.RecordedAt,
                Category = x.GoodnessType,
                Point = x.Point,
                Description = x.Description,
                Status = x.Status
            })
            .Take(30)
            .ToListAsync(cancellationToken);

        var model = new StudentCareReportViewModel
        {
            AssignedClassroomName = assignedClassroomName,
            BehaviorLowThreshold = settings.StudentCareLowBehaviorScoreThreshold,
            BehaviorDeductionTransactions = behaviorDeductionTransactions,
            RecentGoodnessTransactions = recentGoodnessTransactions,
            LowBehaviorScoreStudents = students
                .Select(student => new StudentCareBehaviorReportRowViewModel
                {
                    StudentId = student.Id,
                    StudentCode = student.StudentCode,
                    FullName = student.FullName,
                    Classroom = student.Classroom?.Name ?? "-",
                    BehaviorScore = behaviorScores.TryGetValue(student.Id, out var score) ? score : settings.StudentCareInitialBehaviorScore,
                    TransactionCount = behaviorCounts.TryGetValue(student.Id, out var count) ? count : 0
                })
                .Where(x => x.BehaviorScore < settings.StudentCareLowBehaviorScoreThreshold)
                .OrderBy(x => x.BehaviorScore)
                .ThenBy(x => x.StudentCode)
                .ToList(),
            TopGoodnessStudents = students
                .Select(student => new StudentCareGoodnessReportRowViewModel
                {
                    StudentId = student.Id,
                    StudentCode = student.StudentCode,
                    FullName = student.FullName,
                    Classroom = student.Classroom?.Name ?? "-",
                    GoodnessPoint = goodnessPoints.TryGetValue(student.Id, out var point) ? point : 0,
                    TransactionCount = goodnessCounts.TryGetValue(student.Id, out var count) ? count : 0
                })
                .Where(x => x.GoodnessPoint > 0)
                .OrderByDescending(x => x.GoodnessPoint)
                .ThenBy(x => x.StudentCode)
                .Take(20)
                .ToList(),
            NotVisitedStudents = students
                .Select(student =>
                {
                    profiles.TryGetValue(student.Id, out var profile);
                    return new StudentCareHomeVisitReportRowViewModel
                    {
                        StudentId = student.Id,
                        StudentCode = student.StudentCode,
                        FullName = student.FullName,
                        Classroom = student.Classroom?.Name ?? "-",
                        LastHomeVisitAt = profile?.LastHomeVisitAt,
                        RiskLevel = profile?.RiskLevel ?? StudentCareRiskLevel.Normal,
                        ProblemsFound = profile?.ProblemsFound
                    };
                })
                .Where(x => !x.LastHomeVisitAt.HasValue)
                .OrderBy(x => x.Classroom)
                .ThenBy(x => x.StudentCode)
                .ToList(),
            RiskStudents = students
                .Select(student =>
                {
                    profiles.TryGetValue(student.Id, out var profile);
                    return new StudentCareHomeVisitReportRowViewModel
                    {
                        StudentId = student.Id,
                        StudentCode = student.StudentCode,
                        FullName = student.FullName,
                        Classroom = student.Classroom?.Name ?? "-",
                        LastHomeVisitAt = profile?.LastHomeVisitAt,
                        RiskLevel = profile?.RiskLevel ?? StudentCareRiskLevel.Normal,
                        ProblemsFound = profile?.ProblemsFound
                    };
                })
                .Where(x => x.RiskLevel is StudentCareRiskLevel.Risk or StudentCareRiskLevel.Urgent)
                .OrderByDescending(x => x.RiskLevel)
                .ThenBy(x => x.StudentCode)
                .ToList(),
            WasteBankTopStudents = wasteBankRows
                .Where(x => x.TransactionCount > 0)
                .OrderByDescending(x => x.TotalAmount)
                .ThenByDescending(x => x.TotalWeightKg)
                .Take(20)
                .ToList(),
            OpenFollowUpCases = openFollowUpCases,
            WasteBankTotalWeightKg = wasteBankRows.Sum(x => x.TotalWeightKg),
            WasteBankTotalAmount = wasteBankRows.Sum(x => x.TotalAmount)
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int studentId, CancellationToken cancellationToken)
    {
        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);
        if (!settings.EnableStudentCareModule)
        {
            return NotFound();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var student = await GetAccessibleStudentWithClassroomAsync(user, studentId, cancellationToken);
        if (student is null)
        {
            return Forbid();
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

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var student = await GetAccessibleStudentWithClassroomAsync(user, studentId, cancellationToken);
        if (student is null)
        {
            return Forbid();
        }

        var model = await BuildStudentCareProfileDetailAsync(student, settings, true, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFollowUpCase(StudentCareFollowUpCaseInputViewModel input, CancellationToken cancellationToken)
    {
        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);
        if (!settings.EnableStudentCareModule)
        {
            return NotFound();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var student = await GetAccessibleStudentAsync(user, input.StudentId, cancellationToken);
        if (student is null)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "กรุณากรอกข้อมูลเคสติดตามให้ครบถ้วน";
            return RedirectToAction(nameof(Detail), new { studentId = input.StudentId });
        }

        var academicYearId = ResolveAcademicYearId(settings, student);
        if (!academicYearId.HasValue)
        {
            TempData["Error"] = "กรุณาตั้งค่าปีการศึกษาปัจจุบันหรือตรวจสอบข้อมูลปีการศึกษาของนักเรียน";
            return RedirectToAction(nameof(Detail), new { studentId = input.StudentId });
        }

        var followUpCase = new StudentCareFollowUpCase
        {
            StudentId = student.Id,
            AcademicYearId = academicYearId.Value,
            Title = input.Title.Trim(),
            Concern = input.Concern.Trim(),
            SupportPlan = input.SupportPlan.Trim(),
            Priority = input.Priority,
            Status = StudentCareFollowUpStatus.Open,
            OpenedAt = DateTime.UtcNow,
            DueDate = input.DueDate?.Date,
            AssignedToUserId = user.Id,
            CreatedByUserId = user.Id,
            LastUpdatedByUserId = user.Id
        };

        _dbContext.StudentCareFollowUpCases.Add(followUpCase);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            "CreateStudentCareFollowUpCase",
            "StudentCareFollowUpCase",
            followUpCase.Id.ToString(),
            $"{student.StudentCode} {student.FullName}: {followUpCase.Title}",
            HttpContextHelper.GetIpAddress(HttpContext),
            cancellationToken);

        TempData["Success"] = "สร้างเคสติดตามช่วยเหลือเรียบร้อย";
        return RedirectToAction(nameof(Detail), new { studentId = student.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateFollowUpCase(StudentCareFollowUpCaseStatusViewModel input, CancellationToken cancellationToken)
    {
        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);
        if (!settings.EnableStudentCareModule)
        {
            return NotFound();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var student = await GetAccessibleStudentAsync(user, input.StudentId, cancellationToken);
        if (student is null)
        {
            return Forbid();
        }

        var followUpCase = await _dbContext.StudentCareFollowUpCases
            .FirstOrDefaultAsync(x => x.Id == input.Id && x.StudentId == student.Id, cancellationToken);
        if (followUpCase is null)
        {
            return NotFound();
        }

        followUpCase.Status = input.Status;
        followUpCase.Outcome = input.Outcome?.Trim();
        followUpCase.CompletedAt = input.Status == StudentCareFollowUpStatus.Completed ? DateTime.UtcNow : null;
        followUpCase.LastUpdatedByUserId = user.Id;
        followUpCase.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            "UpdateStudentCareFollowUpCase",
            "StudentCareFollowUpCase",
            followUpCase.Id.ToString(),
            $"{student.StudentCode} {student.FullName}: {followUpCase.Status}",
            HttpContextHelper.GetIpAddress(HttpContext),
            cancellationToken);

        TempData["Success"] = "อัปเดตเคสติดตามช่วยเหลือเรียบร้อย";
        return RedirectToAction(nameof(Detail), new { studentId = student.Id });
    }

    [HttpGet]
    public async Task<IActionResult> HomeVisit(int studentId, CancellationToken cancellationToken)
    {
        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);
        if (!settings.EnableStudentCareModule || !settings.EnableHomeVisitModule)
        {
            return NotFound();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var student = await GetAccessibleStudentWithClassroomAsync(user, studentId, cancellationToken);
        if (student is null)
        {
            return Forbid();
        }

        var latestVisit = await _dbContext.HomeVisitRecords
            .AsNoTracking()
            .Where(x => x.StudentId == student.Id)
            .OrderByDescending(x => x.VisitDate)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var profile = await _dbContext.StudentCareProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.StudentId == student.Id, cancellationToken);
        var guardian = await _dbContext.StudentGuardians
            .AsNoTracking()
            .Where(x => x.StudentId == student.Id)
            .OrderByDescending(x => x.IsPrimaryContact)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var model = new HomeVisitFormViewModel
        {
            StudentId = student.Id,
            StudentCode = student.StudentCode,
            StudentName = student.FullName,
            Classroom = student.Classroom?.Name ?? "-",
            VisitDate = DateTime.Today,
            VisitStatus = HomeVisitStatus.Completed,
            LivesWith = latestVisit?.LivesWith ?? profile?.LivesWith,
            GuardianName = guardian?.FullName ?? student.GuardianName,
            GuardianNationalId = guardian?.NationalId ?? student.GuardianNationalId,
            GuardianRelationship = guardian?.Relationship ?? profile?.GuardianRelationship,
            GuardianPhone = guardian?.PhoneNumber ?? student.GuardianPhone,
            GuardianOccupation = guardian?.Occupation,
            HouseholdIncome = latestVisit?.HouseholdIncome ?? profile?.HouseholdIncome ?? guardian?.MonthlyIncome,
            HouseCondition = latestVisit?.HouseCondition,
            FamilyRelationship = latestVisit?.FamilyRelationship,
            LearningSupportAtHome = latestVisit?.LearningSupportAtHome,
            RiskBehaviors = latestVisit?.RiskBehaviors ?? profile?.RiskBehaviors,
            ProblemsFound = latestVisit?.ProblemsFound ?? profile?.ProblemsFound,
            TeacherObservation = latestVisit?.TeacherObservation,
            SupportPlan = latestVisit?.SupportPlan ?? profile?.SupportRecommendation,
            Latitude = latestVisit?.Latitude ?? profile?.HomeLatitude,
            Longitude = latestVisit?.Longitude ?? profile?.HomeLongitude,
            RiskLevel = profile?.RiskLevel ?? StudentCareRiskLevel.Normal,
            CurrentParentPhotoPath = latestVisit?.ParentPhotoPath ?? guardian?.PhotoPath,
            CurrentHousePhotoPath = latestVisit?.HousePhotoPath
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HomeVisit(HomeVisitFormViewModel model, CancellationToken cancellationToken)
    {
        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);
        if (!settings.EnableStudentCareModule || !settings.EnableHomeVisitModule)
        {
            return NotFound();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var student = await GetAccessibleStudentWithClassroomAsync(user, model.StudentId, cancellationToken);
        if (student is null)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            model.StudentCode = student.StudentCode;
            model.StudentName = student.FullName;
            model.Classroom = student.Classroom?.Name ?? "-";
            return View(model);
        }

        var academicYearId = ResolveAcademicYearId(settings, student);
        if (!academicYearId.HasValue)
        {
            TempData["Error"] = "กรุณาตั้งค่าปีการศึกษาปัจจุบันหรือตรวจสอบข้อมูลปีการศึกษาของนักเรียน";
            return RedirectToAction(nameof(Index));
        }

        string? parentPhotoPath = model.CurrentParentPhotoPath;
        string? housePhotoPath = model.CurrentHousePhotoPath;

        try
        {
            if (model.ParentPhoto is { Length: > 0 })
            {
                parentPhotoPath = await _fileStorageService.SaveStudentCarePhotoAsync(model.ParentPhoto, student.Id, "parent", cancellationToken);
            }

            if (model.HousePhoto is { Length: > 0 })
            {
                housePhotoPath = await _fileStorageService.SaveStudentCarePhotoAsync(model.HousePhoto, student.Id, "house", cancellationToken);
            }
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            model.StudentCode = student.StudentCode;
            model.StudentName = student.FullName;
            model.Classroom = student.Classroom?.Name ?? "-";
            return View(model);
        }

        var record = new HomeVisitRecord
        {
            StudentId = student.Id,
            AcademicYearId = academicYearId.Value,
            VisitDate = model.VisitDate.Date,
            TeacherUserId = user.Id,
            VisitStatus = model.VisitStatus,
            LivesWith = model.LivesWith?.Trim(),
            HouseCondition = model.HouseCondition?.Trim(),
            FamilyRelationship = model.FamilyRelationship?.Trim(),
            LearningSupportAtHome = model.LearningSupportAtHome?.Trim(),
            HouseholdIncome = model.HouseholdIncome,
            RiskBehaviors = model.RiskBehaviors?.Trim(),
            ProblemsFound = model.ProblemsFound?.Trim(),
            TeacherObservation = model.TeacherObservation?.Trim(),
            SupportPlan = model.SupportPlan?.Trim(),
            ParentPhotoPath = parentPhotoPath,
            HousePhotoPath = housePhotoPath,
            Latitude = model.Latitude,
            Longitude = model.Longitude,
            SubmittedAt = DateTime.UtcNow,
            ApprovedByUserId = user.Id,
            ApprovedAt = DateTime.UtcNow
        };

        _dbContext.HomeVisitRecords.Add(record);

        var profile = await _dbContext.StudentCareProfiles
            .FirstOrDefaultAsync(x => x.StudentId == student.Id, cancellationToken);
        if (profile is null)
        {
            profile = new StudentCareProfile { StudentId = student.Id };
            _dbContext.StudentCareProfiles.Add(profile);
        }

        profile.LivesWith = model.LivesWith?.Trim();
        profile.GuardianRelationship = model.GuardianRelationship?.Trim();
        profile.HouseholdIncome = model.HouseholdIncome;
        profile.HomeLatitude = model.Latitude ?? profile.HomeLatitude;
        profile.HomeLongitude = model.Longitude ?? profile.HomeLongitude;
        if (model.Latitude.HasValue && model.Longitude.HasValue)
        {
            profile.HomeLocationSharedAt = DateTime.UtcNow;
        }
        profile.RiskLevel = model.RiskLevel;
        profile.RiskBehaviors = model.RiskBehaviors?.Trim();
        profile.ProblemsFound = model.ProblemsFound?.Trim();
        profile.SupportRecommendation = model.SupportPlan?.Trim();
        profile.LastHomeVisitAt = model.VisitDate.Date;
        student.GuardianNationalId = model.GuardianNationalId?.Trim() ?? student.GuardianNationalId;

        await UpsertGuardianAsync(student.Id, model, parentPhotoPath, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            "RecordHomeVisit",
            "HomeVisitRecord",
            record.Id.ToString(),
            $"{student.StudentCode} {student.FullName}: {record.VisitDate:yyyy-MM-dd}, {record.VisitStatus}",
            HttpContextHelper.GetIpAddress(HttpContext),
            cancellationToken);

        TempData["Success"] = "บันทึกเยี่ยมบ้านเรียบร้อย";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveHomeLocation(StudentHomeLocationInputViewModel input, CancellationToken cancellationToken)
    {
        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);
        if (!settings.EnableStudentCareModule || !settings.EnableHomeVisitModule)
        {
            return NotFound();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var student = await GetAccessibleStudentAsync(user, input.StudentId, cancellationToken);
        if (student is null)
        {
            return Forbid();
        }

        if (!ModelState.IsValid || !IsValidCoordinate(input.Latitude, input.Longitude))
        {
            TempData["Error"] = "พิกัดบ้านนักเรียนไม่ถูกต้อง";
            return RedirectToAction(nameof(Index));
        }

        var profile = await _dbContext.StudentCareProfiles
            .FirstOrDefaultAsync(x => x.StudentId == student.Id, cancellationToken);
        if (profile is null)
        {
            profile = new StudentCareProfile { StudentId = student.Id };
            _dbContext.StudentCareProfiles.Add(profile);
        }

        profile.HomeLatitude = input.Latitude;
        profile.HomeLongitude = input.Longitude;
        profile.HomeLocationSharedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            "SaveStudentHomeLocation",
            "StudentCareProfile",
            student.Id.ToString(),
            $"{student.StudentCode} {student.FullName}: {input.Latitude.ToString(CultureInfo.InvariantCulture)},{input.Longitude.ToString(CultureInfo.InvariantCulture)}",
            HttpContextHelper.GetIpAddress(HttpContext),
            cancellationToken);

        TempData["Success"] = "บันทึกพิกัดบ้านนักเรียนเรียบร้อย";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteHomeLocation(int studentId, CancellationToken cancellationToken)
    {
        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);
        if (!settings.EnableStudentCareModule || !settings.EnableHomeVisitModule)
        {
            return NotFound();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var student = await GetAccessibleStudentAsync(user, studentId, cancellationToken);
        if (student is null)
        {
            return Forbid();
        }

        var profile = await _dbContext.StudentCareProfiles
            .FirstOrDefaultAsync(x => x.StudentId == student.Id, cancellationToken);
        if (profile is not null)
        {
            profile.HomeLatitude = null;
            profile.HomeLongitude = null;
            profile.HomeLocationSharedAt = null;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        await _auditLogService.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            "DeleteStudentHomeLocation",
            "StudentCareProfile",
            student.Id.ToString(),
            $"{student.StudentCode} {student.FullName}: cleared home location",
            HttpContextHelper.GetIpAddress(HttpContext),
            cancellationToken);

        TempData["Success"] = "ลบพิกัดบ้านนักเรียนเรียบร้อย";
        return RedirectToAction(nameof(Index));
    }

    private async Task<StudentCareDashboardViewModel> BuildDashboardAsync(
        ApplicationUser user,
        SystemSetting settings,
        StudentCareDashboardFilterViewModel filter,
        CancellationToken cancellationToken)
    {
        var (students, assignedClassroomName) = await GetAccessibleStudentsAsync(user, filter, cancellationToken);
        var studentIds = students.Select(x => x.Id).ToList();
        var profiles = await _dbContext.StudentCareProfiles
            .AsNoTracking()
            .Where(x => studentIds.Contains(x.StudentId))
            .ToDictionaryAsync(x => x.StudentId, cancellationToken);
        var behaviorScores = await BuildBehaviorScoreMapAsync(studentIds, settings.AcademicYearCurrentId, settings.StudentCareInitialBehaviorScore, cancellationToken);
        var goodnessPoints = await BuildGoodnessPointMapAsync(studentIds, settings.AcademicYearCurrentId, cancellationToken);
        var wasteBankTotals = await BuildWasteBankTotalsMapAsync(studentIds, settings.AcademicYearCurrentId, cancellationToken);
        var photoPaths = await _dbContext.StudentPhotos
            .AsNoTracking()
            .Where(x => studentIds.Contains(x.StudentId))
            .OrderByDescending(x => x.IsPrimary)
            .ThenByDescending(x => x.CapturedAt)
            .GroupBy(x => x.StudentId)
            .Select(x => new
            {
                StudentId = x.Key,
                FilePath = x.Select(p => p.FilePath).FirstOrDefault()
            })
            .ToDictionaryAsync(x => x.StudentId, x => x.FilePath, cancellationToken);
        var today = DateTime.Today;
        var reminderCutoff = today.AddDays(7);
        var openFollowUpQuery = _dbContext.StudentCareFollowUpCases
            .AsNoTracking()
            .Where(x => studentIds.Contains(x.StudentId))
            .Where(x => !settings.AcademicYearCurrentId.HasValue || x.AcademicYearId == settings.AcademicYearCurrentId.Value)
            .Where(x => x.Status == StudentCareFollowUpStatus.Open || x.Status == StudentCareFollowUpStatus.InProgress);
        var openFollowUpCaseCount = await openFollowUpQuery.CountAsync(cancellationToken);
        var overdueFollowUpCaseCount = await openFollowUpQuery
            .CountAsync(x => x.DueDate.HasValue && x.DueDate.Value.Date < today, cancellationToken);
        var dueTodayFollowUpCaseCount = await openFollowUpQuery
            .CountAsync(x => x.DueDate.HasValue && x.DueDate.Value.Date == today, cancellationToken);
        var followUpReminders = await openFollowUpQuery
            .Include(x => x.Student)!.ThenInclude(x => x!.Classroom)
            .Where(x => !x.DueDate.HasValue || x.DueDate.Value.Date <= reminderCutoff)
            .OrderBy(x => x.DueDate.HasValue ? x.DueDate.Value : DateTime.MaxValue)
            .ThenByDescending(x => x.Priority)
            .ThenByDescending(x => x.OpenedAt)
            .Select(x => new StudentCareFollowUpReminderViewModel
            {
                StudentId = x.StudentId,
                StudentCode = x.Student != null ? x.Student.StudentCode : "-",
                FullName = x.Student != null ? x.Student.FullName : "-",
                Classroom = x.Student != null && x.Student.Classroom != null ? x.Student.Classroom.Name : "-",
                Title = x.Title,
                Priority = x.Priority,
                Status = x.Status,
                OpenedAt = x.OpenedAt,
                DueDate = x.DueDate,
                IsOverdue = x.DueDate.HasValue && x.DueDate.Value.Date < today,
                IsDueToday = x.DueDate.HasValue && x.DueDate.Value.Date == today
            })
            .Take(10)
            .ToListAsync(cancellationToken);
        var gradeLevelOptions = await BuildGradeLevelOptionsAsync(cancellationToken);
        var classroomOptions = await BuildClassroomOptionsAsync(filter.GradeLevelId, cancellationToken);
        var behaviorCategoryOptions = await _dbContext.BehaviorScoreCategories
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new StudentCareCategoryOptionViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Point = x.ScoreChange,
                Description = x.Description
            })
            .ToListAsync(cancellationToken);
        var goodnessCategoryOptions = await _dbContext.GoodnessBankCategories
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new StudentCareCategoryOptionViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Point = x.Point,
                Description = x.Description
            })
            .ToListAsync(cancellationToken);

        return new StudentCareDashboardViewModel
        {
            TeacherName = string.IsNullOrWhiteSpace(user.FullName) ? (user.UserName ?? "Teacher") : user.FullName,
            AssignedClassroomName = assignedClassroomName,
            Filter = filter,
            CanFilterAllStudents = CanViewAllStudentCare(),
            IsStudentCareEnabled = settings.EnableStudentCareModule,
            IsBehaviorScoreEnabled = settings.EnableBehaviorScoreModule,
            IsGoodnessBankEnabled = settings.EnableGoodnessBankModule,
            IsHomeVisitEnabled = settings.EnableHomeVisitModule,
            IsWasteBankEnabled = false,
            StudentCount = students.Count,
            ProfileCount = profiles.Count,
            HomeVisitedCount = profiles.Values.Count(x => x.LastHomeVisitAt.HasValue),
            RiskStudentCount = profiles.Values.Count(x => x.RiskLevel is StudentCareRiskLevel.Risk or StudentCareRiskLevel.Urgent),
            UrgentRiskStudentCount = profiles.Values.Count(x => x.RiskLevel == StudentCareRiskLevel.Urgent),
            OpenFollowUpCaseCount = openFollowUpCaseCount,
            OverdueFollowUpCaseCount = overdueFollowUpCaseCount,
            DueTodayFollowUpCaseCount = dueTodayFollowUpCaseCount,
            WasteBankTotalWeightKg = wasteBankTotals.Values.Sum(x => x.TotalWeightKg),
            WasteBankTotalAmount = wasteBankTotals.Values.Sum(x => x.TotalAmount),
            GradeLevelOptions = gradeLevelOptions,
            ClassroomOptions = classroomOptions,
            BehaviorCategoryOptions = behaviorCategoryOptions,
            GoodnessCategoryOptions = goodnessCategoryOptions,
            FollowUpReminders = followUpReminders,
            StudentOptions = students
                .Select(x => new StudentCareStudentOptionViewModel
                {
                    Value = x.Id,
                    Text = $"{x.StudentCode} - {x.FullName}",
                    StudentCode = x.StudentCode,
                    FullName = x.FullName,
                    NationalId = x.NationalId,
                    GuardianNationalId = x.GuardianNationalId,
                    GradeLevelId = x.GradeLevelId,
                    ClassroomId = x.ClassroomId,
                    PhotoPath = photoPaths.TryGetValue(x.Id, out var photoPath) ? photoPath : null
                })
                .ToList(),
            Students = students.Select(student =>
            {
                profiles.TryGetValue(student.Id, out var profile);
                return new StudentCareDashboardStudentViewModel
                {
                    StudentId = student.Id,
                    StudentCode = student.StudentCode,
                    FullName = student.FullName,
                    Classroom = student.Classroom?.Name ?? "-",
                    BehaviorScore = behaviorScores.TryGetValue(student.Id, out var score) ? score : settings.StudentCareInitialBehaviorScore,
                    GoodnessPoint = goodnessPoints.TryGetValue(student.Id, out var point) ? point : 0,
                    WasteBankWeightKg = wasteBankTotals.TryGetValue(student.Id, out var wasteTotal) ? wasteTotal.TotalWeightKg : 0m,
                    WasteBankAmount = wasteBankTotals.TryGetValue(student.Id, out wasteTotal) ? wasteTotal.TotalAmount : 0m,
                    RiskLevel = profile?.RiskLevel ?? StudentCareRiskLevel.Normal,
                    LastHomeVisitAt = profile?.LastHomeVisitAt,
                    LivesWith = profile?.LivesWith,
                    HasHomeLocation = profile?.HomeLatitude.HasValue == true && profile.HomeLongitude.HasValue,
                    HomeLatitude = profile?.HomeLatitude,
                    HomeLongitude = profile?.HomeLongitude,
                    HomeLocationSharedAt = profile?.HomeLocationSharedAt
                };
            }).ToList()
        };
    }

    private async Task<StudentCareProfileDetailViewModel> BuildStudentCareProfileDetailAsync(
        Student student,
        SystemSetting settings,
        bool isPrintMode,
        CancellationToken cancellationToken)
    {
        var academicYearId = ResolveAcademicYearId(settings, student);
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
                NationalId = x.NationalId,
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
            .Where(x => x.StudentId == student.Id)
            .Where(x => !academicYearId.HasValue || x.AcademicYearId == academicYearId.Value)
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
            .Where(x => x.StudentId == student.Id)
            .Where(x => !academicYearId.HasValue || x.AcademicYearId == academicYearId.Value)
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
            .Where(x => x.StudentId == student.Id)
            .Where(x => !academicYearId.HasValue || x.AcademicYearId == academicYearId.Value)
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
            .Where(x => x.StudentId == student.Id)
            .Where(x => !academicYearId.HasValue || x.AcademicYearId == academicYearId.Value)
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
            .Where(x => x.StudentId == student.Id)
            .Where(x => !academicYearId.HasValue || x.AcademicYearId == academicYearId.Value)
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
            .Where(x => x.StudentId == student.Id && x.Status == StudentCareRecordStatus.Approved)
            .Where(x => !academicYearId.HasValue || x.AcademicYearId == academicYearId.Value)
            .SumAsync(x => (int?)x.ScoreChange, cancellationToken) ?? 0;
        var approvedGoodnessPoint = await _dbContext.GoodnessBankTransactions
            .AsNoTracking()
            .Where(x => x.StudentId == student.Id && x.Status == StudentCareRecordStatus.Approved)
            .Where(x => !academicYearId.HasValue || x.AcademicYearId == academicYearId.Value)
            .SumAsync(x => (int?)x.Point, cancellationToken) ?? 0;
        var wasteTotals = await _dbContext.WasteBankTransactions
            .AsNoTracking()
            .Where(x => x.StudentId == student.Id)
            .Where(x => !academicYearId.HasValue || x.AcademicYearId == academicYearId.Value)
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
            Guardians = guardians,
            BehaviorTransactions = behaviorTransactions,
            GoodnessTransactions = goodnessTransactions,
            HomeVisits = homeVisits,
            WasteBankTransactions = wasteBankTransactions,
            FollowUpCases = followUpCases,
            FollowUpInput = new StudentCareFollowUpCaseInputViewModel
            {
                StudentId = student.Id,
                Priority = profile?.RiskLevel == StudentCareRiskLevel.Urgent
                    ? StudentCareFollowUpPriority.Urgent
                    : profile?.RiskLevel == StudentCareRiskLevel.Risk
                        ? StudentCareFollowUpPriority.High
                        : StudentCareFollowUpPriority.Normal
            }
        };
    }

    private async Task<(List<Student> Students, string AssignedClassroomName)> GetAccessibleStudentsAsync(
        ApplicationUser user,
        StudentCareDashboardFilterViewModel? filter,
        CancellationToken cancellationToken)
    {
        var canViewAll = CanViewAllStudentCare();
        var classroomId = canViewAll ? null : user.AssignedClassroomId;
        var studentsQuery = _dbContext.Students
            .AsNoTracking()
            .Include(x => x.GradeLevel)
            .Include(x => x.Classroom)
            .Where(x => x.IsActive);

        if (canViewAll)
        {
            if (filter?.GradeLevelId.HasValue == true)
            {
                studentsQuery = studentsQuery.Where(x => x.GradeLevelId == filter.GradeLevelId.Value);
            }

            if (filter?.ClassroomId.HasValue == true)
            {
                studentsQuery = studentsQuery.Where(x => x.ClassroomId == filter.ClassroomId.Value);
            }
        }
        else if (classroomId.HasValue)
        {
            studentsQuery = studentsQuery.Where(x => x.ClassroomId == classroomId.Value);
        }
        else
        {
            studentsQuery = studentsQuery.Where(x => false);
        }

        var keyword = filter?.Keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            studentsQuery = studentsQuery.Where(x =>
                x.StudentCode.Contains(keyword) ||
                x.FirstName.Contains(keyword) ||
                x.LastName.Contains(keyword) ||
                (x.NationalId != null && x.NationalId.Contains(keyword)) ||
                (x.GuardianNationalId != null && x.GuardianNationalId.Contains(keyword)) ||
                (x.FirstName + " " + x.LastName).Contains(keyword) ||
                (x.Prefix + x.FirstName + " " + x.LastName).Contains(keyword));
        }

        var students = await studentsQuery
            .OrderBy(x => x.Classroom!.Name)
            .ThenBy(x => x.StudentNo)
            .ThenBy(x => x.StudentCode)
            .ToListAsync(cancellationToken);

        var assignedClassroomName = "-";
        if (classroomId.HasValue)
        {
            assignedClassroomName = await _dbContext.Classrooms
                .AsNoTracking()
                .Where(x => x.Id == classroomId.Value)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken)
                ?? "-";
        }
        else if (canViewAll)
        {
            assignedClassroomName = "ทุกห้องเรียน";
        }

        return (students, assignedClassroomName);
    }

    private Task<(List<Student> Students, string AssignedClassroomName)> GetAccessibleStudentsAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        return GetAccessibleStudentsAsync(user, null, cancellationToken);
    }

    private async Task<Student?> GetAccessibleStudentAsync(
        ApplicationUser user,
        int studentId,
        CancellationToken cancellationToken)
    {
        var canViewAll = CanViewAllStudentCare();
        var query = _dbContext.Students.Where(x => x.Id == studentId && x.IsActive);
        if (!canViewAll)
        {
            if (!user.AssignedClassroomId.HasValue)
            {
                return null;
            }

            query = query.Where(x => x.ClassroomId == user.AssignedClassroomId.Value);
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Student?> GetAccessibleStudentWithClassroomAsync(
        ApplicationUser user,
        int studentId,
        CancellationToken cancellationToken)
    {
        var canViewAll = CanViewAllStudentCare();
        var query = _dbContext.Students
            .Include(x => x.Classroom)
            .Include(x => x.GradeLevel)
            .Where(x => x.Id == studentId && x.IsActive);

        if (!canViewAll)
        {
            if (!user.AssignedClassroomId.HasValue)
            {
                return null;
            }

            query = query.Where(x => x.ClassroomId == user.AssignedClassroomId.Value);
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    private bool CanViewAllStudentCare()
    {
        return User.IsInRole("SuperAdmin") || User.IsInRole("Admin") || User.IsInRole("StudentCareAdmin");
    }

    private bool CanManageWasteBank()
    {
        return User.IsInRole("SuperAdmin") || User.IsInRole("Admin") || User.IsInRole("WasteBankStaff");
    }

    private async Task<List<SelectOptionViewModel>> BuildGradeLevelOptionsAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.GradeLevels
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new SelectOptionViewModel { Value = x.Id, Text = x.Name })
            .ToListAsync(cancellationToken);
    }

    private async Task<List<StudentCareClassroomOptionViewModel>> BuildClassroomOptionsAsync(int? gradeLevelId, CancellationToken cancellationToken)
    {
        var query = _dbContext.Classrooms
            .AsNoTracking()
            .Where(x => x.IsActive);

        if (gradeLevelId.HasValue)
        {
            query = query.Where(x => x.GradeLevelId == gradeLevelId.Value);
        }

        return await query
            .OrderBy(x => x.Name)
            .Select(x => new StudentCareClassroomOptionViewModel
            {
                Value = x.Id,
                Text = x.Name,
                GradeLevelId = x.GradeLevelId
            })
            .ToListAsync(cancellationToken);
    }

    private async Task UpsertGuardianAsync(
        int studentId,
        HomeVisitFormViewModel model,
        string? parentPhotoPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.GuardianName) &&
            string.IsNullOrWhiteSpace(model.GuardianNationalId) &&
            string.IsNullOrWhiteSpace(model.GuardianRelationship) &&
            string.IsNullOrWhiteSpace(model.GuardianPhone) &&
            string.IsNullOrWhiteSpace(model.GuardianOccupation) &&
            !model.HouseholdIncome.HasValue &&
            string.IsNullOrWhiteSpace(parentPhotoPath))
        {
            return;
        }

        var guardian = await _dbContext.StudentGuardians
            .Where(x => x.StudentId == studentId)
            .OrderByDescending(x => x.IsPrimaryContact)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (guardian is null)
        {
            guardian = new StudentGuardian
            {
                StudentId = studentId,
                IsPrimaryContact = true
            };
            _dbContext.StudentGuardians.Add(guardian);
        }

        guardian.FullName = string.IsNullOrWhiteSpace(model.GuardianName)
            ? "ไม่ระบุชื่อผู้ปกครอง"
            : model.GuardianName.Trim();
        guardian.NationalId = model.GuardianNationalId?.Trim();
        guardian.Relationship = model.GuardianRelationship?.Trim();
        guardian.PhoneNumber = model.GuardianPhone?.Trim();
        guardian.Occupation = model.GuardianOccupation?.Trim();
        guardian.MonthlyIncome = model.HouseholdIncome;
        guardian.PhotoPath = parentPhotoPath ?? guardian.PhotoPath;
    }

    private async Task<Dictionary<int, int>> BuildBehaviorScoreMapAsync(
        IReadOnlyCollection<int> studentIds,
        int? academicYearId,
        int initialBehaviorScore,
        CancellationToken cancellationToken)
    {
        if (studentIds.Count == 0)
        {
            return [];
        }

        var changes = await _dbContext.BehaviorScoreTransactions
            .AsNoTracking()
            .Where(x => studentIds.Contains(x.StudentId) && x.Status == StudentCareRecordStatus.Approved)
            .Where(x => !academicYearId.HasValue || x.AcademicYearId == academicYearId.Value)
            .GroupBy(x => x.StudentId)
            .Select(x => new { StudentId = x.Key, Score = initialBehaviorScore + x.Sum(t => t.ScoreChange) })
            .ToDictionaryAsync(x => x.StudentId, x => x.Score, cancellationToken);

        return changes;
    }

    private async Task<Dictionary<int, int>> BuildGoodnessPointMapAsync(
        IReadOnlyCollection<int> studentIds,
        int? academicYearId,
        CancellationToken cancellationToken)
    {
        if (studentIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.GoodnessBankTransactions
            .AsNoTracking()
            .Where(x => studentIds.Contains(x.StudentId) && x.Status == StudentCareRecordStatus.Approved)
            .Where(x => !academicYearId.HasValue || x.AcademicYearId == academicYearId.Value)
            .GroupBy(x => x.StudentId)
            .Select(x => new { StudentId = x.Key, Point = x.Sum(t => t.Point) })
            .ToDictionaryAsync(x => x.StudentId, x => x.Point, cancellationToken);
    }

    private async Task<Dictionary<int, WasteBankTotal>> BuildWasteBankTotalsMapAsync(
        IReadOnlyCollection<int> studentIds,
        int? academicYearId,
        CancellationToken cancellationToken)
    {
        if (studentIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.WasteBankTransactions
            .AsNoTracking()
            .Where(x => studentIds.Contains(x.StudentId))
            .Where(x => !academicYearId.HasValue || x.AcademicYearId == academicYearId.Value)
            .GroupBy(x => x.StudentId)
            .Select(x => new
            {
                StudentId = x.Key,
                TotalWeightKg = x.Sum(t => t.WeightKg),
                TotalAmount = x.Sum(t => t.Amount),
                TransactionCount = x.Count()
            })
            .ToDictionaryAsync(
                x => x.StudentId,
                x => new WasteBankTotal(x.TotalWeightKg, x.TotalAmount, x.TransactionCount),
                cancellationToken);
    }

    private async Task<List<StudentCareWasteBankReportRowViewModel>> BuildWasteBankReportRowsAsync(
        IReadOnlyCollection<Student> students,
        int? academicYearId,
        CancellationToken cancellationToken)
    {
        var studentIds = students.Select(x => x.Id).ToList();
        var totals = await BuildWasteBankTotalsMapAsync(studentIds, academicYearId, cancellationToken);

        return students
            .Select(student =>
            {
                totals.TryGetValue(student.Id, out var total);
                return new StudentCareWasteBankReportRowViewModel
                {
                    StudentId = student.Id,
                    StudentCode = student.StudentCode,
                    FullName = student.FullName,
                    Classroom = student.Classroom?.Name ?? "-",
                    TotalWeightKg = total?.TotalWeightKg ?? 0m,
                    TotalAmount = total?.TotalAmount ?? 0m,
                    TransactionCount = total?.TransactionCount ?? 0
                };
            })
            .ToList();
    }

    private static int? ResolveAcademicYearId(SystemSetting settings, Student student)
        => settings.AcademicYearCurrentId ?? student.AcademicYearId;

    private static bool IsValidCoordinate(decimal latitude, decimal longitude)
        => latitude is >= -90m and <= 90m && longitude is >= -180m and <= 180m;

    private sealed record WasteBankTotal(decimal TotalWeightKg, decimal TotalAmount, int TransactionCount);
}
