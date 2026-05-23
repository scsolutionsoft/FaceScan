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

public class TeacherScanController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ITeacherAttendanceService _teacherAttendanceService;
    private readonly IFaceRecognitionServiceResolver _faceRecognitionServiceResolver;
    private readonly IScanDeviceLookupService _scanDeviceLookupService;
    private readonly ISystemSettingService _systemSettingService;
    private readonly IHubContext<ScanLiveHub> _scanLiveHub;

    public TeacherScanController(
        ApplicationDbContext dbContext,
        ITeacherAttendanceService teacherAttendanceService,
        IFaceRecognitionServiceResolver faceRecognitionServiceResolver,
        IScanDeviceLookupService scanDeviceLookupService,
        ISystemSettingService systemSettingService,
        IHubContext<ScanLiveHub> scanLiveHub)
    {
        _dbContext = dbContext;
        _teacherAttendanceService = teacherAttendanceService;
        _faceRecognitionServiceResolver = faceRecognitionServiceResolver;
        _scanDeviceLookupService = scanDeviceLookupService;
        _systemSettingService = systemSettingService;
        _scanLiveHub = scanLiveHub;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Index(string? stationCode, string? stationToken, CancellationToken cancellationToken)
    {
        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);

        if (!User.Identity!.IsAuthenticated)
        {
            if (!HasStationCredential(stationCode, stationToken))
            {
                return Content("ไม่อนุญาตให้เข้าใช้งานหน้าแสกนครู กรุณาระบุ stationCode และ stationToken ให้ถูกต้อง");
            }

            var validStation = await IsValidStationAsync(stationCode, stationToken, cancellationToken);
            if (!validStation)
            {
                return Content("ไม่อนุญาตให้เข้าใช้งานหน้าแสกนครู กรุณาระบุ stationCode และ stationToken ให้ถูกต้อง");
            }
        }

        return View(new ScanViewModel
        {
            StationCode = stationCode,
            StationToken = stationToken,
            SchoolName = settings.SchoolName,
            RecognitionProfile = FaceRecognitionProfiles.Auto,
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

        var recognitionProfile = _faceRecognitionServiceResolver.NormalizeProfile(request.RecognitionProfile);
        var error = ScanRequestValidator.Validate(request);
        if (error is not null)
        {
            return BadRequest(new ScanVerifyResponseViewModel
            {
                Success = false,
                Message = error,
                RecognitionProfile = recognitionProfile
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
                    RecognitionProfile = recognitionProfile
                });
            }

            var validStation = await IsValidStationAsync(request.StationCode, request.StationToken, cancellationToken);
            if (!validStation)
            {
                return Unauthorized(new ScanVerifyResponseViewModel
                {
                    Success = false,
                    Message = "stationCode หรือ stationToken ไม่ถูกต้อง",
                    RecognitionProfile = recognitionProfile
                });
            }
        }

        await using var stream = request.Image!.OpenReadStream();
        var processResult = await _teacherAttendanceService.ProcessScanAsync(request, stream, cancellationToken);

        await PublishLiveEventAsync(request, processResult, cancellationToken);

        return Json(new ScanVerifyResponseViewModel
        {
            Success = processResult.Success,
            Message = processResult.Message,
            Provider = processResult.Provider,
            StudentCode = processResult.Username,
            StudentName = processResult.FullName,
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

        var faceMatch = await faceRecognitionService.VerifyTeacherAsync(rawMemory, recognitionProfile);
        if (faceMatch is null)
        {
            return Json(new ScanPreviewResponseViewModel
            {
                Success = false,
                Message = "ไม่สามารถประมวลผลข้อมูลใบหน้าได้",
                RecognitionProfile = recognitionProfile
            });
        }

        if (!faceMatch.Success || string.IsNullOrWhiteSpace(faceMatch.UserId))
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
                StudentCode = faceMatch.UserName ?? faceMatch.StudentCode,
                StudentName = faceMatch.FullName ?? faceMatch.StudentName,
                Confidence = faceMatch.ConfidenceScore
            });
        }

        return Json(new ScanPreviewResponseViewModel
        {
            Success = true,
            Message = "พบใบหน้า",
            Provider = faceMatch.Provider,
            RecognitionProfile = recognitionProfile,
            StudentCode = faceMatch.UserName ?? faceMatch.StudentCode,
            StudentName = faceMatch.FullName ?? faceMatch.StudentName,
            Confidence = faceMatch.ConfidenceScore
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
                return Unauthorized(new { success = false, message = "ไม่พบข้อมูลยืนยันตัวตนของสถานีแสกน" });
            }

            var validStation = await IsValidStationAsync(stationCode, stationToken, cancellationToken);
            if (!validStation)
            {
                return Unauthorized(new { success = false, message = "ข้อมูลยืนยันตัวตนของสถานีแสกนไม่ถูกต้อง" });
            }
        }

        var normalizedTake = Math.Clamp(take, 1, 100);
        var items = await _dbContext.TeacherAttendanceTransactions
            .AsNoTracking()
            .Include(x => x.User)
            .Where(x => !x.IsDuplicate)
            .OrderByDescending(x => x.ScanTime)
            .Take(normalizedTake)
            .Select(x => new ScanRecentItemViewModel
            {
                TransactionId = x.Id,
                ScanTime = x.ScanTime,
                ScanType = x.ScanType,
                StudentCode = x.User != null ? (x.User.UserName ?? "-") : "-",
                StudentName = x.User != null ? x.User.FullName : "-",
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

    private Task PublishLiveEventAsync(ScanVerifyRequestViewModel request, TeacherScanProcessResult processResult, CancellationToken cancellationToken)
    {
        var eventType = !processResult.Success
            ? "failed"
            : processResult.IsDuplicate
                ? "duplicate"
                : "success";

        var payload = new
        {
            scope = "teacher",
            eventType,
            success = processResult.Success,
            isDuplicate = processResult.IsDuplicate,
            message = processResult.Message,
            scanType = processResult.ScanType?.ToString(),
            scanTime = processResult.ScanTime,
            code = processResult.Username,
            name = processResult.FullName,
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
