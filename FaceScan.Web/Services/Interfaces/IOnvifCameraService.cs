namespace FaceScan.Web.Services.Interfaces;

/// <summary>
/// ONVIF camera discovery and management service
/// </summary>
public interface IOnvifCameraService
{
    /// <summary>
    /// Discover ONVIF cameras on the specified IP and port
    /// </summary>
    Task<OnvifDeviceInfo?> DiscoverCameraAsync(string ipAddress, int port, string? username = null, string? password = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Test connection to ONVIF camera
    /// </summary>
    Task<bool> TestConnectionAsync(string onvifUri, string? username = null, string? password = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Test connection to RTSP stream
    /// </summary>
    Task<bool> TestRtspConnectionAsync(string rtspUri, string? username = null, string? password = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get snapshot URL from ONVIF camera
    /// </summary>
    Task<string?> GetSnapshotUriAsync(string onvifUri, string? username = null, string? password = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get RTSP URI from ONVIF camera
    /// </summary>
    Task<string?> GetRtspUriAsync(string onvifUri, string? username = null, string? password = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get device information from ONVIF camera
    /// </summary>
    Task<OnvifDeviceInfo?> GetDeviceInfoAsync(string onvifUri, string? username = null, string? password = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// ONVIF device information
/// </summary>
public class OnvifDeviceInfo
{
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? FirmwareVersion { get; set; }
    public string? HardwareId { get; set; }
    public string? SnapshotUri { get; set; }
    public string? RtspUri { get; set; }
    public string? OnvifUri { get; set; }
    public int Port { get; set; } = 8080;
    public bool IsOnline { get; set; }
    public DateTime DiscoveredAtUtc { get; set; }
    public string? ErrorMessage { get; set; }
}
