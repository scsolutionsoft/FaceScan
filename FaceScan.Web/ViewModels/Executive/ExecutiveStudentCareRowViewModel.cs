using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.ViewModels.Executive;

public class ExecutiveStudentCareRowViewModel
{
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string GradeLevel { get; set; } = "-";
    public string Classroom { get; set; } = "-";
    public int BehaviorScore { get; set; } = 100;
    public int GoodnessPoint { get; set; }
    public StudentCareRiskLevel RiskLevel { get; set; } = StudentCareRiskLevel.Normal;
    public DateTime? LastHomeVisitAt { get; set; }
    public bool HasHomeLocation { get; set; }
    public decimal? HomeLatitude { get; set; }
    public decimal? HomeLongitude { get; set; }
    public DateTime? HomeLocationSharedAt { get; set; }
    public decimal WasteWeightKg { get; set; }
    public decimal WasteAmount { get; set; }
    public string? ProblemsFound { get; set; }
}
