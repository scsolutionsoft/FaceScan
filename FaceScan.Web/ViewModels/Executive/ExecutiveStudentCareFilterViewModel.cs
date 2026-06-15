using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.ViewModels.Executive;

public class ExecutiveStudentCareFilterViewModel
{
    public int? AcademicYearId { get; set; }
    public int? GradeLevelId { get; set; }
    public int? ClassroomId { get; set; }
    public StudentCareRiskLevel? RiskLevel { get; set; }
    public string? StudentKeyword { get; set; }
}
