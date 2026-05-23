using FaceScan.Web.Data;
using FaceScan.Web.Helpers;
using FaceScan.Web.Models.Enums;
using FaceScan.Web.Services.Interfaces;
using FaceScan.Web.ViewModels;
using FaceScan.Web.ViewModels.Executive;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace FaceScan.Web.Areas.Executive.Controllers;

[Area("Executive")]
[Authorize(Roles = "SuperAdmin,Admin,Executive")]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAttendanceService _attendanceService;

    public ReportsController(ApplicationDbContext dbContext, IAttendanceService attendanceService)
    {
        _dbContext = dbContext;
        _attendanceService = attendanceService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] ExecutiveReportFilterViewModel filter, CancellationToken cancellationToken)
    {
        var model = await BuildViewModelAsync(filter, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Print([FromQuery] ExecutiveReportFilterViewModel filter, CancellationToken cancellationToken)
    {
        var model = await BuildViewModelAsync(filter, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> LiveMonitor([FromQuery] ExecutiveReportFilterViewModel filter, CancellationToken cancellationToken)
    {
        var model = await BuildViewModelAsync(filter, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> LiveSummary([FromQuery] ExecutiveReportFilterViewModel filter, CancellationToken cancellationToken)
    {
        var model = await BuildViewModelAsync(filter, cancellationToken);

        return Json(new
        {
            totalStudents = model.TotalStudents,
            registeredFaceCount = model.RegisteredFaceCount,
            presentCount = model.PresentCount,
            absentCount = model.AbsentCount,
            lateCount = model.LateCount,
            truancyAndOtherCount = model.TruancyCount + model.OtherCount,
            updatedAt = DateTime.Now
        });
    }

    [HttpGet]
    public async Task<IActionResult> ExportGradeCsv([FromQuery] ExecutiveReportFilterViewModel filter, CancellationToken cancellationToken)
    {
        var model = await BuildViewModelAsync(filter, cancellationToken);
        var records = model.GradeSummaries.Select(x => new ExecutiveGroupCsvRow
        {
            GroupName = x.Name,
            SecondaryLabel = x.SecondaryLabel,
            StudentCount = x.StudentCount,
            ActiveAttendanceCount = x.ActiveAttendanceCount,
            InactiveAttendanceCount = x.InactiveAttendanceCount,
            AttendanceRate = x.AttendanceRate,
            RegisteredFaceCount = x.RegisteredFaceCount,
            FaceRegistrationRate = x.FaceRegistrationRate,
            LateCount = x.LateCount,
            LeaveCount = x.LeaveCount,
            TruancyCount = x.TruancyCount,
            OtherCount = x.OtherCount,
            RiskLevel = GetRiskLevel(x.AttendanceRate),
            FirstActivityTime = x.FirstActivityTime,
            LastActivityTime = x.LastActivityTime
        });

        var fileName = BuildCsvFileName("grade-summary", model.Filter.DateFrom, model.Filter.DateTo);
        var bytes = CsvExportHelper.Export(records);
        return File(bytes, "text/csv", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> ExportClassroomCsv([FromQuery] ExecutiveReportFilterViewModel filter, CancellationToken cancellationToken)
    {
        var model = await BuildViewModelAsync(filter, cancellationToken);
        var records = model.ClassroomSummaries.Select(x => new ExecutiveGroupCsvRow
        {
            GroupName = x.Name,
            SecondaryLabel = x.SecondaryLabel,
            StudentCount = x.StudentCount,
            ActiveAttendanceCount = x.ActiveAttendanceCount,
            InactiveAttendanceCount = x.InactiveAttendanceCount,
            AttendanceRate = x.AttendanceRate,
            RegisteredFaceCount = x.RegisteredFaceCount,
            FaceRegistrationRate = x.FaceRegistrationRate,
            LateCount = x.LateCount,
            LeaveCount = x.LeaveCount,
            TruancyCount = x.TruancyCount,
            OtherCount = x.OtherCount,
            RiskLevel = GetRiskLevel(x.AttendanceRate),
            FirstActivityTime = x.FirstActivityTime,
            LastActivityTime = x.LastActivityTime
        });

        var fileName = BuildCsvFileName("classroom-summary", model.Filter.DateFrom, model.Filter.DateTo);
        var bytes = CsvExportHelper.Export(records);
        return File(bytes, "text/csv", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> ExportStudentCsv([FromQuery] ExecutiveReportFilterViewModel filter, CancellationToken cancellationToken)
    {
        var model = await BuildViewModelAsync(filter, cancellationToken);
        var records = model.Rows.Select(x => new ExecutiveStudentCsvRow
        {
            StudentCode = x.StudentCode,
            StudentName = x.StudentName,
            GradeLevel = x.GradeLevel,
            Classroom = x.Classroom,
            FirstCheckInTime = x.FirstCheckInTime,
            LastCheckOutTime = x.LastCheckOutTime,
            PresentPeriods = x.PresentPeriods,
            LeavePeriods = x.LeavePeriods,
            AbsentPeriods = x.AbsentPeriods,
            TruancyPeriods = x.TruancyPeriods,
            LatePeriods = x.LatePeriods,
            OtherPeriods = x.OtherPeriods,
            TotalTrackedPeriods = x.PresentPeriods + x.LeavePeriods + x.AbsentPeriods + x.TruancyPeriods + x.LatePeriods + x.OtherPeriods,
            PresentRate = CalculatePresentRate(x),
            RiskLevel = GetRiskLevel(CalculatePresentRate(x))
        });

        var fileName = BuildCsvFileName("student-detail", model.Filter.DateFrom, model.Filter.DateTo);
        var bytes = CsvExportHelper.Export(records);
        return File(bytes, "text/csv", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> ExportRiskTrendCsv([FromQuery] ExecutiveReportFilterViewModel filter, CancellationToken cancellationToken)
    {
        var model = await BuildViewModelAsync(filter, cancellationToken);
        var records = model.RiskTrendPoints.Select(x => new ExecutiveRiskTrendCsvRow
        {
            Date = x.Date,
            PresentCount = x.PresentCount,
            AbsentCount = x.AbsentCount,
            AttendanceRate = x.AttendanceRate,
            RiskLevel = x.RiskLevel
        });

        var fileName = BuildCsvFileName("risk-trend", model.Filter.DateFrom, model.Filter.DateTo);
        var bytes = CsvExportHelper.Export(records);
        return File(bytes, "text/csv", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> ExportRiskStudentCsv([FromQuery] ExecutiveReportFilterViewModel filter, CancellationToken cancellationToken)
    {
        var model = await BuildViewModelAsync(filter, cancellationToken);
        var records = model.TopRiskStudents.Select(x => new ExecutiveRiskStudentCsvRow
        {
            StudentCode = x.StudentCode,
            StudentName = x.StudentName,
            GradeLevel = x.GradeLevel,
            Classroom = x.Classroom,
            PresentRate = x.PresentRate,
            AbsentPeriods = x.AbsentPeriods,
            TruancyPeriods = x.TruancyPeriods,
            LatePeriods = x.LatePeriods,
            TotalTrackedPeriods = x.TotalTrackedPeriods,
            RiskScore = x.RiskScore,
            RiskLevel = x.RiskLevel,
            RiskReason = x.RiskReason
        });

        var fileName = BuildCsvFileName("risk-students", model.Filter.DateFrom, model.Filter.DateTo);
        var bytes = CsvExportHelper.Export(records);
        return File(bytes, "text/csv", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> ExportTopRiskGradeCsv([FromQuery] ExecutiveReportFilterViewModel filter, CancellationToken cancellationToken)
    {
        var model = await BuildViewModelAsync(filter, cancellationToken);
        var records = model.TopRiskGrades.Select(x => new ExecutiveGroupCsvRow
        {
            GroupName = x.Name,
            SecondaryLabel = x.SecondaryLabel,
            StudentCount = x.StudentCount,
            ActiveAttendanceCount = x.ActiveAttendanceCount,
            InactiveAttendanceCount = x.InactiveAttendanceCount,
            AttendanceRate = x.AttendanceRate,
            RegisteredFaceCount = x.RegisteredFaceCount,
            FaceRegistrationRate = x.FaceRegistrationRate,
            LateCount = x.LateCount,
            LeaveCount = x.LeaveCount,
            TruancyCount = x.TruancyCount,
            OtherCount = x.OtherCount,
            RiskLevel = GetRiskLevel(x.AttendanceRate),
            FirstActivityTime = x.FirstActivityTime,
            LastActivityTime = x.LastActivityTime
        });

        var fileName = BuildCsvFileName("top-risk-grades", model.Filter.DateFrom, model.Filter.DateTo);
        var bytes = CsvExportHelper.Export(records);
        return File(bytes, "text/csv", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> ExportTopRiskClassroomCsv([FromQuery] ExecutiveReportFilterViewModel filter, CancellationToken cancellationToken)
    {
        var model = await BuildViewModelAsync(filter, cancellationToken);
        var records = model.TopRiskClassrooms.Select(x => new ExecutiveGroupCsvRow
        {
            GroupName = x.Name,
            SecondaryLabel = x.SecondaryLabel,
            StudentCount = x.StudentCount,
            ActiveAttendanceCount = x.ActiveAttendanceCount,
            InactiveAttendanceCount = x.InactiveAttendanceCount,
            AttendanceRate = x.AttendanceRate,
            RegisteredFaceCount = x.RegisteredFaceCount,
            FaceRegistrationRate = x.FaceRegistrationRate,
            LateCount = x.LateCount,
            LeaveCount = x.LeaveCount,
            TruancyCount = x.TruancyCount,
            OtherCount = x.OtherCount,
            RiskLevel = GetRiskLevel(x.AttendanceRate),
            FirstActivityTime = x.FirstActivityTime,
            LastActivityTime = x.LastActivityTime
        });

        var fileName = BuildCsvFileName("top-risk-classrooms", model.Filter.DateFrom, model.Filter.DateTo);
        var bytes = CsvExportHelper.Export(records);
        return File(bytes, "text/csv", fileName);
    }

    private async Task<ExecutiveDashboardViewModel> BuildViewModelAsync(
        ExecutiveReportFilterViewModel filter,
        CancellationToken cancellationToken)
    {
        var from = filter.DateFrom == default ? DateTime.Today : filter.DateFrom.Date;
        var to = filter.DateTo == default ? DateTime.Today : filter.DateTo.Date;

        if (from > to)
        {
            (from, to) = (to, from);
        }

        if ((to - from).TotalDays > 31)
        {
            to = from.AddDays(31);
        }

        filter.DateFrom = from;
        filter.DateTo = to;

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            await _attendanceService.BuildDailySummaryAsync(date, cancellationToken);
        }

        var grades = await _dbContext.GradeLevels
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
            .Include(x => x.StudentPhotos)
            .Where(x => x.IsActive);

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
                ((x.Prefix ?? string.Empty) + x.FirstName + " " + x.LastName).Contains(keyword));
        }

        var students = await studentsQuery
            .OrderBy(x => x.StudentCode)
            .ToListAsync(cancellationToken);

        var studentIds = students.Select(x => x.Id).ToList();
        if (studentIds.Count == 0)
        {
            return new ExecutiveDashboardViewModel
            {
                Filter = filter,
                GradeLevels = grades,
                Classrooms = classrooms
            };
        }

        var dailySummaries = await _dbContext.AttendanceDailySummaries
            .AsNoTracking()
            .Where(x => studentIds.Contains(x.StudentId) && x.Date >= from && x.Date <= to)
            .ToListAsync(cancellationToken);

        var periodRecords = await _dbContext.PeriodAttendanceRecords
            .AsNoTracking()
            .Include(x => x.PeriodAttendanceSession)
            .Where(x =>
                studentIds.Contains(x.StudentId) &&
                x.PeriodAttendanceSession != null &&
                x.PeriodAttendanceSession.Date >= from &&
                x.PeriodAttendanceSession.Date <= to)
            .ToListAsync(cancellationToken);

        var dailyByStudent = dailySummaries
            .GroupBy(x => x.StudentId)
            .ToDictionary(x => x.Key, x => x.ToList());

        var periodByStudent = periodRecords
            .GroupBy(x => x.StudentId)
            .ToDictionary(x => x.Key, x => x.ToList());

        var rows = students.Select(student =>
        {
            dailyByStudent.TryGetValue(student.Id, out var studentDaily);
            periodByStudent.TryGetValue(student.Id, out var studentPeriods);

            var firstCheckIn = studentDaily?
                .Where(x => x.FirstCheckInTime.HasValue)
                .MinBy(x => x.FirstCheckInTime) // earliest in range
                ?.FirstCheckInTime;

            var lastCheckOut = studentDaily?
                .Where(x => x.LastCheckOutTime.HasValue)
                .MaxBy(x => x.LastCheckOutTime) // latest in range
                ?.LastCheckOutTime;

            var primaryPhoto = student.StudentPhotos
                .OrderByDescending(x => x.IsPrimary)
                .ThenByDescending(x => x.CapturedAt)
                .FirstOrDefault();

            return new ExecutiveStudentReportRowViewModel
            {
                StudentId = student.Id,
                StudentCode = student.StudentCode,
                StudentName = student.FullName,
                GradeLevel = student.GradeLevel?.Name ?? "-",
                Classroom = student.Classroom?.Name ?? "-",
                PhotoPath = primaryPhoto?.FilePath,
                FirstCheckInTime = firstCheckIn,
                LastCheckOutTime = lastCheckOut,
                PresentPeriods = studentPeriods?.Count(x => x.Status == PeriodAttendanceStatus.Present) ?? 0,
                AbsentPeriods = studentPeriods?.Count(x => x.Status == PeriodAttendanceStatus.Absent) ?? 0,
                LeavePeriods = studentPeriods?.Count(x => x.Status == PeriodAttendanceStatus.Leave) ?? 0,
                LatePeriods = studentPeriods?.Count(x => x.Status == PeriodAttendanceStatus.Late) ?? 0,
                TruancyPeriods = studentPeriods?.Count(x => x.Status == PeriodAttendanceStatus.Truancy) ?? 0,
                OtherPeriods = studentPeriods?.Count(x => x.Status == PeriodAttendanceStatus.Other) ?? 0
            };
        }).ToList();

        var registeredFaceCount = await _dbContext.FaceProfiles
            .AsNoTracking()
            .CountAsync(
                x => studentIds.Contains(x.StudentId) &&
                     (x.EnrollmentStatus == EnrollmentStatus.Pending || x.EnrollmentStatus == EnrollmentStatus.Ready),
                cancellationToken);

        var registeredFaceStudentIds = await _dbContext.FaceProfiles
            .AsNoTracking()
            .Where(x => studentIds.Contains(x.StudentId) &&
                        (x.EnrollmentStatus == EnrollmentStatus.Pending || x.EnrollmentStatus == EnrollmentStatus.Ready))
            .Select(x => x.StudentId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var registeredFaceSet = registeredFaceStudentIds.ToHashSet();

        var studentReportItems = students.Select(student =>
        {
            var row = rows.First(x => x.StudentId == student.Id);
            return new StudentReportAggregateItem(
                student.Id,
                student.GradeLevelId,
                student.GradeLevel?.Name ?? "-",
                student.ClassroomId,
                student.Classroom?.Name ?? "-",
                row,
                registeredFaceSet.Contains(student.Id));
        }).ToList();

        var gradeSummaries = studentReportItems
            .GroupBy(x => new { x.GradeLevelId, x.GradeLevel })
            .OrderBy(x => ParseSortToken(x.Key.GradeLevel))
            .ThenBy(x => x.Key.GradeLevel, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => BuildGroupSummary(
                group.Key.GradeLevel,
                $"{group.Select(x => x.Classroom).Distinct().Count()} ห้อง",
                group,
                group.Key.GradeLevelId,
                null))
            .ToList();

        var riskOrderedGrades = gradeSummaries
            .OrderBy(x => GetRiskRank(x.AttendanceRate))
            .ThenBy(x => x.AttendanceRate)
            .ThenByDescending(x => x.InactiveAttendanceCount)
            .ThenBy(x => ParseSortToken(x.Name))
            .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var topRiskGrades = riskOrderedGrades
            .Where(x => x.StudentCount > 0)
            .Take(5)
            .ToList();

        var classroomSummaries = studentReportItems
            .GroupBy(x => new { x.GradeLevelId, x.GradeLevel, x.ClassroomId, x.Classroom })
            .OrderBy(x => ParseSortToken(x.Key.GradeLevel))
            .ThenBy(x => x.Key.GradeLevel, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.Key.Classroom, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => BuildGroupSummary(
                group.Key.Classroom,
                group.Key.GradeLevel,
                group,
                group.Key.GradeLevelId,
                group.Key.ClassroomId))
            .ToList();

        var riskOrderedClassrooms = classroomSummaries
            .OrderBy(x => GetRiskRank(x.AttendanceRate))
            .ThenBy(x => x.AttendanceRate)
            .ThenByDescending(x => x.InactiveAttendanceCount)
            .ThenBy(x => ParseSortToken(x.SecondaryLabel))
            .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var topRiskClassrooms = riskOrderedClassrooms
            .Where(x => x.StudentCount > 0)
            .Take(5)
            .ToList();

        var topRiskStudents = rows
            .Select(BuildRiskStudent)
            .Where(x => x.TotalTrackedPeriods > 0)
            .OrderByDescending(x => x.RiskScore)
            .ThenByDescending(x => x.TruancyPeriods)
            .ThenByDescending(x => x.AbsentPeriods)
            .ThenBy(x => x.StudentCode)
            .Take(5)
            .ToList();

        var dateBuckets = Enumerable
            .Range(0, (to - from).Days + 1)
            .Select(offset => from.AddDays(offset).Date)
            .ToList();

        var dailySummariesByDate = dailySummaries
            .GroupBy(x => x.Date)
            .ToDictionary(x => x.Key, x => x.ToList());

        var periodByDate = periodRecords
            .GroupBy(x => x.PeriodAttendanceSession!.Date)
            .ToDictionary(x => x.Key, x => x.ToList());

        var daily = new List<ExecutiveDailySummaryViewModel>();
        var riskTrendPoints = new List<ExecutiveRiskTrendPointViewModel>();
        foreach (var date in dateBuckets)
        {
            dailySummariesByDate.TryGetValue(date, out var daySummaries);
            periodByDate.TryGetValue(date, out var dayPeriods);

            var present = daySummaries?.Count(x => x.IsPresent) ?? 0;
            var absent = Math.Max(students.Count - present, 0);

            daily.Add(new ExecutiveDailySummaryViewModel
            {
                Date = date,
                PresentCount = present,
                AbsentCount = absent,
                LeaveCount = dayPeriods?.Count(x => x.Status == PeriodAttendanceStatus.Leave) ?? 0,
                LateCount = dayPeriods?.Count(x => x.Status == PeriodAttendanceStatus.Late) ?? 0,
                TruancyCount = dayPeriods?.Count(x => x.Status == PeriodAttendanceStatus.Truancy) ?? 0,
                OtherCount = dayPeriods?.Count(x => x.Status == PeriodAttendanceStatus.Other) ?? 0
            });

            var attendanceRate = students.Count == 0
                ? 0m
                : Math.Round((decimal)present / students.Count * 100m, 1);

            riskTrendPoints.Add(new ExecutiveRiskTrendPointViewModel
            {
                Date = date,
                PresentCount = present,
                AbsentCount = absent,
                AttendanceRate = attendanceRate,
                RiskLevel = GetRiskLevel(attendanceRate)
            });
        }

        return new ExecutiveDashboardViewModel
        {
            Filter = filter,
            GradeLevels = grades,
            Classrooms = classrooms,
            Rows = rows,
            DailySummaries = daily,
            GradeSummaries = riskOrderedGrades,
            ClassroomSummaries = riskOrderedClassrooms,
            TopRiskGrades = topRiskGrades,
            TopRiskClassrooms = topRiskClassrooms,
            TopRiskStudents = topRiskStudents,
            RiskTrendPoints = riskTrendPoints,
            Insights = BuildInsights(filter, students.Count, registeredFaceCount, rows),
            TotalStudents = students.Count,
            RegisteredFaceCount = registeredFaceCount,
            PresentCount = rows.Count(x => x.FirstCheckInTime.HasValue),
            AbsentCount = rows.Count(x => !x.FirstCheckInTime.HasValue),
            LeaveCount = rows.Sum(x => x.LeavePeriods),
            LateCount = rows.Sum(x => x.LatePeriods),
            TruancyCount = rows.Sum(x => x.TruancyPeriods),
            OtherCount = rows.Sum(x => x.OtherPeriods)
        };
    }

    private static List<ExecutiveAttendanceInsightViewModel> BuildInsights(
        ExecutiveReportFilterViewModel filter,
        int totalStudents,
        int registeredFaceCount,
        IReadOnlyCollection<ExecutiveStudentReportRowViewModel> rows)
    {
        var activeAttendanceCount = rows.Count(x => x.FirstCheckInTime.HasValue);
        var attendanceRate = totalStudents == 0
            ? 0m
            : Math.Round((decimal)activeAttendanceCount / totalStudents * 100m, 1);
        var faceRegistrationRate = totalStudents == 0
            ? 0m
            : Math.Round((decimal)registeredFaceCount / totalStudents * 100m, 1);
        var rangeDays = Math.Max(1, (filter.DateTo.Date - filter.DateFrom.Date).Days + 1);
        var lateHotspot = rows
            .OrderByDescending(x => x.LatePeriods)
            .ThenBy(x => x.StudentCode)
            .FirstOrDefault(x => x.LatePeriods > 0);

        return
        [
            new ExecutiveAttendanceInsightViewModel
            {
                Title = "ช่วงวันที่รายงาน",
                Value = $"{rangeDays.ToString("N0", CultureInfo.InvariantCulture)} วัน",
                Caption = $"จาก {filter.DateFrom:dd/MM/yyyy} ถึง {filter.DateTo:dd/MM/yyyy}",
                Tone = "neutral"
            },
            new ExecutiveAttendanceInsightViewModel
            {
                Title = "อัตรามาเรียนในช่วงที่เลือก",
                Value = $"{attendanceRate:0.0}%",
                Caption = $"พบการเข้าเรียน {activeAttendanceCount:N0} จาก {totalStudents:N0} คน",
                Tone = attendanceRate >= 85m ? "good" : attendanceRate >= 70m ? "warn" : "alert"
            },
            new ExecutiveAttendanceInsightViewModel
            {
                Title = "ความพร้อมข้อมูลใบหน้า",
                Value = $"{faceRegistrationRate:0.0}%",
                Caption = $"ลงทะเบียนใบหน้าแล้ว {registeredFaceCount:N0} คน",
                Tone = faceRegistrationRate >= 90m ? "good" : faceRegistrationRate >= 75m ? "warn" : "alert"
            },
            new ExecutiveAttendanceInsightViewModel
            {
                Title = "นักเรียนมาสายสูงสุด",
                Value = lateHotspot is null ? "ไม่มี" : lateHotspot.StudentName,
                Caption = lateHotspot is null ? "ยังไม่พบความเสี่ยงมาสายจากข้อมูลช่วงนี้" : $"มาสาย {lateHotspot.LatePeriods:N0} ครั้งในช่วงที่เลือก",
                Tone = lateHotspot is null ? "neutral" : "warn"
            }
        ];
    }

    private static ExecutiveAttendanceGroupSummaryViewModel BuildGroupSummary<TKey>(
        string name,
        string secondaryLabel,
        IGrouping<TKey, StudentReportAggregateItem> group,
        int? gradeLevelId,
        int? classroomId)
    {
        var items = group.ToList();
        var activeAttendanceCount = items.Count(x => x.Row.FirstCheckInTime.HasValue);
        var studentCount = items.Count;
        var registeredFaceCount = items.Count(x => x.HasRegisteredFace);

        return new ExecutiveAttendanceGroupSummaryViewModel
        {
            GradeLevelId = gradeLevelId,
            ClassroomId = classroomId,
            Name = name,
            SecondaryLabel = secondaryLabel,
            StudentCount = studentCount,
            RegisteredFaceCount = registeredFaceCount,
            ActiveAttendanceCount = activeAttendanceCount,
            InactiveAttendanceCount = Math.Max(studentCount - activeAttendanceCount, 0),
            LeaveCount = items.Sum(x => x.Row.LeavePeriods),
            LateCount = items.Sum(x => x.Row.LatePeriods),
            TruancyCount = items.Sum(x => x.Row.TruancyPeriods),
            OtherCount = items.Sum(x => x.Row.OtherPeriods),
            AttendanceRate = studentCount == 0 ? 0m : Math.Round((decimal)activeAttendanceCount / studentCount * 100m, 1),
            FaceRegistrationRate = studentCount == 0 ? 0m : Math.Round((decimal)registeredFaceCount / studentCount * 100m, 1),
            FirstActivityTime = items
                .Where(x => x.Row.FirstCheckInTime.HasValue)
                .Min(x => x.Row.FirstCheckInTime),
            LastActivityTime = items
                .Where(x => x.Row.LastCheckOutTime.HasValue)
                .Max(x => x.Row.LastCheckOutTime)
        };
    }

    private static int ParseSortToken(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return int.MaxValue;
        }

        var digits = new string(label.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var value) ? value : int.MaxValue;
    }

    private sealed record StudentReportAggregateItem(
        int StudentId,
        int? GradeLevelId,
        string GradeLevel,
        int? ClassroomId,
        string Classroom,
        ExecutiveStudentReportRowViewModel Row,
        bool HasRegisteredFace);

    private static string BuildCsvFileName(string prefix, DateTime from, DateTime to)
        => $"executive-{prefix}-{from:yyyyMMdd}-{to:yyyyMMdd}.csv";

    private static decimal CalculatePresentRate(ExecutiveStudentReportRowViewModel row)
    {
        var total = row.PresentPeriods + row.LeavePeriods + row.AbsentPeriods + row.TruancyPeriods + row.LatePeriods + row.OtherPeriods;
        if (total <= 0)
        {
            return 0m;
        }

        return Math.Round((decimal)row.PresentPeriods / total * 100m, 1);
    }

    private static ExecutiveRiskStudentViewModel BuildRiskStudent(ExecutiveStudentReportRowViewModel row)
    {
        var presentRate = CalculatePresentRate(row);
        var totalTracked = row.PresentPeriods + row.LeavePeriods + row.AbsentPeriods + row.TruancyPeriods + row.LatePeriods + row.OtherPeriods;
        var riskScore = (row.AbsentPeriods * 1.2m) + (row.TruancyPeriods * 2.1m) + (row.LatePeriods * 0.7m) + (Math.Max(100m - presentRate, 0m) / 12m);

        return new ExecutiveRiskStudentViewModel
        {
            StudentId = row.StudentId,
            StudentCode = row.StudentCode,
            StudentName = row.StudentName,
            GradeLevel = row.GradeLevel,
            Classroom = row.Classroom,
            PresentRate = presentRate,
            AbsentPeriods = row.AbsentPeriods,
            TruancyPeriods = row.TruancyPeriods,
            LatePeriods = row.LatePeriods,
            TotalTrackedPeriods = totalTracked,
            RiskScore = Math.Round(riskScore, 2),
            RiskLevel = GetRiskLevel(presentRate),
            RiskReason = BuildStudentRiskReason(row, presentRate)
        };
    }

    private static string BuildStudentRiskReason(ExecutiveStudentReportRowViewModel row, decimal presentRate)
    {
        if (row.TruancyPeriods > 0)
        {
            return $"พบหนีเรียน {row.TruancyPeriods} ครั้ง";
        }

        if (row.AbsentPeriods > 0)
        {
            return $"พบขาดเรียน {row.AbsentPeriods} ครั้ง";
        }

        if (row.LatePeriods > 0)
        {
            return $"พบมาสาย {row.LatePeriods} ครั้ง";
        }

        return $"อัตรามาเรียน {presentRate:0.0}%";
    }

    private static string GetRiskLevel(decimal attendanceRate)
    {
        if (attendanceRate < 70m)
        {
            return "เสี่ยงสูง";
        }

        if (attendanceRate < 85m)
        {
            return "เฝ้าระวัง";
        }

        return "ปกติ";
    }

    private static int GetRiskRank(decimal attendanceRate)
    {
        if (attendanceRate < 70m)
        {
            return 0;
        }

        if (attendanceRate < 85m)
        {
            return 1;
        }

        return 2;
    }

    private sealed class ExecutiveGroupCsvRow
    {
        public string GroupName { get; set; } = string.Empty;
        public string SecondaryLabel { get; set; } = string.Empty;
        public int StudentCount { get; set; }
        public int ActiveAttendanceCount { get; set; }
        public int InactiveAttendanceCount { get; set; }
        public decimal AttendanceRate { get; set; }
        public int RegisteredFaceCount { get; set; }
        public decimal FaceRegistrationRate { get; set; }
        public int LateCount { get; set; }
        public int LeaveCount { get; set; }
        public int TruancyCount { get; set; }
        public int OtherCount { get; set; }
        public string RiskLevel { get; set; } = string.Empty;
        public DateTime? FirstActivityTime { get; set; }
        public DateTime? LastActivityTime { get; set; }
    }

    private sealed class ExecutiveStudentCsvRow
    {
        public string StudentCode { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;
        public string Classroom { get; set; } = string.Empty;
        public DateTime? FirstCheckInTime { get; set; }
        public DateTime? LastCheckOutTime { get; set; }
        public int PresentPeriods { get; set; }
        public int LeavePeriods { get; set; }
        public int AbsentPeriods { get; set; }
        public int TruancyPeriods { get; set; }
        public int LatePeriods { get; set; }
        public int OtherPeriods { get; set; }
        public int TotalTrackedPeriods { get; set; }
        public decimal PresentRate { get; set; }
        public string RiskLevel { get; set; } = string.Empty;
    }

    private sealed class ExecutiveRiskTrendCsvRow
    {
        public DateTime Date { get; set; }
        public int PresentCount { get; set; }
        public int AbsentCount { get; set; }
        public decimal AttendanceRate { get; set; }
        public string RiskLevel { get; set; } = string.Empty;
    }

    private sealed class ExecutiveRiskStudentCsvRow
    {
        public string StudentCode { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;
        public string Classroom { get; set; } = string.Empty;
        public decimal PresentRate { get; set; }
        public int AbsentPeriods { get; set; }
        public int TruancyPeriods { get; set; }
        public int LatePeriods { get; set; }
        public int TotalTrackedPeriods { get; set; }
        public decimal RiskScore { get; set; }
        public string RiskLevel { get; set; } = string.Empty;
        public string RiskReason { get; set; } = string.Empty;
    }
}
