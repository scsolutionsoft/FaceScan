namespace FaceScan.Web.ViewModels.Devices;

public class DeviceListItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string StationCode { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string RecognitionProfile { get; set; } = string.Empty;
    public DateTime? LastSeenAt { get; set; }
    public DateTime? EdgeAgentLastSeenAtUtc { get; set; }
    public bool EdgeAgentOnline { get; set; }
    public string? EdgeAgentId { get; set; }
    public bool HasCamera { get; set; }
    public string? CameraType { get; set; }
    public int CameraIntervalMs { get; set; }
    public decimal CameraMinConfidence { get; set; }
    public int CameraSkipFrames { get; set; }
    public bool CameraOverlayEnabled { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string ScanFaceMode { get; set; } = "single";
    public string ScanEngine { get; set; } = "classic";
    public bool ScanAutoTuneEnabled { get; set; } = true;
    public int ScanMultiFaceMax { get; set; } = 3;
}
