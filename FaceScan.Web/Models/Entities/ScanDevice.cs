using FaceScan.Web.Models;

namespace FaceScan.Web.Models.Entities;

public class ScanDevice : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string StationCode { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string? Location { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastSeenAt { get; set; }
    public string? Notes { get; set; }
    public string RecognitionProfile { get; set; } = FaceRecognitionProfiles.Auto;

    // IP Camera config
    public string? CameraType { get; set; }       // hikvision | dahua | generic | onvif | rtsp | onvif-rtsp
    public string? CameraSnapshotUrl { get; set; } // http://ip/ISAPI/Streaming/channels/101/picture
    public string? CameraUsername { get; set; }
    public string? CameraPassword { get; set; }
    public int CameraIntervalMs { get; set; } = 1500;
    public decimal CameraMinConfidence { get; set; } = 0.60m;
    public int CameraSkipFrames { get; set; } = 1;
    public bool CameraOverlayEnabled { get; set; } = true;

    // RTSP specific config
    public string? RtspStreamUrl { get; set; }    // rtsp://ip:554/h264/ch1/main/av_stream
    public int RtspPort { get; set; } = 554;      // Default RTSP port
    public string? RtspUsername { get; set; }     // Optional: for cameras with credentials
    public string? RtspPassword { get; set; }     // Optional: encrypted in DB
    public bool RtspTestPassed { get; set; }

    // ONVIF specific config
    public string? OnvifDeviceUri { get; set; }   // http://ip:port/onvif/device_service
    public int OnvifPort { get; set; } = 8080;    // 8080, 8081, or 8899 for custom
    public string? OnvifManufacturer { get; set; }
    public string? OnvifModel { get; set; }
    public string? OnvifSerialNumber { get; set; }
    public string? OnvifFirmwareVersion { get; set; }
    public DateTime? OnvifLastDiscoveredAtUtc { get; set; }
    public bool OnvifTestPassed { get; set; }

    public ICollection<AttendanceTransaction> AttendanceTransactions { get; set; } = new List<AttendanceTransaction>();
}
