using System.ComponentModel.DataAnnotations;
using FaceScan.Web.Models;

namespace FaceScan.Web.ViewModels.Devices;

public class DeviceEditViewModel
{
    public int? Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string StationCode { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? AccessToken { get; set; }

    [MaxLength(200)]
    public string? Location { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    [Required]
    [MaxLength(20)]
    public string RecognitionProfile { get; set; } = FaceRecognitionProfiles.Auto;

    [MaxLength(10)]
    public string ScanFaceMode { get; set; } = "single"; // single | multi

    [MaxLength(10)]
    public string ScanEngine { get; set; } = "classic"; // classic | buffered

    public bool ScanAutoTuneEnabled { get; set; } = true;

    [Range(2, 6)]
    public int ScanMultiFaceMax { get; set; } = 3;

    // IP Camera
    [MaxLength(20)]
    public string? CameraType { get; set; }        // hikvision | dahua | generic | onvif | rtsp | onvif-rtsp

    [MaxLength(500)]
    [Url]
    public string? CameraSnapshotUrl { get; set; }

    [MaxLength(100)]
    public string? CameraUsername { get; set; }

    [MaxLength(100)]
    public string? CameraPassword { get; set; }

    [Range(500, 10000)]
    public int CameraIntervalMs { get; set; } = 1500;

    [Range(typeof(decimal), "0", "1")]
    public decimal CameraMinConfidence { get; set; } = 0.60m;

    [Range(1, 10)]
    public int CameraSkipFrames { get; set; } = 1;

    public bool CameraOverlayEnabled { get; set; } = true;

    // RTSP specific
    [MaxLength(500)]
    public string? RtspStreamUrl { get; set; }

    [Range(1, 65535)]
    public int RtspPort { get; set; } = 554;

    [MaxLength(100)]
    public string? RtspUsername { get; set; }

    [MaxLength(100)]
    public string? RtspPassword { get; set; }

    public bool RtspTestPassed { get; set; }

    // ONVIF specific
    [MaxLength(500)]
    [Url]
    public string? OnvifDeviceUri { get; set; }

    [Range(1, 65535)]
    public int OnvifPort { get; set; } = 8080;

    [MaxLength(100)]
    public string? OnvifManufacturer { get; set; }

    [MaxLength(100)]
    public string? OnvifModel { get; set; }

    [MaxLength(100)]
    public string? OnvifSerialNumber { get; set; }

    [MaxLength(100)]
    public string? OnvifFirmwareVersion { get; set; }

    public DateTime? OnvifLastDiscoveredAtUtc { get; set; }

    public bool OnvifTestPassed { get; set; }
}
