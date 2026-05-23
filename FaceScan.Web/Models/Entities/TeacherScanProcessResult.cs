using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.Models.Entities;

public class TeacherScanProcessResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? Username { get; set; }
    public string? FullName { get; set; }
    public string? RoleName { get; set; }
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
