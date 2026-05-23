using System.Globalization;
using FaceScan.Web.Data;
using FaceScan.Web.Helpers;
using FaceScan.Web.Models.Entities;
using FaceScan.Web.Models.Enums;
using FaceScan.Web.Services.Interfaces;
using FaceScan.Web.ViewModels.Scan;
using Microsoft.EntityFrameworkCore;

namespace FaceScan.Web.Services;

public class TeacherAttendanceService : ITeacherAttendanceService
{
    private static readonly string[] ClientTimeFormats =
    [
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.FFF",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFF"
    ];

    private const double MaxAcceptableSkewMinutes = 1440d;
    private const double WarnSkewMinutes = 10d;

    private readonly ApplicationDbContext _dbContext;
    private readonly IFaceRecognitionServiceResolver _faceRecognitionServiceResolver;
    private readonly IFileStorageService _fileStorageService;
    private readonly IScanDeviceLookupService _scanDeviceLookupService;
    private readonly ISystemSettingService _systemSettingService;

    public TeacherAttendanceService(
        ApplicationDbContext dbContext,
        IFaceRecognitionServiceResolver faceRecognitionServiceResolver,
        IFileStorageService fileStorageService,
        IScanDeviceLookupService scanDeviceLookupService,
        ISystemSettingService systemSettingService)
    {
        _dbContext = dbContext;
        _faceRecognitionServiceResolver = faceRecognitionServiceResolver;
        _fileStorageService = fileStorageService;
        _scanDeviceLookupService = scanDeviceLookupService;
        _systemSettingService = systemSettingService;
    }

    public async Task<TeacherScanProcessResult> ProcessScanAsync(
        ScanVerifyRequestViewModel request,
        Stream imageStream,
        CancellationToken cancellationToken = default)
    {
        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);
        var clockContext = ResolveScanClock(request);
        var scanTime = clockContext.ScanTime;
        var recognitionProfile = _faceRecognitionServiceResolver.NormalizeProfile(request.RecognitionProfile);
        var faceRecognitionService = _faceRecognitionServiceResolver.ResolveForScan(recognitionProfile);

        await using var rawMemory = new MemoryStream();
        await imageStream.CopyToAsync(rawMemory, cancellationToken);
        rawMemory.Position = 0;

        var faceMatch = await faceRecognitionService.VerifyTeacherAsync(rawMemory, recognitionProfile);
        if (!faceMatch.Success || string.IsNullOrWhiteSpace(faceMatch.UserId))
        {
            return new TeacherScanProcessResult
            {
                Success = false,
                Message = faceMatch.Message,
                ScanTime = scanTime,
                ConfidenceScore = faceMatch.ConfidenceScore,
                Provider = faceMatch.Provider,
                RecognitionProfile = recognitionProfile,
                TimeSource = clockContext.Source,
                ClockAnomaly = clockContext.ClockAnomaly,
                ClockSkewMinutes = clockContext.SkewMinutes
            };
        }

        if (faceMatch.ConfidenceScore < settings.FaceConfidenceThreshold)
        {
            return new TeacherScanProcessResult
            {
                Success = false,
                Message = "ความมั่นใจต่ำกว่าเกณฑ์ที่กำหนด",
                UserId = faceMatch.UserId,
                Username = faceMatch.UserName,
                FullName = faceMatch.FullName,
                ScanTime = scanTime,
                ConfidenceScore = faceMatch.ConfidenceScore,
                Provider = faceMatch.Provider,
                RecognitionProfile = recognitionProfile,
                TimeSource = clockContext.Source,
                ClockAnomaly = clockContext.ClockAnomaly,
                ClockSkewMinutes = clockContext.SkewMinutes
            };
        }

        var teacherDirectory = await LoadTeacherDirectoryAsync(cancellationToken);
        var teacherInfo = teacherDirectory.FirstOrDefault(x => x.UserId == faceMatch.UserId);

