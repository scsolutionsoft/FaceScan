using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.Models.Entities;

public class StudentCareFollowUpCase : BaseEntity
{
    public int StudentId { get; set; }
    public int AcademicYearId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Concern { get; set; } = string.Empty;
    public string SupportPlan { get; set; } = string.Empty;
    public StudentCareFollowUpPriority Priority { get; set; } = StudentCareFollowUpPriority.Normal;
    public StudentCareFollowUpStatus Status { get; set; } = StudentCareFollowUpStatus.Open;
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Outcome { get; set; }
    public string? AssignedToUserId { get; set; }
    public string? CreatedByUserId { get; set; }
    public string? LastUpdatedByUserId { get; set; }

    public Student? Student { get; set; }
    public AcademicYear? AcademicYear { get; set; }
    public ApplicationUser? AssignedToUser { get; set; }
    public ApplicationUser? CreatedByUser { get; set; }
    public ApplicationUser? LastUpdatedByUser { get; set; }
}
