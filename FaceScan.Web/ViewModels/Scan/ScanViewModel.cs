using FaceScan.Web.Models;
using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.ViewModels.Scan;

public class ScanViewModel
{
    public string? StationCode { get; set; }
    public string? StationToken { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public ScanType RequestedType { get; set; } = ScanType.CheckIn;
    public string RecognitionProfile { get; set; } = FaceRecognitionProfiles.Auto;
    public bool IsPublicMode { get; set; }
}
