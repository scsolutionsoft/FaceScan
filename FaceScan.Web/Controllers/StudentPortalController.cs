using System.Globalization;
using System.Security.Claims;
using FaceScan.Web.Data;
using FaceScan.Web.Helpers;
using FaceScan.Web.Models.Entities;
using FaceScan.Web.Models.Enums;
using FaceScan.Web.Services.Interfaces;
using FaceScan.Web.ViewModels.StudentPortal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FaceScan.Web.Controllers;

[Authorize(Roles = "Student")]
public class StudentPortalController : Controller
{
    private static readonly HashSet<string> AllowedCapturedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private readonly ApplicationDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;
    private readonly IAttendanceService _attendanceService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IFaceRecognitionService _faceRecognitionService;
    private readonly IAuditLogService _auditLogService;
    private readonly ISystemSettingService _systemSettingService;

    public StudentPortalController(
        ApplicationDbContext dbContext,
        IWebHostEnvironment environment,
        IAttendanceService attendanceService,
        IFileStorageService fileStorageService,
        IFaceRecognitionService faceRecognitionService,
        IAuditLogService auditLogService,
        ISystemSettingService systemSettingService)
    {
        _dbContext = dbContext;
        _environment = environment;
        _attendanceService = attendanceService;
        _fileStorageService = fileStorageService;
        _faceRecognitionService = faceRecognitionService;
        _auditLogService = auditLogService;
        _systemSettingService = systemSettingService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var student = await GetCurrentStudentWithRelatedDataAsync(cancellationToken);
        if (student is null)
        {
            return Forbid();
        }

        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);
        var today = DateTime.Today;
        await _attendanceService.BuildDailySummaryAsync(today, cancellationToken);