        if (teacherInfo is null)
        {
            return new TeacherScanProcessResult
            {
                Success = false,
                Message = "ไม่พบข้อมูลครูผู้ปฏิบัติราชการ",
                ScanTime = scanTime,
                ConfidenceScore = faceMatch.ConfidenceScore,
                Provider = faceMatch.Provider,
                RecognitionProfile = recognitionProfile,
                TimeSource = clockContext.Source,
                ClockAnomaly = clockContext.ClockAnomaly,
                ClockSkewMinutes = clockContext.SkewMinutes
            };
        }

        var todayTransactions = await _dbContext.TeacherAttendanceTransactions
            .Where(x => x.UserId == teacherInfo.UserId && x.ScanDate == scanTime.Date && !x.IsDuplicate)
            .OrderBy(x => x.ScanTime)
            .ToListAsync(cancellationToken);

        var scanType = DetermineCheckType(todayTransactions, scanTime, settings.TeacherCheckOutStartTime, request.RequestedType);
        var validation = ValidateDailyScanRule(scanType, scanTime, todayTransactions, settings);

        if (!validation.CanCreate)
        {
            return new TeacherScanProcessResult
            {
                Success = false,
                Message = validation.Message,
                UserId = teacherInfo.UserId,
                Username = teacherInfo.Username,
                FullName = teacherInfo.FullName,
                RoleName = teacherInfo.RoleName,
                ScanType = scanType,
                ScanTime = scanTime,
                ConfidenceScore = faceMatch.ConfidenceScore,
                IsDuplicate = validation.IsDuplicate,
                Provider = faceMatch.Provider,
                RecognitionProfile = recognitionProfile,
                TimeSource = clockContext.Source,
                ClockAnomaly = clockContext.ClockAnomaly,
                ClockSkewMinutes = clockContext.SkewMinutes
            };
        }

        string? snapshotPath = null;
        if (settings.SaveSnapshots)
        {
            var ext = request.Image is not null
                ? GetExtensionFromContentType(request.Image.ContentType)
                : ".jpg";

            snapshotPath = await _fileStorageService.SaveScanSnapshotAsync(rawMemory, ext, cancellationToken);
        }

        var transaction = new TeacherAttendanceTransaction
        {
            UserId = teacherInfo.UserId,
            ScanType = scanType,
            ScanTime = scanTime,
            ScanDate = scanTime.Date,
            DeviceId = await ResolveDeviceIdAsync(request.StationCode, request.StationToken, cancellationToken),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            LocationAccuracyMeters = request.LocationAccuracyMeters,
            ConfidenceScore = faceMatch.ConfidenceScore,
            RecognitionProvider = faceMatch.Provider,
            SnapshotPath = snapshotPath,
            IsDuplicate = false,
            RawResponseJson = faceMatch.RawResponseJson
        };

        _dbContext.TeacherAttendanceTransactions.Add(transaction);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await UpsertDailySummaryAsync(teacherInfo, scanTime.Date, settings, cancellationToken);

