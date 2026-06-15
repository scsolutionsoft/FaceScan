using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.ViewModels.Teacher;

public class StudentCareHomeVisitReportRowViewModel
{
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Classroom { get; set; } = "-";
    public DateTime? LastHomeVisitAt { get; set; }
    public StudentCareRiskLevel RiskLevel { get; set; } = StudentCareRiskLevel.Normal;
    public string? ProblemsFound { get; set; }
}
