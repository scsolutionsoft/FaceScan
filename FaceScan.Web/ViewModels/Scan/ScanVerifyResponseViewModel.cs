using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.ViewModels.Scan;

public class ScanVerifyResponseViewModel
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public int? StudentId { get; set; }
    public string? StudentCode { get; set; }
    public string? StudentName { get; set; }
    public string GradeLevel { get; set; } = string.Empty;
    public string Classroom { get; set; } = string.Empty;
    public ScanType? ScanType { get; set; }
    public DateTime ScanTime { get; set; }
    public decimal Confidence { get; set; }
    public string RecognitionProfile { get; set; } = string.Empty;
    public bool IsDuplicate { get; set; }
    public string TimeSource { get; set; } = "server";
    public bool ClockAnomaly { get; set; }
    public decimal? ClockSkewMinutes { get; set; }
}
