using FaceScan.Web.Data;
using FaceScan.Web.Helpers;
using FaceScan.Web.Models;
using FaceScan.Web.Models.Entities;
using FaceScan.Web.Services.Interfaces;
using FaceScan.Web.ViewModels.IpCamera;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenCvSharp;

namespace FaceScan.Web.Controllers;

/// <summary>
/// Walk-by IP camera scan station.
/// Requires stationCode + stationToken in query string (same as Scan/Index).
/// ProxySnapshot fetches snapshot from the configured camera URL server-side
/// so browser never receives camera credentials.
/// </summary>
[AllowAnonymous]
public class IpCameraController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ISystemSettingService _systemSettingService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOnvifCameraService _onvifCameraService;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public IpCameraController(
        ApplicationDbContext dbContext,
        ISystemSettingService systemSettingService,
        IHttpClientFactory httpClientFactory,
        IOnvifCameraService onvifCameraService,
        IWebHostEnvironment webHostEnvironment)
    {
        _dbContext = dbContext;
        _systemSettingService = systemSettingService;
        _httpClientFactory = httpClientFactory;
        _onvifCameraService = onvifCameraService;
        _webHostEnvironment = webHostEnvironment;
    }

    // GET /IpCamera/Menu
    [HttpGet]
    public async Task<IActionResult> Menu(CancellationToken cancellationToken)
    {
        var appSettings = await _systemSettingService.GetSettingsAsync(cancellationToken);

        var stations = await _dbContext.ScanDevices
            .AsNoTracking()
            .Where(d => d.IsActive &&
                        ((d.CameraSnapshotUrl != null && d.CameraSnapshotUrl != string.Empty)
                        || (d.RtspStreamUrl != null && d.RtspStreamUrl != string.Empty)))
            .OrderBy(d => d.Name)
            .Select(d => new IpCameraMenuItemViewModel
            {
                StationCode = d.StationCode,
                Name = d.Name,
                Location = d.Location,
                CameraType = d.CameraType
            })
            .ToListAsync(cancellationToken);

        ViewBag.AppName = string.IsNullOrWhiteSpace(appSettings.ApplicationDisplayName)
            ? "FaceScan"
            : appSettings.ApplicationDisplayName;

        return View(stations);
    }

    // GET /IpCamera/Public?stationCode=GATE-01
    // Public entry for kiosk without login. Token is loaded server-side from device config.
    [HttpGet]
    public async Task<IActionResult> Public(string? stationCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(stationCode))
        {
            return RedirectToAction(nameof(Menu));
        }

        var device = await _dbContext.ScanDevices
            .AsNoTracking()
            .FirstOrDefaultAsync(
                d => d.StationCode == stationCode && d.IsActive,
                cancellationToken);

        if (device is null || string.IsNullOrWhiteSpace(device.AccessToken))
        {
            return View("Error", "ไม่พบสถานีสแกนที่ใช้งานได้");
        }

        return await Index(device.StationCode, device.AccessToken, cancellationToken);
    }

    // GET /IpCamera/WalkBy?stationCode=GATE-01
    // Dedicated walk-by scan page (separate route for camera scanning flow).
    [HttpGet]
    public async Task<IActionResult> WalkBy(string? stationCode, bool kiosk = false, bool minimal = false, bool autofs = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(stationCode))
        {
            return RedirectToAction(nameof(Menu));
        }

        var device = await _dbContext.ScanDevices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.StationCode == stationCode && d.IsActive, cancellationToken);

        if (device is null || string.IsNullOrWhiteSpace(device.AccessToken))
        {
            return View("Error", "ไม่พบสถานีสแกนที่ใช้งานได้");
        }

        return RedirectToAction(nameof(Index), new
        {
            stationCode = device.StationCode,
            stationToken = device.AccessToken,
            kiosk = kiosk ? 1 : 0,
            minimal = minimal ? 1 : 0,
            autofs = autofs ? 1 : 0
        });
    }

    // GET /IpCamera/Index?stationCode=GATE-01&stationToken=xxxx
    [HttpGet]
    public async Task<IActionResult> Index(string? stationCode, string? stationToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(stationCode) || string.IsNullOrWhiteSpace(stationToken))
        {
            return View("Error", "ต้องระบุ stationCode และ stationToken ใน URL");
        }

        var device = await _dbContext.ScanDevices
            .AsNoTracking()
            .FirstOrDefaultAsync(
                d => d.StationCode == stationCode && d.IsActive,
                cancellationToken);

        if (device is null || device.AccessToken != stationToken)
        {
            return View("Error", "stationCode หรือ stationToken ไม่ถูกต้อง");
        }

        if (string.IsNullOrWhiteSpace(device.CameraSnapshotUrl) && string.IsNullOrWhiteSpace(device.RtspStreamUrl))
        {
            return View("Error", "ยังไม่ได้ตั้งค่า Snapshot หรือ RTSP URL ของกล้อง กรุณาตั้งค่าในหน้าจัดการอุปกรณ์");
        }

        var appSettings = await _systemSettingService.GetSettingsAsync();
        ViewBag.StationCode = stationCode;
        ViewBag.StationToken = stationToken;
        ViewBag.StationName = device.Name;
        ViewBag.CameraType = device.CameraType ?? "generic";
        ViewBag.IntervalMs = device.CameraIntervalMs > 0 ? device.CameraIntervalMs : 1500;
        ViewBag.CameraMinConfidence = device.CameraMinConfidence >= 0 && device.CameraMinConfidence <= 1
            ? device.CameraMinConfidence
            : 0.60m;
        ViewBag.CameraSkipFrames = device.CameraSkipFrames >= 1 ? device.CameraSkipFrames : 1;
        ViewBag.CameraOverlayEnabled = device.CameraOverlayEnabled;
        ViewBag.RecognitionProfile = string.IsNullOrWhiteSpace(device.RecognitionProfile)
            ? FaceRecognitionProfiles.Auto
            : device.RecognitionProfile;
        var scanSettings = ScanCameraSettingsHelper.Parse(device.Notes);
        ViewBag.ScanFaceMode = scanSettings.FaceMode;
        ViewBag.ScanEngine = scanSettings.ScanEngine;
        ViewBag.ScanAutoTuneEnabled = scanSettings.AutoTuneEnabled;
        ViewBag.ScanMultiFaceMax = scanSettings.MultiFaceMax;
        ViewBag.AppName = string.IsNullOrWhiteSpace(appSettings.ApplicationDisplayName)
            ? "FaceScan"
            : appSettings.ApplicationDisplayName;

        return View();
    }

    // GET /IpCamera/StationHealth?stationCode=GATE-01
    // Public status endpoint for menu card health indicator.
    [HttpGet]
    public async Task<IActionResult> StationHealth(string? stationCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(stationCode))
        {
            return BadRequest(new
            {
                stationCode = stationCode ?? string.Empty,
                online = false,
                message = "ต้องระบุ stationCode",
                checkedAt = DateTimeOffset.UtcNow
            });
        }

        var device = await _dbContext.ScanDevices
            .AsNoTracking()
            .FirstOrDefaultAsync(
                d => d.StationCode == stationCode && d.IsActive,
                cancellationToken);

        if (device is null || (string.IsNullOrWhiteSpace(device.CameraSnapshotUrl) && string.IsNullOrWhiteSpace(device.RtspStreamUrl)))
        {
            return Json(new
            {
                stationCode,
                online = false,
                message = "ไม่พบสถานีที่พร้อมใช้งาน",
                checkedAt = DateTimeOffset.UtcNow
            });
        }

        if (IsRtspPreferred(device.CameraType) && !string.IsNullOrWhiteSpace(device.RtspStreamUrl))
        {
            var (rtspUsername, rtspPassword) = GetPreferredRtspCredentials(device);
            var rtspProbe = await TryFetchImageFromRtspAsync(device.RtspStreamUrl, rtspUsername, rtspPassword, cancellationToken);
            if (rtspProbe.IsSuccess)
            {
                return Json(new
                {
                    stationCode,
                    online = true,
                    message = "ออนไลน์ (RTSP)",
                    checkedAt = DateTimeOffset.UtcNow
                });
            }

            if (TryParseRtspEndpoint(device.RtspStreamUrl, out var rtspHost, out var rtspPort)
                && await CanConnectTcpAsync(rtspHost, rtspPort, 2500, cancellationToken))
            {
                return Json(new
                {
                    stationCode,
                    online = true,
                    message = "ออนไลน์ (RTSP TCP) - ตรวจสอบ path/credentials เพิ่มเติม",
                    checkedAt = DateTimeOffset.UtcNow
                });
            }
        }

        if (string.IsNullOrWhiteSpace(device.CameraSnapshotUrl))
        {
            return Json(new
            {
                stationCode,
                online = false,
                message = "RTSP ไม่พร้อมใช้งาน",
                checkedAt = DateTimeOffset.UtcNow
            });
        }

        try
        {
            var client = _httpClientFactory.CreateClient("IpCamera");
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

            using var request = CreateCameraRequest(device.CameraSnapshotUrl, device.CameraUsername, device.CameraPassword);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);

            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!response.IsSuccessStatusCode)
            {
                return Json(new
                {
                    stationCode,
                    online = false,
                    message = $"HTTP {(int)response.StatusCode}",
                    checkedAt = DateTimeOffset.UtcNow
                });
            }

            if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new
                {
                    stationCode,
                    online = false,
                    message = "กล้องตอบกลับไม่ใช่ภาพ",
                    checkedAt = DateTimeOffset.UtcNow
                });
            }

            return Json(new
            {
                stationCode,
                online = true,
                message = "ออนไลน์",
                checkedAt = DateTimeOffset.UtcNow
            });
        }
        catch (TaskCanceledException)
        {
            return Json(new
            {
                stationCode,
                online = false,
                message = "หมดเวลารอการตอบกลับ",
                checkedAt = DateTimeOffset.UtcNow
            });
        }
        catch (HttpRequestException)
        {
            return Json(new
            {
                stationCode,
                online = false,
                message = "เชื่อมต่อกล้องไม่สำเร็จ",
                checkedAt = DateTimeOffset.UtcNow
            });
        }
        catch (Exception)
        {
            return Json(new
            {
                stationCode,
                online = false,
                message = "ไม่สามารถตรวจสอบสถานะกล้องได้",
                checkedAt = DateTimeOffset.UtcNow
            });
        }
    }

    // GET /IpCamera/TestConnection?stationCode=GATE-01&stationToken=xxxx
    // Explicit camera connectivity test endpoint with source diagnostics.
    [HttpGet]
    public async Task<IActionResult> TestConnection(string? stationCode, string? stationToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(stationCode) || string.IsNullOrWhiteSpace(stationToken))
        {
            return Unauthorized(new { success = false, message = "ต้องระบุ stationCode และ stationToken" });
        }

        var device = await _dbContext.ScanDevices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.StationCode == stationCode && d.IsActive, cancellationToken);

        if (device is null || device.AccessToken != stationToken)
        {
            return Unauthorized(new { success = false, message = "stationCode หรือ stationToken ไม่ถูกต้อง" });
        }

        if (!string.IsNullOrWhiteSpace(device.RtspStreamUrl))
        {
            var (rtspUsername, rtspPassword) = GetPreferredRtspCredentials(device);
            var rtspResult = await TryFetchImageFromRtspAsync(device.RtspStreamUrl, rtspUsername, rtspPassword, cancellationToken);
            if (rtspResult.IsSuccess)
            {
                return Json(new
                {
                    success = true,
                    source = "rtsp",
                    streamUrl = rtspResult.UsedUrl ?? device.RtspStreamUrl,
                    message = "เชื่อมต่อ RTSP ผ่าน"
                });
            }

            if (TryParseRtspEndpoint(device.RtspStreamUrl, out var rtspHost, out var rtspPort)
                && await CanConnectTcpAsync(rtspHost, rtspPort, 2500, cancellationToken))
            {
                return Json(new
                {
                    success = true,
                    source = "rtsp-tcp",
                    streamUrl = device.RtspStreamUrl,
                    message = "เชื่อมต่อพอร์ต RTSP ผ่าน แต่ยังถอดรหัสภาพไม่ได้ (ตรวจสอบ path/credentials)"
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(device.CameraSnapshotUrl))
        {
            var client = _httpClientFactory.CreateClient("IpCamera");
            var snapResult = await TryFetchImageAsync(client, device.CameraSnapshotUrl, device.CameraUsername, device.CameraPassword, cancellationToken);
            if (snapResult.IsSuccess)
            {
                return Json(new
                {
                    success = true,
                    source = "snapshot",
                    streamUrl = device.CameraSnapshotUrl,
                    message = "เชื่อมต่อ Snapshot ผ่าน"
                });
            }
        }

        return Json(new
        {
            success = false,
            source = "none",
            message = "ทดสอบการเชื่อมต่อไม่ผ่าน"
        });
    }

    // GET /IpCamera/ProxySnapshot?stationCode=GATE-01&stationToken=xxxx
    // Server-side proxy: looks up camera URL from DB, fetches snapshot, returns JPEG bytes.
    // Never exposes camera URL/credentials to the client.
    [HttpGet]
    [ResponseCache(Duration = 0, NoStore = true, VaryByHeader = "*")]
    public async Task<IActionResult> ProxySnapshot(string? stationCode, string? stationToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(stationCode) || string.IsNullOrWhiteSpace(stationToken))
        {
            return Unauthorized();
        }

        var device = await _dbContext.ScanDevices
            .AsNoTracking()
            .FirstOrDefaultAsync(
                d => d.StationCode == stationCode && d.IsActive,
                cancellationToken);

        if (device is null || device.AccessToken != stationToken)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(device.CameraSnapshotUrl) && string.IsNullOrWhiteSpace(device.RtspStreamUrl))
        {
            return NotFound();
        }

        try
        {
            var client = _httpClientFactory.CreateClient("IpCamera");
            var frame = await TryFetchBestFrameAsync(device, client, cancellationToken);
            if (frame.IsSuccess)
            {
                return File(frame.Bytes!, frame.ContentType!);
            }

            var failureMessage = await BuildCameraFailureMessageAsync(device.RtspStreamUrl, device.CameraSnapshotUrl, cancellationToken);
            return StatusCode(502, failureMessage);
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, "ไม่สามารถเชื่อมต่อกล้องได้");
        }
        catch (TaskCanceledException)
        {
            return StatusCode(504, "กล้องไม่ตอบสนอง");
        }
    }

    // GET /IpCamera/MjpegStream?stationCode=GATE-01&stationToken=xxxx&resolution=640x360&fps=10
    // Server-side MJPEG stream for smoother real-time display. Keeps camera credentials hidden.
    [HttpGet]
    [ResponseCache(Duration = 0, NoStore = true, VaryByHeader = "*")]
    public async Task<IActionResult> MjpegStream(
        string? stationCode,
        string? stationToken,
        string? resolution,
        int? fps,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(stationCode) || string.IsNullOrWhiteSpace(stationToken))
        {
            return Unauthorized();
        }

        var device = await _dbContext.ScanDevices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.StationCode == stationCode && d.IsActive, cancellationToken);

        if (device is null || device.AccessToken != stationToken)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(device.CameraSnapshotUrl) && string.IsNullOrWhiteSpace(device.RtspStreamUrl))
        {
            return NotFound();
        }

        var boundary = "frame";
        var frameIntervalMs = Math.Clamp(fps.GetValueOrDefault(10), 4, 20);
        var delayMs = Math.Max(50, 1000 / frameIntervalMs);
        var hasTargetResolution = TryParseResolution(resolution, out var targetWidth, out var targetHeight);

        Response.StatusCode = 200;
        Response.ContentType = $"multipart/x-mixed-replace; boundary={boundary}";
        Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";
        Response.Headers["X-Accel-Buffering"] = "no";

        var client = _httpClientFactory.CreateClient("IpCamera");
        var streamToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, HttpContext.RequestAborted).Token;

        try
        {
            while (!streamToken.IsCancellationRequested)
            {
                var frame = await TryFetchBestFrameAsync(device, client, streamToken);
                if (frame.IsSuccess)
                {
                    var jpegBytes = frame.Bytes!;
                    if (hasTargetResolution)
                    {
                        jpegBytes = ResizeJpegFrame(jpegBytes, targetWidth, targetHeight);
                    }

                    await Response.WriteAsync($"--{boundary}\r\n", streamToken);
                    await Response.WriteAsync("Content-Type: image/jpeg\r\n", streamToken);
                    await Response.WriteAsync($"Content-Length: {jpegBytes.Length}\r\n\r\n", streamToken);
                    await Response.Body.WriteAsync(jpegBytes, streamToken);
                    await Response.WriteAsync("\r\n", streamToken);
                    await Response.Body.FlushAsync(streamToken);
                }

                await Task.Delay(delayMs, streamToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected.
        }
        catch
        {
            // Stream ends silently; viewer will reconnect.
        }

        return new EmptyResult();
    }

    // POST /IpCamera/UploadDebugSnapshot?stationCode=GATE-01&stationToken=xxxx
    // Receives critical-latency debug images from client and stores them server-side for audit.
    [HttpPost]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> UploadDebugSnapshot(
        string? stationCode,
        string? stationToken,
        IFormFile? image,
        string? reason,
        string? criticalSeconds,
        string? analysisMs,
        string? loopMs,
        string? displayFps,
        string? analysisFps,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(stationCode) || string.IsNullOrWhiteSpace(stationToken))
        {
            return Unauthorized(new { success = false, message = "ต้องระบุ stationCode และ stationToken" });
        }

        var device = await _dbContext.ScanDevices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.StationCode == stationCode && d.IsActive, cancellationToken);

        if (device is null || device.AccessToken != stationToken)
        {
            return Unauthorized(new { success = false, message = "stationCode หรือ stationToken ไม่ถูกต้อง" });
        }

        if (image is null || image.Length <= 0)
        {
            return BadRequest(new { success = false, message = "ไม่พบไฟล์ภาพ" });
        }

        var contentType = image.ContentType ?? string.Empty;
        if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { success = false, message = "ไฟล์ไม่ใช่รูปภาพ" });
        }

        var rootPath = _webHostEnvironment.WebRootPath;
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return StatusCode(500, new { success = false, message = "ไม่พบ web root path" });
        }

        var stationFolder = SanitizePathSegment(stationCode);
        var saveDir = Path.Combine(rootPath, "uploads", "ipcamera-debug", stationFolder);
        Directory.CreateDirectory(saveDir);

        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        var fileName = $"critical-{timestamp}.jpg";
        var savePath = Path.Combine(saveDir, fileName);

        await using (var stream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await image.CopyToAsync(stream, cancellationToken);
        }

        var metaPath = Path.Combine(saveDir, $"critical-{timestamp}.txt");
        var metaLines = new[]
        {
            $"stationCode={stationCode}",
            $"deviceName={device.Name}",
            $"reason={reason}",
            $"criticalSeconds={criticalSeconds}",
            $"analysisMs={analysisMs}",
            $"loopMs={loopMs}",
            $"displayFps={displayFps}",
            $"analysisFps={analysisFps}",
            $"capturedAtUtc={DateTimeOffset.UtcNow:O}"
        };
        await System.IO.File.WriteAllLinesAsync(metaPath, metaLines, cancellationToken);

        var webPath = $"/uploads/ipcamera-debug/{stationFolder}/{fileName}";
        return Json(new { success = true, path = webPath, message = "บันทึกภาพ debug เรียบร้อย" });
    }

    // GET /IpCamera/DebugSnapshots?stationCode=GATE-01&stationToken=xxxx&take=8
    [HttpGet]
    public async Task<IActionResult> DebugSnapshots(
        string? stationCode,
        string? stationToken,
        int take = 8,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(stationCode) || string.IsNullOrWhiteSpace(stationToken))
        {
            return Unauthorized(new { success = false, message = "ต้องระบุ stationCode และ stationToken" });
        }

        var device = await _dbContext.ScanDevices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.StationCode == stationCode && d.IsActive, cancellationToken);

        if (device is null || device.AccessToken != stationToken)
        {
            return Unauthorized(new { success = false, message = "stationCode หรือ stationToken ไม่ถูกต้อง" });
        }

        var rootPath = _webHostEnvironment.WebRootPath;
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return Json(new { success = true, items = Array.Empty<object>() });
        }

        var stationFolder = SanitizePathSegment(stationCode);
        var dir = Path.Combine(rootPath, "uploads", "ipcamera-debug", stationFolder);
        if (!Directory.Exists(dir))
        {
            return Json(new { success = true, items = Array.Empty<object>() });
        }

        var cappedTake = Math.Clamp(take, 1, 40);
        var files = Directory.EnumerateFiles(dir, "critical-*.jpg", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderByDescending(f => f.CreationTimeUtc)
            .Take(cappedTake)
            .ToArray();

        var items = files.Select(file =>
        {
            var stem = Path.GetFileNameWithoutExtension(file.Name);
            var metaPath = Path.Combine(dir, stem + ".txt");
            var metadata = ReadMetadata(metaPath);
            var safeFileName = Uri.EscapeDataString(file.Name);
            var url = $"/uploads/ipcamera-debug/{stationFolder}/{safeFileName}";

            metadata.TryGetValue("criticalSeconds", out var criticalSeconds);
            metadata.TryGetValue("analysisMs", out var analysisMs);
            metadata.TryGetValue("loopMs", out var loopMs);
            metadata.TryGetValue("displayFps", out var displayFps);
            metadata.TryGetValue("analysisFps", out var analysisFps);

            return new
            {
                fileName = file.Name,
                url,
                capturedAtUtc = file.CreationTimeUtc,
                criticalSeconds,
                analysisMs,
                loopMs,
                displayFps,
                analysisFps
            };
        });

        return Json(new { success = true, items });
    }

    private async Task<(bool IsSuccess, byte[]? Bytes, string? ContentType)> TryFetchBestFrameAsync(
        ScanDevice device,
        HttpClient client,
        CancellationToken cancellationToken)
    {
        var (rtspUsername, rtspPassword) = GetPreferredRtspCredentials(device);

        if (IsRtspPreferred(device.CameraType) && !string.IsNullOrWhiteSpace(device.RtspStreamUrl))
        {
            var rtspPrimary = await TryFetchImageFromRtspAsync(device.RtspStreamUrl, rtspUsername, rtspPassword, cancellationToken);
            if (rtspPrimary.IsSuccess)
            {
                return (true, rtspPrimary.Bytes, "image/jpeg");
            }
        }

        if (!string.IsNullOrWhiteSpace(device.CameraSnapshotUrl))
        {
            var primary = await TryFetchImageAsync(client, device.CameraSnapshotUrl, device.CameraUsername, device.CameraPassword, cancellationToken);
            if (primary.IsSuccess)
            {
                return (true, primary.Bytes, primary.ContentType);
            }
        }

        if (!string.IsNullOrWhiteSpace(device.RtspStreamUrl))
        {
            var rtspFallback = await TryFetchImageFromRtspAsync(device.RtspStreamUrl, rtspUsername, rtspPassword, cancellationToken);
            if (rtspFallback.IsSuccess)
            {
                return (true, rtspFallback.Bytes, "image/jpeg");
            }
        }

        var preferredUsername = string.IsNullOrWhiteSpace(device.CameraUsername) ? device.RtspUsername : device.CameraUsername;
        var preferredPassword = string.IsNullOrWhiteSpace(device.CameraPassword) ? device.RtspPassword : device.CameraPassword;
        foreach (var onvifUri in BuildOnvifUriCandidates(device))
        {
            var recoveredSnapshotUrl = await _onvifCameraService.GetSnapshotUriAsync(
                onvifUri,
                preferredUsername,
                preferredPassword,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(recoveredSnapshotUrl))
            {
                continue;
            }

            var recovered = await TryFetchImageAsync(client, recoveredSnapshotUrl, preferredUsername, preferredPassword, cancellationToken);
            if (!recovered.IsSuccess)
            {
                recovered = await TryFetchImageAsync(client, recoveredSnapshotUrl, null, null, cancellationToken);
            }

            if (!recovered.IsSuccess)
            {
                continue;
            }

            if (!string.Equals(device.CameraSnapshotUrl, recoveredSnapshotUrl, StringComparison.OrdinalIgnoreCase))
            {
                var tracked = await _dbContext.ScanDevices.FirstOrDefaultAsync(d => d.Id == device.Id, cancellationToken);
                if (tracked is not null)
                {
                    tracked.CameraSnapshotUrl = recoveredSnapshotUrl;
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
            }

            return (true, recovered.Bytes, recovered.ContentType);
        }

        return (false, null, null);
    }

    private static (string? Username, string? Password) GetPreferredRtspCredentials(ScanDevice device)
    {
        var username = !string.IsNullOrWhiteSpace(device.RtspUsername)
            ? device.RtspUsername
            : device.CameraUsername;
        var password = !string.IsNullOrWhiteSpace(device.RtspPassword)
            ? device.RtspPassword
            : device.CameraPassword;

        return (username, password);
    }

    private static bool TryParseResolution(string? resolution, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (string.IsNullOrWhiteSpace(resolution) || string.Equals(resolution, "native", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parts = resolution.Split('x', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out width) || !int.TryParse(parts[1], out height))
        {
            return false;
        }

        if (width < 160 || height < 120 || width > 3840 || height > 2160)
        {
            return false;
        }

        return true;
    }

    private static byte[] ResizeJpegFrame(byte[] sourceBytes, int targetWidth, int targetHeight)
    {
        try
        {
            using var source = Cv2.ImDecode(sourceBytes, ImreadModes.Color);
            if (source.Empty())
            {
                return sourceBytes;
            }

            var scale = Math.Min((double)targetWidth / source.Width, (double)targetHeight / source.Height);
            var resizedWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
            var resizedHeight = Math.Max(1, (int)Math.Round(source.Height * scale));

            using var resized = new Mat();
            Cv2.Resize(source, resized, new Size(resizedWidth, resizedHeight), 0, 0, InterpolationFlags.Area);
            Cv2.ImEncode(".jpg", resized, out var resizedBytes);
            return resizedBytes;
        }
        catch
        {
            return sourceBytes;
        }
    }

    private static HttpRequestMessage CreateCameraRequest(
        string cameraSnapshotUrl,
        string? cameraUsername,
        string? cameraPassword)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, cameraSnapshotUrl);

        if (!string.IsNullOrWhiteSpace(cameraUsername))
        {
            var credentials = Convert.ToBase64String(
                System.Text.Encoding.ASCII.GetBytes(
                    $"{cameraUsername}:{cameraPassword ?? string.Empty}"));
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        }

        return request;
    }

    private static bool IsOnvifCameraType(string? cameraType)
    {
        return string.Equals(cameraType, "onvif", StringComparison.OrdinalIgnoreCase)
            || string.Equals(cameraType, "onvif-rtsp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRtspPreferred(string? cameraType)
    {
        return string.Equals(cameraType, "rtsp", StringComparison.OrdinalIgnoreCase)
            || string.Equals(cameraType, "onvif-rtsp", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildOnvifUriFromSnapshotUrl(string snapshotUrl)
    {
        try
        {
            var uri = new Uri(snapshotUrl);
            var port = uri.IsDefaultPort ? 80 : uri.Port;
            return $"http://{uri.Host}:{port}/onvif/device_service";
        }
        catch
        {
            return snapshotUrl;
        }
    }

    private static IEnumerable<string> BuildOnvifUriCandidates(ScanDevice device)
    {
        if (!string.IsNullOrWhiteSpace(device.OnvifDeviceUri))
        {
            yield return device.OnvifDeviceUri!;
        }

        if (!string.IsNullOrWhiteSpace(device.CameraSnapshotUrl))
        {
            var fromSnapshot = BuildOnvifUriFromSnapshotUrl(device.CameraSnapshotUrl);
            if (!string.IsNullOrWhiteSpace(fromSnapshot))
            {
                yield return fromSnapshot;
            }
        }

        if (!string.IsNullOrWhiteSpace(device.RtspStreamUrl)
            && Uri.TryCreate(device.RtspStreamUrl, UriKind.Absolute, out var rtspUri)
            && !string.IsNullOrWhiteSpace(rtspUri.Host))
        {
            var preferredPort = device.OnvifPort > 0 ? device.OnvifPort : 8899;
            var candidatePorts = new[] { preferredPort, 8899, 8080, 80 }
                .Distinct();

            foreach (var port in candidatePorts)
            {
                yield return $"http://{rtspUri.Host}:{port}/onvif/device_service";
            }
        }
    }

    private static async Task<(bool IsSuccess, byte[]? Bytes, string? ContentType)> TryFetchImageAsync(
        HttpClient client,
        string cameraSnapshotUrl,
        string? cameraUsername,
        string? cameraPassword,
        CancellationToken cancellationToken)
    {
        using var request = CreateCameraRequest(cameraSnapshotUrl, cameraUsername, cameraPassword);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return (false, null, null);
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        var isImageType = contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        var isJpegHeader = bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xD8;
        var isPngHeader = bytes.Length >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;

        if (!isImageType && !isJpegHeader && !isPngHeader)
        {
            return (false, null, null);
        }

        return (true, bytes, string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType);
    }

    private static async Task<(bool IsSuccess, byte[]? Bytes, string? UsedUrl)> TryFetchImageFromRtspAsync(
        string rtspStreamUrl,
        string? rtspUsername,
        string? rtspPassword,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rtspStreamUrl))
        {
            return (false, null, null);
        }

        var candidateUrls = BuildRtspCandidates(rtspStreamUrl)
            .SelectMany(url => BuildRtspUrlsWithCredentialFallback(url, rtspUsername, rtspPassword))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return await Task.Run<(bool IsSuccess, byte[]? Bytes, string? UsedUrl)>(() =>
        {
            var previousFfmpegOptions = Environment.GetEnvironmentVariable("OPENCV_FFMPEG_CAPTURE_OPTIONS");
            var transportModes = new[]
            {
                "rtsp_transport;tcp|stimeout;5000000",
                "stimeout;5000000",
                (string?)null
            };

            try
            {
                foreach (var transportMode in transportModes)
                {
                    Environment.SetEnvironmentVariable("OPENCV_FFMPEG_CAPTURE_OPTIONS", transportMode);

                    foreach (var finalRtspUrl in candidateUrls)
                    {
                        try
                        {
                            using var capture = new VideoCapture(finalRtspUrl, VideoCaptureAPIs.FFMPEG);
                            if (!capture.IsOpened())
                            {
                                continue;
                            }

                            using var frame = new Mat();
                            // Read a few frames to allow stream warm-up.
                            for (var i = 0; i < 12; i++)
                            {
                                if (cancellationToken.IsCancellationRequested)
                                {
                                    return (false, (byte[]?)null, null);
                                }

                                if (!capture.Read(frame) || frame.Empty())
                                {
                                    continue;
                                }

                                Cv2.ImEncode(".jpg", frame, out var jpegBytes);
                                return (true, jpegBytes, finalRtspUrl);
                            }
                        }
                        catch
                        {
                            // Try next RTSP candidate URL.
                        }
                    }
                }

                return (false, (byte[]?)null, null);
            }
            finally
            {
                Environment.SetEnvironmentVariable("OPENCV_FFMPEG_CAPTURE_OPTIONS", previousFfmpegOptions);
            }
        }, cancellationToken);
    }

    private static IEnumerable<string> BuildRtspCandidates(string rtspStreamUrl)
    {
        yield return rtspStreamUrl;

        if (!Uri.TryCreate(rtspStreamUrl, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, "rtsp", StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        var authority = uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
        var baseRtsp = $"rtsp://{authority}";

        // Common RTSP paths across Hikvision / Dahua / Generic / OEM cameras.
        var commonPaths = new[]
        {
            "/h264/ch1/main/av_stream",
            "/Streaming/Channels/101",
            "/Streaming/Channels/102",
            "/live/ch00_0",
            "/live/ch0",
            "/cam/realmonitor?channel=1&subtype=0",
            "/cam/realmonitor?channel=1&subtype=1",
            "/stream1",
            "/stream",
            "/profile1/media.smp",
            "/h264Preview_01_main"
        };

        foreach (var path in commonPaths)
        {
            yield return baseRtsp + path;
        }
    }

    private static string BuildRtspUrlWithCredentials(string rtspStreamUrl, string? username, string? password)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return rtspStreamUrl;
        }

        try
        {
            var builder = new UriBuilder(rtspStreamUrl)
            {
                UserName = username,
                Password = password ?? string.Empty
            };

            return builder.Uri.ToString();
        }
        catch
        {
            return rtspStreamUrl;
        }
    }

    private static string BuildRtspUrlWithUsernameOnly(string rtspStreamUrl, string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return rtspStreamUrl;
        }

        try
        {
            var builder = new UriBuilder(rtspStreamUrl)
            {
                UserName = username,
                Password = string.Empty
            };

            return builder.Uri.ToString();
        }
        catch
        {
            return rtspStreamUrl;
        }
    }

    private static IEnumerable<string> BuildRtspUrlsWithCredentialFallback(string rtspStreamUrl, string? username, string? password)
    {
        if (!string.IsNullOrWhiteSpace(username))
        {
            var withCredentials = BuildRtspUrlWithCredentials(rtspStreamUrl, username, password);
            if (!string.Equals(withCredentials, rtspStreamUrl, StringComparison.OrdinalIgnoreCase))
            {
                yield return withCredentials;
            }

            var withUsernameOnly = BuildRtspUrlWithUsernameOnly(rtspStreamUrl, username);
            if (!string.Equals(withUsernameOnly, rtspStreamUrl, StringComparison.OrdinalIgnoreCase))
            {
                yield return withUsernameOnly;
            }
        }

        // Keep unauthenticated URL as fallback for cameras that reject credentials in URI.
        yield return rtspStreamUrl;
    }

    private static bool TryParseRtspEndpoint(string rtspUrl, out string host, out int port)
    {
        host = string.Empty;
        port = 554;

        if (!Uri.TryCreate(rtspUrl, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, "rtsp", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        host = uri.Host;
        port = uri.IsDefaultPort ? 554 : uri.Port;
        return !string.IsNullOrWhiteSpace(host);
    }

    private static async Task<bool> CanConnectTcpAsync(string host, int port, int timeoutMs, CancellationToken cancellationToken)
    {
        using var client = new System.Net.Sockets.TcpClient();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(timeoutMs);

        try
        {
            await client.ConnectAsync(host, port, linkedCts.Token);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> BuildCameraFailureMessageAsync(string? rtspStreamUrl, string? snapshotUrl, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(rtspStreamUrl)
            && TryParseRtspEndpoint(rtspStreamUrl, out var host, out var port))
        {
            if (!await CanConnectTcpAsync(host, port, 2500, cancellationToken))
            {
                return "พอร์ต RTSP ปลายทางไม่ตอบสนอง";
            }

            var statusLine = await ProbeRtspDescribeStatusLineAsync(rtspStreamUrl, cancellationToken);
            if (statusLine.Contains("401", StringComparison.OrdinalIgnoreCase))
            {
                return "RTSP ต้องใช้รหัสผ่านกล้องที่ถูกต้อง";
            }

            if (statusLine.Contains("404", StringComparison.OrdinalIgnoreCase))
            {
                return "RTSP path ไม่ถูกต้อง";
            }

            return "เชื่อมต่อ RTSP ได้ แต่ยังถอดรหัสภาพไม่ได้";
        }

        if (!string.IsNullOrWhiteSpace(snapshotUrl))
        {
            return "Snapshot URL ไม่ตอบกลับเป็นภาพ";
        }

        return "ไม่สามารถเชื่อมต่อกล้องได้";
    }

    private static async Task<string> ProbeRtspDescribeStatusLineAsync(string rtspUrl, CancellationToken cancellationToken)
    {
        if (!TryParseRtspEndpoint(rtspUrl, out var host, out var port))
        {
            return string.Empty;
        }

        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(TimeSpan.FromSeconds(3));
            await client.ConnectAsync(host, port, linkedCts.Token);

            await using var stream = client.GetStream();
            var request = $"DESCRIBE {rtspUrl} RTSP/1.0\r\nCSeq: 2\r\nAccept: application/sdp\r\nUser-Agent: FaceScanProbe\r\n\r\n";
            var reqBytes = System.Text.Encoding.ASCII.GetBytes(request);
            await stream.WriteAsync(reqBytes, cancellationToken);

            var buffer = new byte[2048];
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read <= 0)
            {
                return string.Empty;
            }

            var response = System.Text.Encoding.ASCII.GetString(buffer, 0, read);
            var firstLine = response.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).FirstOrDefault();
            return firstLine ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string SanitizePathSegment(string? value)
    {
        var source = string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(source
            .Select(ch => invalidChars.Contains(ch) ? '-' : ch)
            .ToArray());

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return "unknown";
        }

        return sanitized.Length > 64 ? sanitized[..64] : sanitized;
    }

    private static Dictionary<string, string> ReadMetadata(string filePath)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!System.IO.File.Exists(filePath))
        {
            return result;
        }

        try
        {
            foreach (var line in System.IO.File.ReadLines(filePath))
            {
                var idx = line.IndexOf('=');
                if (idx <= 0)
                {
                    continue;
                }

                var key = line[..idx].Trim();
                var value = line[(idx + 1)..].Trim();
                if (key.Length == 0)
                {
                    continue;
                }

                result[key] = value;
            }
        }
        catch
        {
            // Ignore malformed metadata files and continue gracefully.
        }

        return result;
    }
}
