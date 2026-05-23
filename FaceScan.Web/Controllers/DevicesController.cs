using FaceScan.Web.Data;
using FaceScan.Web.Helpers;
using FaceScan.Web.Models;
using FaceScan.Web.Models.Entities;
using FaceScan.Web.Services.Interfaces;
using FaceScan.Web.ViewModels.Devices;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace FaceScan.Web.Controllers;

[Authorize(Roles = "SuperAdmin,Admin")]
public class DevicesController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditLogService _auditLogService;
    private readonly IOnvifCameraService _onvifCameraService;

    public DevicesController(ApplicationDbContext dbContext, IAuditLogService auditLogService, IOnvifCameraService onvifCameraService)
    {
        _dbContext = dbContext;
        _auditLogService = auditLogService;
        _onvifCameraService = onvifCameraService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var deviceEntities = await _dbContext.ScanDevices
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var devices = deviceEntities
            .Select(x =>
            {
                var scanSettings = ScanCameraSettingsHelper.Parse(x.Notes);
                return new DeviceListItemViewModel
                {
                    Id = x.Id,
                    Name = x.Name,
                    StationCode = x.StationCode,
                    AccessToken = x.AccessToken,
                    Location = x.Location ?? string.Empty,
                    Notes = ScanCameraSettingsHelper.StripMeta(x.Notes),
                    IsActive = x.IsActive,
                    RecognitionProfile = x.RecognitionProfile,
                    LastSeenAt = x.LastSeenAt,
                    HasCamera = !string.IsNullOrEmpty(x.CameraSnapshotUrl) || !string.IsNullOrEmpty(x.RtspStreamUrl),
                    CameraType = x.CameraType,
                    CameraIntervalMs = x.CameraIntervalMs,
                    CameraMinConfidence = x.CameraMinConfidence,
                    CameraSkipFrames = x.CameraSkipFrames,
                    CameraOverlayEnabled = x.CameraOverlayEnabled,
                    ScanFaceMode = scanSettings.FaceMode,
                    ScanEngine = scanSettings.ScanEngine,
                    ScanAutoTuneEnabled = scanSettings.AutoTuneEnabled,
                    ScanMultiFaceMax = scanSettings.MultiFaceMax
                };
            })
            .ToList();

        var latestHeartbeats = await _dbContext.EdgeAgentHeartbeats
            .AsNoTracking()
            .OrderByDescending(x => x.LastSeenAtUtc)
            .ToListAsync(cancellationToken);

        var heartbeatLookup = latestHeartbeats
            .GroupBy(x => x.StationCode)
            .ToDictionary(g => g.Key, g => g.First());

        var onlineThresholdUtc = DateTime.UtcNow.AddSeconds(-90);
        foreach (var device in devices)
        {
            if (!heartbeatLookup.TryGetValue(device.StationCode, out var heartbeat))
            {
                continue;
            }

            device.EdgeAgentId = heartbeat.AgentId;
            device.EdgeAgentLastSeenAtUtc = heartbeat.LastSeenAtUtc;
            device.EdgeAgentOnline = heartbeat.LastSeenAtUtc >= onlineThresholdUtc;
        }

        ViewBag.EditModel = new DeviceEditViewModel();
        return View(devices);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(DeviceEditViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "ข้อมูลอุปกรณ์ไม่ถูกต้อง";
            return RedirectToAction(nameof(Index));
        }

        var stationCode = model.StationCode.Trim();

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var ip = HttpContextHelper.GetIpAddress(HttpContext);

        ScanDevice? device;
        var action = "Create";

        if (model.Id.HasValue)
        {
            device = await _dbContext.ScanDevices.FirstOrDefaultAsync(x => x.Id == model.Id.Value, cancellationToken);
            if (device is null)
            {
                TempData["Error"] = "ไม่พบข้อมูลอุปกรณ์";
                return RedirectToAction(nameof(Index));
            }

            action = "Update";
        }
        else
        {
            // Treat save as upsert by StationCode to avoid duplicate-key crashes from form submissions without edit Id.
            device = await _dbContext.ScanDevices.FirstOrDefaultAsync(x => x.StationCode == stationCode, cancellationToken);
            if (device is null)
            {
                device = new ScanDevice();
                _dbContext.ScanDevices.Add(device);
            }
            else
            {
                action = "Update";
            }
        }

        // Guard against StationCode conflict when updating another record.
        var hasConflictingStationCode = await _dbContext.ScanDevices
            .AsNoTracking()
            .AnyAsync(x => x.StationCode == stationCode && x.Id != device.Id, cancellationToken);
        if (hasConflictingStationCode)
        {
            TempData["Error"] = $"รหัสสถานี {stationCode} ถูกใช้งานแล้ว กรุณาใช้รหัสอื่น";
            return RedirectToAction(nameof(Index));
        }

        device.Name = model.Name.Trim();
        device.StationCode = stationCode;

        var incomingToken = model.AccessToken?.Trim();
        if (!string.IsNullOrWhiteSpace(incomingToken))
        {
            device.AccessToken = incomingToken;
        }
        else if (string.IsNullOrWhiteSpace(device.AccessToken))
        {
            device.AccessToken = GenerateAccessToken();
        }

        device.Location = model.Location?.Trim();
        var scanFaceMode = ScanCameraSettingsHelper.NormalizeFaceMode(model.ScanFaceMode);
        var scanEngine = ScanCameraSettingsHelper.NormalizeScanEngine(model.ScanEngine);
        var scanAutoTuneEnabled = model.ScanAutoTuneEnabled;
        var scanMultiFaceMax = Math.Clamp(model.ScanMultiFaceMax, 2, 6);
        var plainNotes = ScanCameraSettingsHelper.StripMeta(model.Notes?.Trim());
        device.Notes = ScanCameraSettingsHelper.Merge(plainNotes, new ScanCameraSettings
        {
            FaceMode = scanFaceMode,
            ScanEngine = scanEngine,
            AutoTuneEnabled = scanAutoTuneEnabled,
            MultiFaceMax = scanMultiFaceMax
        });
        device.IsActive = model.IsActive;
        device.RecognitionProfile = NormalizeRecognitionProfile(model.RecognitionProfile);
        device.CameraType = string.IsNullOrWhiteSpace(model.CameraType) ? null : model.CameraType.Trim();
        device.CameraSnapshotUrl = string.IsNullOrWhiteSpace(model.CameraSnapshotUrl) ? null : model.CameraSnapshotUrl.Trim();
        device.CameraUsername = string.IsNullOrWhiteSpace(model.CameraUsername) ? null : model.CameraUsername.Trim();
        // Only update password if provided (blank = keep existing)
        if (!string.IsNullOrWhiteSpace(model.CameraPassword))
        {
            device.CameraPassword = model.CameraPassword.Trim();
        }
        device.CameraIntervalMs = model.CameraIntervalMs > 0 ? model.CameraIntervalMs : 1500;
        device.CameraMinConfidence = model.CameraMinConfidence >= 0 && model.CameraMinConfidence <= 1
            ? model.CameraMinConfidence
            : 0.60m;
        device.CameraSkipFrames = model.CameraSkipFrames >= 1 ? model.CameraSkipFrames : 1;
        device.CameraOverlayEnabled = model.CameraOverlayEnabled;

        // RTSP fields
        device.RtspStreamUrl = string.IsNullOrWhiteSpace(model.RtspStreamUrl) ? null : model.RtspStreamUrl.Trim();
        device.RtspPort = model.RtspPort > 0 ? model.RtspPort : 554;
        device.RtspUsername = string.IsNullOrWhiteSpace(model.RtspUsername) ? null : model.RtspUsername.Trim();
        // Only update RTSP password if provided (blank = keep existing)
        if (!string.IsNullOrWhiteSpace(model.RtspPassword))
        {
            device.RtspPassword = model.RtspPassword.Trim();
        }

        // RTSP mode should use stream URL as primary source; clear stale snapshot endpoint if any.
        if (string.Equals(device.CameraType, "rtsp", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(device.RtspStreamUrl))
        {
            device.CameraSnapshotUrl = null;
        }

        // ONVIF fields
        device.OnvifDeviceUri = string.IsNullOrWhiteSpace(model.OnvifDeviceUri) ? null : model.OnvifDeviceUri.Trim();
        device.OnvifPort = model.OnvifPort > 0 ? model.OnvifPort : 8080;
        device.OnvifManufacturer = string.IsNullOrWhiteSpace(model.OnvifManufacturer) ? null : model.OnvifManufacturer.Trim();
        device.OnvifModel = string.IsNullOrWhiteSpace(model.OnvifModel) ? null : model.OnvifModel.Trim();
        device.OnvifSerialNumber = string.IsNullOrWhiteSpace(model.OnvifSerialNumber) ? null : model.OnvifSerialNumber.Trim();
        device.OnvifFirmwareVersion = string.IsNullOrWhiteSpace(model.OnvifFirmwareVersion) ? null : model.OnvifFirmwareVersion.Trim();
        device.OnvifLastDiscoveredAtUtc = model.OnvifLastDiscoveredAtUtc;
        device.OnvifTestPassed = model.OnvifTestPassed;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627))
        {
            TempData["Error"] = $"รหัสสถานี {stationCode} ถูกใช้งานแล้ว กรุณาใช้รหัสอื่น";
            return RedirectToAction(nameof(Index));
        }

        await _auditLogService.LogAsync(userId, action, "ScanDevice", device.Id.ToString(), $"{action} อุปกรณ์ {device.StationCode}", ip, cancellationToken);

        TempData["Success"] = "บันทึกข้อมูลอุปกรณ์เรียบร้อย";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var device = await _dbContext.ScanDevices.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (device is null)
        {
            TempData["Error"] = "ไม่พบข้อมูลอุปกรณ์";
            return RedirectToAction(nameof(Index));
        }

        _dbContext.ScanDevices.Remove(device);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var ip = HttpContextHelper.GetIpAddress(HttpContext);
        await _auditLogService.LogAsync(userId, "Delete", "ScanDevice", id.ToString(), $"ลบอุปกรณ์ {device.StationCode}", ip, cancellationToken);

        TempData["Success"] = "ลบอุปกรณ์เรียบร้อย";
        return RedirectToAction(nameof(Index));
    }

    private static string NormalizeRecognitionProfile(string? recognitionProfile)
    {
        var normalized = recognitionProfile?.Trim().ToLowerInvariant();
        return normalized switch
        {
            FaceRecognitionProfiles.Fast => FaceRecognitionProfiles.Fast,
            FaceRecognitionProfiles.Stable => FaceRecognitionProfiles.Stable,
            FaceRecognitionProfiles.Accurate => FaceRecognitionProfiles.Accurate,
            FaceRecognitionProfiles.Balanced => FaceRecognitionProfiles.Auto,
            FaceRecognitionProfiles.Auto => FaceRecognitionProfiles.Auto,
            _ => FaceRecognitionProfiles.Auto
        };
    }

    private static string GenerateAccessToken()
    {
        Span<byte> bytes = stackalloc byte[24];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// API endpoint for ONVIF camera discovery
    /// </summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    [Route("api/devices/onvif/discover")]
    public async Task<IActionResult> DiscoverOnvifCamera([FromBody] OnvifDiscoveryRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { error = "Invalid request body" });
        }

        if (string.IsNullOrWhiteSpace(request.IpAddress))
        {
            return BadRequest(new { error = "IP Address required" });
        }

        try
        {
            var deviceInfo = await _onvifCameraService.DiscoverCameraAsync(
                request.IpAddress,
                request.Port > 0 ? request.Port : 8080,
                request.Username,
                request.Password,
                cancellationToken);

            if (deviceInfo?.IsOnline == false)
            {
                return BadRequest(new { error = deviceInfo.ErrorMessage ?? "Failed to discover camera" });
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    manufacturer = deviceInfo?.Manufacturer,
                    model = deviceInfo?.Model,
                    serialNumber = deviceInfo?.SerialNumber,
                    firmwareVersion = deviceInfo?.FirmwareVersion,
                    snapshotUri = deviceInfo?.SnapshotUri,
                    onvifUri = deviceInfo?.OnvifUri,
                    port = deviceInfo?.Port ?? 8080
                }
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// API endpoint for ONVIF camera connection test
    /// </summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    [Route("api/devices/onvif/test")]
    public async Task<IActionResult> TestOnvifConnection([FromBody] OnvifTestRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { error = "Invalid request body" });
        }

        if (string.IsNullOrWhiteSpace(request.OnvifUri))
        {
            return BadRequest(new { error = "ONVIF URI required" });
        }

        try
        {
            var isConnected = await _onvifCameraService.TestConnectionAsync(
                request.OnvifUri,
                request.Username,
                request.Password,
                cancellationToken);

            return Ok(new
            {
                success = true,
                connected = isConnected,
                message = isConnected ? "Camera connection successful" : "Camera connection failed"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// API endpoint for getting ONVIF camera snapshot
    /// </summary>
    [HttpGet]
    [Route("api/devices/onvif/snapshot")]
    public async Task<IActionResult> GetOnvifSnapshot([FromQuery] string onvifUri, [FromQuery] string? username = null, [FromQuery] string? password = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(onvifUri))
        {
            return BadRequest(new { error = "ONVIF URI required" });
        }

        try
        {
            var snapshotUrl = await _onvifCameraService.GetSnapshotUriAsync(onvifUri, username, password, cancellationToken);

            if (string.IsNullOrEmpty(snapshotUrl))
            {
                return NotFound(new { error = "Snapshot URL not found" });
            }

            return Ok(new
            {
                success = true,
                snapshotUrl = snapshotUrl
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// API endpoint for ONVIF RTSP discovery (get RTSP stream URL from ONVIF camera)
    /// </summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    [Route("api/devices/onvif/discover-rtsp")]
    public async Task<IActionResult> DiscoverOnvifRtsp([FromBody] OnvifDiscoveryRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { error = "Invalid request body" });
        }

        if (string.IsNullOrWhiteSpace(request.IpAddress))
        {
            return BadRequest(new { error = "IP Address required" });
        }

        try
        {
            // First discover camera to get device info
            var deviceInfo = await _onvifCameraService.DiscoverCameraAsync(
                request.IpAddress,
                request.Port > 0 ? request.Port : 8080,
                request.Username,
                request.Password,
                cancellationToken);

            if (deviceInfo?.IsOnline == false)
            {
                return BadRequest(new { error = deviceInfo.ErrorMessage ?? "Failed to discover camera" });
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    manufacturer = deviceInfo?.Manufacturer,
                    model = deviceInfo?.Model,
                    serialNumber = deviceInfo?.SerialNumber,
                    firmwareVersion = deviceInfo?.FirmwareVersion,
                    rtspUri = deviceInfo?.RtspUri,
                    snapshotUri = deviceInfo?.SnapshotUri,
                    onvifUri = deviceInfo?.OnvifUri,
                    port = deviceInfo?.Port ?? 8080
                }
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// API endpoint for RTSP stream test
    /// </summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    [Route("api/devices/rtsp/test")]
    public async Task<IActionResult> TestRtspConnection([FromBody] RtspTestRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { error = "Invalid request body" });
        }

        if (string.IsNullOrWhiteSpace(request.RtspUri))
        {
            return BadRequest(new { error = "RTSP URI required" });
        }

        try
        {
            var isConnected = await _onvifCameraService.TestRtspConnectionAsync(
                request.RtspUri,
                request.Username,
                request.Password,
                cancellationToken);

            return Ok(new
            {
                success = true,
                connected = isConnected,
                message = isConnected ? "RTSP stream accessible" : "RTSP stream not accessible"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// API endpoint for direct RTSP discovery (without ONVIF)
    /// </summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    [Route("api/devices/rtsp/discover")]
    public async Task<IActionResult> DiscoverRtspStream([FromBody] RtspDiscoveryRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { error = "Invalid request body" });
        }

        if (string.IsNullOrWhiteSpace(request.IpAddress))
        {
            return BadRequest(new { error = "IP Address required" });
        }

        try
        {
            int port = request.Port > 0 ? request.Port : 554;
            
            // Construct RTSP URI based on common patterns
            var rtspPatterns = new[]
            {
                $"rtsp://{request.IpAddress}:{port}/h264/ch1/main/av_stream",  // Hikvision
                $"rtsp://{request.IpAddress}:{port}/stream1",  // Generic
                $"rtsp://{request.IpAddress}:{port}/ISAPI/streaming/channels/101",  // Hikvision alternative
                $"rtsp://{request.IpAddress}:{port}/live/ch0",  // Dahua
            };

            // Try each pattern with authentication if provided
            foreach (var rtspUri in rtspPatterns)
            {
                var finalUri = rtspUri;
                if (!string.IsNullOrEmpty(request.Username) && !string.IsNullOrEmpty(request.Password))
                {
                    finalUri = rtspUri.Replace("rtsp://", $"rtsp://{request.Username}:{request.Password}@");
                }

                var isAccessible = await _onvifCameraService.TestRtspConnectionAsync(finalUri, null, null, cancellationToken);
                if (isAccessible)
                {
                    return Ok(new
                    {
                        success = true,
                        rtspUri = finalUri,
                        message = "RTSP stream found"
                    });
                }
            }

            // If no pattern matched, return a default suggestion
            var defaultRtspUri = $"rtsp://{request.IpAddress}:554/stream";
            return Ok(new
            {
                success = true,
                rtspUri = defaultRtspUri,
                message = "Using default RTSP URI pattern"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public class OnvifDiscoveryRequest
{
    public string? IpAddress { get; set; }
    public int Port { get; set; } = 8080;
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public class OnvifTestRequest
{
    public string? OnvifUri { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public class RtspTestRequest
{
    public string? RtspUri { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public class RtspDiscoveryRequest
{
    public string? IpAddress { get; set; }
    public int Port { get; set; } = 554;
    public string? Username { get; set; }
    public string? Password { get; set; }
}

