namespace FaceScan.Web.ViewModels.IpCamera;

public class IpCameraMenuItemViewModel
{
    public string StationCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? CameraType { get; set; }
}
