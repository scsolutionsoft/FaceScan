using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.ViewModels.Teacher;

public class StudentCareFollowUpCaseDetailViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Concern { get; set; } = string.Empty;
    public string SupportPlan { get; set; } = string.Empty;
    public StudentCareFollowUpPriority Priority { get; set; } = StudentCareFollowUpPriority.Normal;
    public StudentCareFollowUpStatus Status { get; set; } = StudentCareFollowUpStatus.Open;
    public DateTime OpenedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Outcome { get; set; }
    public bool IsOverdue => Status is StudentCareFollowUpStatus.Open or StudentCareFollowUpStatus.InProgress
        && DueDate.HasValue
        && DueDate.Value.Date < DateTime.Today;
}
