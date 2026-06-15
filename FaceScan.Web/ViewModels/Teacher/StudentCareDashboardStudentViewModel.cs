using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.ViewModels.Teacher;

public class StudentCareDashboardStudentViewModel
{
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Classroom { get; set; } = "-";
    public int BehaviorScore { get; set; } = 100;
    public int GoodnessPoint { get; set; }
    public decimal WasteBankAmount { get; set; }
    public decimal WasteBankWeightKg { get; set; }
    public StudentCareRiskLevel RiskLevel { get; set; } = StudentCareRiskLevel.Normal;
    public DateTime? LastHomeVisitAt { get; set; }
    public string? LivesWith { get; set; }
    public bool HasHomeLocation { get; set; }
    public decimal? HomeLatitude { get; set; }
    public decimal? HomeLongitude { get; set; }
    public DateTime? HomeLocationSharedAt { get; set; }
}
