using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.Models.Entities;

public class TeacherAttendanceTransaction : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public ScanType ScanType { get; set; }
    public DateTime ScanTime { get; set; }
    public DateTime ScanDate { get; set; }
    public int? DeviceId { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal? LocationAccuracyMeters { get; set; }
    public decimal ConfidenceScore { get; set; }
    public string RecognitionProvider { get; set; } = string.Empty;
    public string? SnapshotPath { get; set; }
    public bool IsDuplicate { get; set; }
    public string? RawResponseJson { get; set; }

    public ApplicationUser? User { get; set; }
    public ScanDevice? Device { get; set; }
}
