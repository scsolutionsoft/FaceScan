using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.Models.Entities;

public class BehaviorScoreTransaction : BaseEntity
{
    public int StudentId { get; set; }
    public int AcademicYearId { get; set; }
    public int ScoreChange { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? EvidencePhotoPath { get; set; }
    public string? RecordedByUserId { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public string? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public StudentCareRecordStatus Status { get; set; } = StudentCareRecordStatus.Approved;

    public Student? Student { get; set; }
    public AcademicYear? AcademicYear { get; set; }
    public ApplicationUser? RecordedByUser { get; set; }
    public ApplicationUser? ApprovedByUser { get; set; }
}
