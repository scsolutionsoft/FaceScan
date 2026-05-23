using FaceScan.Web.Models.Enums;
using Microsoft.AspNetCore.Http;

namespace FaceScan.Web.ViewModels.Scan;

public class ScanVerifyRequestViewModel
{
    public IFormFile? Image { get; set; }
    public string? StationCode { get; set; }
    public string? StationToken { get; set; }
    public string? ClientCapturedAtLocal { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal? LocationAccuracyMeters { get; set; }
    public ScanType? RequestedType { get; set; }
    public string? RecognitionProfile { get; set; }
    public bool IsPublicMode { get; set; }
}
