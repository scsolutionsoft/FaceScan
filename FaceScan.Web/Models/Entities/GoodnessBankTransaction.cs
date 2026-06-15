using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.Models.Entities;

public class GoodnessBankTransaction : BaseEntity
{
    public int StudentId { get; set; }
    public int AcademicYearId { get; set; }
    public string GoodnessType { get; set; } = string.Empty;
    public int Point { get; set; }
    public string? Description { get; set; }
    public string? EvidencePhotoPath { get; set; }
    public string? RecordedByUserId { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public StudentCareRecordStatus Status { get; set; } = StudentCareRecordStatus.Approved;

    public Student? Student { get; set; }
    public AcademicYear? AcademicYear { get; set; }
    public ApplicationUser? RecordedByUser { get; set; }
}
