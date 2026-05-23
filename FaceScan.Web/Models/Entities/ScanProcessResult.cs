using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.Models.Entities;

public class ScanProcessResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? StudentId { get; set; }
    public string? StudentCode { get; set; }
    public string? StudentName { get; set; }
    public string GradeLevel { get; set; } = string.Empty;
    public string Classroom { get; set; } = string.Empty;
    public ScanType? ScanType { get; set; }
    public DateTime ScanTime { get; set; }
    public decimal ConfidenceScore { get; set; }
    public bool IsDuplicate { get; set; }
    public string? Provider { get; set; }
    public string RecognitionProfile { get; set; } = string.Empty;
    public string TimeSource { get; set; } = "server";
    public bool ClockAnomaly { get; set; }
    public decimal? ClockSkewMinutes { get; set; }
}
