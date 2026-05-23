using FaceScan.Web.Data;
using FaceScan.Web.Helpers;
using FaceScan.Web.Services.Interfaces;
using FaceScan.Web.ViewModels.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace FaceScan.Web.Services;

public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAttendanceService _attendanceService;
    private readonly ISystemSettingService _systemSettingService;

    public DashboardService(
        ApplicationDbContext dbContext,
        IAttendanceService attendanceService,
        ISystemSettingService systemSettingService)
    {
        _dbContext = dbContext;
        _attendanceService = attendanceService;
        _systemSettingService = systemSettingService;
    }

    public async Task<DashboardViewModel> GetDashboardAsync(DateTime? date = null, CancellationToken cancellationToken = default)
    {
        var targetDate = (date ?? DateTime.Today).Date;
        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);

        await _attendanceService.BuildDailySummaryAsync(targetDate, cancellationToken);

        var totalStudents = await _dbContext.Students.CountAsync(x => x.IsActive, cancellationToken);

        var summaries = await _dbContext.AttendanceDailySummaries
            .Include(x => x.Student)
                .ThenInclude(x => x!.GradeLevel)
            .Include(x => x.Student)
                .ThenInclude(x => x!.Classroom)
            .Include(x => x.Student)
                .ThenInclude(x => x!.StudentPhotos)
            .Where(x => x.Date == targetDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var transactions = await _dbContext.AttendanceTransactions
            .Include(x => x.Student)
            .Where(x => x.ScanDate == targetDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var present = summaries.Count(x => x.IsPresent);
        var absent = Math.Max(totalStudents - present, 0);

        var periodRecords = await _dbContext.PeriodAttendanceRecords
            .AsNoTracking()
            .Include(x => x.PeriodAttendanceSession)
            .Where(x => x.PeriodAttendanceSession != null && x.PeriodAttendanceSession.Date == targetDate)
            .ToListAsync(cancellationToken);

        var lateDailyStudents = summaries
            .Where(x => x.FirstCheckInTime.HasValue && x.FirstCheckInTime.Value.TimeOfDay > settings.LateAfterTime)
            .Select(x => x.StudentId)
            .ToHashSet();

        var latePeriodStudents = periodRecords
            .Where(x => x.Status == Models.Enums.PeriodAttendanceStatus.Late)
            .Select(x => x.StudentId)
            .ToHashSet();

        lateDailyStudents.UnionWith(latePeriodStudents);

        var leaveStudents = periodRecords
            .Where(x => x.Status == Models.Enums.PeriodAttendanceStatus.Leave)
            .Select(x => x.StudentId)
            .Distinct()
            .Count();

        var truancyStudents = periodRecords
            .Where(x => x.Status == Models.Enums.PeriodAttendanceStatus.Truancy)
            .Select(x => x.StudentId)
            .Distinct()
            .Count();

        var gradeSummaries = summaries
            .Where(x => x.Student?.GradeLevel is not null)
            .GroupBy(x => x.Student!.GradeLevel!.Name)
            .Select(g => new DashboardGradeSummaryViewModel
            {
                GradeLevel = g.Key,
                TotalStudents = g.Count(),
                PresentStudents = g.Count(x => x.IsPresent),
                AbsentStudents = g.Count(x => !x.IsPresent)
            })
            .OrderBy(x => x.GradeLevel)
            .ToList();

        var classroomSummaries = summaries
            .Where(x => x.Student?.Classroom is not null && x.Student?.GradeLevel is not null)
            .GroupBy(x => new { Grade = x.Student!.GradeLevel!.Name, Room = x.Student!.Classroom!.Name })
            .Select(g => new DashboardClassroomSummaryViewModel
            {
                GradeLevel = g.Key.Grade,
                Classroom = g.Key.Room,
                TotalStudents = g.Count(),
                PresentStudents = g.Count(x => x.IsPresent),
                AbsentStudents = g.Count(x => !x.IsPresent)
            })
            .OrderBy(x => x.GradeLevel)
            .ThenBy(x => x.Classroom)
            .ToList();

        var latestScans = transactions
            .OrderByDescending(x => x.ScanTime)
            .Take(10)
            .Select(x => new DashboardLatestScanViewModel
            {
                ScanTime = x.ScanTime,
                StudentCode = x.Student?.StudentCode ?? "-",
                StudentName = x.Student?.FullName ?? "-",
                ScanType = x.ScanType.GetDisplayName()
            })
            .ToList();

        var latestScanByStudent = transactions
            .GroupBy(x => x.StudentId)
            .ToDictionary(x => x.Key, x => x.Max(t => t.ScanTime));

        var realtimeRows = summaries
            .Where(x => x.Student is not null)
            .Select(x =>
            {
                var photoPath = x.Student!.StudentPhotos
                    .OrderByDescending(p => p.IsPrimary)
                    .ThenByDescending(p => p.CapturedAt)
                    .Select(p => p.FilePath)
                    .FirstOrDefault();

                latestScanByStudent.TryGetValue(x.StudentId, out var latestScanTime);
                return new DashboardRealtimeStudentViewModel
                {
                    StudentId = x.StudentId,
                    StudentCode = x.Student.StudentCode,
                    StudentName = x.Student.FullName,
                    Classroom = x.Student.Classroom?.Name ?? "-",
                    PhotoPath = photoPath,
                    FirstCheckInTime = x.FirstCheckInTime,
                    LastCheckOutTime = x.LastCheckOutTime,
                    LatestScanTime = latestScanTime == default ? null : latestScanTime
                };
            })
            .Where(x => x.FirstCheckInTime.HasValue || x.LastCheckOutTime.HasValue || x.LatestScanTime.HasValue)
            .OrderByDescending(x => x.LatestScanTime ?? x.FirstCheckInTime ?? DateTime.MinValue)
            .Take(25)
            .ToList();

        return new DashboardViewModel
        {
            TargetDate = targetDate,
            TotalStudents = totalStudents,
            PresentToday = present,
            AbsentToday = absent,
            LateToday = lateDailyStudents.Count,
            LeaveToday = leaveStudents,
            TruancyToday = truancyStudents,
            CheckInCount = transactions.Count(x => x.ScanType == Models.Enums.ScanType.CheckIn),
            CheckOutCount = transactions.Count(x => x.ScanType == Models.Enums.ScanType.CheckOut),
            PendingCheckoutCount = summaries.Count(x => x.AttendanceStatus == Models.Enums.AttendanceStatus.PendingCheckout),
            GradeSummaries = gradeSummaries,
            ClassroomSummaries = classroomSummaries,
            LatestScans = latestScans,
            RealtimeRows = realtimeRows
        };
    }
}
