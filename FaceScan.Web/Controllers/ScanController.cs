using FaceScan.Web.Data;
using FaceScan.Web.Hubs;
using FaceScan.Web.Models;
using FaceScan.Web.Models.Entities;
using FaceScan.Web.Services.Interfaces;
using FaceScan.Web.Validators;
using FaceScan.Web.ViewModels.Scan;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FaceScan.Web.Controllers;

public class ScanController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAttendanceService _attendanceService;
    private readonly IFaceRecognitionServiceResolver _faceRecognitionServiceResolver;
    private readonly IScanDeviceLookupService _scanDeviceLookupService;
    private readonly ISystemSettingService _systemSettingService;
    private readonly IHubContext<ScanLiveHub> _scanLiveHub;
    private readonly IQueuedScanVerifyService _queuedScanVerifyService;

    public ScanController(
        ApplicationDbContext dbContext,
        IAttendanceService attendanceService,
        IFaceRecognitionServiceResolver faceRecognitionServiceResolver,
        IScanDeviceLookupService scanDeviceLookupService,
        ISystemSettingService systemSettingService,
        IHubContext<ScanLiveHub> scanLiveHub,
        IQueuedScanVerifyService queuedScanVerifyService)
    {
        _dbContext = dbContext;
        _attendanceService = attendanceService;
        _faceRecognitionServiceResolver = faceRecognitionServiceResolver;
        _scanDeviceLookupService = scanDeviceLookupService;
        _systemSettingService = systemSettingService;
        _scanLiveHub = scanLiveHub;
        _queuedScanVerifyService = queuedScanVerifyService;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Index(string? stationCode, string? stationToken, CancellationToken cancellationToken)
    {
        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);
        var recognitionProfile = FaceRecognitionProfiles.Auto;

        if (!User.Identity!.IsAuthenticated)
        {
            if (!HasStationCredential(stationCode, stationToken))
            {
                return Content("ไม่อนุญาตให้เข้าใช้งานหน้า Scan กรุณาระบุ stationCode และ stationToken ให้ถูกต้อง");
            }

            var validStation = await IsValidStationAsync(stationCode, stationToken, cancellationToken);
            if (!validStation)
            {
                return Content("ไม่อนุญาตให้เข้าใช้งานหน้า Scan กรุณาระบุ stationCode และ stationToken ให้ถูกต้อง");
            }
        }

        if (!string.IsNullOrWhiteSpace(stationCode) && !string.IsNullOrWhiteSpace(stationToken))
        {
            recognitionProfile = await _dbContext.ScanDevices
                .AsNoTracking()
                .Where(x => x.StationCode == stationCode && x.AccessToken == stationToken && x.IsActive)
                .Select(x => x.RecognitionProfile)
                .FirstOrDefaultAsync(cancellationToken) ?? FaceRecognitionProfiles.Auto;
        }

        return View(new ScanViewModel
        {
            StationCode = stationCode,
            StationToken = stationToken,
            SchoolName = settings.SchoolName,
            RecognitionProfile = recognitionProfile,
            IsPublicMode = false
        });
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Public(CancellationToken cancellationToken)
    {
        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);

        return View("Index", new ScanViewModel
        {
            SchoolName = settings.SchoolName,
            StationCode = string.Empty,
            StationToken = string.Empty,
            RecognitionProfile = FaceRecognitionProfiles.Auto,
            IsPublicMode = true
        });
    }

    [AllowAnonymous]
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Verify([FromForm] ScanVerifyRequestViewModel? request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new ScanVerifyResponseViewModel
            {
                Success = false,
                Message = "ข้อมูลคำขอไม่ถูกต้อง",
                RecognitionProfile = FaceRecognitionProfiles.Auto
            });
        }

        if (request.IsPublicMode)
        {
            request.StationCode = null;
            request.StationToken = null;
        }

        var error = ScanRequestValidator.Validate(request);
        if (error is not null)
        {
            return BadRequest(new ScanVerifyResponseViewModel
            {
                Success = false,
                Message = error,
                RecognitionProfile = _faceRecognitionServiceResolver.NormalizeProfile(request.RecognitionProfile)
            });
        }

        if (!User.Identity!.IsAuthenticated && !request.IsPublicMode)
        {
            if (!HasStationCredential(request.StationCode, request.StationToken))
            {
                return Unauthorized(new ScanVerifyResponseViewModel
                {
                    Success = false,
                    Message = "ต้องระบุ stationCode และ stationToken",
                    RecognitionProfile = _faceRecognitionServiceResolver.NormalizeProfile(request.RecognitionProfile)
                });
            }

            var validStation = await IsValidStationAsync(request.StationCode, request.StationToken, cancellationToken);
            if (!validStation)
            {
                return Unauthorized(new ScanVerifyResponseViewModel
                {
                    Success = false,
                    Message = "stationCode หรือ stationToken ไม่ถูกต้อง",
                    RecognitionProfile = _faceRecognitionServiceResolver.NormalizeProfile(request.RecognitionProfile)
                });
            }
        }

        await using var stream = request.Image!.OpenReadStream();
        var processResult = await _attendanceService.ProcessScanAsync(request, stream, cancellationToken);

        await PublishLiveEventAsync(request, processResult, cancellationToken);

        return Json(new ScanVerifyResponseViewModel
        {
            Success = processResult.Success,
            Message = processResult.Message,
            Provider = processResult.Provider,
            StudentId = processResult.StudentId,
            StudentCode = processResult.StudentCode,
            StudentName = processResult.StudentName,
            GradeLevel = processResult.GradeLevel,
            Classroom = processResult.Classroom,
            ScanType = processResult.ScanType,
            ScanTime = processResult.ScanTime,
            Confidence = processResult.ConfidenceScore,
            RecognitionProfile = processResult.RecognitionProfile,
            IsDuplicate = processResult.IsDuplicate,
            TimeSource = processResult.TimeSource,
            ClockAnomaly = processResult.ClockAnomaly,
            ClockSkewMinutes = processResult.ClockSkewMinutes
        });
    }

    [AllowAnonymous]
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Preview([FromForm] ScanVerifyRequestViewModel? request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new ScanPreviewResponseViewModel
            {
                Success = false,
                Message = "ข้อมูลคำขอไม่ถูกต้อง",
                RecognitionProfile = FaceRecognitionProfiles.Auto
            });
        }

        if (request.IsPublicMode)
        {
            request.StationCode = null;
            request.StationToken = null;
        }

        var recognitionProfile = _faceRecognitionServiceResolver.NormalizeProfile(request.RecognitionProfile);

        if (request.Image is null || request.Image.Length == 0)
        {
            return BadRequest(new ScanPreviewResponseViewModel
            {
                Success = false,
                Message = "ไม่พบภาพสำหรับตรวจจับใบหน้า",
                RecognitionProfile = recognitionProfile
            });
        }

        if (!User.Identity!.IsAuthenticated && !request.IsPublicMode)
        {
            if (!HasStationCredential(request.StationCode, request.StationToken))
            {
                return Unauthorized(new ScanPreviewResponseViewModel
                {
                    Success = false,
                    Message = "ต้องระบุ stationCode และ stationToken",
                    RecognitionProfile = recognitionProfile
                });
            }

            var validStation = await IsValidStationAsync(request.StationCode, request.StationToken, cancellationToken);
            if (!validStation)
            {
                return Unauthorized(new ScanPreviewResponseViewModel
                {
                    Success = false,
                    Message = "stationCode หรือ stationToken ไม่ถูกต้อง",
                    RecognitionProfile = recognitionProfile
                });
            }
        }

        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);
        var faceRecognitionService = _faceRecognitionServiceResolver.ResolveForScan(recognitionProfile);

        await using var rawMemory = new MemoryStream();
        await request.Image.CopyToAsync(rawMemory, cancellationToken);
        rawMemory.Position = 0;

        var faceMatch = await faceRecognitionService.VerifyAsync(rawMemory, recognitionProfile);
        if (faceMatch is null)
        {
            return Json(new ScanPreviewResponseViewModel
            {
                Success = false,
                Message = "ไม่สามารถประมวลผลข้อมูลใบหน้าได้",
                RecognitionProfile = recognitionProfile
            });
        }

        if (!faceMatch.Success || !faceMatch.StudentId.HasValue)
        {
            return Json(new ScanPreviewResponseViewModel
            {
                Success = false,
                Message = faceMatch.Message,
                Provider = faceMatch.Provider,
                RecognitionProfile = recognitionProfile,
                Confidence = faceMatch.ConfidenceScore
            });
        }

        if (faceMatch.ConfidenceScore < settings.FaceConfidenceThreshold)
        {
            return Json(new ScanPreviewResponseViewModel
            {
                Success = false,
                Message = "ความมั่นใจต่ำเกินเกณฑ์",
                Provider = faceMatch.Provider,
                RecognitionProfile = recognitionProfile,
                StudentId = faceMatch.StudentId,
                StudentCode = faceMatch.StudentCode,
                StudentName = faceMatch.StudentName,
                Confidence = faceMatch.ConfidenceScore
            });
        }

        return Json(new ScanPreviewResponseViewModel
        {
            Success = true,
            Message = "พบใบหน้า",
            Provider = faceMatch.Provider,
            RecognitionProfile = recognitionProfile,
            StudentId = faceMatch.StudentId,
            StudentCode = faceMatch.StudentCode,
            StudentName = faceMatch.StudentName,
            Confidence = faceMatch.ConfidenceScore
        });
    }

    [AllowAnonymous]
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> VerifyQueued([FromForm] ScanVerifyRequestViewModel? request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { success = false, message = "ข้อมูลคำขอไม่ถูกต้อง" });
        }

        if (request.IsPublicMode)
        {
            request.StationCode = null;
            request.StationToken = null;
        }

        var error = ScanRequestValidator.Validate(request);
        if (error is not null)
        {
            return BadRequest(new { success = false, message = error });
        }

        if (!User.Identity!.IsAuthenticated && !request.IsPublicMode)
        {
            if (!HasStationCredential(request.StationCode, request.StationToken))
            {
                return Unauthorized(new { success = false, message = "ต้องระบุ stationCode และ stationToken" });
            }

            var validStation = await IsValidStationAsync(request.StationCode, request.StationToken, cancellationToken);
            if (!validStation)
            {
                return Unauthorized(new { success = false, message = "stationCode หรือ stationToken ไม่ถูกต้อง" });
            }
        }

        await using var buffer = new MemoryStream();
        await request.Image!.CopyToAsync(buffer, cancellationToken);

        if (!_queuedScanVerifyService.TryEnqueue(new QueuedScanVerifyRequest
        {
            ImageBytes = buffer.ToArray(),
            StationCode = request.StationCode,
            StationToken = request.StationToken,
            ClientCapturedAtLocal = request.ClientCapturedAtLocal,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            LocationAccuracyMeters = request.LocationAccuracyMeters,
            RequestedType = request.RequestedType,
            RecognitionProfile = request.RecognitionProfile,
            IsPublicMode = request.IsPublicMode
        }, out var jobId, out var decision))
        {
            var pending = _queuedScanVerifyService.PendingCount;
            if (string.Equals(decision.Code, "low-quality", StringComparison.OrdinalIgnoreCase)
                || string.Equals(decision.Code, "invalid-image", StringComparison.OrdinalIgnoreCase)
                || string.Equals(decision.Code, "invalid", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    success = false,
                    status = decision.Code,
                    message = decision.Message,
                    pending,
                    quality = decision.Quality
                });
            }

            if (string.Equals(decision.Code, "duplicate-frame", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status409Conflict, new
                {
                    success = false,
                    status = decision.Code,
                    message = decision.Message,
                    pending,
                    retryAfterMs = decision.RetryAfterMs,
                    quality = decision.Quality
                });
            }

            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                success = false,
                status = string.IsNullOrWhiteSpace(decision.Code) ? "overloaded" : decision.Code,
                message = string.IsNullOrWhiteSpace(decision.Message) ? "คิวประมวลผลเต็มชั่วคราว" : decision.Message,
                pending,
                retryAfterMs = decision.RetryAfterMs > 0 ? decision.RetryAfterMs : Math.Clamp(700 + (pending * 18), 900, 3000),
                quality = decision.Quality
            });
        }

        return Json(new { success = true, status = "queued", jobId, pending = _queuedScanVerifyService.PendingCount });
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult VerifyQueuedResult([FromQuery] string? jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return BadRequest(new { success = false, message = "ต้องระบุ jobId" });
        }

        if (!_queuedScanVerifyService.TryGetResult(jobId, out var result) || result is null)
        {
            return NotFound(new { success = false, status = "notfound" });
        }

        if (string.Equals(result.Status, "completed", StringComparison.OrdinalIgnoreCase))
        {
            return Json(new { success = true, status = "completed", result = result.Response });
        }

        if (string.Equals(result.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            return Json(new { success = false, status = "failed", message = result.Message });
        }

        return Json(new { success = true, status = "pending" });
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult VerifyQueuedMetrics()
    {
        var metrics = _queuedScanVerifyService.GetMetricsSnapshot();
        return Json(new
        {
            success = true,
            serverTimeUtc = DateTimeOffset.UtcNow,
            metrics
        });
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Recent(
        string? stationCode,
        string? stationToken,
        bool isPublicMode = false,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        if (isPublicMode)
        {
            stationCode = null;
            stationToken = null;
        }

        if (!User.Identity!.IsAuthenticated && !isPublicMode)
        {
            if (!HasStationCredential(stationCode, stationToken))
            {
                return Unauthorized(new { success = false, message = "ไม่พบข้อมูลยืนยันตัวตนของสถานีสแกน" });
            }

            var validStation = await IsValidStationAsync(stationCode, stationToken, cancellationToken);
            if (!validStation)
            {
                return Unauthorized(new { success = false, message = "ข้อมูลยืนยันตัวตนของสถานีสแกนไม่ถูกต้อง" });
            }
        }

        var normalizedTake = Math.Clamp(take, 1, 100);
        var items = await _dbContext.AttendanceTransactions
            .AsNoTracking()
            .Include(x => x.Student)
                .ThenInclude(x => x!.GradeLevel)
            .Include(x => x.Student)
                .ThenInclude(x => x!.Classroom)
            .Where(x => !x.IsDuplicate)
            .OrderByDescending(x => x.ScanTime)
            .Take(normalizedTake)
            .Select(x => new ScanRecentItemViewModel
            {
                TransactionId = x.Id,
                ScanTime = x.ScanTime,
                ScanType = x.ScanType,
                StudentCode = x.Student != null ? x.Student.StudentCode : "-",
                StudentName = x.Student != null
                    ? ((x.Student.Prefix ?? string.Empty) + x.Student.FirstName + " " + x.Student.LastName).Trim()
                    : "-",
                GradeLevel = x.Student != null && x.Student.GradeLevel != null ? x.Student.GradeLevel.Name : string.Empty,
                Classroom = x.Student != null && x.Student.Classroom != null ? x.Student.Classroom.Name : string.Empty,
                Confidence = x.ConfidenceScore,
                Provider = x.RecognitionProvider
            })
            .ToListAsync(cancellationToken);

        return Json(new
        {
            success = true,
            generatedAt = DateTime.Now,
            items
        });
    }

    private async Task<bool> IsValidStationAsync(string? stationCode, string? stationToken, CancellationToken cancellationToken)
    {
        return await _scanDeviceLookupService.IsValidActiveDeviceAsync(stationCode, stationToken, cancellationToken);
    }

    private Task PublishLiveEventAsync(ScanVerifyRequestViewModel request, ScanProcessResult processResult, CancellationToken cancellationToken)
    {
        var eventType = !processResult.Success
            ? "failed"
            : processResult.IsDuplicate
                ? "duplicate"
                : "success";

        var payload = new
        {
            scope = "student",
            eventType,
            success = processResult.Success,
            isDuplicate = processResult.IsDuplicate,
            message = processResult.Message,
            scanType = processResult.ScanType?.ToString(),
            scanTime = processResult.ScanTime,
            code = processResult.StudentCode,
            name = processResult.StudentName,
            gradeLevel = processResult.GradeLevel,
            classroom = processResult.Classroom,
            confidence = processResult.ConfidenceScore,
            provider = processResult.Provider,
            recognitionProfile = processResult.RecognitionProfile,
            stationCode = request.IsPublicMode ? "PUBLIC" : request.StationCode,
            isPublicMode = request.IsPublicMode
        };

        return _scanLiveHub.Clients
            .Group(ScanLiveHub.ExecutiveGroup)
            .SendAsync(ScanLiveHub.ScanEventMethod, payload, cancellationToken);
    }

    private static bool HasStationCredential(string? stationCode, string? stationToken)
    {
        return !string.IsNullOrWhiteSpace(stationCode) && !string.IsNullOrWhiteSpace(stationToken);
    }
}
