using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.ViewModels.Teacher;

public class StudentCareScoreTransactionReportRowViewModel
{
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Classroom { get; set; } = "-";
    public DateTime RecordedAt { get; set; }
    public string Category { get; set; } = string.Empty;
    public int Point { get; set; }
    public string? Description { get; set; }
    public StudentCareRecordStatus Status { get; set; }
}