        return new TeacherScanProcessResult
        {
            Success = true,
            Message = BuildScanSuccessMessage(scanType, clockContext),
            UserId = teacherInfo.UserId,
            Username = teacherInfo.Username,
            FullName = teacherInfo.FullName,
            RoleName = teacherInfo.RoleName,
            ScanType = scanType,
            ScanTime = scanTime,
            ConfidenceScore = faceMatch.ConfidenceScore,
            IsDuplicate = false,
            Provider = faceMatch.Provider,
            RecognitionProfile = recognitionProfile,
            TimeSource = clockContext.Source,
            ClockAnomaly = clockContext.ClockAnomaly,
            ClockSkewMinutes = clockContext.SkewMinutes
        };
    }

    public async Task<ScanType> DetermineCheckTypeAsync(
        string userId,
        DateTime scanTime,
        ScanType? requestedType = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);
        var todayTransactions = await _dbContext.TeacherAttendanceTransactions
            .Where(x => x.UserId == userId && x.ScanDate == scanTime.Date && !x.IsDuplicate)
            .OrderBy(x => x.ScanTime)
            .ToListAsync(cancellationToken);

        return DetermineCheckType(todayTransactions, scanTime, settings.TeacherCheckOutStartTime, requestedType);
    }

    public async Task BuildDailySummaryAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var targetDate = date.Date;
        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);
        var activeTeachers = await LoadTeacherDirectoryAsync(cancellationToken);

        var teacherIds = activeTeachers.Select(x => x.UserId).ToList();
        var transactions = teacherIds.Count == 0
            ? new List<TeacherAttendanceTransaction>()
            : await _dbContext.TeacherAttendanceTransactions
                .Where(x => x.ScanDate == targetDate && !x.IsDuplicate && teacherIds.Contains(x.UserId))
                .AsNoTracking()
                .ToListAsync(cancellationToken);

        var existingSummaries = await _dbContext.TeacherAttendanceDailySummaries
            .Where(x => x.Date == targetDate)
            .ToListAsync(cancellationToken);

        var summaryByUser = existingSummaries.ToDictionary(x => x.UserId);

        foreach (var teacher in activeTeachers)
        {
            var teacherTransactions = transactions
                .Where(x => x.UserId == teacher.UserId)
                .OrderBy(x => x.ScanTime)
                .ToList();

            var firstCheckIn = teacherTransactions
                .Where(x => x.ScanType == ScanType.CheckIn)
                .Select(x => (DateTime?)x.ScanTime)
                .FirstOrDefault();

            var lastCheckOut = teacherTransactions
                .Where(x => x.ScanType == ScanType.CheckOut)
                .Select(x => (DateTime?)x.ScanTime)
                .LastOrDefault();

            var status = AttendanceStatus.Absent;
            var isPresent = teacherTransactions.Count > 0;
            var remark = string.Empty;

            if (!isPresent)
            {
                status = AttendanceStatus.Absent;
                remark = "ไม่มาปฏิบัติราชการ";
            }
            else if (firstCheckIn.HasValue && !lastCheckOut.HasValue)
            {
                status = AttendanceStatus.PendingCheckout;
                remark = "ลงเวลามาแล้ว ยังไม่ลงเวลากลับ";
            }
            else if (firstCheckIn.HasValue && lastCheckOut.HasValue)
            {
                status = AttendanceStatus.Present;
                remark = "มาปฏิบัติราชการ";
            }
            else
            {
                status = AttendanceStatus.Partial;
                remark = "ข้อมูลไม่สมบูรณ์";
            }

            if (firstCheckIn.HasValue && firstCheckIn.Value.TimeOfDay > settings.TeacherLateAfterTime)
            {
                remark = $"{remark} (มาสาย)";
            }

            if (!summaryByUser.TryGetValue(teacher.UserId, out var summary))
            {
                summary = new TeacherAttendanceDailySummary
                {
                    UserId = teacher.UserId,
                    Date = targetDate
                };
                _dbContext.TeacherAttendanceDailySummaries.Add(summary);
            }

            summary.FirstCheckInTime = firstCheckIn;
            summary.LastCheckOutTime = lastCheckOut;
            summary.TotalScans = teacherTransactions.Count;
            summary.IsPresent = isPresent;
            summary.AttendanceStatus = status;
            summary.Remark = remark;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TeacherAttendanceDailySummary>> GetTodaySummaryAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var targetDate = date.Date;
        return await _dbContext.TeacherAttendanceDailySummaries
            .Include(x => x.User)
            .Where(x => x.Date == targetDate)
            .AsNoTracking()
            .OrderBy(x => x.User!.FullName)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<TeacherDirectoryItem>> LoadTeacherDirectoryAsync(CancellationToken cancellationToken)
    {
        var rows = await (
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
                classroom != null ? classroom.Name : "-"))
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.UserId)
            .Select(x => x.First())
            .OrderBy(x => x.FullName)
            .ToList();
    }

    private static ScanClockContext ResolveScanClock(ScanVerifyRequestViewModel request)
    {
        var serverNow = DateTime.Now;

        if (string.IsNullOrWhiteSpace(request.ClientCapturedAtLocal))
        {
            return new ScanClockContext(serverNow, "server", false, null);
        }

        if (!DateTime.TryParseExact(
                request.ClientCapturedAtLocal.Trim(),
                ClientTimeFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var clientLocal))
        {
            return new ScanClockContext(serverNow, "server", true, null);
        }

        var skewMinutes = Math.Abs((serverNow - clientLocal).TotalMinutes);
        var roundedSkewMinutes = Math.Round((decimal)skewMinutes, 2);

        if (skewMinutes > MaxAcceptableSkewMinutes)
        {
            return new ScanClockContext(serverNow, "server", true, roundedSkewMinutes);
        }

        var anomaly = skewMinutes > WarnSkewMinutes;
        return new ScanClockContext(clientLocal, "client", anomaly, roundedSkewMinutes);
    }

    private static string BuildScanSuccessMessage(ScanType scanType, ScanClockContext clockContext)
    {
        var baseMessage = scanType == ScanType.CheckIn
            ? "เช็คเข้าปฏิบัติราชการสำเร็จ"
            : "เช็คออกจากการปฏิบัติราชการสำเร็จ";

        if (!clockContext.ClockAnomaly)
        {
            return baseMessage;
        }

        if (string.Equals(clockContext.Source, "client", StringComparison.OrdinalIgnoreCase) &&
            clockContext.SkewMinutes.HasValue)
        {
            return $"{baseMessage} (เวลาเครื่องต่างจากเซิร์ฟเวอร์ {clockContext.SkewMinutes.Value:0.##} นาที)";
        }

        return $"{baseMessage} (เวลาเครื่องผิดปกติ ระบบใช้เวลาเซิร์ฟเวอร์)";
    }

    private static ScanType DetermineCheckType(
        IReadOnlyList<TeacherAttendanceTransaction> todayTransactions,
        DateTime scanTime,
        TimeSpan checkOutStartTime,
        ScanType? requestedType)
    {
        if (requestedType.HasValue)
        {
            return requestedType.Value;
        }

        var hasCheckIn = todayTransactions.Any(x => x.ScanType == ScanType.CheckIn);
        var hasCheckOut = todayTransactions.Any(x => x.ScanType == ScanType.CheckOut);

        if (!hasCheckIn)
        {
            return ScanType.CheckIn;
        }

        if (!hasCheckOut && scanTime.TimeOfDay >= checkOutStartTime)
        {
            return ScanType.CheckOut;
        }

        return ScanType.CheckIn;
    }

    private static (bool CanCreate, bool IsDuplicate, string Message) ValidateDailyScanRule(
        ScanType scanType,
        DateTime scanTime,
        IReadOnlyList<TeacherAttendanceTransaction> todayTransactions,
        SystemSetting settings)
    {
        var hasCheckIn = todayTransactions.Any(x => x.ScanType == ScanType.CheckIn);
        var hasCheckOut = todayTransactions.Any(x => x.ScanType == ScanType.CheckOut);
        var latestScan = todayTransactions.OrderByDescending(x => x.ScanTime).FirstOrDefault();
        var duplicateWindow = TimeSpan.FromMinutes(settings.DuplicateWindowMinutes);

        if (scanType == ScanType.CheckIn)
        {
            if (hasCheckIn)
            {
                return (false, true, "วันนี้ลงเวลาเข้าปฏิบัติราชการแล้ว");
            }

            if (latestScan is not null && (scanTime - latestScan.ScanTime) <= duplicateWindow)
            {
                return (false, true, "สแกนซ้ำในช่วงเวลาใกล้เคียง");
            }

            return (true, false, string.Empty);
        }

        if (!hasCheckIn)
        {
            return (false, false, "ยังไม่มีการลงเวลาเข้าปฏิบัติราชการในวันนี้");
        }

        if (scanTime.TimeOfDay < settings.TeacherCheckOutStartTime)
        {
            return (false, false, $"ยังไม่ถึงเวลาลงเวลากลับ (เริ่มได้ {settings.TeacherCheckOutStartTime:hh\\:mm} น.)");
        }

        if (hasCheckOut)
        {
            return (false, true, "วันนี้ลงเวลาออกจากการปฏิบัติราชการแล้ว");
        }

        if (latestScan is not null && (scanTime - latestScan.ScanTime) <= duplicateWindow)
        {
            return (false, true, "สแกนซ้ำในช่วงเวลาใกล้เคียง");
        }

        return (true, false, string.Empty);
    }

    private static string GetExtensionFromContentType(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".jpg"
        };
    }

    private async Task<int?> ResolveDeviceIdAsync(string? stationCode, string? stationToken, CancellationToken cancellationToken)
    {
        var deviceId = await _scanDeviceLookupService.GetActiveDeviceIdAsync(stationCode, stationToken, cancellationToken);
        if (deviceId.HasValue)
        {
            await _scanDeviceLookupService.TouchLastSeenAsync(deviceId.Value, cancellationToken);
        }

        return deviceId;
    }

    private async Task UpsertDailySummaryAsync(
        TeacherDirectoryItem teacher,
        DateTime date,
        SystemSetting settings,
        CancellationToken cancellationToken)
    {
        var targetDate = date.Date;
        var transactions = await _dbContext.TeacherAttendanceTransactions
            .Where(x => x.UserId == teacher.UserId && x.ScanDate == targetDate && !x.IsDuplicate)
            .AsNoTracking()
            .OrderBy(x => x.ScanTime)
            .ToListAsync(cancellationToken);

        var summary = await _dbContext.TeacherAttendanceDailySummaries
            .FirstOrDefaultAsync(x => x.UserId == teacher.UserId && x.Date == targetDate, cancellationToken);

        if (summary is null)
        {
            summary = new TeacherAttendanceDailySummary
            {
                UserId = teacher.UserId,
                Date = targetDate
            };

            _dbContext.TeacherAttendanceDailySummaries.Add(summary);
        }

        var payload = BuildSummaryPayload(transactions, settings);
        summary.FirstCheckInTime = payload.FirstCheckIn;
        summary.LastCheckOutTime = payload.LastCheckOut;
        summary.TotalScans = transactions.Count;
        summary.IsPresent = payload.IsPresent;
        summary.AttendanceStatus = payload.Status;
        summary.Remark = payload.Remark;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static TeacherAttendanceSummaryPayload BuildSummaryPayload(
        IReadOnlyList<TeacherAttendanceTransaction> teacherTransactions,
        SystemSetting settings)
    {
        var firstCheckIn = teacherTransactions
            .Where(x => x.ScanType == ScanType.CheckIn)
            .Select(x => (DateTime?)x.ScanTime)
            .FirstOrDefault();

        var lastCheckOut = teacherTransactions
            .Where(x => x.ScanType == ScanType.CheckOut)
            .Select(x => (DateTime?)x.ScanTime)
            .LastOrDefault();

        var status = AttendanceStatus.Absent;
        var isPresent = teacherTransactions.Count > 0;
        var remark = string.Empty;

        if (!isPresent)
        {
            status = AttendanceStatus.Absent;
            remark = "ไม่มาปฏิบัติราชการ";
        }
        else if (firstCheckIn.HasValue && !lastCheckOut.HasValue)
        {
            status = AttendanceStatus.PendingCheckout;
            remark = "ลงเวลามาแล้ว ยังไม่ลงเวลากลับ";
        }
        else if (firstCheckIn.HasValue && lastCheckOut.HasValue)
        {
            status = AttendanceStatus.Present;
            remark = "มาปฏิบัติราชการ";
        }
        else
        {
            status = AttendanceStatus.Partial;
            remark = "ข้อมูลไม่สมบูรณ์";
        }

        if (firstCheckIn.HasValue && firstCheckIn.Value.TimeOfDay > settings.TeacherLateAfterTime)
        {
            remark = $"{remark} (มาสาย)";
        }

        return new TeacherAttendanceSummaryPayload(firstCheckIn, lastCheckOut, status, isPresent, remark);
    }

    private sealed record ScanClockContext(
        DateTime ScanTime,
        string Source,
        bool ClockAnomaly,
        decimal? SkewMinutes);

    private sealed record TeacherAttendanceSummaryPayload(
        DateTime? FirstCheckIn,
        DateTime? LastCheckOut,
        AttendanceStatus Status,
        bool IsPresent,
        string Remark);

    private sealed record TeacherDirectoryItem(
        string UserId,
        string Username,
        string FullName,
        string RoleName,
        int? AssignedClassroomId,
        string AssignedClassroomName);
}
