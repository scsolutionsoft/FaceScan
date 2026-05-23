namespace FaceScan.Web.Models.Entities;

public class FaceEnrollResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public decimal QualityScore { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? RawResponseJson { get; set; }
}