        var todaySummary = await _dbContext.AttendanceDailySummaries
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.StudentId == student.Id && x.Date == today, cancellationToken);

        var model = new StudentPortalIndexViewModel
        {
            StudentCode = student.StudentCode,
            StudentName = student.FullName,
            GradeLevel = student.GradeLevel?.Name ?? "-",
            Classroom = student.Classroom?.Name ?? "-",
            ProfilePhotoPath = student.StudentPhotos
                .OrderByDescending(x => x.IsPrimary)
                .ThenByDescending(x => x.CapturedAt)
                .Select(x => x.FilePath)
                .FirstOrDefault(),
            EnrollmentStatus = student.FaceProfile?.EnrollmentStatus ?? EnrollmentStatus.NotRegistered,
            PhotoCount = student.StudentPhotos.Count,
            ProfilePhotoOptions = student.StudentPhotos
                .OrderByDescending(x => x.IsPrimary)
                .ThenByDescending(x => x.CapturedAt)
                .Select(x => new StudentPortalProfilePhotoViewModel
                {
                    PhotoId = x.Id,
                    FilePath = x.FilePath,
                    IsPrimary = x.IsPrimary,
                    CapturedAt = x.CapturedAt
                })
                .ToList(),
            TodayCheckIn = todaySummary?.FirstCheckInTime,
            TodayCheckOut = todaySummary?.LastCheckOutTime,
            IsLateToday = todaySummary?.FirstCheckInTime is DateTime t && t.TimeOfDay > settings.LateAfterTime,
            LateAfterTime = settings.LateAfterTime,
            IsStudentCareEnabled = settings.EnableStudentCareModule,
            IsBehaviorScoreEnabled = settings.EnableBehaviorScoreModule,
            IsGoodnessBankEnabled = settings.EnableGoodnessBankModule,
            IsHomeVisitEnabled = settings.EnableHomeVisitModule,
            IsWasteBankEnabled = settings.EnableWasteBankModule
        };

        if (settings.EnableStudentCareModule)
        {
            var profile = await _dbContext.StudentCareProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.StudentId == student.Id, cancellationToken);
            model.HasHomeLocation = profile?.HomeLatitude.HasValue == true && profile.HomeLongitude.HasValue;
            model.HomeLatitude = profile?.HomeLatitude;
            model.HomeLongitude = profile?.HomeLongitude;
            model.HomeLocationSharedAt = profile?.HomeLocationSharedAt;

            var academicYearId = settings.AcademicYearCurrentId ?? student.AcademicYearId;

            if (settings.EnableBehaviorScoreModule)
            {
                var behaviorTransactions = await _dbContext.BehaviorScoreTransactions
                    .AsNoTracking()
                    .Where(x => x.StudentId == student.Id &&
                                x.AcademicYearId == academicYearId &&
                                x.Status == StudentCareRecordStatus.Approved)
                    .OrderByDescending(x => x.RecordedAt)
                    .ToListAsync(cancellationToken);

                model.BehaviorScore = settings.StudentCareInitialBehaviorScore + behaviorTransactions.Sum(x => x.ScoreChange);
                model.RecentBehaviorTransactions = behaviorTransactions
                    .Take(5)
                    .Select(x => new StudentPortalCareTransactionViewModel
                    {
                        RecordedAt = x.RecordedAt,
                        Category = x.Category,
                        Value = x.ScoreChange,
                        Description = x.Reason
                    })
                    .ToList();
            }

            if (settings.EnableGoodnessBankModule)
            {
                var goodnessTransactions = await _dbContext.GoodnessBankTransactions
                    .AsNoTracking()
                    .Where(x => x.StudentId == student.Id &&
                                x.AcademicYearId == academicYearId &&
                                x.Status == StudentCareRecordStatus.Approved)
                    .OrderByDescending(x => x.RecordedAt)
                    .ToListAsync(cancellationToken);

                model.GoodnessPoint = goodnessTransactions.Sum(x => x.Point);
                model.RecentGoodnessTransactions = goodnessTransactions
                    .Take(5)
                    .Select(x => new StudentPortalCareTransactionViewModel
                    {
                        RecordedAt = x.RecordedAt,
                        Category = x.GoodnessType,
                        Value = x.Point,
                        Description = x.Description
                    })
                    .ToList();
            }

            if (settings.EnableWasteBankModule)
            {
                var wasteTransactions = await _dbContext.WasteBankTransactions
                    .AsNoTracking()
                    .Where(x => x.StudentId == student.Id && x.AcademicYearId == academicYearId)
                    .OrderByDescending(x => x.RecordedAt)
                    .ToListAsync(cancellationToken);

                model.WasteBankTotalWeightKg = wasteTransactions.Sum(x => x.WeightKg);
                model.WasteBankTotalAmount = wasteTransactions.Sum(x => x.Amount);
                model.RecentWasteBankTransactions = wasteTransactions
                    .Take(5)
                    .Select(x => new StudentPortalWasteBankTransactionViewModel
                    {
                        RecordedAt = x.RecordedAt,
                        WasteType = x.WasteType,
                        WeightKg = x.WeightKg,
                        PricePerKg = x.PricePerKg,
                        Amount = x.Amount,
                        Note = x.Note
                    })
                    .ToList();
            }
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetProfilePhoto(int photoId, CancellationToken cancellationToken)
    {
        var student = await GetCurrentStudentWithRelatedDataAsync(cancellationToken);
        if (student is null)
        {
            return Forbid();
        }

        var target = student.StudentPhotos.FirstOrDefault(x => x.Id == photoId);
        if (target is null)
        {
            TempData["Error"] = "ไม่พบรูปที่ต้องการใช้เป็นรูปหน้าตนเอง";
            return RedirectToAction(nameof(Index));
        }

        foreach (var photo in student.StudentPhotos)
        {
            photo.IsPrimary = photo.Id == photoId;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var ipAddress = HttpContextHelper.GetIpAddress(HttpContext);
        await _auditLogService.LogAsync(
            userId,
            "StudentSetProfilePhoto",
            "Student",
            student.Id.ToString(),
            $"เปลี่ยนรูปหน้าตนเองเป็นรหัสรูป {photoId}",
            ipAddress,
            cancellationToken);

        TempData["Success"] = "เปลี่ยนรูปหน้าตนเองเรียบร้อย";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ShareHomeLocation(StudentHomeLocationViewModel model, CancellationToken cancellationToken)
    {
        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);
        if (!settings.EnableStudentCareModule || !settings.EnableHomeVisitModule)
        {
            return NotFound();
        }

        var student = await GetCurrentStudentWithRelatedDataAsync(cancellationToken);
        if (student is null)
        {
            return Forbid();
        }

        if (!ModelState.IsValid ||
            model.Latitude < -90 || model.Latitude > 90 ||
            model.Longitude < -180 || model.Longitude > 180)
        {
            TempData["Error"] = "พิกัดบ้านไม่ถูกต้อง";
            return RedirectToAction(nameof(Index));
        }

        var profile = await _dbContext.StudentCareProfiles
            .FirstOrDefaultAsync(x => x.StudentId == student.Id, cancellationToken);
        if (profile is null)
        {
            profile = new StudentCareProfile { StudentId = student.Id };
            _dbContext.StudentCareProfiles.Add(profile);
        }

        profile.HomeLatitude = model.Latitude;
        profile.HomeLongitude = model.Longitude;
        profile.HomeLocationSharedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            "StudentShareHomeLocation",
            "StudentCareProfile",
            student.Id.ToString(),
            $"{student.StudentCode} shared home location",
            HttpContextHelper.GetIpAddress(HttpContext),
            cancellationToken);

        TempData["Success"] = "บันทึกพิกัดบ้านเรียบร้อย";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> FaceEnrollment(CancellationToken cancellationToken)
    {
        var student = await GetCurrentStudentWithRelatedDataAsync(cancellationToken);
        if (student is null)
        {
            return Forbid();
        }

        var model = new StudentFaceEnrollmentViewModel
        {
            StudentId = student.Id,
            StudentCode = student.StudentCode,
            StudentName = student.FullName,
            Classroom = student.Classroom?.Name ?? "-",
            EnrollmentStatus = student.FaceProfile?.EnrollmentStatus ?? EnrollmentStatus.NotRegistered,
            CurrentPhotoCount = student.StudentPhotos.Count,
            PhotoPaths = student.StudentPhotos
                .OrderByDescending(x => x.CapturedAt)
                .Select(x => x.FilePath)
                .ToList(),
            Photos = student.StudentPhotos
                .OrderByDescending(x => x.IsPrimary)
                .ThenByDescending(x => x.CapturedAt)
                .Select(x => new StudentFaceEnrollmentPhotoViewModel
                {
                    PhotoId = x.Id,
                    FilePath = x.FilePath,
                    ContentType = x.ContentType,
                    FileSizeBytes = ResolvePhotoFileSize(x.FilePath),
                    IsPrimary = x.IsPrimary,
                    CapturedAt = x.CapturedAt
                })
                .ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FaceEnrollment(StudentFaceEnrollmentViewModel model, CancellationToken cancellationToken)
    {
        var student = await GetCurrentStudentWithRelatedDataAsync(cancellationToken);
        if (student is null || student.Id != model.StudentId)
        {
            return Forbid();
        }

        var uploadedFiles = (model.Files ?? [])
            .Where(x => x is { Length: > 0 })
            .ToList();
        var capturedFiles = ConvertCapturedImagesToFiles(model.CapturedImages ?? []);
        var incomingFiles = uploadedFiles.Concat(capturedFiles.Select(x => x.FormFile)).ToList();

        if (incomingFiles.Count == 0)
        {
            TempData["Error"] = "กรุณาเลือกหรือถ่ายรูปอย่างน้อย 1 รูป";
            return RedirectToAction(nameof(FaceEnrollment));
        }

        var hasPrimary = student.StudentPhotos.Any(x => x.IsPrimary);
        var addedCount = 0;
        var skippedCount = 0;

        try
        {
            foreach (var file in incomingFiles)
            {
                try
                {
                    var path = await _fileStorageService.SaveStudentPhotoAsync(file, student.Id, cancellationToken);
                    var photo = new StudentPhoto
                    {
                        StudentId = student.Id,
                        FilePath = path,
                        FileName = Path.GetFileName(path),
                        ContentType = file.ContentType,
                        IsPrimary = !hasPrimary,
                        CapturedAt = DateTime.UtcNow,
                        QualityScore = 0.90m
                    };

                    student.StudentPhotos.Add(photo);
                    hasPrimary = true;
                    addedCount++;
                }
                catch (InvalidOperationException)
                {
                    skippedCount++;
                }
            }

            if (addedCount == 0)
            {
                TempData["Error"] = skippedCount > 0
                    ? "ไม่สามารถเพิ่มรูปที่ถูกต้องได้ กรุณาใช้ไฟล์ JPG, PNG หรือ WEBP และขนาดไม่เกินที่กำหนด"
                    : "กรุณาเลือกหรือถ่ายรูปอย่างน้อย 1 รูป";
                return RedirectToAction(nameof(FaceEnrollment));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            var imagePaths = student.StudentPhotos
                .Select(x => x.FilePath)
                .Distinct()
                .ToList();
            var enrollResult = await _faceRecognitionService.EnrollStudentAsync(student.Id, imagePaths);

            var message = enrollResult.Message;
            if (skippedCount > 0)
            {
                message = $"{message} (ข้ามไฟล์ที่ไม่ถูกต้อง: {skippedCount})";
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var ipAddress = HttpContextHelper.GetIpAddress(HttpContext);
            await _auditLogService.LogAsync(
                userId,
                "StudentSelfEnrollFace",
                "Student",
                student.Id.ToString(),
                message,
                ipAddress,
                cancellationToken);

            TempData[enrollResult.Success ? "Success" : "Error"] = message;
            return RedirectToAction(nameof(FaceEnrollment));
        }
        finally
        {
            DisposeCapturedStreams(capturedFiles);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFaceEnrollmentPhoto(int photoId, CancellationToken cancellationToken)
    {
        var student = await GetCurrentStudentWithRelatedDataAsync(cancellationToken);
        if (student is null)
        {
            return Forbid();
        }

        var photo = student.StudentPhotos.FirstOrDefault(x => x.Id == photoId);
        if (photo is null)
        {
            TempData["Error"] = "ไม่พบรูปที่ต้องการลบ";
            return RedirectToAction(nameof(FaceEnrollment));
        }

        var filePath = photo.FilePath;
        _dbContext.StudentPhotos.Remove(photo);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _fileStorageService.DeleteFileIfExistsAsync(filePath);

        await NormalizePrimaryFacePhotoAsync(student.Id, cancellationToken);
        var remainingPaths = await _dbContext.StudentPhotos
            .AsNoTracking()
            .Where(x => x.StudentId == student.Id)
            .OrderByDescending(x => x.IsPrimary)
            .ThenByDescending(x => x.CapturedAt)
            .Select(x => x.FilePath)
            .ToListAsync(cancellationToken);

        if (remainingPaths.Count == 0)
        {
            await _faceRecognitionService.RemoveStudentProfileAsync(student.Id);
        }
        else
        {
            await _faceRecognitionService.EnrollStudentAsync(student.Id, remainingPaths);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var ipAddress = HttpContextHelper.GetIpAddress(HttpContext);
        await _auditLogService.LogAsync(
            userId,
            "StudentSelfDeleteFacePhoto",
            "Student",
            student.Id.ToString(),
            $"ลบรูปใบหน้ารหัส {photoId}",
            ipAddress,
            cancellationToken);

        TempData["Success"] = "ลบรูปใบหน้าเรียบร้อย";
        return RedirectToAction(nameof(FaceEnrollment));
    }

    private async Task NormalizePrimaryFacePhotoAsync(int studentId, CancellationToken cancellationToken)
    {
        var photos = await _dbContext.StudentPhotos
            .Where(x => x.StudentId == studentId)
            .OrderByDescending(x => x.IsPrimary)
            .ThenByDescending(x => x.CapturedAt)
            .ToListAsync(cancellationToken);

        if (photos.Count == 0)
        {
            return;
        }

        var primary = photos.FirstOrDefault(x => x.IsPrimary) ?? photos[0];
        foreach (var photo in photos)
        {
            photo.IsPrimary = photo.Id == primary.Id;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private long? ResolvePhotoFileSize(string filePath)
    {
        if (string.IsNullOrWhiteSpace(_environment.WebRootPath) || string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        var relativePath = filePath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(_environment.WebRootPath, relativePath);
        return System.IO.File.Exists(fullPath) ? new FileInfo(fullPath).Length : null;
    }

    [HttpGet]
    public async Task<IActionResult> Attendance([FromQuery] StudentAttendanceReportFilterViewModel filter, CancellationToken cancellationToken)
    {
        var student = await GetCurrentStudentWithRelatedDataAsync(cancellationToken);
        if (student is null)
        {
            return Forbid();
        }

        var model = await BuildAttendanceReportAsync(student, filter, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> AttendancePrint([FromQuery] StudentAttendanceReportFilterViewModel filter, CancellationToken cancellationToken)
    {
        var student = await GetCurrentStudentWithRelatedDataAsync(cancellationToken);
        if (student is null)
        {
            return Forbid();
        }

        var model = await BuildAttendanceReportAsync(student, filter, cancellationToken);
        return View(model);
    }

    private async Task<StudentAttendanceReportViewModel> BuildAttendanceReportAsync(
        Student student,
        StudentAttendanceReportFilterViewModel filter,
        CancellationToken cancellationToken)
    {
        var startDate = (filter.StartDate ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)).Date;
        var endDate = (filter.EndDate ?? DateTime.Today).Date;

        if (endDate < startDate)
        {
            (startDate, endDate) = (endDate, startDate);
        }

        var maxPeriod = startDate.AddDays(92);
        if (endDate > maxPeriod)
        {
            endDate = maxPeriod;
        }

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            await _attendanceService.BuildDailySummaryAsync(date, cancellationToken);
        }

        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);
        var rows = await _dbContext.AttendanceDailySummaries
            .AsNoTracking()
            .Where(x => x.StudentId == student.Id && x.Date >= startDate && x.Date <= endDate)
            .OrderByDescending(x => x.Date)
            .Select(x => new StudentAttendanceReportRowViewModel
            {
                Date = x.Date,
                FirstCheckInTime = x.FirstCheckInTime,
                LastCheckOutTime = x.LastCheckOutTime,
                AttendanceStatus = x.AttendanceStatus
            })
            .ToListAsync(cancellationToken);

        var transactions = await _dbContext.AttendanceTransactions
            .AsNoTracking()
            .Where(x => x.StudentId == student.Id && x.ScanDate >= startDate && x.ScanDate <= endDate && !x.IsDuplicate)
            .Select(x => new
            {
                x.ScanDate,
                x.ScanType,
                x.ScanTime,
                x.Latitude,
                x.Longitude
            })
            .ToListAsync(cancellationToken);

        var txByDate = transactions
            .GroupBy(x => x.ScanDate)
            .ToDictionary(x => x.Key, x => x.OrderBy(t => t.ScanTime).ToList());

        foreach (var row in rows)
        {
            row.IsLate = row.FirstCheckInTime.HasValue && row.FirstCheckInTime.Value.TimeOfDay > settings.LateAfterTime;
            row.StatusLabel = BuildStatusLabel(row.AttendanceStatus, row.IsLate);

            if (!txByDate.TryGetValue(row.Date.Date, out var dayTx))
            {
                continue;
            }

            var checkInTx = dayTx
                .Where(x => x.ScanType == ScanType.CheckIn)
                .OrderBy(x => x.ScanTime)
                .FirstOrDefault();
            var checkOutTx = dayTx
                .Where(x => x.ScanType == ScanType.CheckOut)
                .OrderByDescending(x => x.ScanTime)
                .FirstOrDefault();

            row.CheckInLatitude = checkInTx?.Latitude;
            row.CheckInLongitude = checkInTx?.Longitude;
            row.CheckOutLatitude = checkOutTx?.Latitude;
            row.CheckOutLongitude = checkOutTx?.Longitude;
            row.CheckInMapUrl = BuildMapUrl(checkInTx?.Latitude, checkInTx?.Longitude);
            row.CheckOutMapUrl = BuildMapUrl(checkOutTx?.Latitude, checkOutTx?.Longitude);
        }

        return new StudentAttendanceReportViewModel
        {
            StudentCode = student.StudentCode,
            StudentName = student.FullName,
            GradeLevel = student.GradeLevel?.Name ?? "-",
            Classroom = student.Classroom?.Name ?? "-",
            LateAfterTime = settings.LateAfterTime,
            Filter = new StudentAttendanceReportFilterViewModel
            {
                StartDate = startDate,
                EndDate = endDate
            },
            Rows = rows,
            PresentCount = rows.Count(x => x.AttendanceStatus == AttendanceStatus.Present && !x.IsLate),
            LateCount = rows.Count(x => x.IsLate),
            AbsentCount = rows.Count(x => x.AttendanceStatus == AttendanceStatus.Absent),
            PendingCheckoutCount = rows.Count(x => x.AttendanceStatus == AttendanceStatus.PendingCheckout)
        };
    }

    private async Task<Student?> GetCurrentStudentWithRelatedDataAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var studentId = await _dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => x.StudentId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!studentId.HasValue)
        {
            return null;
        }

        return await _dbContext.Students
            .Include(x => x.GradeLevel)
            .Include(x => x.Classroom)
            .Include(x => x.FaceProfile)
            .Include(x => x.StudentPhotos)
            .FirstOrDefaultAsync(x => x.Id == studentId.Value && x.IsActive, cancellationToken);
    }

    private static List<(IFormFile FormFile, MemoryStream Stream)> ConvertCapturedImagesToFiles(IReadOnlyList<string> capturedImages)
    {
        var files = new List<(IFormFile FormFile, MemoryStream Stream)>();
        var index = 0;

        foreach (var encoded in capturedImages.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            if (!TryDecodeCapturedImage(encoded, out var bytes, out var contentType, out var extension))
            {
                continue;
            }

            var stream = new MemoryStream(bytes);
            var fileName = $"camera_{DateTime.UtcNow:yyyyMMddHHmmss}_{index}{extension}";
            var formFile = new FormFile(stream, 0, stream.Length, $"CapturedImage{index}", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };

            files.Add((formFile, stream));
            index++;
        }

        return files;
    }

    private static void DisposeCapturedStreams(IEnumerable<(IFormFile FormFile, MemoryStream Stream)> capturedFiles)
    {
        foreach (var (_, stream) in capturedFiles)
        {
            stream.Dispose();
        }
    }

    private static bool TryDecodeCapturedImage(
        string encodedValue,
        out byte[] bytes,
        out string contentType,
        out string extension)
    {
        bytes = [];
        contentType = "image/jpeg";
        extension = ".jpg";

        if (string.IsNullOrWhiteSpace(encodedValue))
        {
            return false;
        }

        var payload = encodedValue.Trim();
        if (payload.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var commaIndex = payload.IndexOf(',');
            if (commaIndex <= 0)
            {
                return false;
            }

            var metadata = payload.Substring(5, commaIndex - 5);
            var separatorIndex = metadata.IndexOf(';');
            if (separatorIndex < 0 || !metadata.EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            contentType = metadata[..separatorIndex];
            if (!AllowedCapturedContentTypes.Contains(contentType))
            {
                return false;
            }

            extension = contentType switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                _ => ".jpg"
            };

            payload = payload[(commaIndex + 1)..];
        }

        try
        {
            bytes = Convert.FromBase64String(payload);
            return bytes.Length > 0;
        }
        catch
        {
            return false;
        }
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
}

