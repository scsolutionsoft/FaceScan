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
using System.Text;

namespace FaceScan.Web.Areas.Teacher.Controllers;

[Area("Teacher")]
[Authorize(Roles = "SuperAdmin,Admin,Teacher,HomeroomHead")]
public class PeriodAttendanceController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPeriodAttendancePdfExportService _pdfExportService;
    private readonly IAuditLogService _auditLogService;

    public PeriodAttendanceController(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IPeriodAttendancePdfExportService pdfExportService,
        IAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _pdfExportService = pdfExportService;
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] PeriodAttendanceFilterViewModel filter, CancellationToken cancellationToken)
    {
        var model = await BuildPageModelAsync(filter, cancellationToken);
        if (model is null)
        {
            return Challenge();
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Print([FromQuery] PeriodAttendanceFilterViewModel filter, CancellationToken cancellationToken)
    {
        var model = await BuildPageModelAsync(filter, cancellationToken);
        if (model is null)
        {
            return Challenge();
        }

        if (!model.HasClassroomPermission || !model.ReportFilter.ClassroomId.HasValue)
        {
            return RedirectToAction(nameof(Index), BuildIndexRouteValues(
                model.Filter.Date,
                model.Filter.ClassroomId,
                model.Filter.ClassPeriodId,
                model.ReportFilter.Date,
                model.ReportFilter.ClassroomId,
                model.ReportFilter.ClassPeriodId));
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ExportPdf([FromQuery] PeriodAttendanceFilterViewModel filter, CancellationToken cancellationToken)
    {
        var model = await BuildPageModelAsync(filter, cancellationToken);
        if (model is null)
        {
            return Challenge();
        }

        if (!model.HasClassroomPermission || !model.ReportFilter.ClassroomId.HasValue)
        {
            return RedirectToAction(nameof(Index), BuildIndexRouteValues(
                model.Filter.Date,
                model.Filter.ClassroomId,
                model.Filter.ClassPeriodId,
                model.ReportFilter.Date,
                model.ReportFilter.ClassroomId,
                model.ReportFilter.ClassPeriodId));
        }

        var classroomName = model.ClassroomOptions.FirstOrDefault(x => x.Value == model.ReportFilter.ClassroomId)?.Text ?? "Classroom";
        var currentPeriod = model.ReportFilter.ClassPeriodId.GetValueOrDefault() > 0
            ? model.PeriodOptions.FirstOrDefault(x => x.Value == model.ReportFilter.ClassPeriodId)?.Text ?? "-"
            : "ทุกคาบ";
        var fileName = BuildPdfFileName(model.ReportFilter.Date, classroomName);
        var pdfBytes = _pdfExportService.Generate(model, classroomName, currentPeriod);

        return File(pdfBytes, "application/pdf", fileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateReportStatus(PeriodAttendanceQuickUpdateViewModel model, CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var isAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");
        var canAccessAllClassrooms = isAdmin || User.IsInRole("Teacher");
        var date = NormalizeIncomingDate(model.Date);
        var checkDate = NormalizeIncomingDate(model.CheckDate);
        var selectedPeriodId = model.SelectedClassPeriodId > 0 ? model.SelectedClassPeriodId : 0;

        if (model.ClassroomId <= 0 || model.ClassPeriodId <= 0 || model.StudentId <= 0)
        {
            TempData["Error"] = "กรุณาเลือกข้อมูลรายงานให้ครบก่อนแก้ไขสถานะ";
            return RedirectToAction(nameof(Index), BuildIndexRouteValues(
                checkDate,
                model.CheckClassroomId,
                model.CheckClassPeriodId,
                date,
                model.ClassroomId,
                selectedPeriodId));
        }

        if (!canAccessAllClassrooms)
        {
            if (!user.AssignedClassroomId.HasValue || user.AssignedClassroomId.Value != model.ClassroomId)
            {
                return Forbid();
            }
        }

        var periodExists = await _dbContext.ClassPeriods
            .AsNoTracking()
            .Where(x => x.Id == model.ClassPeriodId && x.IsActive && x.IsVisibleForCheck)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(periodExists))
        {
            TempData["Error"] = "คาบเรียนที่ต้องการแก้ไขไม่พร้อมใช้งาน";
            return RedirectToAction(nameof(Index), BuildIndexRouteValues(
                checkDate,
                model.CheckClassroomId,
                model.CheckClassPeriodId,
                date,
                model.ClassroomId,
                selectedPeriodId));
        }

        var student = await _dbContext.Students
            .AsNoTracking()
            .Where(x => x.Id == model.StudentId && x.ClassroomId == model.ClassroomId && x.IsActive)
            .Select(x => new { x.Id, x.StudentCode, x.FullName })
            .FirstOrDefaultAsync(cancellationToken);

        if (student is null)
        {
            TempData["Error"] = "ไม่พบนักเรียนในห้องเรียนที่เลือก";
            return RedirectToAction(nameof(Index), BuildIndexRouteValues(
                checkDate,
                model.CheckClassroomId,
                model.CheckClassPeriodId,
                date,
                model.ClassroomId,
                selectedPeriodId));
        }

        var session = await _dbContext.PeriodAttendanceSessions
            .Include(x => x.Records)
            .FirstOrDefaultAsync(
                x => x.Date == date &&
                     x.ClassroomId == model.ClassroomId &&
                     x.ClassPeriodId == model.ClassPeriodId,
                cancellationToken);

        if (session is null)
        {
            TempData["Error"] = "คาบนี้ยังไม่มีการเช็ค จึงยังแก้ไขรายงานไม่ได้ กรุณาบันทึกการเช็คก่อน";
            return RedirectToAction(nameof(Index), BuildIndexRouteValues(
                checkDate,
                model.CheckClassroomId,
                model.CheckClassPeriodId,
                date,
                model.ClassroomId,
                selectedPeriodId));
        }

        var editReason = NormalizeText(model.EditReason);
        if (string.IsNullOrWhiteSpace(editReason))
        {
            TempData["Error"] = "กรุณาระบุเหตุผลในการแก้ไขรายคาบ";
            return RedirectToAction(nameof(Index), BuildIndexRouteValues(
                checkDate,
                model.CheckClassroomId,
                model.CheckClassPeriodId,
                date,
                model.ClassroomId,
                selectedPeriodId));
        }

        var record = session.Records.FirstOrDefault(x => x.StudentId == model.StudentId);
        var existingStatus = record?.Status;
        var existingRemark = NormalizeText(record?.Remark);
        var updatedRemark = NormalizeText(model.Remark);
        if (existingStatus == model.Status && string.Equals(existingRemark, updatedRemark, StringComparison.Ordinal))
        {
            TempData["Error"] = "ไม่พบการเปลี่ยนแปลงของรายการนี้ จึงยังไม่บันทึกการแก้ไข";
            return RedirectToAction(nameof(Index), BuildIndexRouteValues(
                checkDate,
                model.CheckClassroomId,
                model.CheckClassPeriodId,
                date,
                model.ClassroomId,
                selectedPeriodId));
        }

        if (record is null)
        {
            record = new PeriodAttendanceRecord
            {
                StudentId = model.StudentId,
                Status = model.Status,
                Remark = updatedRemark
            };
            session.Records.Add(record);
        }
        else
        {
            record.Status = model.Status;
            record.Remark = updatedRemark;
        }

        session.CheckedByUserId = user.Id;
        session.CheckedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditLogService.LogAsync(
            user.Id,
            "PeriodAttendanceQuickEdit",
            "PeriodAttendanceSession",
            BuildPeriodAttendanceEntityId(date, model.ClassroomId, model.ClassPeriodId),
            BuildQuickEditAuditDetail(
                editReason!,
                periodExists,
                student.StudentCode,
                student.FullName,
                existingStatus,
                model.Status,
                existingRemark,
                updatedRemark),
            GetRequestIpAddress(),
            cancellationToken);

        TempData["Success"] = $"อัปเดตสถานะของ {student.FullName} ใน {periodExists} เรียบร้อย";
        return RedirectToAction(nameof(Index), BuildIndexRouteValues(
            checkDate,
            model.CheckClassroomId,
            model.CheckClassPeriodId,
            date,
            model.ClassroomId,
            selectedPeriodId));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(PeriodAttendanceSaveViewModel model, CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var isAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");
        var canAccessAllClassrooms = isAdmin || User.IsInRole("Teacher");
        var date = NormalizeIncomingDate(model.Date);
        var reportDate = NormalizeIncomingDate(model.ReportDate ?? date);

        if (model.ClassroomId <= 0 || model.ClassPeriodId <= 0)
        {
            TempData["Error"] = "กรุณาเลือกห้องเรียนและคาบเรียน";
            return RedirectToAction(nameof(Index), BuildIndexRouteValues(
                date,
                model.ClassroomId,
                model.ClassPeriodId,
                reportDate,
                model.ReportClassroomId,
                model.ReportClassPeriodId));
        }

        var periodName = await _dbContext.ClassPeriods
            .AsNoTracking()
            .Where(x => x.Id == model.ClassPeriodId && x.IsActive && x.IsVisibleForCheck)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(periodName))
        {
            TempData["Error"] = "คาบเรียนที่เลือกไม่พร้อมใช้งาน";
            return RedirectToAction(nameof(Index), BuildIndexRouteValues(
                date,
                model.ClassroomId,
                model.ClassPeriodId,
                reportDate,
                model.ReportClassroomId,
                model.ReportClassPeriodId));
        }

        if (!canAccessAllClassrooms)
        {
            if (!user.AssignedClassroomId.HasValue || user.AssignedClassroomId.Value != model.ClassroomId)
            {
                return Forbid();
            }
        }

        var classroomStudents = await _dbContext.Students
            .AsNoTracking()
            .Where(x => x.ClassroomId == model.ClassroomId && x.IsActive)
            .OrderBy(x => x.StudentCode)
            .Select(x => new
            {
                x.Id,
                x.StudentCode,
                x.FullName
            })
            .ToListAsync(cancellationToken);

        if (classroomStudents.Count == 0)
        {
            TempData["Error"] = "ไม่พบนักเรียนในห้องเรียนนี้";
            return RedirectToAction(nameof(Index), BuildIndexRouteValues(
                date,
                model.ClassroomId,
                model.ClassPeriodId,
                reportDate,
                model.ReportClassroomId,
                model.ReportClassPeriodId));
        }

        var classroomStudentIds = classroomStudents.Select(x => x.Id).ToHashSet();
        var studentLabels = classroomStudents.ToDictionary(
            x => x.Id,
            x => string.IsNullOrWhiteSpace(x.StudentCode) ? x.FullName : $"{x.StudentCode} {x.FullName}");

        var posted = (model.Students ?? [])
            .Where(x => classroomStudentIds.Contains(x.StudentId))
            .GroupBy(x => x.StudentId)
            .ToDictionary(x => x.Key, x => x.Last());

        var session = await _dbContext.PeriodAttendanceSessions
            .Include(x => x.Records)
            .FirstOrDefaultAsync(
                x => x.Date == date &&
                     x.ClassroomId == model.ClassroomId &&
                     x.ClassPeriodId == model.ClassPeriodId,
                cancellationToken);

        var isEditingExistingSession = session is not null;
        var teacherStatusNote = NormalizeText(model.TeacherStatusNote);
        var editReason = NormalizeText(model.EditReason);
        var changeMessages = new List<string>();

        if (isEditingExistingSession)
        {
            if (string.IsNullOrWhiteSpace(editReason))
            {
                TempData["Error"] = "คาบนี้ถูกเช็คแล้ว หากต้องการแก้ไข กรุณาระบุเหตุผลทุกครั้ง";
                return RedirectToAction(nameof(Index), BuildIndexRouteValues(
                    date,
                    model.ClassroomId,
                    model.ClassPeriodId,
                    reportDate,
                    model.ReportClassroomId,
                    model.ReportClassPeriodId));
            }

            changeMessages = BuildSessionChangeMessages(
                session!,
                model.TeacherStatus,
                teacherStatusNote,
                classroomStudentIds,
                posted,
                studentLabels);

            if (changeMessages.Count == 0)
            {
                TempData["Error"] = "ไม่พบการเปลี่ยนแปลงของคาบนี้ จึงยังไม่บันทึกการแก้ไข";
                return RedirectToAction(nameof(Index), BuildIndexRouteValues(
                    date,
                    model.ClassroomId,
                    model.ClassPeriodId,
                    reportDate,
                    model.ReportClassroomId,
                    model.ReportClassPeriodId));
            }
        }

        if (session is null)
        {
            session = new PeriodAttendanceSession
            {
                Date = date,
                ClassroomId = model.ClassroomId,
                ClassPeriodId = model.ClassPeriodId,
                CheckedByUserId = user.Id
            };
            _dbContext.PeriodAttendanceSessions.Add(session);
        }

        session.TeacherStatus = model.TeacherStatus;
        session.TeacherStatusNote = teacherStatusNote;
        session.CheckedByUserId = user.Id;
        session.CheckedAt = DateTime.UtcNow;

        foreach (var studentId in classroomStudentIds)
        {
            var selected = posted.TryGetValue(studentId, out var value)
                ? value
                : new PeriodAttendanceSaveStudentViewModel { StudentId = studentId, Status = PeriodAttendanceStatus.Present };

            var existing = session.Records.FirstOrDefault(x => x.StudentId == studentId);
            if (existing is null)
            {
                session.Records.Add(new PeriodAttendanceRecord
                {
                    StudentId = studentId,
                    Status = selected.Status,
                    Remark = NormalizeText(selected.Remark)
                });
            }
            else
            {
                existing.Status = selected.Status;
                existing.Remark = NormalizeText(selected.Remark);
            }
        }

        var stale = session.Records
            .Where(x => !classroomStudentIds.Contains(x.StudentId))
            .ToList();

        if (stale.Count > 0)
        {
            _dbContext.PeriodAttendanceRecords.RemoveRange(stale);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (isEditingExistingSession)
        {
            await _auditLogService.LogAsync(
                user.Id,
                "PeriodAttendanceEdit",
                "PeriodAttendanceSession",
                BuildPeriodAttendanceEntityId(date, model.ClassroomId, model.ClassPeriodId),
                BuildBulkEditAuditDetail(editReason!, periodName!, changeMessages),
                GetRequestIpAddress(),
                cancellationToken);
        }

        TempData["Success"] = isEditingExistingSession
            ? "บันทึกการแก้ไขคาบเรียนพร้อมเหตุผลเรียบร้อย"
            : "บันทึกการเช็คเวลาเรียนรายคาบเรียบร้อย";
        return RedirectToAction(nameof(Index), BuildIndexRouteValues(
            date,
            model.ClassroomId,
            model.ClassPeriodId,
            reportDate,
            model.ReportClassroomId,
            model.ReportClassPeriodId));
    }

    private async Task<List<SelectOptionViewModel>> LoadClassroomsAsync(
        ApplicationUser user,
        bool canAccessAllClassrooms,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Classrooms
            .AsNoTracking()
            .Include(x => x.GradeLevel)
            .Where(x => x.IsActive);

        if (!canAccessAllClassrooms)
        {
            if (!user.AssignedClassroomId.HasValue)
            {
                return [];
            }

            query = query.Where(x => x.Id == user.AssignedClassroomId.Value);
        }

        return await query
            .OrderBy(x => x.GradeLevel!.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new SelectOptionViewModel
            {
                Value = x.Id,
                Text = $"{x.GradeLevel!.Name}/{x.Name}"
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<PeriodAttendancePageViewModel?> BuildPageModelAsync(
        PeriodAttendanceFilterViewModel filter,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return null;
        }

        var isAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");
        var canAccessAllClassrooms = isAdmin || User.IsInRole("Teacher");
        var isHomeroomRestricted = !canAccessAllClassrooms && User.IsInRole("HomeroomHead");
        var targetDate = NormalizeIncomingDate(filter.Date == default ? DateTime.Today : filter.Date);
        var reportDate = NormalizeIncomingDate(filter.ReportDate ?? targetDate);

        var classroomOptions = await LoadClassroomsAsync(user, canAccessAllClassrooms, cancellationToken);
        if (classroomOptions.Count > 0)
        {
            var classroomOptionIds = classroomOptions.Select(x => x.Value).ToHashSet();
            if (!filter.ClassroomId.HasValue || !classroomOptionIds.Contains(filter.ClassroomId.Value))
            {
                filter.ClassroomId = classroomOptions[0].Value;
            }

            if (!filter.ReportClassroomId.HasValue || !classroomOptionIds.Contains(filter.ReportClassroomId.Value))
            {
                filter.ReportClassroomId = filter.ClassroomId;
            }
        }

        var periods = await _dbContext.ClassPeriods
            .AsNoTracking()
            .Where(x => x.IsActive && x.IsVisibleForCheck)
            .OrderBy(x => x.SortOrder)
            .Select(x => new SelectOptionViewModel
            {
                Value = x.Id,
                Text = x.StartTime.HasValue
                    ? $"{x.Name} ({x.StartTime:hh\\:mm})"
                    : x.Name
            })
            .ToListAsync(cancellationToken);

        if (periods.Count > 0)
        {
            var periodOptionIds = periods.Select(x => x.Value).ToHashSet();
            if (!filter.ClassPeriodId.HasValue || !periodOptionIds.Contains(filter.ClassPeriodId.Value))
            {
                filter.ClassPeriodId = periods[0].Value;
            }

            if (!filter.ReportClassPeriodId.HasValue || filter.ReportClassPeriodId.Value < 0)
            {
                filter.ReportClassPeriodId = 0;
            }
            else if (filter.ReportClassPeriodId.Value > 0 && !periodOptionIds.Contains(filter.ReportClassPeriodId.Value))
            {
                filter.ReportClassPeriodId = filter.ClassPeriodId;
            }
        }

        var model = new PeriodAttendancePageViewModel
        {
            Filter = new PeriodAttendanceFilterViewModel
            {
                Date = targetDate,
                ClassroomId = filter.ClassroomId,
                ClassPeriodId = filter.ClassPeriodId
            },
            ReportFilter = new PeriodAttendanceFilterViewModel
            {
                Date = reportDate,
                ClassroomId = filter.ReportClassroomId,
                ClassPeriodId = filter.ReportClassPeriodId
            },
            ClassroomOptions = classroomOptions,
            PeriodOptions = periods,
            IsClassroomLocked = isHomeroomRestricted && user.AssignedClassroomId.HasValue,
            HasClassroomPermission = canAccessAllClassrooms || user.AssignedClassroomId.HasValue
        };

        if (!model.HasClassroomPermission || !filter.ClassroomId.HasValue || !filter.ClassPeriodId.HasValue)
        {
            return model;
        }

        var students = await _dbContext.Students
            .AsNoTracking()
            .Include(x => x.GradeLevel)
            .Include(x => x.Classroom)
            .Include(x => x.StudentPhotos)
            .Where(x => x.ClassroomId == filter.ClassroomId.Value && x.IsActive)
            .OrderBy(x => x.StudentCode)
            .ToListAsync(cancellationToken);

        var session = await _dbContext.PeriodAttendanceSessions
            .AsNoTracking()
            .Include(x => x.Records)
            .Include(x => x.CheckedByUser)
            .FirstOrDefaultAsync(
                x => x.Date == targetDate &&
                     x.ClassroomId == filter.ClassroomId.Value &&
                     x.ClassPeriodId == filter.ClassPeriodId.Value,
                cancellationToken);

        model.HasSavedData = session is not null;
        model.TeacherStatus = session?.TeacherStatus ?? TeacherTeachingStatus.Normal;
        model.TeacherStatusNote = session?.TeacherStatusNote;
        model.LastUpdatedAt = session?.CheckedAt;
        model.LastUpdatedByName = session?.CheckedByUser?.FullName;
        model.EditHistory = session is null
            ? []
            : await LoadEditHistoryAsync(targetDate, filter.ClassroomId.Value, filter.ClassPeriodId.Value, cancellationToken);

        if (!model.ReportFilter.ClassroomId.HasValue)
        {
            return model;
        }

        var reportStudents = await _dbContext.Students
            .AsNoTracking()
            .Where(x => x.ClassroomId == model.ReportFilter.ClassroomId.Value && x.IsActive)
            .OrderBy(x => x.StudentCode)
            .Select(x => new
            {
                x.Id,
                x.StudentCode,
                x.FullName
            })
            .ToListAsync(cancellationToken);

        var dailySessions = await _dbContext.PeriodAttendanceSessions
            .AsNoTracking()
            .Include(x => x.Records)
            .Where(x => x.Date == model.ReportFilter.Date && x.ClassroomId == model.ReportFilter.ClassroomId.Value)
            .ToListAsync(cancellationToken);

        var reportByPeriod = dailySessions.ToDictionary(x => x.ClassPeriodId, x => x);

        var existingStatus = session?.Records.ToDictionary(x => x.StudentId, x => x);
        model.Students = students.Select(x =>
        {
            PeriodAttendanceRecord? attendance = null;
            if (existingStatus is not null)
            {
                existingStatus.TryGetValue(x.Id, out attendance);
            }

            var primaryPhoto = x.StudentPhotos
                .OrderByDescending(p => p.IsPrimary)
                .ThenByDescending(p => p.CapturedAt)
                .FirstOrDefault();

            return new PeriodAttendanceStudentItemViewModel
            {
                StudentId = x.Id,
                StudentCode = x.StudentCode,
                StudentName = x.FullName,
                GradeLevel = x.GradeLevel?.Name ?? "-",
                Classroom = x.Classroom?.Name ?? "-",
                PhotoPath = primaryPhoto?.FilePath,
                Status = attendance?.Status ?? PeriodAttendanceStatus.Present,
                Remark = attendance?.Remark
            };
        }).ToList();

        var highlightedReportPeriodId = model.ReportFilter.ClassPeriodId.GetValueOrDefault();

        model.ReportRows = reportStudents.Select(student => new PeriodAttendanceReportRowViewModel
        {
            StudentId = student.Id,
            StudentCode = student.StudentCode,
            StudentName = student.FullName,
            Periods = periods.Select(period =>
            {
                PeriodAttendanceStatus? reportStatus = null;
                PeriodAttendanceRecord? periodRecord = null;
                var hasSession = reportByPeriod.TryGetValue(period.Value, out var periodSession);
                if (hasSession)
                {
                    periodRecord = periodSession!.Records
                        .FirstOrDefault(record => record.StudentId == student.Id);
                    reportStatus = periodRecord?.Status;
                }

                return new PeriodAttendanceReportCellViewModel
                {
                    ClassPeriodId = period.Value,
                    ClassPeriodName = period.Text,
                    Status = reportStatus,
                    IsCurrentPeriod = highlightedReportPeriodId > 0 && highlightedReportPeriodId == period.Value,
                    CanEdit = hasSession,
                    Remark = periodRecord?.Remark,
                    HasRecordedStatus = periodRecord is not null
                };
            }).ToList()
        }).ToList();

        return model;
    }

    private static object BuildIndexRouteValues(
        DateTime checkDate,
        int? checkClassroomId,
        int? checkClassPeriodId,
        DateTime reportDate,
        int? reportClassroomId,
        int? reportClassPeriodId) => new
        {
            Date = NormalizeIncomingDate(checkDate).ToString("yyyy-MM-dd"),
            ClassroomId = checkClassroomId,
            ClassPeriodId = checkClassPeriodId,
            ReportDate = NormalizeIncomingDate(reportDate).ToString("yyyy-MM-dd"),
            ReportClassroomId = reportClassroomId,
            ReportClassPeriodId = reportClassPeriodId
        };

    private static DateTime NormalizeIncomingDate(DateTime value)
    {
        var date = value.Date;
        return date.Year >= 2400 ? date.AddYears(-543) : date;
    }

    private async Task<IReadOnlyList<PeriodAttendanceEditHistoryItemViewModel>> LoadEditHistoryAsync(
        DateTime date,
        int classroomId,
        int classPeriodId,
        CancellationToken cancellationToken)
    {
        var entityId = BuildPeriodAttendanceEntityId(date, classroomId, classPeriodId);
        var logs = await _dbContext.AuditLogs
            .AsNoTracking()
            .Include(x => x.User)
            .Where(x => x.EntityName == "PeriodAttendanceSession" &&
                        x.EntityId == entityId &&
                        (x.Action == "PeriodAttendanceEdit" || x.Action == "PeriodAttendanceQuickEdit"))
            .OrderByDescending(x => x.LoggedAt)
            .ToListAsync(cancellationToken);

        return logs.Select(x => new PeriodAttendanceEditHistoryItemViewModel
        {
            LoggedAt = x.LoggedAt,
            ActionLabel = x.Action switch
            {
                "PeriodAttendanceEdit" => "แก้ไขทั้งคาบ",
                "PeriodAttendanceQuickEdit" => "แก้ไขรายคาบ",
                _ => x.Action
            },
            UserName = string.IsNullOrWhiteSpace(x.User?.FullName) ? "ระบบ" : x.User!.FullName,
            Detail = x.Detail
        }).ToList();
    }

    private static List<string> BuildSessionChangeMessages(
        PeriodAttendanceSession session,
        TeacherTeachingStatus teacherStatus,
        string? teacherStatusNote,
        IReadOnlyCollection<int> classroomStudentIds,
        IReadOnlyDictionary<int, PeriodAttendanceSaveStudentViewModel> posted,
        IReadOnlyDictionary<int, string> studentLabels)
    {
        var changeMessages = new List<string>();
        var previousTeacherStatusNote = NormalizeText(session.TeacherStatusNote);

        if (session.TeacherStatus != teacherStatus || !string.Equals(previousTeacherStatusNote, teacherStatusNote, StringComparison.Ordinal))
        {
            changeMessages.Add(
                $"สถานะครูผู้สอน: {session.TeacherStatus.GetDisplayName()} -> {teacherStatus.GetDisplayName()} | หมายเหตุ: {previousTeacherStatusNote ?? "-"} -> {teacherStatusNote ?? "-"}");
        }

        var existingRecords = session.Records.ToDictionary(x => x.StudentId, x => x);
        foreach (var studentId in classroomStudentIds)
        {
            var selected = posted.TryGetValue(studentId, out var value)
                ? value
                : new PeriodAttendanceSaveStudentViewModel
                {
                    StudentId = studentId,
                    Status = PeriodAttendanceStatus.Present
                };

            existingRecords.TryGetValue(studentId, out var existingRecord);
            var previousStatus = existingRecord?.Status ?? PeriodAttendanceStatus.Present;
            var previousRemark = NormalizeText(existingRecord?.Remark);
            var updatedRemark = NormalizeText(selected.Remark);
            var statusChanged = previousStatus != selected.Status;
            var remarkChanged = !string.Equals(previousRemark, updatedRemark, StringComparison.Ordinal);

            if (!statusChanged && !remarkChanged)
            {
                continue;
            }

            var studentLabel = studentLabels.TryGetValue(studentId, out var label)
                ? label
                : $"นักเรียน #{studentId}";

            if (statusChanged && remarkChanged)
            {
                changeMessages.Add(
                    $"{studentLabel}: {previousStatus.GetDisplayName()} -> {selected.Status.GetDisplayName()} | หมายเหตุ: {previousRemark ?? "-"} -> {updatedRemark ?? "-"}");
                continue;
            }

            if (statusChanged)
            {
                changeMessages.Add($"{studentLabel}: {previousStatus.GetDisplayName()} -> {selected.Status.GetDisplayName()}");
                continue;
            }

            changeMessages.Add($"{studentLabel}: หมายเหตุ {previousRemark ?? "-"} -> {updatedRemark ?? "-"}");
        }

        var staleRecords = session.Records
            .Where(x => !classroomStudentIds.Contains(x.StudentId))
            .Select(x => x.StudentId)
            .ToList();

        if (staleRecords.Count > 0)
        {
            changeMessages.Add($"ลบรายการนักเรียนที่ไม่อยู่ในห้องปัจจุบัน {staleRecords.Count} รายการ");
        }

        return changeMessages;
    }

    private static string BuildBulkEditAuditDetail(string editReason, string periodName, IReadOnlyList<string> changeMessages)
    {
        var lines = new List<string>
        {
            $"เหตุผล: {editReason}",
            $"คาบเรียน: {periodName}",
            $"จำนวนรายการที่แก้ไข: {changeMessages.Count}"
        };
        lines.AddRange(changeMessages);

        return BuildAuditDetail(lines);
    }

    private static string BuildQuickEditAuditDetail(
        string editReason,
        string periodName,
        string? studentCode,
        string studentName,
        PeriodAttendanceStatus? existingStatus,
        PeriodAttendanceStatus updatedStatus,
        string? existingRemark,
        string? updatedRemark)
    {
        var studentLabel = string.IsNullOrWhiteSpace(studentCode)
            ? studentName
            : $"{studentCode} {studentName}";

        return BuildAuditDetail(
        [
            $"เหตุผล: {editReason}",
            $"คาบเรียน: {periodName}",
            $"นักเรียน: {studentLabel}",
            $"สถานะ: {(existingStatus?.GetDisplayName() ?? "ยังไม่มีข้อมูล")} -> {updatedStatus.GetDisplayName()}",
            $"หมายเหตุ: {existingRemark ?? "-"} -> {updatedRemark ?? "-"}"
        ]);
    }

    private static string BuildPeriodAttendanceEntityId(DateTime date, int classroomId, int classPeriodId)
        => $"{NormalizeIncomingDate(date):yyyyMMdd}:{classroomId}:{classPeriodId}";

    private static string? NormalizeText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private string? GetRequestIpAddress()
    {
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private static string BuildAuditDetail(IReadOnlyList<string> lines)
    {
        const int maxLength = 4000;
        var builder = new StringBuilder();

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var separator = builder.Length == 0 ? string.Empty : Environment.NewLine;
            if (builder.Length + separator.Length + line.Length <= maxLength)
            {
                builder.Append(separator).Append(line);
                continue;
            }

            var remaining = lines.Count - index;
            var overflowMessage = $"... และอีก {remaining} รายการ";
            if (builder.Length + separator.Length + overflowMessage.Length <= maxLength)
            {
                builder.Append(separator).Append(overflowMessage);
            }

            break;
        }

        return builder.ToString();
    }

    private static string BuildPdfFileName(DateTime date, string classroomName)
    {
        var sanitized = string.Concat(classroomName
            .Replace('/', '-')
            .Replace('\\', '-')
            .Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)));

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "classroom";
        }

        return $"period-attendance-{sanitized}-{date:yyyyMMdd}.pdf";
    }
}
