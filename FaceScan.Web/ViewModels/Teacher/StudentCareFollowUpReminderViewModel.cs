using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.ViewModels.Teacher;

public class StudentCareFollowUpReminderViewModel
{
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Classroom { get; set; } = "-";
    public string Title { get; set; } = string.Empty;
    public StudentCareFollowUpPriority Priority { get; set; } = StudentCareFollowUpPriority.Normal;
    public StudentCareFollowUpStatus Status { get; set; } = StudentCareFollowUpStatus.Open;
    public DateTime OpenedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsOverdue { get; set; }
    public bool IsDueToday { get; set; }
}
