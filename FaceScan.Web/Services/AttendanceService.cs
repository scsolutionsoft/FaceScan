using FaceScan.Web.Data;
using FaceScan.Web.Models.Entities;
using FaceScan.Web.Models.Enums;
using FaceScan.Web.Services.Interfaces;
using FaceScan.Web.ViewModels.Attendance;
using FaceScan.Web.ViewModels.Scan;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace FaceScan.Web.Services;

public class AttendanceService : IAttendanceService
{
    private static readonly string[] ClientTimeFormats =
    [
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.FFF",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFF"
    ];

    // Accept up to one day gap to tolerate timezone drift; beyond this uses server clock.
    private const double MaxAcceptableSkewMinutes = 1440d;
    private const double WarnSkewMinutes = 10d;
    private static readonly TimeSpan FixedCheckoutThreshold = new(15, 30, 0);

    private readonly ApplicationDbContext _dbContext;
    private readonly IFaceRecognitionServiceResolver _faceRecognitionServiceResolver;
    private readonly IFileStorageService _fileStorageService;
    private readonly IScanDeviceLookupService _scanDeviceLookupService;
    private readonly ISystemSettingService _systemSettingService;

    public AttendanceService(
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

    public async Task<ScanProcessResult> ProcessScanAsync(
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

        var faceMatch = await faceRecognitionService.VerifyAsync(rawMemory, recognitionProfile);
        if (!faceMatch.Success || !faceMatch.StudentId.HasValue)
        {
            return new ScanProcessResult
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

        var student = await _dbContext.Students
            .AsNoTracking()
            .Include(x => x.GradeLevel)
            .Include(x => x.Classroom)
            .FirstOrDefaultAsync(x => x.Id == faceMatch.StudentId.Value && x.IsActive, cancellationToken);

        if (student is null)
        {
            return new ScanProcessResult
            {
                Success = false,
                Message = "ไม่พบข้อมูลนักเรียน",
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
            return new ScanProcessResult
            {
                Success = false,
                Message = "ความมั่นใจต่ำกว่าเกณฑ์ที่กำหนด",
                StudentId = student.Id,
                StudentCode = student.StudentCode,
                StudentName = student.FullName,
                GradeLevel = student.GradeLevel?.Name ?? string.Empty,
                Classroom = student.Classroom?.Name ?? string.Empty,
                ScanTime = scanTime,
                ConfidenceScore = faceMatch.ConfidenceScore,
                Provider = faceMatch.Provider,
                RecognitionProfile = recognitionProfile,
                TimeSource = clockContext.Source,
                ClockAnomaly = clockContext.ClockAnomaly,
                ClockSkewMinutes = clockContext.SkewMinutes
            };
        }

        var todayTransactions = await _dbContext.AttendanceTransactions
            .Where(x => x.StudentId == student.Id && x.ScanDate == scanTime.Date && !x.IsDuplicate)
            .OrderBy(x => x.ScanTime)
            .ToListAsync(cancellationToken);

        var effectiveCheckOutStartTime = ResolveEffectiveCheckOutStartTime(settings.CheckOutStartTime);
        var scanType = DetermineCheckType(todayTransactions, scanTime, effectiveCheckOutStartTime, request.RequestedType);
        var validation = ValidateDailyScanRule(scanType, scanTime, todayTransactions, settings, effectiveCheckOutStartTime);
        var isAbnormalFirstCheckIn = todayTransactions.Count == 0
            && scanType == ScanType.CheckIn
            && scanTime.TimeOfDay >= FixedCheckoutThreshold;

        if (!validation.CanCreate)
        {
            return new ScanProcessResult
            {
                Success = false,
                Message = validation.Message,
                StudentId = student.Id,
                StudentCode = student.StudentCode,
                StudentName = student.FullName,
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

        var transaction = new AttendanceTransaction
        {
            StudentId = student.Id,
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

        _dbContext.AttendanceTransactions.Add(transaction);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await UpsertDailySummaryAsync(student, scanTime.Date, settings, cancellationToken);

        return new ScanProcessResult
        {
            Success = true,
            Message = BuildScanSuccessMessage(scanType, clockContext, isAbnormalFirstCheckIn),
            StudentId = student.Id,
            StudentCode = student.StudentCode,
            StudentName = student.FullName,
            GradeLevel = student.GradeLevel?.Name ?? string.Empty,
            Classroom = student.Classroom?.Name ?? string.Empty,
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
        int studentId,
        DateTime scanTime,
        ScanType? requestedType = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);
        var todayTransactions = await _dbContext.AttendanceTransactions
            .Where(x => x.StudentId == studentId && x.ScanDate == scanTime.Date && !x.IsDuplicate)
            .OrderBy(x => x.ScanTime)
            .ToListAsync(cancellationToken);

        var effectiveCheckOutStartTime = ResolveEffectiveCheckOutStartTime(settings.CheckOutStartTime);
        return DetermineCheckType(todayTransactions, scanTime, effectiveCheckOutStartTime, requestedType);
    }

    public async Task<bool> IsDuplicateScanAsync(int studentId, DateTime scanTime, CancellationToken cancellationToken = default)
    {
        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);
        var window = TimeSpan.FromMinutes(settings.DuplicateWindowMinutes);

        var lastScan = await _dbContext.AttendanceTransactions
            .Where(x => x.StudentId == studentId)
            .OrderByDescending(x => x.ScanTime)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastScan is null)
        {
            return false;
        }

        return (scanTime - lastScan.ScanTime) <= window;
    }

    public async Task BuildDailySummaryAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var targetDate = date.Date;
        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);

        var activeStudents = await _dbContext.Students
            .Where(x => x.IsActive)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var transactions = await _dbContext.AttendanceTransactions
            .Where(x => x.ScanDate == targetDate && !x.IsDuplicate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var existingSummaries = await _dbContext.AttendanceDailySummaries
            .Where(x => x.Date == targetDate)
            .ToListAsync(cancellationToken);

        var summaryByStudent = existingSummaries.ToDictionary(x => x.StudentId);

        foreach (var student in activeStudents)
        {
            var studentTransactions = transactions
                .Where(x => x.StudentId == student.Id)
                .OrderBy(x => x.ScanTime)
                .ToList();

            var firstCheckIn = studentTransactions
                .Where(x => x.ScanType == ScanType.CheckIn)
                .Select(x => (DateTime?)x.ScanTime)
                .FirstOrDefault();

            var lastCheckOut = studentTransactions
                .Where(x => x.ScanType == ScanType.CheckOut)
                .Select(x => (DateTime?)x.ScanTime)
                .LastOrDefault();

            var status = AttendanceStatus.Absent;
            var isPresent = studentTransactions.Count > 0;
            var remark = string.Empty;

            if (!isPresent)
            {
                status = AttendanceStatus.Absent;
                remark = "ไม่มาเรียน";
            }
            else if (firstCheckIn.HasValue && !lastCheckOut.HasValue)
            {
                status = AttendanceStatus.PendingCheckout;
                remark = "เช็คอินแล้ว ยังไม่เช็คเอาท์";
            }
            else if (firstCheckIn.HasValue && lastCheckOut.HasValue)
            {
                status = AttendanceStatus.Present;
                remark = "มาเรียน";
            }
            else
            {
                status = AttendanceStatus.Partial;
                remark = "ข้อมูลไม่สมบูรณ์";
            }

            if (firstCheckIn.HasValue && firstCheckIn.Value.TimeOfDay > settings.LateAfterTime)
            {
                remark = $"{remark} (มาสาย)";
            }

            if (firstCheckIn.HasValue && firstCheckIn.Value.TimeOfDay >= FixedCheckoutThreshold)
            {
                remark = $"{remark} (ผิดปกติ: สแกนครั้งแรกหลัง 15:30)";
            }

            if (!summaryByStudent.TryGetValue(student.Id, out var summary))
            {
                summary = new AttendanceDailySummary
                {
                    StudentId = student.Id,
                    Date = targetDate
                };
                _dbContext.AttendanceDailySummaries.Add(summary);
            }

            summary.ClassroomId = student.ClassroomId;
            summary.FirstCheckInTime = firstCheckIn;
            summary.LastCheckOutTime = lastCheckOut;
            summary.TotalScans = studentTransactions.Count;
            summary.IsPresent = isPresent;
            summary.AttendanceStatus = status;
            summary.Remark = remark;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AttendanceDailySummary>> GetTodaySummaryAsync(
        AttendanceFilterViewModel filter,
        CancellationToken cancellationToken = default)
    {
        var date = (filter.Date ?? DateTime.Today).Date;

        var query = _dbContext.AttendanceDailySummaries
            .Include(x => x.Student)
                .ThenInclude(x => x!.GradeLevel)
            .Include(x => x.Student)
                .ThenInclude(x => x!.Classroom)
            .Where(x => x.Date == date)
            .AsNoTracking();

        if (filter.ClassroomId.HasValue)
        {
            query = query.Where(x => x.ClassroomId == filter.ClassroomId.Value);
        }

        if (filter.GradeLevelId.HasValue)
        {
            query = query.Where(x => x.Student != null && x.Student.GradeLevelId == filter.GradeLevelId.Value);
        }

        if (filter.AcademicYearId.HasValue)
        {
            query = query.Where(x => x.Student != null && x.Student.AcademicYearId == filter.AcademicYearId.Value);
        }

        var list = await query
            .OrderBy(x => x.Student!.StudentCode)
            .ToListAsync(cancellationToken);

        return list;
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

    private static string BuildScanSuccessMessage(ScanType scanType, ScanClockContext clockContext, bool isAbnormalFirstCheckIn)
    {
        var baseMessage = scanType == ScanType.CheckIn
            ? "เช็คอินสำเร็จ"
            : "เช็คเอาท์สำเร็จ";

        if (isAbnormalFirstCheckIn)
        {
            baseMessage = $"{baseMessage} (ผิดปกติ: สแกนครั้งแรกหลัง 15:30)";
        }

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
        IReadOnlyList<AttendanceTransaction> todayTransactions,
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
        IReadOnlyList<AttendanceTransaction> todayTransactions,
        SystemSetting settings,
        TimeSpan checkOutStartTime)
    {
        var hasCheckIn = todayTransactions.Any(x => x.ScanType == ScanType.CheckIn);
        var hasCheckOut = todayTransactions.Any(x => x.ScanType == ScanType.CheckOut);
        var latestScan = todayTransactions.OrderByDescending(x => x.ScanTime).FirstOrDefault();
        var duplicateWindow = TimeSpan.FromMinutes(settings.DuplicateWindowMinutes);

        if (scanType == ScanType.CheckIn)
        {
            if (hasCheckIn)
            {
                return (false, true, "วันนี้สแกนเข้าเรียนแล้ว");
            }

            if (latestScan is not null && (scanTime - latestScan.ScanTime) <= duplicateWindow)
            {
                return (false, true, "สแกนซ้ำในช่วงเวลาใกล้เคียง");
            }

            return (true, false, string.Empty);
        }

        if (!hasCheckIn)
        {
            return (false, false, "ยังไม่มีการสแกนเข้าเรียนในวันนี้");
        }

        if (scanTime.TimeOfDay < checkOutStartTime)
        {
            return (false, false, $"ยังไม่ถึงเวลาสแกนกลับ (เริ่มได้ {checkOutStartTime:hh\\:mm} น.)");
        }

        if (hasCheckOut)
        {
            return (false, true, "วันนี้สแกนกลับบ้านแล้ว");
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
        Student student,
        DateTime date,
        SystemSetting settings,
        CancellationToken cancellationToken)
    {
        var targetDate = date.Date;
        var transactions = await _dbContext.AttendanceTransactions
            .Where(x => x.StudentId == student.Id && x.ScanDate == targetDate && !x.IsDuplicate)
            .AsNoTracking()
            .OrderBy(x => x.ScanTime)
            .ToListAsync(cancellationToken);

        var summary = await _dbContext.AttendanceDailySummaries
            .FirstOrDefaultAsync(x => x.StudentId == student.Id && x.Date == targetDate, cancellationToken);

        if (summary is null)
        {
            summary = new AttendanceDailySummary
            {
                StudentId = student.Id,
                Date = targetDate
            };

            _dbContext.AttendanceDailySummaries.Add(summary);
        }

        var payload = BuildSummaryPayload(transactions, settings);
        summary.ClassroomId = student.ClassroomId;
        summary.FirstCheckInTime = payload.FirstCheckIn;
        summary.LastCheckOutTime = payload.LastCheckOut;
        summary.TotalScans = transactions.Count;
        summary.IsPresent = payload.IsPresent;
        summary.AttendanceStatus = payload.Status;
        summary.Remark = payload.Remark;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static AttendanceSummaryPayload BuildSummaryPayload(
        IReadOnlyList<AttendanceTransaction> studentTransactions,
        SystemSetting settings)
    {
        var firstCheckIn = studentTransactions
            .Where(x => x.ScanType == ScanType.CheckIn)
            .Select(x => (DateTime?)x.ScanTime)
            .FirstOrDefault();

        var lastCheckOut = studentTransactions
            .Where(x => x.ScanType == ScanType.CheckOut)
            .Select(x => (DateTime?)x.ScanTime)
            .LastOrDefault();

        var status = AttendanceStatus.Absent;
        var isPresent = studentTransactions.Count > 0;
        var remark = string.Empty;

        if (!isPresent)
        {
            status = AttendanceStatus.Absent;
            remark = "ไม่มาเรียน";
        }
        else if (firstCheckIn.HasValue && !lastCheckOut.HasValue)
        {
            status = AttendanceStatus.PendingCheckout;
            remark = "เช็คอินแล้ว ยังไม่เช็คเอาท์";
        }
        else if (firstCheckIn.HasValue && lastCheckOut.HasValue)
        {
            status = AttendanceStatus.Present;
            remark = "มาเรียน";
        }
        else
        {
            status = AttendanceStatus.Partial;
            remark = "ข้อมูลไม่สมบูรณ์";
        }

        if (firstCheckIn.HasValue && firstCheckIn.Value.TimeOfDay > settings.LateAfterTime)
        {
            remark = $"{remark} (มาสาย)";
        }

        if (firstCheckIn.HasValue && firstCheckIn.Value.TimeOfDay >= FixedCheckoutThreshold)
        {
            remark = $"{remark} (ผิดปกติ: สแกนครั้งแรกหลัง 15:30)";
        }

        return new AttendanceSummaryPayload(firstCheckIn, lastCheckOut, status, isPresent, remark);
    }

    private sealed record AttendanceSummaryPayload(
        DateTime? FirstCheckIn,
        DateTime? LastCheckOut,
        AttendanceStatus Status,
        bool IsPresent,
        string Remark);

    private static TimeSpan ResolveEffectiveCheckOutStartTime(TimeSpan configuredValue)
    {
        return configuredValue < FixedCheckoutThreshold
            ? FixedCheckoutThreshold
            : configuredValue;
    }

    private sealed record ScanClockContext(
        DateTime ScanTime,
        string Source,
        bool ClockAnomaly,
        decimal? SkewMinutes);
}
