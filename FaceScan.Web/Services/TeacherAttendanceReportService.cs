using System.Globalization;
using FaceScan.Web.Data;
using FaceScan.Web.Helpers;
using FaceScan.Web.Models.Enums;
using FaceScan.Web.Services.Interfaces;
using FaceScan.Web.ViewModels;
using FaceScan.Web.ViewModels.TeacherAttendance;
using Microsoft.EntityFrameworkCore;

namespace FaceScan.Web.Services;

public class TeacherAttendanceReportService : ITeacherAttendanceReportService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ITeacherAttendanceService _teacherAttendanceService;
    private readonly ISystemSettingService _systemSettingService;

    public TeacherAttendanceReportService(
        ApplicationDbContext dbContext,
        ITeacherAttendanceService teacherAttendanceService,
        ISystemSettingService systemSettingService)
    {
        _dbContext = dbContext;
        _teacherAttendanceService = teacherAttendanceService;
        _systemSettingService = systemSettingService;
    }

    public async Task<TeacherAttendanceIndexViewModel> GetTransactionsAsync(TeacherAttendanceFilterViewModel filter, CancellationToken cancellationToken = default)
    {
        var date = (filter.Date ?? DateTime.Today).Date;
        var teachers = await LoadTeacherDirectoryAsync(filter, cancellationToken);
        var teacherMap = teachers.ToDictionary(x => x.UserId);
        var teacherIds = teacherMap.Keys.ToList();

        var transactionRows = teacherIds.Count == 0
            ? []
            : await _dbContext.TeacherAttendanceTransactions
                .Where(x => x.ScanDate == date && teacherIds.Contains(x.UserId))
                .AsNoTracking()
                .OrderByDescending(x => x.ScanTime)
                .Select(x => new
                {
                    x.UserId,
                    x.ScanTime,
                    x.ScanType,
                    x.Latitude,
                    x.Longitude,
                    x.LocationAccuracyMeters,
                    x.IsDuplicate,
                    x.ConfidenceScore,
                    x.RecognitionProvider
                })
                .ToListAsync(cancellationToken);

        var items = transactionRows.Select(x =>
        {
            teacherMap.TryGetValue(x.UserId, out var teacher);
            return new TeacherAttendanceTransactionRowViewModel
            {
                ScanTime = x.ScanTime,
                Username = teacher?.Username ?? "-",
                FullName = teacher?.FullName ?? "-",
                RoleName = teacher?.RoleName ?? "-",
                AssignedClassroomName = teacher?.AssignedClassroomName ?? "-",
                ScanType = x.ScanType.GetDisplayName(),
                Latitude = x.Latitude,
                Longitude = x.Longitude,
                LocationAccuracyMeters = x.LocationAccuracyMeters,
                IsDuplicate = x.IsDuplicate,
                Confidence = x.ConfidenceScore,
                Provider = x.RecognitionProvider,
                MapUrl = BuildMapUrl(x.Latitude, x.Longitude)
            };
        }).ToList();

        var options = await LoadOptionsAsync(cancellationToken);
        return new TeacherAttendanceIndexViewModel
        {
            Filter = filter,
            Roles = options.Roles,
            Classrooms = options.Classrooms,
            Transactions = items
        };
    }

    public async Task<TeacherAttendanceDailyViewModel> GetDailyAsync(TeacherAttendanceFilterViewModel filter, CancellationToken cancellationToken = default)
    {
        var date = (filter.Date ?? DateTime.Today).Date;
        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);
        await _teacherAttendanceService.BuildDailySummaryAsync(date, cancellationToken);

        var teachers = await LoadTeacherDirectoryAsync(filter, cancellationToken);
        var teacherMap = teachers.ToDictionary(x => x.UserId);
        var teacherIds = teacherMap.Keys.ToList();

        var summaryRows = teacherIds.Count == 0
            ? []
            : await _dbContext.TeacherAttendanceDailySummaries
                .Where(x => x.Date == date && teacherIds.Contains(x.UserId))
                .AsNoTracking()
                .OrderBy(x => x.UserId)
                .Select(x => new
                {
                    x.UserId,
                    x.FirstCheckInTime,
                    x.LastCheckOutTime,
                    x.AttendanceStatus,
                    x.IsPresent
                })
                .ToListAsync(cancellationToken);

        var rows = summaryRows.Select(x =>
        {
            teacherMap.TryGetValue(x.UserId, out var teacher);
            return new TeacherAttendanceDailyRowViewModel
            {
                UserId = x.UserId,
                Username = teacher?.Username ?? "-",
                FullName = teacher?.FullName ?? "-",
                RoleName = teacher?.RoleName ?? "-",
                AssignedClassroomName = teacher?.AssignedClassroomName ?? "-",
                FirstCheckInTime = x.FirstCheckInTime,
                LastCheckOutTime = x.LastCheckOutTime,
                AttendanceStatus = x.AttendanceStatus,
                StatusText = x.AttendanceStatus.ToString(),
                IsPresent = x.IsPresent
            };
        }).ToList();

        if (teacherIds.Count > 0 && rows.Count > 0)
        {
            var dayTransactions = await _dbContext.TeacherAttendanceTransactions
                .AsNoTracking()
                .Where(x => x.ScanDate == date && !x.IsDuplicate && teacherIds.Contains(x.UserId))
                .Select(x => new
                {
                    x.UserId,
                    x.ScanType,
                    x.ScanTime,
                    x.Latitude,
                    x.Longitude
                })
                .ToListAsync(cancellationToken);

            var txByUser = dayTransactions
                .GroupBy(x => x.UserId)
                .ToDictionary(x => x.Key, x => x.OrderBy(t => t.ScanTime).ToList());

            foreach (var row in rows)
            {
                if (!txByUser.TryGetValue(row.UserId, out var teacherTx))
                {
                    continue;
                }

                var firstCheckInTx = teacherTx
                    .Where(x => x.ScanType == ScanType.CheckIn)
                    .OrderBy(x => x.ScanTime)
                    .FirstOrDefault();

                var lastCheckOutTx = teacherTx
                    .Where(x => x.ScanType == ScanType.CheckOut)
                    .OrderByDescending(x => x.ScanTime)
                    .FirstOrDefault();

                row.CheckInLatitude = firstCheckInTx?.Latitude;
                row.CheckInLongitude = firstCheckInTx?.Longitude;
                row.CheckInMapUrl = BuildMapUrl(firstCheckInTx?.Latitude, firstCheckInTx?.Longitude);
                row.CheckOutLatitude = lastCheckOutTx?.Latitude;
                row.CheckOutLongitude = lastCheckOutTx?.Longitude;
                row.CheckOutMapUrl = BuildMapUrl(lastCheckOutTx?.Latitude, lastCheckOutTx?.Longitude);
            }
        }

        foreach (var row in rows)
        {
            row.IsLate = IsLate(row.FirstCheckInTime, settings.TeacherLateAfterTime);
            row.StatusLabel = BuildStatusLabel(row.AttendanceStatus, row.IsLate);
            row.StatusSortOrder = BuildStatusSortOrder(row.AttendanceStatus, row.IsLate);
        }

        rows = ApplyStatusFilter(rows, filter).ToList();
        rows = ApplySort(rows, filter.SortBy).ToList();

        var options = await LoadOptionsAsync(cancellationToken);
        return new TeacherAttendanceDailyViewModel
        {
            Filter = filter,
            LateAfterTime = settings.TeacherLateAfterTime,
            Roles = options.Roles,
            Classrooms = options.Classrooms,
            Rows = rows
        };
    }

    public async Task<TeacherAttendanceDailyViewModel> GetLateAsync(TeacherAttendanceFilterViewModel filter, CancellationToken cancellationToken = default)
    {
        var lateFilter = new TeacherAttendanceFilterViewModel
        {
            Date = filter.Date,
            AssignedClassroomId = filter.AssignedClassroomId,
            RoleName = filter.RoleName,
            TeacherKeyword = filter.TeacherKeyword,
            SortBy = string.IsNullOrWhiteSpace(filter.SortBy) ? "status" : filter.SortBy,
            Status = filter.Status,
            IsLateOnly = true
        };

        return await GetDailyAsync(lateFilter, cancellationToken);
    }

    private async Task<List<TeacherDirectoryItem>> LoadTeacherDirectoryAsync(TeacherAttendanceFilterViewModel filter, CancellationToken cancellationToken)
    {
        var query =
            from user in _dbContext.Users.AsNoTracking()
            join userRole in _dbContext.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
            join role in _dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            join classroom in _dbContext.Classrooms.AsNoTracking() on user.AssignedClassroomId equals classroom.Id into classroomJoin
            from classroom in classroomJoin.DefaultIfEmpty()
            where user.IsActive &&
                  role.Name != null &&
                  TeacherRoleCatalog.AttendanceRoles.Contains(role.Name)
            select new TeacherDirectoryItem(
                user.Id,
                user.UserName ?? string.Empty,
                user.FullName,
                role.Name!,
                user.AssignedClassroomId,
                classroom != null ? classroom.Name : "-");

        if (filter.AssignedClassroomId.HasValue)
        {
            query = query.Where(x => x.AssignedClassroomId == filter.AssignedClassroomId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.RoleName))
        {
            var roleName = filter.RoleName.Trim();
            query = query.Where(x => x.RoleName == roleName);
        }

        if (!string.IsNullOrWhiteSpace(filter.TeacherKeyword))
        {
            var keyword = filter.TeacherKeyword.Trim();
            query = query.Where(x =>
                x.Username.Contains(keyword) ||
                x.FullName.Contains(keyword));
        }

        var rows = await query.ToListAsync(cancellationToken);
        return rows
            .GroupBy(x => x.UserId)
            .Select(x => x.First())
            .OrderBy(x => x.FullName)
            .ToList();
    }

    private static IEnumerable<TeacherAttendanceDailyRowViewModel> ApplyStatusFilter(
        IReadOnlyList<TeacherAttendanceDailyRowViewModel> rows,
        TeacherAttendanceFilterViewModel filter)
    {
        IEnumerable<TeacherAttendanceDailyRowViewModel> filtered = rows;

        if (filter.IsLateOnly == true)
        {
            filtered = filtered.Where(x => x.IsLate);
        }

        if (string.IsNullOrWhiteSpace(filter.Status) || string.Equals(filter.Status, "all", StringComparison.OrdinalIgnoreCase))
        {
            return filtered;
        }

        return filter.Status.ToLowerInvariant() switch
        {
            "late" => filtered.Where(x => x.IsLate),
            "present" => filtered.Where(x => x.AttendanceStatus == AttendanceStatus.Present && !x.IsLate),
            "absent" => filtered.Where(x => x.AttendanceStatus == AttendanceStatus.Absent),
            "pendingcheckout" => filtered.Where(x => x.AttendanceStatus == AttendanceStatus.PendingCheckout),
            "partial" => filtered.Where(x => x.AttendanceStatus == AttendanceStatus.Partial),
            _ => filtered
        };
    }

    private static IEnumerable<TeacherAttendanceDailyRowViewModel> ApplySort(IReadOnlyList<TeacherAttendanceDailyRowViewModel> rows, string? sortBy)
    {
        return (sortBy ?? "status").ToLowerInvariant() switch
        {
            "username" => rows.OrderBy(x => x.Username),
            "name" => rows.OrderBy(x => x.FullName),
            "checkin" => rows.OrderBy(x => x.FirstCheckInTime ?? DateTime.MaxValue).ThenBy(x => x.FullName),
            "checkout" => rows.OrderBy(x => x.LastCheckOutTime ?? DateTime.MaxValue).ThenBy(x => x.FullName),
            _ => rows.OrderBy(x => x.StatusSortOrder).ThenBy(x => x.RoleName).ThenBy(x => x.FullName)
        };
    }

    private static int BuildStatusSortOrder(AttendanceStatus status, bool isLate)
    {
        if (isLate)
        {
            return 1;
        }

        return status switch
        {
            AttendanceStatus.PendingCheckout => 2,
            AttendanceStatus.Present => 3,
            AttendanceStatus.Absent => 4,
            _ => 5
        };
    }

    private static string BuildStatusLabel(AttendanceStatus status, bool isLate)
    {
        if (isLate)
        {
            return status == AttendanceStatus.PendingCheckout
                ? "มาสาย (ยังไม่ลงเวลากลับ)"
                : "มาสาย";
        }

        return status switch
        {
            AttendanceStatus.Present => "มาปฏิบัติราชการ",
            AttendanceStatus.Absent => "ขาดปฏิบัติราชการ",
            AttendanceStatus.PendingCheckout => "ยังไม่ลงเวลากลับ",
            _ => "ข้อมูลไม่สมบูรณ์"
        };
    }

    private static bool IsLate(DateTime? firstCheckInTime, TimeSpan lateAfterTime)
    {
        return firstCheckInTime.HasValue && firstCheckInTime.Value.TimeOfDay > lateAfterTime;
    }

    private static string? BuildMapUrl(decimal? latitude, decimal? longitude)
    {
        if (!latitude.HasValue || !longitude.HasValue)
        {
            return null;
        }

        var latText = latitude.Value.ToString("0.######", CultureInfo.InvariantCulture);
        var lngText = longitude.Value.ToString("0.######", CultureInfo.InvariantCulture);
        return $"https://www.google.com/maps?q={latText},{lngText}";
    }

    private async Task<(IReadOnlyList<string> Roles, IReadOnlyList<SelectOptionViewModel> Classrooms)> LoadOptionsAsync(CancellationToken cancellationToken)
    {
        var roles = TeacherRoleCatalog.AttendanceRoles
            .ToList();

        var classrooms = await _dbContext.Classrooms
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SelectOptionViewModel { Value = x.Id, Text = x.Name })
            .ToListAsync(cancellationToken);

        return (roles, classrooms);
    }

    private sealed record TeacherDirectoryItem(
        string UserId,
        string Username,
        string FullName,
        string RoleName,
        int? AssignedClassroomId,
        string AssignedClassroomName);
}
