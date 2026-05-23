using System.Globalization;
using FaceScan.Web.Data;
using FaceScan.Web.Helpers;
using FaceScan.Web.Models.Enums;
using FaceScan.Web.Services.Interfaces;
using FaceScan.Web.ViewModels;
using FaceScan.Web.ViewModels.Attendance;
using Microsoft.EntityFrameworkCore;

namespace FaceScan.Web.Services;

public class AttendanceReportService : IAttendanceReportService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAttendanceService _attendanceService;
    private readonly ISystemSettingService _systemSettingService;

    public AttendanceReportService(
        ApplicationDbContext dbContext,
        IAttendanceService attendanceService,
        ISystemSettingService systemSettingService)
    {
        _dbContext = dbContext;
        _attendanceService = attendanceService;
        _systemSettingService = systemSettingService;
    }

    public async Task<AttendanceIndexViewModel> GetTransactionsAsync(AttendanceFilterViewModel filter, CancellationToken cancellationToken = default)
    {
        var date = (filter.Date ?? DateTime.Today).Date;

        var query = _dbContext.AttendanceTransactions
            .Include(x => x.Student)
                .ThenInclude(x => x!.GradeLevel)
            .Include(x => x.Student)
                .ThenInclude(x => x!.Classroom)
            .Where(x => x.ScanDate == date)
            .AsNoTracking();

        if (filter.AcademicYearId.HasValue)
        {
            query = query.Where(x => x.Student != null && x.Student.AcademicYearId == filter.AcademicYearId.Value);
        }

        if (filter.GradeLevelId.HasValue)
        {
            query = query.Where(x => x.Student != null && x.Student.GradeLevelId == filter.GradeLevelId.Value);
        }

        if (filter.ClassroomId.HasValue)
        {
            query = query.Where(x => x.Student != null && x.Student.ClassroomId == filter.ClassroomId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.StudentKeyword))
        {
            var keyword = filter.StudentKeyword.Trim();
            query = query.Where(x =>
                x.Student != null &&
                (x.Student.StudentCode.Contains(keyword) ||
                 ((x.Student.Prefix ?? string.Empty) + x.Student.FirstName + " " + x.Student.LastName).Contains(keyword)));
        }

        var items = await query
            .OrderByDescending(x => x.ScanTime)
            .Select(x => new AttendanceTransactionRowViewModel
            {
                ScanTime = x.ScanTime,
                StudentCode = x.Student!.StudentCode,
                StudentName = x.Student.FullName,
                ScanType = x.ScanType.GetDisplayName(),
                Latitude = x.Latitude,
                Longitude = x.Longitude,
                LocationAccuracyMeters = x.LocationAccuracyMeters,
                IsDuplicate = x.IsDuplicate,
                Confidence = x.ConfidenceScore,
                Provider = x.RecognitionProvider
            })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            item.MapUrl = BuildMapUrl(item.Latitude, item.Longitude);
        }

        var options = await LoadOptionsAsync(cancellationToken);
        return new AttendanceIndexViewModel
        {
            Filter = filter,
            AcademicYears = options.AcademicYears,
            GradeLevels = options.GradeLevels,
            Classrooms = options.Classrooms,
            Transactions = items
        };
    }

    public async Task<AttendanceDailyViewModel> GetDailyAsync(AttendanceFilterViewModel filter, CancellationToken cancellationToken = default)
    {
        var date = (filter.Date ?? DateTime.Today).Date;
        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);
        await _attendanceService.BuildDailySummaryAsync(date, cancellationToken);

        var query = _dbContext.AttendanceDailySummaries
            .Include(x => x.Student)
                .ThenInclude(x => x!.GradeLevel)
            .Include(x => x.Student)
                .ThenInclude(x => x!.Classroom)
            .Where(x => x.Date == date)
            .AsNoTracking();

        if (filter.AcademicYearId.HasValue)
        {
            query = query.Where(x => x.Student != null && x.Student.AcademicYearId == filter.AcademicYearId.Value);
        }

        if (filter.GradeLevelId.HasValue)
        {
            query = query.Where(x => x.Student != null && x.Student.GradeLevelId == filter.GradeLevelId.Value);
        }

        if (filter.ClassroomId.HasValue)
        {
            query = query.Where(x => x.Student != null && x.Student.ClassroomId == filter.ClassroomId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.StudentKeyword))
        {
            var keyword = filter.StudentKeyword.Trim();
            query = query.Where(x =>
                x.Student != null &&
                (x.Student.StudentCode.Contains(keyword) ||
                 ((x.Student.Prefix ?? string.Empty) + x.Student.FirstName + " " + x.Student.LastName).Contains(keyword)));
        }

        var rows = await query
            .OrderBy(x => x.Student!.StudentCode)
            .Select(x => new AttendanceDailyRowViewModel
            {
                StudentId = x.StudentId,
                StudentCode = x.Student!.StudentCode,
                StudentName = x.Student.FullName,
                GradeLevel = x.Student.GradeLevel!.Name,
                Classroom = x.Student.Classroom!.Name,
                FirstCheckInTime = x.FirstCheckInTime,
                LastCheckOutTime = x.LastCheckOutTime,
                AttendanceStatus = x.AttendanceStatus,
                StatusText = x.AttendanceStatus.ToString(),
                IsPresent = x.IsPresent
            })
            .ToListAsync(cancellationToken);

        var rowStudentIds = rows.Select(x => x.StudentId).Distinct().ToList();
        if (rowStudentIds.Count > 0)
        {
            var dayTransactions = await _dbContext.AttendanceTransactions
                .AsNoTracking()
                .Where(x => x.ScanDate == date && !x.IsDuplicate && rowStudentIds.Contains(x.StudentId))
                .Select(x => new
                {
                    x.StudentId,
                    x.ScanType,
                    x.ScanTime,
                    x.Latitude,
                    x.Longitude
                })
                .ToListAsync(cancellationToken);

            var txByStudent = dayTransactions
                .GroupBy(x => x.StudentId)
                .ToDictionary(x => x.Key, x => x.OrderBy(t => t.ScanTime).ToList());

            foreach (var row in rows)
            {
                if (!txByStudent.TryGetValue(row.StudentId, out var studentTx))
                {
                    continue;
                }

                var firstCheckInTx = studentTx
                    .Where(x => x.ScanType == ScanType.CheckIn)
                    .OrderBy(x => x.ScanTime)
                    .FirstOrDefault();

                var lastCheckOutTx = studentTx
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
            row.IsLate = IsLate(row.FirstCheckInTime, settings.LateAfterTime);
            row.StatusLabel = BuildStatusLabel(row.AttendanceStatus, row.IsLate);
            row.StatusSortOrder = BuildStatusSortOrder(row.AttendanceStatus, row.IsLate);
        }

        rows = ApplyStatusFilter(rows, filter).ToList();
        rows = ApplySort(rows, filter.SortBy).ToList();

        var options = await LoadOptionsAsync(cancellationToken);
        return new AttendanceDailyViewModel
        {
            Filter = filter,
            LateAfterTime = settings.LateAfterTime,
            AcademicYears = options.AcademicYears,
            GradeLevels = options.GradeLevels,
            Classrooms = options.Classrooms,
            Rows = rows
        };
    }

    public async Task<AttendanceDailyViewModel> GetLateAsync(AttendanceFilterViewModel filter, CancellationToken cancellationToken = default)
    {
        var lateFilter = new AttendanceFilterViewModel
        {
            Date = filter.Date,
            AcademicYearId = filter.AcademicYearId,
            GradeLevelId = filter.GradeLevelId,
            ClassroomId = filter.ClassroomId,
            StudentKeyword = filter.StudentKeyword,
            SortBy = string.IsNullOrWhiteSpace(filter.SortBy) ? "status" : filter.SortBy,
            Status = filter.Status,
            IsLateOnly = true
        };

        return await GetDailyAsync(lateFilter, cancellationToken);
    }

    public async Task<AttendanceByClassViewModel> GetByClassAsync(AttendanceFilterViewModel filter, CancellationToken cancellationToken = default)
    {
        var daily = await GetDailyAsync(filter, cancellationToken);

        var grouped = daily.Rows
            .GroupBy(x => new { x.GradeLevel, x.Classroom })
            .Select(g => new AttendanceByClassItemViewModel
            {
                GradeLevel = g.Key.GradeLevel,
                Classroom = g.Key.Classroom,
                TotalCount = g.Count(),
                LateCount = g.Count(x => x.IsLate),
                PresentCount = g.Count(x => x.AttendanceStatus == AttendanceStatus.Present && !x.IsLate),
                PartialCount = g.Count(x => x.AttendanceStatus == AttendanceStatus.PendingCheckout || x.AttendanceStatus == AttendanceStatus.Partial),
                AbsentCount = g.Count(x => x.AttendanceStatus == AttendanceStatus.Absent)
            })
            .OrderBy(x => x.GradeLevel)
            .ThenBy(x => x.Classroom)
            .ToList();

        return new AttendanceByClassViewModel
        {
            Filter = filter,
            AcademicYears = daily.AcademicYears,
            GradeLevels = daily.GradeLevels,
            Classrooms = daily.Classrooms,
            Items = grouped
        };
    }

    public async Task<byte[]> ExportDailyCsvAsync(AttendanceFilterViewModel filter, CancellationToken cancellationToken = default)
    {
        var daily = await GetDailyAsync(filter, cancellationToken);
        var rows = daily.Rows.Select(x => new
        {
            x.StudentCode,
            x.StudentName,
            x.GradeLevel,
            x.Classroom,
            FirstCheckIn = x.FirstCheckInTime?.ToString("HH:mm:ss"),
            LastCheckOut = x.LastCheckOutTime?.ToString("HH:mm:ss"),
            CheckInMap = x.CheckInMapUrl ?? "-",
            CheckOutMap = x.CheckOutMapUrl ?? "-",
            IsLate = x.IsLate ? "ใช่" : "ไม่ใช่",
            Status = x.StatusLabel
        });

        return CsvExportHelper.Export(rows);
    }

    public async Task<byte[]> ExportLateCsvAsync(AttendanceFilterViewModel filter, CancellationToken cancellationToken = default)
    {
        var late = await GetLateAsync(filter, cancellationToken);
        var rows = late.Rows.Select(x => new
        {
            x.StudentCode,
            x.StudentName,
            x.GradeLevel,
            x.Classroom,
            FirstCheckIn = x.FirstCheckInTime?.ToString("HH:mm:ss"),
            CheckInMap = x.CheckInMapUrl ?? "-",
            LateAfter = late.LateAfterTime.ToString(@"hh\:mm"),
            x.StatusLabel
        });

        return CsvExportHelper.Export(rows);
    }

    public async Task<byte[]> ExportByClassCsvAsync(AttendanceFilterViewModel filter, CancellationToken cancellationToken = default)
    {
        var byClass = await GetByClassAsync(filter, cancellationToken);
        var rows = byClass.Items.Select(x => new
        {
            x.GradeLevel,
            x.Classroom,
            x.TotalCount,
            x.PresentCount,
            x.LateCount,
            x.AbsentCount,
            x.PartialCount
        });

        return CsvExportHelper.Export(rows);
    }

    private static IEnumerable<AttendanceDailyRowViewModel> ApplyStatusFilter(
        IReadOnlyList<AttendanceDailyRowViewModel> rows,
        AttendanceFilterViewModel filter)
    {
        IEnumerable<AttendanceDailyRowViewModel> filtered = rows;

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

    private static IEnumerable<AttendanceDailyRowViewModel> ApplySort(IReadOnlyList<AttendanceDailyRowViewModel> rows, string? sortBy)
    {
        return (sortBy ?? "status").ToLowerInvariant() switch
        {
            "studentcode" => rows.OrderBy(x => x.StudentCode),
            "name" => rows.OrderBy(x => x.StudentName),
            "checkin" => rows.OrderBy(x => x.FirstCheckInTime ?? DateTime.MaxValue).ThenBy(x => x.StudentCode),
            "checkout" => rows.OrderBy(x => x.LastCheckOutTime ?? DateTime.MaxValue).ThenBy(x => x.StudentCode),
            _ => rows.OrderBy(x => x.StatusSortOrder).ThenBy(x => x.GradeLevel).ThenBy(x => x.Classroom).ThenBy(x => x.StudentCode)
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
                ? "มาสาย (ยังไม่เช็คกลับ)"
                : "มาสาย";
        }

        return status switch
        {
            AttendanceStatus.Present => "มาเรียน",
            AttendanceStatus.Absent => "ขาด",
            AttendanceStatus.PendingCheckout => "ยังไม่เช็คกลับ",
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

    private async Task<(IReadOnlyList<SelectOptionViewModel> AcademicYears, IReadOnlyList<SelectOptionViewModel> GradeLevels, IReadOnlyList<SelectOptionViewModel> Classrooms)> LoadOptionsAsync(CancellationToken cancellationToken)
    {
        var academicYears = await _dbContext.AcademicYears
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.IsCurrent)
            .ThenByDescending(x => x.StartDate)
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

        return (academicYears, gradeLevels, classrooms);
    }
}
